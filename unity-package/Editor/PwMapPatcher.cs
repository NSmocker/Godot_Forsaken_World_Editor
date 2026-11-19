using System;
using System.Collections.Generic;
using System.IO;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Adds existing, already positioned BMDs to PW v12 worlds. Original bytes are retained;
    /// only block-table offsets change, with new BSD records and replacement blocks appended.
    /// See CCommon/EC_WorldFile.h and CElementEditor/SceneExportor.cpp in PW_ClientSource.
    /// </summary>
    public static class PwMapPatcher
    {
        public sealed class Input
        {
            public string RelativePath;
            public byte[] Bmd;
        }

        public sealed class Result
        {
            public byte[] World;
            public byte[] SharedData;
            public int Added;
            public int Skipped;
            public int ChangedBlocks;
        }

        private sealed class Block
        {
            public int Start, End, InsertAt, OrnamentCount;
            public ushort Row, Column;
            public readonly List<KeyValuePair<uint, uint>> Added = new List<KeyValuePair<uint, uint>>();
        }

        public static Result Build(byte[] world, byte[] sharedData, IReadOnlyList<Input> inputs,
            GfxTextDecoder decoder, Func<string, byte[]> encodePath)
        {
            if (inputs == null || decoder == null || encodePath == null)
                throw new ArgumentNullException("inputs/decoder/encodePath");
            var r = new PwMapCursor(world, "ECWLD");
            if (r.U32() != 0x45435744 || r.U32() != 12)
                throw new InvalidDataException("Map Patcher підтримує PW ECWLD версії 12.");
            float width = r.Float(), length = r.Float(), blockSize = r.Float();
            int grid = r.Count(), count = r.Count(), rows = r.Count(), cols = r.Count();
            if (width <= 0 || length <= 0 || blockSize <= 0 || grid == 0 || count == 0 ||
                (long)rows * cols != count || Math.Abs(width - cols * blockSize) > 0.01f ||
                Math.Abs(length - rows * blockSize) > 0.01f)
                throw new InvalidDataException("Некоректна сітка блоків ECWLD.");
            uint treeOffset = r.U32(), grassOffset = r.U32();
            int treeTypes = r.Count(), grassTypes = r.Count();
            if (r.U32() != 0)
                throw new InvalidDataException("Підтримуються карти ElementEditor; формат Home editor не підтримується.");
            r.Seek(116);
            if ((long)count * 4 > r.Remaining)
                throw new InvalidDataException("Обрізана таблиця блоків ECWLD.");
            var offsets = new int[count];
            for (int i = 0; i < count; i++) offsets[i] = r.Offset();
            int tableEnd = r.Position;

            var bsd = new PwMapCursor(sharedData, "ECBSD");
            if (bsd.U32() != 0x42584f4d || bsd.U32() != 0x42534446 || bsd.U32() != 12)
                throw new InvalidDataException("Map Patcher підтримує MOXB/ECBSD версії 12.");
            int subCount = bsd.Count();
            bsd.Seek(76);
            for (int i = 0; i < subCount; i++) bsd.StringBytes();
            int bsdDataStart = bsd.Position;

            var blocks = new Block[count];
            var ornamentRefs = new Dictionary<uint, uint>();
            var knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            uint maxId = 0;
            for (int i = 0; i < count; i++)
            {
                if (offsets[i] < tableEnd) throw new InvalidDataException("Блок перекриває заголовок ECWLD.");
                r.Seek(offsets[i]);
                var block = new Block { Start = r.Position, Row = r.U16(), Column = r.U16() };
                if (block.Row != i / cols || block.Column != i % cols)
                    throw new InvalidDataException("Індекс блока не відповідає його рядку/стовпцю.");
                var counts = new int[10];
                for (int k = 0; k < counts.Length; k++) counts[k] = r.Count();
                block.OrnamentCount = counts[3];
                r.Skip((long)counts[0] * 16); // trees: type ID is not an export ID
                ReadRefs(r, counts[1], ref maxId, null); // water
                ReadRefs(r, counts[3], ref maxId, ornamentRefs);
                block.InsertAt = r.Position;
                ReadRefs(r, counts[4], ref maxId, null); // boxes precede grass in payload
                ReadRefs(r, counts[2], ref maxId, null);
                ReadObjects(r, counts[5], 60, 1, ref maxId); // GFX including attenuation
                ReadObjects(r, counts[6], 40, 2, ref maxId); // ECM + action
                ReadRefs(r, counts[7], ref maxId, null);
                ReadRefs(r, counts[8], ref maxId, null);
                ReadObjects(r, counts[9], 32, 1, ref maxId);
                block.End = r.Position;
                blocks[i] = block;
            }
            var sorted = (Block[])blocks.Clone();
            Array.Sort(sorted, (a, b) => a.Start.CompareTo(b.Start));
            for (int i = 1; i < sorted.Length; i++)
                if (sorted[i - 1].End > sorted[i].Start)
                    throw new InvalidDataException("Блоки ECWLD перекриваються.");
            // These tables are retained byte-for-byte; validate their strings and ranges first.
            ValidateTypeTable(world, treeOffset, treeTypes, 0, 2, tableEnd, sorted);
            ValidateTypeTable(world, grassOffset, grassTypes, 24, 1, tableEnd, sorted);
            foreach (var pair in ornamentRefs)
            {
                if (pair.Value < bsdDataStart || pair.Value > int.MaxValue)
                    throw new InvalidDataException("Некоректне посилання Litmodel у ECBSD.");
                bsd.Seek((int)pair.Value);
                bsd.Skip(40);
                knownPaths.Add(NormalizePath(decoder.DecodeGbk(bsd.StringBytes())));
            }

            var result = new Result();
            using (var outputBsd = new MemoryStream())
            using (var bw = new BinaryWriter(outputBsd))
            {
                bw.Write(sharedData);
                foreach (var input in inputs)
                {
                    if (input == null) throw new InvalidDataException("Порожній запис Litmodel.");
                    string path = NormalizePath(input.RelativePath);
                    if (knownPaths.Contains(path)) { result.Skipped++; continue; }
                    byte[] pathBytes = encodePath(path);
                    if (pathBytes.Length == 0 || pathBytes.Length >= 260 || decoder.DecodeGbk(pathBytes) != path)
                        throw new InvalidDataException("Шлях не можна без втрат записати у GBK: " + path);
                    var model = PwLitmodelPlacement.Read(input.Bmd, path);
                    var touched = new List<Block>();
                    foreach (var block in blocks)
                    {
                        float left = -width * 0.5f + block.Column * blockSize;
                        float top = length * 0.5f - block.Row * blockSize;
                        // CStaticModelObject::IsIntersectionWithArea uses inclusive AABB edges.
                        if (model.MinX <= left + blockSize && model.MaxX >= left &&
                            model.MinZ <= top && model.MaxZ >= top - blockSize)
                            touched.Add(block);
                    }
                    if (touched.Count == 0)
                        throw new InvalidDataException("Litmodel розташований поза картою: " + path);
                    if (maxId == uint.MaxValue) throw new InvalidDataException("Вичерпано export ID карти.");
                    uint id = ++maxId;
                    uint offset = checked((uint)outputBsd.Position);
                    WriteVector(bw, model.Pos);
                    WriteVector(bw, model.Dir);
                    WriteVector(bw, model.Up);
                    bw.Write(0u); // reflect/refract false + two MSVC padding bytes
                    bw.Write(pathBytes.Length);
                    bw.Write(pathBytes);
                    foreach (var block in touched)
                        block.Added.Add(new KeyValuePair<uint, uint>(id, offset));
                    knownPaths.Add(path);
                    result.Added++;
                }
                result.SharedData = outputBsd.ToArray();
            }

            using (var outputWorld = new MemoryStream())
            using (var bw = new BinaryWriter(outputWorld))
            {
                bw.Write(world);
                for (int i = 0; i < blocks.Length; i++)
                {
                    var block = blocks[i];
                    if (block.Added.Count == 0) continue;
                    long offset = outputWorld.Position;
                    bw.Write(world, block.Start, block.InsertAt - block.Start);
                    foreach (var pair in block.Added) { bw.Write(pair.Key); bw.Write(pair.Value); }
                    bw.Write(world, block.InsertAt, block.End - block.InsertAt);
                    long end = outputWorld.Position;
                    outputWorld.Position = offset + 16; // iNumOrnament, not iNumGrass
                    bw.Write(checked(block.OrnamentCount + block.Added.Count));
                    outputWorld.Position = 116 + i * 4;
                    bw.Write(checked((uint)offset));
                    outputWorld.Position = end;
                    result.ChangedBlocks++;
                }
                result.World = outputWorld.ToArray();
            }
            return result;
        }

        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("Вкажіть шлях Litmodel.");
            path = path.Trim().Replace('/', '\\');
            if (path.StartsWith("\\", StringComparison.Ordinal) || path.IndexOfAny(new[] { ':', '\0', '*', '?', '"', '<', '>', '|' }) >= 0)
                throw new InvalidDataException("Потрібен відносний шлях: " + path);
            foreach (string part in path.Split('\\'))
                if (part.Length == 0 || part == "." || part == ".." || part.EndsWith(".", StringComparison.Ordinal) || part.EndsWith(" ", StringComparison.Ordinal))
                    throw new InvalidDataException("Некоректна частина шляху: " + path);
            if (!path.EndsWith(".bmd", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Вкажіть файл Litmodel з розширенням .bmd: " + path);
            return path;
        }

        private static void ReadRefs(PwMapCursor r, int count, ref uint maxId, Dictionary<uint, uint> refs)
        {
            if ((long)count * 8 > r.Remaining) throw new InvalidDataException("Обрізаний список посилань.");
            for (int i = 0; i < count; i++)
            {
                uint id = r.U32(), offset = r.U32();
                maxId = Math.Max(maxId, id);
                if (refs == null) continue;
                if (refs.TryGetValue(id, out uint old) && old != offset)
                    throw new InvalidDataException("Litmodel ID посилається на різні записи ECBSD.");
                refs[id] = offset;
            }
        }

        private static void ReadObjects(PwMapCursor r, int count, int size, int strings, ref uint maxId)
        {
            if ((long)count * (size + 4 * strings) > r.Remaining) throw new InvalidDataException("Обрізаний список об'єктів.");
            for (int i = 0; i < count; i++)
            {
                maxId = Math.Max(maxId, r.U32());
                r.Skip(size - 4);
                for (int s = 0; s < strings; s++) r.StringBytes();
            }
        }

        private static void ValidateTypeTable(byte[] data, uint offset, int count, int fixedSize,
            int strings, int tableEnd, Block[] blocks)
        {
            if (count == 0) return;
            if (offset < tableEnd || offset > int.MaxValue) throw new InvalidDataException("Некоректна таблиця типів.");
            var r = new PwMapCursor(data, "Таблиця типів");
            r.Seek((int)offset);
            if ((long)count * (fixedSize + strings * 4) > r.Remaining) throw new InvalidDataException("Обрізана таблиця типів.");
            for (int i = 0; i < count; i++)
            {
                r.Skip(fixedSize);
                for (int j = 0; j < strings; j++) r.StringBytes();
            }
            foreach (var b in blocks)
                if (offset < b.End && r.Position > b.Start) throw new InvalidDataException("Таблиця типів перекриває блок.");
        }

        private static void WriteVector(BinaryWriter writer, float[] vector)
        {
            foreach (float value in vector) writer.Write(value);
        }
    }

    internal sealed class PwMapCursor
    {
        private readonly byte[] data;
        private readonly string name;
        public int Position { get; private set; }
        public int Remaining => data.Length - Position;
        public PwMapCursor(byte[] data, string name)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            this.name = name;
        }
        public void Seek(int offset)
        {
            if (offset < 0 || offset > data.Length) throw new InvalidDataException(name + ": offset поза файлом.");
            Position = offset;
        }
        public void Skip(long count)
        {
            if (count < 0 || count > Remaining) throw new InvalidDataException(name + ": обрізані дані на " + Position);
            Position += (int)count;
        }
        public byte Byte() { Skip(1); return data[Position - 1]; }
        public ushort U16() { Skip(2); return (ushort)(data[Position - 2] | data[Position - 1] << 8); }
        public uint U32() { Skip(4); return BitConverter.ToUInt32(data, Position - 4); }
        public int Count()
        {
            uint value = U32();
            if (value > int.MaxValue) throw new InvalidDataException(name + ": від'ємний лічильник.");
            return (int)value;
        }
        public int Offset() => Count();
        public float Float()
        {
            Skip(4);
            float value = BitConverter.ToSingle(data, Position - 4);
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException(name + ": некоректне число.");
            return value;
        }
        public float[] Vector() => new[] { Float(), Float(), Float() };
        public byte[] StringBytes()
        {
            int count = Count(), start = Position;
            Skip(count);
            var bytes = new byte[count];
            Buffer.BlockCopy(data, start, bytes, 0, count);
            return bytes;
        }
    }
}
