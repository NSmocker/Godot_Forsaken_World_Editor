using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>Bounded readers for Angelica2 Loader B, matching angelica_terrain.gd.</summary>
    public sealed class TerrainParser
    {
        private readonly GfxTextDecoder decoder;
        public TerrainParser(GfxTextDecoder decoder = null) { this.decoder = decoder ?? new GfxTextDecoder(); }

        public TerrainDocument Parse(byte[] data, string source = "TRN2")
        {
            var r = Reader(data, source, 0x54524e32, 1, out _);
            var t = new TerrainDocument();
            t.Width = Positive(r); t.Length = Positive(r); t.BlockSize = Positive(r);
            t.BlockGrid = Count(r, 256, 1); t.BlockCount = Count(r, 1048576, 1);
            t.BlockRows = Count(r, 65536, 1); t.BlockColumns = Count(r, 65536, 1);
            int textures = Count(r, 2048, 1);
            t.MaskAreaCount = Count(r, 65536, 1); t.MaskBlock = Count(r, 64, 1);
            t.MaskGrid = Count(r, 1024, 1);
            int subs = Count(r, 4096, 1);
            t.SubRows = Count(r, 4096, 1); t.SubColumns = Count(r, 4096, 1);
            t.SubGrid = Count(r, 16384, 1); t.GridSize = Positive(r);
            int textureOffset = Offset(r);
            Require(r.ReadInt32LittleEndian() == 1, source + ": only terrain Loader B (1) is supported");
            int loaderOffset = Offset(r);
            r.Skip(56);
            Require((long)t.BlockRows * t.BlockColumns == t.BlockCount &&
                t.SubRows * t.SubColumns == subs && t.MaskBlock * t.BlockGrid == t.MaskGrid &&
                t.SubGrid % t.MaskGrid == 0 &&
                (long)t.SubRows * t.SubGrid == (long)t.BlockRows * t.BlockGrid &&
                (long)t.SubColumns * t.SubGrid == (long)t.BlockColumns * t.BlockGrid &&
                (long)t.AreasPerSubSide * t.AreasPerSubSide * subs == t.MaskAreaCount,
                source + ": inconsistent terrain grid dimensions");
            Require(Math.Abs(t.Width - t.BlockColumns * t.BlockGrid * t.GridSize) < 0.1f &&
                Math.Abs(t.Length - t.BlockRows * t.BlockGrid * t.GridSize) < 0.1f,
                source + ": terrain size does not match grid spacing");
            t.Subs = new TerrainSub[subs];
            for (int i = 0; i < subs; i++)
                t.Subs[i] = new TerrainSub { MinHeight = Finite(r), MaxHeight = Finite(r) };
            Require(textureOffset >= r.Position && loaderOffset >= r.Position,
                source + ": invalid terrain section offsets");
            r.Seek(textureOffset);
            t.Textures = new string[textures];
            for (int i = 0; i < textures; i++) t.Textures[i] = ReadString(r);
            r.Seek(loaderOffset);
            t.MaskMapSize = Count(r, 4096);
            foreach (var sub in t.Subs)
            {
                sub.Name = ReadString(r);
                Require(!string.IsNullOrWhiteSpace(sub.Name) && sub.Name.IndexOfAny(new[] {'/', '\\', ':', '\0'}) < 0 &&
                    sub.Name != "." && sub.Name != "..", source + ": invalid subterrain name");
            }
            return t;
        }

        public Dictionary<long, TerrainBlock> ReadBlocks(byte[] data, int blockGrid, string source = "T2BK")
        {
            Require(blockGrid > 0 && blockGrid <= 256, source + ": invalid block grid");
            int vertices = (blockGrid + 1) * (blockGrid + 1);
            var r = Reader(data, source, 0x54424b42, 6, out int version);
            int count = Count(r, 1048576);
            int compression = 0;
            if (version >= 2)
            {
                compression = r.ReadByte(); r.Skip(3);
                if (version < 5) compression = compression == 0 ? 0 : 0x15;
            }
            Require((compression & ~0x3f) == 0, source + ": unknown compression flags");
            var offsets = Offsets(r, count);
            var blocks = new Dictionary<long, TerrainBlock>();
            int header = new[] { 0, 16, 24, 28, 32, 32, 36 }[version];
            foreach (int offset in offsets)
            {
                r.Seek(offset);
                var b = new TerrainBlock { MaskArea = r.ReadInt32LittleEndian(), Row = r.ReadUInt16LittleEndian(), Column = r.ReadUInt16LittleEndian() };
                r.Skip(8);
                int heightSize = vertices * 4, normalSize = vertices * 12;
                if (version >= 2) { heightSize = Count(r, data.Length); normalSize = Count(r, data.Length); }
                r.Seek(checked(offset + header));
                var height = r.ReadBytes(heightSize);
                var normal = r.ReadBytes(normalSize);
                if (version >= 6) { r.Seek(offset + 32); b.Flags = r.ReadUInt32LittleEndian(); }
                int expectedHeight = (compression & 8) != 0 ? 16 + vertices * 2 : vertices * 4;
                int expectedNormal = (compression & 32) != 0 ? 8 + vertices * 2 : vertices * 12;
                if ((compression & 4) != 0) height = Inflate(height, expectedHeight, source + " heights");
                if ((compression & 16) != 0) normal = Inflate(normal, expectedNormal, source + " normals");
                Require(height.Length == expectedHeight && normal.Length == expectedNormal, source + ": invalid block payload size");
                b.Heights = new float[vertices]; b.Normals = new float[vertices * 3];
                var h = new BinaryDataReader(height, source + " heights");
                if ((compression & 8) != 0)
                {
                    h.Skip(4); float min = Finite(h), scale = (Finite(h) - min) / 32767f;
                    Require(h.ReadInt32LittleEndian() == vertices, source + ": invalid incremental height count");
                    int accumulator = 0;
                    for (int i = 0; i < vertices; i++)
                    {
                        accumulator += unchecked((short)h.ReadUInt16LittleEndian());
                        b.Heights[i] = min + accumulator * scale;
                        Require(!float.IsNaN(b.Heights[i]) && !float.IsInfinity(b.Heights[i]), source + ": invalid height");
                    }
                }
                else for (int i = 0; i < vertices; i++) b.Heights[i] = Finite(h);
                var n = new BinaryDataReader(normal, source + " normals");
                if ((compression & 32) != 0)
                {
                    n.Skip(4);
                    Require(n.ReadInt32LittleEndian() == vertices, source + ": invalid compressed normal count");
                    for (int i = 0; i < vertices; i++)
                    {
                        double az = n.ReadByte() * Math.PI / 128, inc = n.ReadByte() * Math.PI / 128;
                        b.Normals[i * 3] = (float)(Math.Sin(inc) * Math.Cos(az));
                        b.Normals[i * 3 + 1] = (float)Math.Cos(inc);
                        b.Normals[i * 3 + 2] = (float)(Math.Sin(inc) * Math.Sin(az));
                    }
                }
                else for (int i = 0; i < b.Normals.Length; i++) b.Normals[i] = Finite(n);
                Require(!blocks.ContainsKey(b.PositionKey), source + ": duplicate block coordinates");
                blocks.Add(b.PositionKey, b);
            }
            return blocks;
        }

        public TerrainMasks ReadMasks(byte[] data, string source = "T2MK")
        {
            var r = Reader(data, source, 0x544d4b42, 2, out int version);
            int areaCount = Count(r, 65536), layerCount = Count(r, 65536);
            bool compressed = false;
            if (version >= 2) { compressed = r.ReadByte() != 0; r.Skip(3); }
            var offsets = Offsets(r, areaCount);
            Require((long)layerCount * 28 <= r.Remaining, source + ": truncated layer table");
            var result = new TerrainMasks { Layers = new TerrainLayer[layerCount], Areas = new TerrainAreaLayer[areaCount][] };
            for (int i = 0; i < layerCount; i++)
            {
                var layer = new TerrainLayer { TextureIndex = r.ReadInt32LittleEndian() };
                r.Skip(4); layer.MaskSize = Count(r, 4096); layer.Axis = r.ReadByte(); r.Skip(3);
                layer.UScale = Finite(r); layer.VScale = Finite(r); r.Skip(4);
                Require(layer.Axis <= 2, source + ": unknown texture projection axis");
                result.Layers[i] = layer;
            }
            for (int a = 0; a < areaCount; a++)
            {
                r.Seek(offsets[a]); int count = Count(r, 256);
                var area = result.Areas[a] = new TerrainAreaLayer[count];
                for (int i = 0; i < count; i++)
                {
                    int layer = Count(r, layerCount - 1);
                    int offset = Offset(r), size = version >= 2 ? Count(r, data.Length) : 0;
                    var item = area[i] = new TerrainAreaLayer { LayerIndex = layer };
                    if (i == 0) continue; // First layer is opaque; its mask offset may be zero.
                    int side = result.Layers[layer].MaskSize;
                    Require(side > 0, source + ": empty overlay mask");
                    int next = r.Position;
                    r.Seek(offset);
                    item.Mask = r.ReadBytes(compressed ? size : side * side);
                    if (compressed) item.Mask = Inflate(item.Mask, side * side, source + " mask");
                    r.Seek(next);
                }
            }
            return result;
        }

        public TerrainLightmap[] ReadLightmaps(byte[] data, string source = "T2LM")
        {
            var r = Reader(data, source, 0x54324c4d, 2, out int version);
            int count = Count(r, 65536), size = Count(r, 4096, 1);
            uint flags = version >= 2 ? r.ReadUInt32LittleEndian() : 0;
            int bytes = version >= 2 ? Count(r, data.Length) : checked(size * size * 3);
            Require((flags & ~1u) == 0 && ((flags & 1) != 0 || bytes == size * size * 3), source + ": unsupported lightmap format");
            var offsets = Offsets(r, count);
            var result = new TerrainLightmap[count];
            for (int i = 0; i < count; i++)
            {
                r.Seek(offsets[i]);
                result[i] = new TerrainLightmap { Size = size, IsDds = (flags & 1) != 0, Data = r.ReadBytes(bytes) };
            }
            return result;
        }

        // Unity's .NET Standard profile lacks ZLibStream. Validate the zlib envelope,
        // use DeflateStream for its payload, then check the Adler32 and exact output size.
        internal static byte[] Inflate(byte[] data, int expected, string source)
        {
            Require(data.Length >= 6 && (data[0] & 15) == 8 && (data[0] >> 4) <= 7 &&
                ((data[0] << 8) + data[1]) % 31 == 0 && (data[1] & 32) == 0, source + ": invalid zlib header");
            var output = new byte[expected];
            using (var input = new MemoryStream(data, 2, data.Length - 6, false))
            using (var stream = new DeflateStream(input, CompressionMode.Decompress))
            {
                int read = 0, n;
                while (read < expected && (n = stream.Read(output, read, expected - read)) != 0) read += n;
                Require(read == expected && stream.ReadByte() == -1, source + ": invalid decompressed size");
            }
            uint a = 1, b = 0;
            foreach (byte value in output) { a = (a + value) % 65521; b = (b + a) % 65521; }
            int end = data.Length - 4;
            uint checksum = (uint)data[end] << 24 | (uint)data[end + 1] << 16 | (uint)data[end + 2] << 8 | data[end + 3];
            Require(checksum == (b << 16 | a), source + ": invalid zlib checksum");
            return output;
        }

        private static BinaryDataReader Reader(byte[] data, string source, uint magic, int maxVersion, out int version)
        {
            var r = new BinaryDataReader(data, source);
            Require(r.ReadUInt32LittleEndian() == magic, source + ": invalid signature");
            version = r.ReadInt32LittleEndian();
            Require(version >= 1 && version <= maxVersion, source + ": unsupported version " + version);
            return r;
        }
        private string ReadString(BinaryDataReader r) => decoder.DecodeGbk(r.ReadBytes(Count(r, 32768))).TrimEnd('\0');
        private static int Count(BinaryDataReader r, int max, int min = 0)
        {
            int value = r.ReadInt32LittleEndian();
            Require(value >= min && value <= max, "Invalid terrain count/size " + value + " at " + (r.Position - 4));
            return value;
        }
        private static int Offset(BinaryDataReader r) => Count(r, r.Length);
        private static int[] Offsets(BinaryDataReader r, int count)
        {
            Require((long)count * 4 <= r.Remaining, "Truncated terrain offset table");
            int firstData = checked(r.Position + count * 4);
            var offsets = new int[count];
            for (int i = 0; i < count; i++) { offsets[i] = Offset(r); Require(offsets[i] >= firstData, "Terrain offset overlaps header"); }
            return offsets;
        }
        private static float Finite(BinaryDataReader r)
        {
            float value = r.ReadSingleLittleEndian();
            Require(!float.IsNaN(value) && !float.IsInfinity(value), "Non-finite terrain value");
            return value;
        }
        private static float Positive(BinaryDataReader r) { float value = Finite(r); Require(value > 0, "Non-positive terrain dimension"); return value; }
        internal static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    }
}
