using System;
using System.Collections.Generic;
using System.IO;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Reads .ecbsd MOXB/BSDF shared data referenced by .ecwld (ornaments, default light,
    /// water area table + BSData payloads), matching gfx_importer/angelica_world.gd.
    /// </summary>
    public sealed class EcbsdParser
    {
        public const uint BsdfMagic = 0x42534446; // 'BSDF'
        public const int BsdHeaderEnd = 76;
        public const int BoxAreaLightEnd = 144;
        public const uint WaterMinVersion = 0x10002000u;
        public const uint WaterMinorVersion = 0x10003000u;

        private readonly GfxTextDecoder decoder;

        public EcbsdParser(GfxTextDecoder decoder = null)
        {
            this.decoder = decoder ?? new GfxTextDecoder();
        }

        public EcbsdDocument Parse(
            byte[] data,
            string sourceName,
            IReadOnlyDictionary<uint, uint> ornamentRefs,
            IReadOnlyDictionary<uint, uint> boxRefs,
            IReadOnlyDictionary<uint, uint> waterRefs,
            Func<string, byte[]> loadBsData = null)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            var document = new EcbsdDocument { SourcePath = sourceName ?? string.Empty };
            if (data.Length < BsdHeaderEnd)
            {
                document.Warnings.Add(sourceName + " fehlt oder ist keine Block-Datei");
                return document;
            }

            if (data[0] != (byte)'M' || data[1] != (byte)'O' || data[2] != (byte)'X' || data[3] != (byte)'B')
            {
                document.Warnings.Add(sourceName + " fehlt oder ist keine Block-Datei");
                return document;
            }

            var reader = new BinaryDataReader(data, sourceName);
            reader.Seek(4);
            var magic = reader.ReadUInt32LittleEndian();
            if (magic != BsdfMagic)
            {
                document.Warnings.Add(sourceName + " fehlt oder ist keine Block-Datei");
                return document;
            }

            reader.Seek(12);
            var nameCount = reader.ReadInt32LittleEndian();
            if (nameCount < 0 || nameCount > 4096)
                throw new InvalidDataException(sourceName + ": invalid BSData name count");

            reader.Seek(BsdHeaderEnd);
            for (var i = 0; i < nameCount; i++)
                document.BsDataNames.Add(ReadAString(reader));

            if (ornamentRefs != null)
            {
                foreach (var pair in ornamentRefs)
                {
                    var off = (int)pair.Value;
                    if (off < 0 || off + 40 + 4 > data.Length)
                        continue;
                    reader.Seek(off);
                    var ornament = new EcbsdOrnament
                    {
                        ExportId = pair.Key,
                        Pos = ReadVec3(reader),
                        Dir = ReadVec3(reader),
                        Up = ReadVec3(reader)
                    };
                    reader.Skip(4); // 3 flags + 1 byte
                    ornament.RelativePath = ReadAString(reader);
                    document.Ornaments.Add(ornament);
                }
            }

            if (boxRefs != null)
            {
                foreach (var pair in boxRefs)
                {
                    var off = (int)pair.Value;
                    if (off < 0 || off + BoxAreaLightEnd > data.Length)
                        continue;
                    reader.Seek(off);
                    var defArea = reader.ReadInt32LittleEndian();
                    if (defArea == 0)
                        continue;
                    reader.Seek(off + 80);
                    var dayBackground = ReadArgbRgb(reader.ReadUInt32LittleEndian());
                    reader.Seek(off + 112);
                    var nightBackground = ReadArgbRgb(reader.ReadUInt32LittleEndian());
                    reader.Seek(off + 116);
                    var dir = ReadVec3(reader);
                    var length = Length(dir);
                    if (length < 0.000001f)
                        continue;
                    dir[0] /= length; dir[1] /= length; dir[2] /= length;
                    var dayAmbient = ReadArgbRgb(reader.ReadUInt32LittleEndian());
                    var dayDir = ReadArgbRgb(reader.ReadUInt32LittleEndian());
                    var nightAmbient = ReadArgbRgb(reader.ReadUInt32LittleEndian());
                    var nightDir = ReadArgbRgb(reader.ReadUInt32LittleEndian());
                    document.DefaultLight = new EcbsdBoxLight
                    {
                        ExportId = pair.Key,
                        LightDirection = dir,
                        DayBackgroundRgb = dayBackground,
                        NightBackgroundRgb = nightBackground,
                        DayAmbientRgb = dayAmbient,
                        DayDirectionalRgb = dayDir,
                        NightAmbientRgb = nightAmbient,
                        NightDirectionalRgb = nightDir
                    };
                    break; // Godot uses the first default box area
                }
            }

            if (waterRefs != null && waterRefs.Count > 0 && loadBsData != null)
            {
                var dats = new Dictionary<int, BsDataFile>();
                foreach (var pair in waterRefs)
                {
                    var off = (int)pair.Value;
                    if (off < 0 || off + 8 > data.Length)
                        continue;
                    reader.Seek(off);
                    var sub = reader.ReadInt32LittleEndian();
                    var index = reader.ReadInt32LittleEndian();
                    if (sub < 0 || sub >= document.BsDataNames.Count)
                        continue;

                    BsDataFile dat;
                    if (!dats.TryGetValue(sub, out dat))
                    {
                        var name = document.BsDataNames[sub];
                        byte[] bytes;
                        try { bytes = loadBsData(name); }
                        catch (Exception e) when (
                            e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
                        {
                            document.Warnings.Add(name + " nicht gefunden, Wasser fehlt (" + e.Message + ")");
                            dats[sub] = null;
                            continue;
                        }

                        if (bytes == null || bytes.Length < 16)
                        {
                            document.Warnings.Add(name + " nicht gefunden, Wasser fehlt");
                            dats[sub] = null;
                            continue;
                        }

                        dat = ReadBsData(bytes, name);
                        dats[sub] = dat;
                        if (dat == null)
                            continue;
                    }

                    if (dat == null || index < 0 || index >= dat.Offsets.Count)
                        continue;

                    var area = ReadWaterArea(dat.Bytes, dat.Offsets[index], pair.Key, document.Warnings);
                    if (area != null)
                        document.WaterAreas.Add(area);
                }
            }

            return document;
        }

        /// <summary>
        /// Building relative paths for litmodels\&lt;map&gt;\ as Godot building_files returns.
        /// </summary>
        public static List<string> BuildingFiles(EcbsdDocument document)
        {
            var list = new List<string>();
            if (document == null)
                return list;
            foreach (var ornament in document.Ornaments)
            {
                if (!string.IsNullOrEmpty(ornament.RelativePath))
                    list.Add(ornament.RelativePath.Replace('\\', '/'));
            }

            return list;
        }

        private static BsDataFile ReadBsData(byte[] bytes, string name)
        {
            if (bytes.Length < 16)
                return null;
            var n = BitConverter.ToUInt32(bytes, bytes.Length - 4);
            if ((long)n * 4 + 4 > bytes.Length)
                return null;
            var offsets = new List<int>((int)n);
            for (var i = 0; i < (int)n; i++)
            {
                var pos = bytes.Length - 4 - ((int)n - i) * 4;
                offsets.Add(unchecked((int)BitConverter.ToUInt32(bytes, pos)));
            }

            return new BsDataFile { Bytes = bytes, Offsets = offsets, Name = name };
        }

        private static WorldWaterArea ReadWaterArea(
            byte[] bytes, int offset, uint fallbackId, List<string> warnings)
        {
            if (offset < 0 || offset + 44 > bytes.Length)
                return null;
            var ver = BitConverter.ToUInt32(bytes, offset);
            if (ver < WaterMinVersion)
            {
                warnings.Add("Wasser " + fallbackId + ": Version 0x" + ver.ToString("X") + " wird nicht unterstützt");
                return null;
            }

            var id = BitConverter.ToUInt32(bytes, offset + 4);
            var height = BitConverter.ToSingle(bytes, offset + 12);
            var width = BitConverter.ToInt32(bytes, offset + 20);
            var length = BitConverter.ToInt32(bytes, offset + 24);
            var grid = BitConverter.ToSingle(bytes, offset + 28);
            var center = new[]
            {
                BitConverter.ToSingle(bytes, offset + 32),
                BitConverter.ToSingle(bytes, offset + 36),
                BitConverter.ToSingle(bytes, offset + 40)
            };
            var q = offset + 44 + (ver >= WaterMinorVersion ? 1 : 0);
            if (width <= 0 || length <= 0 || q + width * length > bytes.Length)
                return null;
            var flags = new byte[width * length];
            Buffer.BlockCopy(bytes, q, flags, 0, flags.Length);
            return new WorldWaterArea
            {
                Id = id,
                Height = height,
                Width = width,
                Length = length,
                Grid = grid,
                Center = center,
                Flags = flags
            };
        }

        private string ReadAString(BinaryDataReader reader)
        {
            var length = reader.ReadInt32LittleEndian();
            if (length < 0 || length > 1 << 20)
                throw new InvalidDataException("invalid AString length " + length);
            if (length == 0)
                return string.Empty;
            return decoder.DecodeGbk(reader.ReadBytes(length));
        }

        private static float[] ReadVec3(BinaryDataReader reader)
        {
            return new[]
            {
                reader.ReadSingleLittleEndian(),
                reader.ReadSingleLittleEndian(),
                reader.ReadSingleLittleEndian()
            };
        }

        private static float[] ReadArgbRgb(uint color)
        {
            return new[]
            {
                ((color >> 16) & 0xFF) / 255f,
                ((color >> 8) & 0xFF) / 255f,
                (color & 0xFF) / 255f
            };
        }

        private static float Length(float[] v)
        {
            return (float)Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
        }

        private sealed class BsDataFile
        {
            public byte[] Bytes;
            public List<int> Offsets;
            public string Name;
        }
    }
}
