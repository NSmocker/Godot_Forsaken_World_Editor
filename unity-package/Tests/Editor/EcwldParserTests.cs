using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class EcwldParserTests
    {
        [Test]
        public void ParsesVersion13BlockWithDedupedObjects()
        {
            var document = CreateParser().Parse(BuildEcwld(version: 13, duplicateSecondBlock: true), "fixture.ecwld");
            Assert.AreEqual(13u, document.Version);
            Assert.AreEqual(1, document.Effects.Count);
            Assert.AreEqual(1, document.Models.Count);
            Assert.AreEqual(1, document.Sounds.Count);
            Assert.AreEqual(1, document.WaterRefs.Count);
            Assert.AreEqual(1, document.OrnamentRefs.Count);
            Assert.AreEqual(1, document.BoxRefs.Count);
            Assert.AreEqual("fx/test.gfx", document.Effects[0].Path);
            Assert.AreEqual(1.5f, document.Effects[0].Scale, 0.0001f);
            Assert.AreEqual(0, document.Effects[0].ValidTime);
            Assert.AreEqual("models/npc.ecm", document.Models[0].Path);
            Assert.AreEqual("Stand", document.Models[0].Action);
            Assert.AreEqual("ambience/wind.wav", document.Sounds[0].Path);
            Assert.AreEqual(10f, document.Sounds[0].MinDistance, 0.0001f);
            Assert.AreEqual(40f, document.Sounds[0].MaxDistance, 0.0001f);
        }

        [Test]
        public void ParsesVersion14SoundPadding()
        {
            var document = CreateParser().Parse(BuildEcwld(version: 14, duplicateSecondBlock: false), "v14.ecwld");
            Assert.AreEqual(14u, document.Version);
            Assert.AreEqual(1, document.Sounds.Count);
            Assert.AreEqual("ambience/wind.wav", document.Sounds[0].Path);
        }

        [Test]
        public void RejectsBadMagicAndUnsupportedVersion()
        {
            var bad = BuildEcwld(version: 13, duplicateSecondBlock: false);
            bad[0] = (byte)'X';
            var error = Assert.Throws<InvalidDataException>(() => CreateParser().Parse(bad, "bad.ecwld"));
            StringAssert.Contains("Szenen", error.Message);

            var unsupported = BuildEcwld(version: 20, duplicateSecondBlock: false);
            error = Assert.Throws<InvalidDataException>(() => CreateParser().Parse(unsupported, "v20.ecwld"));
            StringAssert.Contains("20", error.Message);
        }

        private static EcwldParser CreateParser() => new EcwldParser(new GfxTextDecoder(() => System.Text.Encoding.GetEncoding(28591)));

        private static byte[] BuildEcwld(uint version, bool duplicateSecondBlock)
        {
            var blocks = new List<byte[]>();
            blocks.Add(BuildBlock(version, exportId: 7));
            if (duplicateSecondBlock)
                blocks.Add(BuildBlock(version, exportId: 7)); // same ids -> dedupe

            var header = new byte[116];
            WriteU32(header, 0, EcwldParser.EcwdMagic);
            WriteU32(header, 4, version);
            WriteI32(header, 24, blocks.Count);

            var table = new byte[blocks.Count * 4];
            var payload = new List<byte>();
            var offset = 116 + table.Length;
            for (var i = 0; i < blocks.Count; i++)
            {
                WriteU32(table, i * 4, (uint)offset);
                payload.AddRange(blocks[i]);
                offset += blocks[i].Length;
            }

            var result = new byte[116 + table.Length + payload.Count];
            Buffer.BlockCopy(header, 0, result, 0, 116);
            Buffer.BlockCopy(table, 0, result, 116, table.Length);
            Buffer.BlockCopy(payload.ToArray(), 0, result, 116 + table.Length, payload.Count);
            return result;
        }

        private static byte[] BuildBlock(uint version, uint exportId)
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write((ushort)0); // row
            w.Write((ushort)0); // col
            // counts: trees, water, grass, buildings, boxes, effects, models, critters, bezier, sounds
            int[] n = { 0, 1, 0, 1, 1, 1, 1, 0, 0, 1 };
            foreach (var c in n)
                w.Write(c);

            // water ref
            w.Write(exportId);
            w.Write(100u);
            // ornament
            w.Write(exportId);
            w.Write(200u);
            // box
            w.Write(exportId);
            w.Write(300u);

            // effect 60 bytes + path
            w.Write(exportId);
            w.Write(1.5f); // scale
            WriteVec3(w, 1, 2, 3);
            WriteVec3(w, 0, 0, 1);
            WriteVec3(w, 0, 1, 0);
            w.Write(1.0f); // speed
            w.Write(0); // valid day
            w.Write(0.9f); // alpha
            w.Write(0); // pad to 60
            WriteAString(w, "fx/test.gfx");

            // model 44 + path + action
            w.Write(exportId);
            WriteVec3(w, 4, 5, 6);
            WriteVec3(w, 0, 0, 1);
            WriteVec3(w, 0, 1, 0);
            w.Write(2); // show_type
            WriteAString(w, "models/npc.ecm");
            WriteAString(w, "Stand");

            // sound
            w.Write(exportId);
            WriteVec3(w, 7, 8, 9);
            w.Write(10f);
            w.Write(40f);
            w.Write(0);
            w.Write(2); // always
            if (version >= 14)
                w.Write(new byte[20]);
            WriteAString(w, "ambience/wind.wav");

            w.Flush();
            return ms.ToArray();
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

        private static void WriteI32(byte[] data, int offset, int value)
        {
            WriteU32(data, offset, unchecked((uint)value));
        }
    }
}
