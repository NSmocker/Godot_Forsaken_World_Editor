using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class TerrainParserTests
    {
        [Test]
        public void ParsesTrn2HeaderTexturesAndSubNames()
        {
            var document = CreateParser().Parse(BuildTrn2(), "fixture.trn2");

            Assert.AreEqual(2f, document.Width);
            Assert.AreEqual(2f, document.Length);
            Assert.AreEqual(2, document.BlockGrid);
            Assert.AreEqual(1, document.BlockCount);
            Assert.AreEqual(1, document.MaskAreaCount);
            Assert.AreEqual(1, document.MaskBlock);
            Assert.AreEqual(2, document.MaskGrid);
            Assert.AreEqual(1, document.SubRows);
            Assert.AreEqual(1, document.SubColumns);
            Assert.AreEqual(2, document.SubGrid);
            Assert.AreEqual(1f, document.GridSize);
            Assert.AreEqual(1, document.Textures.Length);
            Assert.AreEqual("tex.dds", document.Textures[0]);
            Assert.AreEqual(1, document.Subs.Length);
            Assert.AreEqual("sub0", document.Subs[0].Name);
            Assert.AreEqual(-1f, document.Subs[0].MinHeight);
            Assert.AreEqual(3f, document.Subs[0].MaxHeight);
            Assert.AreEqual(8, document.MaskMapSize);
        }

        [Test]
        public void RejectsUnsupportedLoaderAndInconsistentGrid()
        {
            var unsupported = BuildTrn2();
            WriteInt32(unsupported, 76, 2);
            var error = Assert.Throws<InvalidDataException>(
                () => CreateParser().Parse(unsupported, "bad-loader.trn2"));
            StringAssert.Contains("Loader B", error.Message);

            var inconsistent = BuildTrn2();
            WriteInt32(inconsistent, 40, 9); // num_mask_area
            error = Assert.Throws<InvalidDataException>(
                () => CreateParser().Parse(inconsistent, "bad-grid.trn2"));
            StringAssert.Contains("inconsistent", error.Message);
        }

        [Test]
        public void ReadsUncompressedAndCompressedBlocks()
        {
            var parser = CreateParser();
            var raw = parser.ReadBlocks(BuildT2bk(version: 1, compressed: false, noRender: false), 2, "raw.t2bk");
            Assert.AreEqual(1, raw.Count);
            Assert.IsTrue(raw.ContainsKey(TerrainBlock.Key(0, 0)));
            Assert.AreEqual(9, raw[TerrainBlock.Key(0, 0)].Heights.Length);
            Assert.AreEqual(1.5f, raw[TerrainBlock.Key(0, 0)].Heights[0], 0.0001f);
            Assert.AreEqual(0f, raw[TerrainBlock.Key(0, 0)].Normals[0], 0.0001f);
            Assert.AreEqual(1f, raw[TerrainBlock.Key(0, 0)].Normals[1], 0.0001f);

            var flagged = parser.ReadBlocks(BuildT2bk(version: 6, compressed: false, noRender: true), 2, "flags.t2bk");
            Assert.IsTrue(flagged[TerrainBlock.Key(0, 0)].NoRender);

            var compressed = parser.ReadBlocks(BuildT2bk(version: 5, compressed: true, noRender: false), 2, "zip.t2bk");
            Assert.AreEqual(9, compressed[TerrainBlock.Key(0, 0)].Heights.Length);
            Assert.AreEqual(1.5f, compressed[TerrainBlock.Key(0, 0)].Heights[0], 0.001f);
        }

        [Test]
        public void ReadsMasksIncludingCompressedOverlays()
        {
            var parser = CreateParser();
            var plain = parser.ReadMasks(BuildT2mk(compressed: false), "plain.t2mk");
            Assert.AreEqual(2, plain.Layers.Length);
            Assert.AreEqual(1, plain.Areas.Length);
            Assert.AreEqual(2, plain.Areas[0].Length);
            Assert.AreEqual(0, plain.Areas[0][0].LayerIndex);
            Assert.AreEqual(0, plain.Areas[0][0].Mask.Length);
            Assert.AreEqual(1, plain.Areas[0][1].LayerIndex);
            Assert.AreEqual(16, plain.Areas[0][1].Mask.Length);
            Assert.AreEqual(200, plain.Areas[0][1].Mask[0]);

            var zipped = parser.ReadMasks(BuildT2mk(compressed: true), "zip.t2mk");
            Assert.AreEqual(16, zipped.Areas[0][1].Mask.Length);
            Assert.AreEqual(200, zipped.Areas[0][1].Mask[0]);
        }

        [Test]
        public void ReadsRawAndDdsLightmaps()
        {
            var parser = CreateParser();
            var raw = parser.ReadLightmaps(BuildT2lm(dds: false), "raw.t2lm");
            Assert.AreEqual(1, raw.Length);
            Assert.AreEqual(2, raw[0].Size);
            Assert.IsFalse(raw[0].IsDds);
            Assert.AreEqual(12, raw[0].Data.Length);
            Assert.AreEqual(10, raw[0].Data[0]);

            var dds = parser.ReadLightmaps(BuildT2lm(dds: true), "dds.t2lm");
            Assert.AreEqual(1, dds.Length);
            Assert.IsTrue(dds[0].IsDds);
            Assert.AreEqual(128, dds[0].Data.Length);
            Assert.AreEqual((byte)'D', dds[0].Data[0]);
            Assert.AreEqual((byte)'D', dds[0].Data[1]);
            Assert.AreEqual((byte)'S', dds[0].Data[2]);
            Assert.AreEqual((byte)' ', dds[0].Data[3]);
        }

        [Test]
        public void RejectsBadSignaturesAndTruncation()
        {
            var parser = CreateParser();
            var bad = BuildTrn2();
            bad[0] = 0;
            Assert.Throws<InvalidDataException>(() => parser.Parse(bad, "bad.trn2"));

            var truncated = BuildT2bk(version: 1, compressed: false, noRender: false);
            Array.Resize(ref truncated, truncated.Length - 4);
            Assert.Throws<InvalidDataException>(() => parser.ReadBlocks(truncated, 2, "trunc.t2bk"));
        }

        private static TerrainParser CreateParser()
        {
            return new TerrainParser(new GfxTextDecoder(() => System.Text.Encoding.GetEncoding(28591)));
        }


        internal static byte[] BuildTrn2(
            float width = 2f,
            float length = 2f,
            int blockGrid = 2,
            int textures = 1,
            string textureName = "tex.dds",
            string subName = "sub0",
            int subRows = 1,
            int subColumns = 1)
        {
            int subCount = subRows * subColumns;
            var body = new List<byte>();
            // Fixed header through offset 140, then sub heights, textures, loader section.
            AddUInt32(body, 0x54524e32);
            AddInt32(body, 1); // version
            AddFloat(body, width);
            AddFloat(body, length);
            AddFloat(body, 2f); // block_size
            AddInt32(body, blockGrid);
            AddInt32(body, subCount); // num_block
            AddInt32(body, subRows); // block_rows
            AddInt32(body, subColumns); // block_cols
            AddInt32(body, textures);
            AddInt32(body, subCount); // num_mask_area
            AddInt32(body, 1); // mask_block
            AddInt32(body, blockGrid); // mask_grid
            AddInt32(body, subCount); // num_sub
            AddInt32(body, subRows); // sub_rows
            AddInt32(body, subColumns); // sub_cols
            AddInt32(body, blockGrid); // sub_grid
            AddFloat(body, 1f); // grid_size
            var textureOffsetAt = body.Count;
            AddInt32(body, 0); // texture_offset placeholder
            AddInt32(body, 1); // loader_id
            var loaderOffsetAt = body.Count;
            AddInt32(body, 0); // loader_offset placeholder
            while (body.Count < 140)
                body.Add(0);
            for (int i = 0; i < subCount; i++)
            {
                AddFloat(body, -1f + i);
                AddFloat(body, 3f + i);
            }

            WriteInt32(body, textureOffsetAt, body.Count);
            for (var i = 0; i < textures; i++)
                AddString(body, textureName);

            WriteInt32(body, loaderOffsetAt, body.Count);
            AddInt32(body, 8); // mask_map_size
            for (int i = 0; i < subCount; i++)
                AddString(body, subCount == 1 ? subName : "sub" + i);
            return body.ToArray();
        }

        internal static byte[] BuildT2bk(int version, bool compressed, bool noRender, int row = 0, int column = 0)
        {
            const int blockGrid = 2;
            var vertices = (blockGrid + 1) * (blockGrid + 1);
            var heights = new float[vertices];
            var normals = new float[vertices * 3];
            for (var i = 0; i < vertices; i++)
            {
                heights[i] = 1.5f + i * 0.01f;
                normals[i * 3] = 0f;
                normals[i * 3 + 1] = 1f;
                normals[i * 3 + 2] = 0f;
            }

            byte[] heightBytes;
            byte[] normalBytes;
            int compression = 0;
            if (compressed)
            {
                compression = 0x15; // COL_ZIP | HEI_ZIP | NOR_ZIP
                heightBytes = Zlib(FloatBytes(heights));
                normalBytes = Zlib(FloatBytes(normals));
            }
            else
            {
                heightBytes = FloatBytes(heights);
                normalBytes = FloatBytes(normals);
            }

            int infoSize = new[] { 0, 16, 24, 28, 32, 32, 36 }[Math.Min(version, 6)];
            var block = new List<byte>();
            AddInt32(block, 0); // mask area
            AddUInt16(block, (ushort)row);
            AddUInt16(block, (ushort)column);
            AddInt32(block, 0);
            AddInt32(block, 0);
            if (version >= 2)
            {
                AddInt32(block, heightBytes.Length);
                AddInt32(block, normalBytes.Length);
            }

            while (block.Count < infoSize)
                block.Add(0);
            if (version >= 6)
            {
                // flags at offset 32
                while (block.Count < 32)
                    block.Add(0);
                WriteUInt32(block, 32, noRender ? 1u : 0u);
                while (block.Count < infoSize)
                    block.Add(0);
            }

            block.AddRange(heightBytes);
            block.AddRange(normalBytes);

            var file = new List<byte>();
            AddUInt32(file, 0x54424b42);
            AddInt32(file, version);
            AddInt32(file, 1); // count
            if (version >= 2)
            {
                file.Add((byte)compression);
                file.Add(0);
                file.Add(0);
                file.Add(0);
            }

            var offsetTableAt = file.Count;
            AddInt32(file, 0); // placeholder offset
            var blockOffset = file.Count;
            WriteInt32(file, offsetTableAt, blockOffset);
            file.AddRange(block);
            return file.ToArray();
        }

        internal static byte[] BuildT2mk(bool compressed)
        {
            const int maskSize = 4;
            var mask = new byte[maskSize * maskSize];
            for (var i = 0; i < mask.Length; i++)
                mask[i] = 200;

            byte[] maskPayload = compressed ? Zlib(mask) : mask;
            int version = compressed ? 2 : 1;
            int entrySize = version >= 2 ? 12 : 8;

            var layers = new List<byte>();
            // layer 0
            AddInt32(layers, 0); // texture
            AddInt32(layers, 0);
            AddInt32(layers, maskSize);
            layers.Add(1); // axis
            layers.Add(0); layers.Add(0); layers.Add(0);
            AddFloat(layers, 1f);
            AddFloat(layers, 1f);
            AddInt32(layers, 0);
            // layer 1
            AddInt32(layers, 0);
            AddInt32(layers, 0);
            AddInt32(layers, maskSize);
            layers.Add(1);
            layers.Add(0); layers.Add(0); layers.Add(0);
            AddFloat(layers, 2f);
            AddFloat(layers, 2f);
            AddInt32(layers, 0);

            var file = new List<byte>();
            AddUInt32(file, 0x544d4b42);
            AddInt32(file, version);
            AddInt32(file, 1); // areas
            AddInt32(file, 2); // layers
            if (version >= 2)
            {
                file.Add(compressed ? (byte)1 : (byte)0);
                file.Add(0); file.Add(0); file.Add(0);
            }

            var areaOffsetAt = file.Count;
            AddInt32(file, 0); // placeholder
            file.AddRange(layers);

            var areaAt = file.Count;
            WriteInt32(file, areaOffsetAt, areaAt);
            AddInt32(file, 2); // entries
            // entry 0: base layer, no mask
            AddInt32(file, 0);
            AddInt32(file, 0);
            if (version >= 2)
                AddInt32(file, 0);
            // entry 1: overlay
            var maskOffsetAt = file.Count;
            AddInt32(file, 1); // layer
            AddInt32(file, 0); // offset placeholder
            if (version >= 2)
                AddInt32(file, maskPayload.Length);
            var maskAt = file.Count;
            WriteInt32(file, maskOffsetAt + 4, maskAt);
            file.AddRange(maskPayload);
            return file.ToArray();
        }

        internal static byte[] BuildT2lm(bool dds)
        {
            const int size = 2;
            byte[] payload;
            if (dds)
            {
                payload = new byte[128];
                payload[0] = (byte)'D';
                payload[1] = (byte)'D';
                payload[2] = (byte)'S';
                payload[3] = (byte)' ';
            }
            else
            {
                payload = new byte[size * size * 3];
                for (var i = 0; i < payload.Length; i++)
                    payload[i] = (byte)(10 + i);
            }

            var file = new List<byte>();
            AddUInt32(file, 0x54324c4d);
            AddInt32(file, 2);
            AddInt32(file, 1); // count
            AddInt32(file, size);
            AddUInt32(file, dds ? 1u : 0u);
            AddInt32(file, payload.Length);
            var offsetAt = file.Count;
            AddInt32(file, 0);
            WriteInt32(file, offsetAt, file.Count);
            file.AddRange(payload);
            return file.ToArray();
        }

        private static byte[] FloatBytes(float[] values)
        {
            var bytes = new byte[values.Length * 4];
            for (var i = 0; i < values.Length; i++)
                Buffer.BlockCopy(BitConverter.GetBytes(values[i]), 0, bytes, i * 4, 4);
            if (!BitConverter.IsLittleEndian)
            {
                for (var i = 0; i < values.Length; i++)
                    Array.Reverse(bytes, i * 4, 4);
            }

            return bytes;
        }

        private static byte[] Zlib(byte[] raw)
        {
            using (var output = new MemoryStream())
            {
                output.WriteByte(0x78);
                output.WriteByte(0x9c);
                using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, true))
                    deflate.Write(raw, 0, raw.Length);
                uint a = 1, b = 0;
                foreach (var value in raw)
                {
                    a = (a + value) % 65521;
                    b = (b + a) % 65521;
                }

                uint checksum = (b << 16) | a;
                output.WriteByte((byte)(checksum >> 24));
                output.WriteByte((byte)(checksum >> 16));
                output.WriteByte((byte)(checksum >> 8));
                output.WriteByte((byte)checksum);
                return output.ToArray();
            }
        }

        private static byte[] Slice(byte[] data, int start, int length)
        {
            var result = new byte[length];
            Buffer.BlockCopy(data, start, result, 0, length);
            return result;
        }

        private static void AddString(List<byte> data, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
            AddInt32(data, bytes.Length);
            data.AddRange(bytes);
        }

        private static void AddFloat(List<byte> data, float value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            data.AddRange(bytes);
        }

        private static void AddInt32(List<byte> data, int value)
        {
            data.Add((byte)value);
            data.Add((byte)(value >> 8));
            data.Add((byte)(value >> 16));
            data.Add((byte)(value >> 24));
        }

        private static void AddUInt32(List<byte> data, uint value)
        {
            AddInt32(data, unchecked((int)value));
        }

        private static void AddUInt16(List<byte> data, ushort value)
        {
            data.Add((byte)value);
            data.Add((byte)(value >> 8));
        }

        private static void WriteInt32(List<byte> data, int offset, int value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteInt32(byte[] data, int offset, int value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt32(List<byte> data, int offset, uint value)
        {
            WriteInt32(data, offset, unchecked((int)value));
        }
    }
}
