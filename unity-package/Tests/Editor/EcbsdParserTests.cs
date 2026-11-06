using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class EcbsdParserTests
    {
        [Test]
        public void ParsesOrnamentsDefaultLightAndWater()
        {
            byte[] bsData = null;
            var bsd = BuildEcbsd(out bsData, out var ornamentOff, out var boxOff, out var waterOff);
            var ornamentRefs = new Dictionary<uint, uint> { { 1u, (uint)ornamentOff } };
            var boxRefs = new Dictionary<uint, uint> { { 2u, (uint)boxOff } };
            var waterRefs = new Dictionary<uint, uint> { { 3u, (uint)waterOff } };

            var document = new EcbsdParser(new GfxTextDecoder(() => System.Text.Encoding.GetEncoding(28591))).Parse(
                bsd,
                "map.ecbsd",
                ornamentRefs,
                boxRefs,
                waterRefs,
                name => bsData);

            Assert.AreEqual(1, document.Ornaments.Count);
            Assert.AreEqual("part/litmodel_1.bmd", document.Ornaments[0].RelativePath);
            Assert.IsNotNull(document.DefaultLight);
            Assert.AreEqual(1, document.WaterAreas.Count);
            Assert.AreEqual(2, document.WaterAreas[0].Width);
            Assert.AreEqual(2, document.WaterAreas[0].Length);
            Assert.AreEqual(4, document.WaterAreas[0].Flags.Length);
            var buildings = EcbsdParser.BuildingFiles(document);
            Assert.AreEqual(1, buildings.Count);
            Assert.AreEqual("part/litmodel_1.bmd", buildings[0]);
        }

        [Test]
        public void ReadsGrassBitsFromBsDataChunk()
        {
            var bits = new byte[8];
            bits[0] = 0x80;
            var payload = new byte[16 + 8 + bits.Length];
            BitConverter.GetBytes(bits.Length).CopyTo(payload, 16);
            BitConverter.GetBytes(bits.Length).CopyTo(payload, 20);
            Buffer.BlockCopy(bits, 0, payload, 24, bits.Length);
            var bs = new byte[payload.Length + 8];
            Buffer.BlockCopy(payload, 0, bs, 0, payload.Length);
            BitConverter.GetBytes(16).CopyTo(bs, payload.Length);
            BitConverter.GetBytes(1).CopyTo(bs, payload.Length + 4);

            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write(Encoding.ASCII.GetBytes("MOXB"));
            w.Write(EcbsdParser.BsdfMagic);
            w.Write(7); // BSD version >= 7: bits live in BSData
            w.Write(1);
            while (ms.Length < EcbsdParser.BsdHeaderEnd)
                w.Write((byte)0);
            WriteAString(w, "BSData\\0.dat");
            var grassOff = (int)ms.Length;
            w.Write(0u);
            WriteVec3(w, 10f, 4f, -6f);
            w.Write(2f);
            w.Write(8);
            w.Write(8);
            w.Write(0);
            w.Write(0);
            w.Flush();

            var document = new EcbsdParser().Parse(
                ms.ToArray(), "map.ecbsd",
                new Dictionary<uint, uint>(),
                new Dictionary<uint, uint>(),
                new Dictionary<uint, uint>(),
                name => bs,
                new Dictionary<uint, uint> { { 9u, (uint)grassOff } });

            Assert.AreEqual(1, document.GrassAreas.Count);
            Assert.AreEqual(8, document.GrassAreas[0].Columns);
            Assert.AreEqual(0x80, document.GrassAreas[0].Bits[0]);
            Assert.AreEqual(10f, document.GrassAreas[0].Center[0], 0.0001f);
        }

        [Test]
        public void WarnsOnBadMagicInsteadOfThrowing()
        {
            var bad = Encoding.ASCII.GetBytes("XXXX") ;
            Array.Resize(ref bad, 80);
            var document = new EcbsdParser().Parse(
                bad, "bad.ecbsd",
                new Dictionary<uint, uint>(),
                new Dictionary<uint, uint>(),
                new Dictionary<uint, uint>());
            Assert.AreEqual(0, document.Ornaments.Count);
            Assert.IsTrue(document.Warnings.Count > 0);
        }

        private static byte[] BuildEcbsd(
            out byte[] bsData, out int ornamentOff, out int boxOff, out int waterOff)
        {
            // BSData with one water area
            bsData = BuildBsData();

            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write(Encoding.ASCII.GetBytes("MOXB"));
            w.Write(EcbsdParser.BsdfMagic);
            w.Write(0); // unused
            w.Write(1); // name count at offset 12
            // pad to BSD_HEADER_END (76)
            while (ms.Length < EcbsdParser.BsdHeaderEnd)
                w.Write((byte)0);
            // names
            WriteAString(w, "BSData\\0.dat");

            ornamentOff = (int)ms.Length;
            WriteVec3(w, 1, 2, 3);
            WriteVec3(w, 0, 0, 1);
            WriteVec3(w, 0, 1, 0);
            w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); w.Write((byte)0);
            WriteAString(w, "part/litmodel_1.bmd");

            boxOff = (int)ms.Length;
            // Build a 144-byte box area with iDefArea=1 and light fields.
            var box = new byte[EcbsdParser.BoxAreaLightEnd];
            WriteI32(box, 0, 1); // def area
            // day background at 80
            WriteU32(box, 80, 0xFF112233);
            // night background at 112
            WriteU32(box, 112, 0xFF010203);
            // light dir at 116
            WriteF32(box, 116, 0f);
            WriteF32(box, 120, -1f);
            WriteF32(box, 124, 0f);
            WriteU32(box, 128, 0xFF404040); // day ambient
            WriteU32(box, 132, 0xFFFFFF00); // day dir
            WriteU32(box, 136, 0xFF101010); // night ambient
            WriteU32(box, 140, 0xFF8080FF); // night dir
            w.Write(box);

            waterOff = (int)ms.Length;
            w.Write(0); // sub index
            w.Write(0); // data index
            w.Flush();
            return ms.ToArray();
        }

        private static byte[] BuildBsData()
        {
            // One water area at offset 0 inside the payload region after a tiny header.
            var area = new MemoryStream();
            var aw = new BinaryWriter(area);
            aw.Write(EcbsdParser.WaterMinVersion);
            aw.Write(99u); // id
            aw.Write(0); // unused
            aw.Write(5f); // height at +12
            aw.Write(0); // pad to keep layout: actually height is at +12, so bytes 8-11 unused
            // Fix: rewrite carefully
            aw.Flush();

            var buf = new byte[44 + 4];
            WriteU32(buf, 0, EcbsdParser.WaterMinVersion);
            WriteU32(buf, 4, 99u);
            WriteF32(buf, 12, 5f);
            WriteI32(buf, 20, 2); // width
            WriteI32(buf, 24, 2); // length
            WriteF32(buf, 28, 1f); // grid
            WriteF32(buf, 32, 10f);
            WriteF32(buf, 36, 0f);
            WriteF32(buf, 40, 20f);
            buf[44] = 1; buf[45] = 1; buf[46] = 0; buf[47] = 1; // flags

            // Wrap as MOXB BSData with offset table at end: [payload][offset0][count]
            var file = new byte[16 + buf.Length + 4 + 4];
            file[0] = (byte)'M'; file[1] = (byte)'O'; file[2] = (byte)'X'; file[3] = (byte)'B';
            var payloadStart = 16;
            Buffer.BlockCopy(buf, 0, file, payloadStart, buf.Length);
            var offsetPos = file.Length - 8;
            WriteU32(file, offsetPos, (uint)payloadStart);
            WriteU32(file, file.Length - 4, 1u);
            return file;
        }

        private static void WriteAString(BinaryWriter w, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            w.Write(bytes.Length);
            w.Write(bytes);
        }

        private static void WriteVec3(BinaryWriter w, float x, float y, float z)
        {
            w.Write(x); w.Write(y); w.Write(z);
        }

        private static void WriteU32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteI32(byte[] data, int offset, int value) =>
            WriteU32(data, offset, unchecked((uint)value));

        private static void WriteF32(byte[] data, int offset, float value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
            Buffer.BlockCopy(bytes, 0, data, offset, 4);
        }
    }
}
