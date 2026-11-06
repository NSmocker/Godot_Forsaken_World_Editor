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
        public void ParsesPw12ModelsWithoutShowTypeAndKeepsFollowingSoundAligned()
        {
            var d = CreateParser().Parse(BuildEcwld(12, true), "pw12.ecwld");
            Assert.AreEqual(12, d.Version);
            Assert.AreEqual(1, d.Models.Count);
            Assert.AreEqual(0, d.Models[0].ShowType);
            Assert.AreEqual("models/npc.ecm", d.Models[0].Path);
            Assert.AreEqual("Stand", d.Models[0].Action);
            Assert.AreEqual("ambience/wind.wav", d.Sounds[0].Path);
            Assert.AreEqual("fx/test.gfx", d.Effects[0].Path);
        }
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
        public void ParsesGrassTypesAndRefsWithoutShiftingEffects()
        {
            var withGrass = BuildBlockWithGrass();
            var bytes = BuildEcwldWithCustomBlocks(new List<byte[]> { withGrass }, 13);
            var grassTable = new MemoryStream();
            var w = new BinaryWriter(grassTable);
            w.Write((byte)1); // alpha blend, ignored at scatter time
            w.Write(new byte[3]);
            w.Write(84);
            w.Write(1.5f);
            w.Write(0.25f);
            w.Write(0.8f);
            w.Write(40f);
            WriteAString(w, "Grasses\\blade.mox");
            w.Flush();
            var table = grassTable.ToArray();
            var merged = new byte[bytes.Length + table.Length];
            Buffer.BlockCopy(bytes, 0, merged, 0, bytes.Length);
            Buffer.BlockCopy(table, 0, merged, bytes.Length, table.Length);
            WriteU32(merged, 40, (uint)bytes.Length);
            WriteI32(merged, 48, 1);

            var document = CreateParser().Parse(merged, "grass.ecwld");
            Assert.AreEqual(1, document.GrassTypes.Count);
            Assert.AreEqual("Grasses\\blade.mox", document.GrassTypes[0].ModelPath);
            Assert.AreEqual(1.5f, document.GrassTypes[0].Size, 0.0001f);
            Assert.AreEqual(84, document.GrassTypes[0].AlphaRef);
            Assert.AreEqual(1, document.GrassRefs.Count);
            Assert.AreEqual(640u, document.GrassRefs[9]);
            Assert.AreEqual("fx/test.gfx", document.Effects[0].Path);
        }

        [Test]
        public void ParsesTreesFromV12Block()
        {
            // v12 block: 1 tree record = type DWORD + XYZ float[3] = 16 bytes
            var blocks = new List<byte[]> { BuildBlockWithTree(version: 12, treeType: 42, posX: 10f, posY: 20f, posZ: 30f) };
            var document = CreateParser().Parse(BuildEcwldWithCustomBlocks(blocks, version: 12), "trees_v12.ecwld");

            Assert.AreEqual(12, document.Version);
            Assert.AreEqual(1, document.Trees.Count);
            Assert.AreEqual(42u, document.Trees[0].Type);
            Assert.AreEqual(10f, document.Trees[0].Pos[0], 0.0001f);
            Assert.AreEqual(20f, document.Trees[0].Pos[1], 0.0001f);
            Assert.AreEqual(30f, document.Trees[0].Pos[2], 0.0001f);
        }

        [Test]
        public void ParsesTreesFromV13Block()
        {
            // v13 still stores ECWDFILETREE5: type + XYZ = 16 bytes.
            var blocks = new List<byte[]> { BuildBlockWithTree(version: 13, treeType: 7, posX: 5f, posY: 6f, posZ: 7f) };
            var document = CreateParser().Parse(BuildEcwldWithCustomBlocks(blocks, version: 13), "trees_v13.ecwld");

            Assert.AreEqual(13u, document.Version);
            Assert.AreEqual(1, document.Trees.Count);
            Assert.AreEqual(7u, document.Trees[0].Type);
            Assert.AreEqual(5f, document.Trees[0].Pos[0], 0.0001f);
            Assert.AreEqual(6f, document.Trees[0].Pos[1], 0.0001f);
            Assert.AreEqual(7f, document.Trees[0].Pos[2], 0.0001f);
        }

        [Test]
        public void ParsesTreeTypesFromHeaderOffset()
        {
            // Build a file with tree type data at a custom offset after blocks.
            // Format per tree type: length-prefixed spt path + length-prefixed comp map path.
            // Type id is the table index, matching CECScene::LoadTreeTypeData.
            var blocks = new List<byte[]> { BuildBlock(version: 12, exportId: 7) };
            var treeTypeData = new List<byte>();
            var ttw = new BinaryWriter(new MemoryStream());
            WriteAString(ttw, "trees/oak.spt");
            WriteAString(ttw, "trees/oak_comp.dds");
            WriteAString(ttw, "trees/pine.spt");
            WriteAString(ttw, "trees/pine_comp.dds");
            ttw.Flush();
            treeTypeData.AddRange(((MemoryStream)ttw.BaseStream).ToArray());

            var header = new byte[116];
            WriteU32(header, 0, EcwldParser.EcwdMagic);
            WriteU32(header, 4, 12u);
            WriteI32(header, 24, blocks.Count);
            // dwTreeOff at offset 36, iNumTreeType at offset 44
            var treeOffset = 116 + blocks.Count * 4 + blocks[0].Length;
            WriteU32(header, 36, (uint)treeOffset);
            WriteI32(header, 44, 2);

            var table = new byte[blocks.Count * 4];
            var payload = new List<byte>();
            var offset = 116 + table.Length;
            WriteU32(table, 0, (uint)offset);
            payload.AddRange(blocks[0]);
            offset += blocks[0].Length;
            payload.AddRange(treeTypeData);

            var result = new byte[116 + table.Length + payload.Count];
            Buffer.BlockCopy(header, 0, result, 0, 116);
            Buffer.BlockCopy(table, 0, result, 116, table.Length);
            Buffer.BlockCopy(payload.ToArray(), 0, result, 116 + table.Length, payload.Count);

            var document = CreateParser().Parse(result, "treetypes.ecwld");

            Assert.AreEqual(2, document.TreeTypes.Count);
            Assert.AreEqual("trees/oak.spt", document.TreeTypes[0]);
            Assert.AreEqual("trees/pine.spt", document.TreeTypes[1]);
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
            int[] n = { version == 12 ? 1 : 0, 1, 0, 1, 1, 1, 1, 0, 0, 1 };
            foreach (var c in n)
                w.Write(c);

            if (version == 12) { w.Write(123u); WriteVec3(w, 1, 2, 3); }

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
            if (version >= 13) w.Write(2); // show_type was not present in PW v12
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

        private static byte[] BuildBlockWithGrass()
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write((ushort)0);
            w.Write((ushort)0);
            int[] n = { 0, 0, 1, 0, 0, 1, 0, 0, 0, 0 };
            foreach (var c in n)
                w.Write(c);
            w.Write(9u);
            w.Write(640u);
            w.Write(9u);
            w.Write(1.5f);
            WriteVec3(w, 1, 2, 3);
            WriteVec3(w, 0, 0, 1);
            WriteVec3(w, 0, 1, 0);
            w.Write(1.0f);
            w.Write(0);
            w.Write(0.9f);
            w.Write(0);
            WriteAString(w, "fx/test.gfx");
            w.Flush();
            return ms.ToArray();
        }

        private static byte[] BuildBlockWithTree(uint version, uint treeType, float posX, float posY, float posZ)
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write((ushort)0); // row
            w.Write((ushort)0); // col
            // counts: trees=1, water=0, grass=0, buildings=0, boxes=0, effects=0, models=0, critters=0, bezier=0, sounds=0
            int[] n = { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            foreach (var c in n)
                w.Write(c);

            // ECWDFILETREE5: type DWORD + XYZ for every supported version.
            w.Write(treeType);
            w.Write(posX);
            w.Write(posY);
            w.Write(posZ);

            w.Flush();
            return ms.ToArray();
        }

        private static byte[] BuildEcwldWithCustomBlocks(List<byte[]> blocks, uint version)
        {
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
