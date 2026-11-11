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
            Func<string, byte[]> loadBsData = null,
            IReadOnlyDictionary<uint, uint> grassRefs = null)
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

            reader.Seek(8);
            document.Version = reader.ReadUInt32LittleEndian();
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

            var dats = new Dictionary<int, BsDataFile>();
            var waterIndex = new Dictionary<uint, int>();
            var grassIndex = new Dictionary<uint, int>();
            if (document.Version >= 7 && loadBsData != null)
                RepairSwappedDataIndices(data, document, waterRefs, grassRefs, dats, loadBsData, waterIndex, grassIndex);

            if (waterRefs != null && waterRefs.Count > 0 && loadBsData != null)
            {
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
                    int repaired;
                    if (waterIndex.TryGetValue(pair.Key, out repaired))
                        index = repaired;

                    var dat = GetBsData(document, sub, dats, loadBsData, "Wasser");
                    if (dat == null || index < 0 || index >= dat.Offsets.Count)
                        continue;

                    var area = ReadWaterArea(dat.Bytes, dat.Offsets[index], pair.Key, document.Warnings);
                    if (area != null)
                        document.WaterAreas.Add(area);
                }
            }

            if (grassRefs != null && grassRefs.Count > 0)
                ReadGrass(reader, data, document, grassRefs, loadBsData, dats, grassIndex);

            return document;
        }

        private static BsDataFile GetBsData(
            EcbsdDocument document,
            int sub,
            Dictionary<int, BsDataFile> dats,
            Func<string, byte[]> loadBsData,
            string what)
        {
            BsDataFile dat;
            if (dats.TryGetValue(sub, out dat))
                return dat;
            var name = document.BsDataNames[sub];
            byte[] bytes;
            try { bytes = loadBsData(name); }
            catch (Exception e) when (
                e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                document.Warnings.Add(name + " nicht gefunden, " + what + " fehlt (" + e.Message + ")");
                dats[sub] = null;
                return null;
            }

            dat = bytes == null || bytes.Length < 16 ? null : ReadBsData(bytes, name);
            dats[sub] = dat;
            if (dat == null)
                document.Warnings.Add(name + " nicht gefunden, " + what + " fehlt");
            return dat;
        }

        /// <summary>
        /// Some shipped maps (world: grass 13753 / water 13748 and grass 25174 / water 25161,
        /// a33: grass+water 1388) carry ECBSDFILEGRASS7 / ECBSDFILEWATER records whose
        /// iDataIndex values are swapped inside one sub-terrain: the grass record points at
        /// the A3DWaterArea blob and the water record at the grass ECWDDTFILECHUNK.
        /// CECScene::LoadGrassArea / LoadWaterArea (CElementClient/EC_Scene.cpp) reject both
        /// (chunk dwOriginSize != bits length, water version &lt; 0x10001000). When exactly one
        /// grass and one water record of a sub-terrain are crossed like that and the swapped
        /// targets fit (water blob / grass chunk of the expected size), swap them back.
        /// </summary>
        private void RepairSwappedDataIndices(
            byte[] data,
            EcbsdDocument document,
            IReadOnlyDictionary<uint, uint> waterRefs,
            IReadOnlyDictionary<uint, uint> grassRefs,
            Dictionary<int, BsDataFile> dats,
            Func<string, byte[]> loadBsData,
            Dictionary<uint, int> waterIndex,
            Dictionary<uint, int> grassIndex)
        {
            if (waterRefs == null || grassRefs == null || waterRefs.Count == 0 || grassRefs.Count == 0)
                return;

            var waters = new Dictionary<int, List<KeyValuePair<uint, int>>>();
            foreach (var pair in waterRefs)
            {
                var off = (int)pair.Value;
                if (off < 0 || off + 8 > data.Length)
                    continue;
                var sub = BitConverter.ToInt32(data, off);
                var index = BitConverter.ToInt32(data, off + 4);
                if (sub < 0 || sub >= document.BsDataNames.Count)
                    continue;
                List<KeyValuePair<uint, int>> list;
                if (!waters.TryGetValue(sub, out list))
                    waters[sub] = list = new List<KeyValuePair<uint, int>>();
                list.Add(new KeyValuePair<uint, int>(pair.Key, index));
            }

            if (waters.Count == 0)
                return;

            var grasses = new Dictionary<int, List<KeyValuePair<uint, int>>>();
            var grassBytes = new Dictionary<uint, int>();
            foreach (var pair in grassRefs)
            {
                var off = (int)pair.Value;
                if (off < 0 || off + 36 > data.Length)
                    continue;
                var sub = BitConverter.ToInt32(data, off + 28);
                if (!waters.ContainsKey(sub))
                    continue;
                var expected = GrassBitLength(BitConverter.ToInt32(data, off + 20), BitConverter.ToInt32(data, off + 24));
                if (expected < 0)
                    continue;
                List<KeyValuePair<uint, int>> list;
                if (!grasses.TryGetValue(sub, out list))
                    grasses[sub] = list = new List<KeyValuePair<uint, int>>();
                list.Add(new KeyValuePair<uint, int>(pair.Key, BitConverter.ToInt32(data, off + 32)));
                grassBytes[pair.Key] = expected;
            }

            foreach (var entry in grasses)
            {
                var dat = GetBsData(document, entry.Key, dats, loadBsData, "Gras/Wasser");
                if (dat == null || !dat.Chunked)
                    continue;

                var badGrass = new List<KeyValuePair<uint, int>>();
                foreach (var g in entry.Value)
                {
                    if (IsWaterBlob(dat, g.Value))
                        badGrass.Add(g);
                }

                var badWater = new List<KeyValuePair<uint, int>>();
                foreach (var w in waters[entry.Key])
                {
                    if (!IsWaterBlob(dat, w.Value) && w.Value >= 0 && w.Value < dat.Offsets.Count)
                        badWater.Add(w);
                }

                if (badGrass.Count != 1 || badWater.Count != 1)
                    continue;
                var grass = badGrass[0];
                var water = badWater[0];
                if (!IsGrassChunk(dat, water.Value, grassBytes[grass.Key]))
                    continue;

                grassIndex[grass.Key] = water.Value;
                waterIndex[water.Key] = grass.Value;
                document.Warnings.Add(
                    "Gras " + grass.Key + " / Wasser " + water.Key + ": Datenindex " + grass.Value + " und " +
                    water.Value + " in " + dat.Name + " vertauscht, repariert");
            }
        }

        private static bool IsWaterBlob(BsDataFile dat, int index)
        {
            if (index < 0 || index >= dat.Offsets.Count)
                return false;
            var off = dat.Offsets[index];
            if (off < 0 || off + 4 > dat.Bytes.Length)
                return false;
            var version = BitConverter.ToUInt32(dat.Bytes, off);
            return version >= 0x10001000u && version <= 0x1000FFFFu;
        }

        private static bool IsGrassChunk(BsDataFile dat, int index, int expected)
        {
            if (index < 0 || index >= dat.Offsets.Count)
                return false;
            var off = dat.Offsets[index];
            if (off < 0 || off + 8 > dat.Bytes.Length)
                return false;
            var comp = BitConverter.ToInt32(dat.Bytes, off);
            var origin = BitConverter.ToInt32(dat.Bytes, off + 4);
            return origin == expected && comp > 0 && off + 8L + comp <= dat.Bytes.Length;
        }

        private void ReadGrass(
            BinaryDataReader reader,
            byte[] data,
            EcbsdDocument document,
            IReadOnlyDictionary<uint, uint> grassRefs,
            Func<string, byte[]> loadBsData,
            Dictionary<int, BsDataFile> dats,
            Dictionary<uint, int> repairedIndex)
        {
            foreach (var pair in grassRefs)
            {
                var off = (int)pair.Value;
                var record = document.Version >= 7 ? 36 : 24;
                if (off < 0 || off + record > data.Length)
                {
                    document.Warnings.Add("Gras " + pair.Key + ": Datensatz abgeschnitten");
                    continue;
                }

                reader.Seek(off);
                var area = new EcbsdGrassArea
                {
                    ExportId = pair.Key,
                    TypeId = reader.ReadUInt32LittleEndian(),
                    Center = ReadVec3(reader),
                    GridSize = reader.ReadSingleLittleEndian(),
                    Columns = reader.ReadInt32LittleEndian(),
                    Rows = reader.ReadInt32LittleEndian()
                };
                if (document.Version >= 7)
                {
                    area.SubTerrain = reader.ReadInt32LittleEndian();
                    area.DataIndex = reader.ReadInt32LittleEndian();
                    int repaired;
                    if (repairedIndex.TryGetValue(pair.Key, out repaired))
                        area.DataIndex = repaired;
                }

                var expected = GrassBitLength(area.Columns, area.Rows);
                if (area.GridSize <= 0f || expected < 0)
                {
                    document.Warnings.Add("Gras " + pair.Key + ": ungültiges Raster");
                    continue;
                }

                if (document.Version < 7)
                {
                    if (reader.Remaining < expected)
                    {
                        document.Warnings.Add("Gras " + pair.Key + ": Bits abgeschnitten");
                        continue;
                    }

                    area.Bits = reader.ReadBytes(expected);
                }
                else
                {
                    area.Bits = LoadGrassBits(document, area, expected, dats, loadBsData);
                    if (area.Bits == null)
                        continue;
                }

                document.GrassAreas.Add(area);
            }
        }

        private static byte[] LoadGrassBits(
            EcbsdDocument document,
            EcbsdGrassArea area,
            int expected,
            Dictionary<int, BsDataFile> dats,
            Func<string, byte[]> loadBsData)
        {
            if (loadBsData == null || area.SubTerrain < 0 || area.SubTerrain >= document.BsDataNames.Count)
            {
                document.Warnings.Add("Gras " + area.ExportId + ": BSData fehlt");
                return null;
            }

            var dat = GetBsData(document, area.SubTerrain, dats, loadBsData, "Gras");
            if (dat == null)
                return null;

            if (area.DataIndex < 0 || area.DataIndex >= dat.Offsets.Count)
            {
                document.Warnings.Add("Gras " + area.ExportId + ": Datenindex fehlt");
                return null;
            }

            var offset = dat.Offsets[area.DataIndex];
            if (dat.HasHeader)
                return ReadGrassChunk(dat, offset, expected, area.ExportId, document.Warnings);
            return ReadGrassPayload(dat.Bytes, offset, expected, dat.Name, area.ExportId, document.Warnings);
        }

        /// <summary>
        /// CECScene::LoadGrassArea for an AFile BSData with header: version &lt; 2 holds the raw
        /// bits; version &gt;= 2 an ECWDDTFILECHUNK {dwCompSize, dwOriginSize} whose origin size
        /// must equal (col * row) &gt;&gt; 3, then zlib data (bCompress) or the raw bits.
        /// </summary>
        private static byte[] ReadGrassChunk(BsDataFile dat, int offset, int expected, uint exportId, List<string> warnings)
        {
            var bytes = dat.Bytes;
            if (!dat.Chunked)
            {
                if (offset < 0 || offset + (long)expected > bytes.Length)
                {
                    warnings.Add("Gras " + exportId + ": Bits abgeschnitten");
                    return null;
                }

                var plain = new byte[expected];
                Buffer.BlockCopy(bytes, offset, plain, 0, expected);
                return plain;
            }

            if (offset < 0 || offset + 8 > bytes.Length)
            {
                warnings.Add("Gras " + exportId + ": Bits abgeschnitten");
                return null;
            }

            var comp = BitConverter.ToInt32(bytes, offset);
            var origin = BitConverter.ToInt32(bytes, offset + 4);
            if (origin != expected)
            {
                var hint = IsWaterBlob(dat, dat.Offsets.IndexOf(offset)) ? " (Datenindex zeigt auf Wasser)" : string.Empty;
                warnings.Add("Gras " + exportId + ": Datenblock hat " + origin + " statt " + expected + " Bytes" + hint);
                return null;
            }

            var size = dat.Compressed ? comp : expected;
            if (size <= 0 || offset + 8L + size > bytes.Length)
            {
                warnings.Add("Gras " + exportId + ": Bits abgeschnitten");
                return null;
            }

            var payload = new byte[size];
            Buffer.BlockCopy(bytes, offset + 8, payload, 0, size);
            if (!dat.Compressed || comp == expected)
                return payload;
            try
            {
                return TerrainParser.Inflate(payload, expected, dat.Name + " grass");
            }
            catch (InvalidDataException e)
            {
                warnings.Add("Gras " + exportId + ": " + e.Message);
                return null;
            }
        }

        internal static int GrassBitLength(int columns, int rows)
        {
            if (columns <= 0 || rows <= 0 || columns > 8192 || rows > 8192)
                return -1;
            if ((columns & 7) != 0)
                return -1;
            var bits = (long)columns * rows;
            if ((bits & 7) != 0 || bits / 8 > 8 * 1024 * 1024)
                return -1;
            return (int)(bits / 8);
        }

        internal static byte[] ReadGrassPayload(
            byte[] bytes,
            int offset,
            int expected,
            string name,
            uint exportId,
            List<string> warnings)
        {
            if (bytes == null || offset < 0 || offset + 8 > bytes.Length)
            {
                warnings.Add("Gras " + exportId + ": Bits abgeschnitten");
                return null;
            }

            var comp = BitConverter.ToInt32(bytes, offset);
            var origin = BitConverter.ToInt32(bytes, offset + 4);
            if (origin == expected && comp > 0 && offset + 8L + comp <= bytes.Length)
            {
                var payload = new byte[comp];
                Buffer.BlockCopy(bytes, offset + 8, payload, 0, comp);
                if (comp == expected)
                    return payload;
                try
                {
                    return TerrainParser.Inflate(payload, expected, name + " grass");
                }
                catch (InvalidDataException e)
                {
                    warnings.Add("Gras " + exportId + ": " + e.Message);
                    return null;
                }
            }

            if (offset + expected <= bytes.Length)
            {
                var raw = new byte[expected];
                Buffer.BlockCopy(bytes, offset, raw, 0, expected);
                return raw;
            }

            warnings.Add("Gras " + exportId + ": Bits abgeschnitten");
            return null;
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

            // AFile head "MOXB", then ECWDDTFILEHEADER {dwVersion, bCompress}
            // (CECScene::PrepareSubTerrainData). Without the head keep the lenient path.
            var hasHeader = bytes[0] == (byte)'M' && bytes[1] == (byte)'O' && bytes[2] == (byte)'X' && bytes[3] == (byte)'B';
            var version = hasHeader ? BitConverter.ToUInt32(bytes, 4) : 0u;
            return new BsDataFile
            {
                Bytes = bytes,
                Offsets = offsets,
                Name = name,
                HasHeader = hasHeader,
                Chunked = hasHeader && version >= 2,
                Compressed = hasHeader && version >= 2 && bytes[8] != 0
            };
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
            public bool HasHeader;
            public bool Chunked;
            public bool Compressed;
        }
    }
}
