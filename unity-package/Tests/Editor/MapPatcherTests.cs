using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class MapPatcherTests
    {
        private static readonly MapPatcherGbk Encoding = new MapPatcherGbk(new byte[126 * 191 * 2]);

        [Test]
        public void AddsToEveryIntersectingBlockAndPreservesEveryOtherSection()
        {
            var fixture = World();
            var result = Patch(fixture, "1\\new.bmd", Bmd(0, 0));
            Assert.AreEqual(1, result.Added);
            Assert.AreEqual(4, result.ChangedBlocks);
            CollectionAssert.AreEqual(fixture.Item2, result.SharedData.Take(fixture.Item2.Length));
            // Original world is preserved except for four block-offset table entries.
            CollectionAssert.AreEqual(fixture.Item1.Take(116), result.World.Take(116));
            CollectionAssert.AreEqual(fixture.Item1.Skip(132), result.World.Skip(132).Take(fixture.Item1.Length - 132));
            var oldDoc = new EcwldParser(Encoding.Decoder).Parse(fixture.Item1);
            var doc = new EcwldParser(Encoding.Decoder).Parse(result.World);
            Assert.AreEqual(2, doc.OrnamentRefs.Count);
            Assert.IsTrue(doc.OrnamentRefs.ContainsKey(1001)); // greatest ID was a sound, not an ornament
            Assert.AreEqual((uint)fixture.Item2.Length, doc.OrnamentRefs[1001]);
            Assert.AreEqual(oldDoc.Effects[0].Path, doc.Effects[0].Path);
            Assert.AreEqual(oldDoc.Models[0].Action, doc.Models[0].Action);
            Assert.AreEqual(oldDoc.Sounds[0].Path, doc.Sounds[0].Path);
            Assert.AreEqual(oldDoc.Trees.Count, doc.Trees.Count);
            for (int i = 0; i < 4; i++)
            {
                int oldStart = I32(fixture.Item1, 116 + i * 4), start = I32(result.World, 116 + i * 4);
                Assert.AreEqual(2, I32(result.World, start + 16));
                int oldNext = i == 3 ? (int)U32(fixture.Item1, 36) : I32(fixture.Item1, 120 + i * 4);
                // Header except count; prefix through old ornament; suffix after inserted ref.
                CollectionAssert.AreEqual(fixture.Item1.Skip(oldStart + 44).Take(32), result.World.Skip(start + 44).Take(32));
                CollectionAssert.AreEqual(fixture.Item1.Skip(oldStart + 76).Take(oldNext - oldStart - 76),
                    result.World.Skip(start + 84).Take(oldNext - oldStart - 76));
            }
            var bsd = new EcbsdParser(Encoding.Decoder).Parse(result.SharedData, "fixture.ecbsd", doc.OrnamentRefs, null, null);
            Assert.AreEqual("1\\new.bmd", bsd.Ornaments.Single(x => x.ExportId == 1001).RelativePath);
            CollectionAssert.AreEqual(new[] { 0f, 10f, 0f }, bsd.Ornaments.Single(x => x.ExportId == 1001).Pos);
        }

        [Test]
        public void UsesBmdRotationScaleAndInclusiveBoundaryIntersections()
        {
            var fixture = World();
            // Center x=-10, local Z half extent=5, scale Z=2, forward +X => touches x=0.
            var result = Patch(fixture, "rotated.bmd", Bmd(-10, 20, extentX: 1, extentZ: 5, rotate: true, scaleZ: 2));
            Assert.AreEqual(2, result.ChangedBlocks); // both north blocks, including boundary contact
            Assert.AreNotEqual(U32(fixture.Item1, 116), U32(result.World, 116));
            Assert.AreNotEqual(U32(fixture.Item1, 120), U32(result.World, 120));
            Assert.AreEqual(U32(fixture.Item1, 124), U32(result.World, 124));
            Assert.AreEqual(U32(fixture.Item1, 128), U32(result.World, 128));
        }

        [Test]
        public void CollisionMeshIdentifiersAreNotArrayIndices()
        {
            byte[] source = Bmd(0, 0);
            using (var s = new MemoryStream())
            using (var w = new BinaryWriter(s))
            {
                w.Write(source, 0, source.Length - 4);
                w.Write(1); // hull count
                w.Write(1); w.Write(515); // persistent mesh ID; BMD has only one mesh
                w.Write(new byte[48]); w.Write(0); w.Write(6); w.Write(new byte[6 * 17]);
                Assert.AreEqual(1, Patch(World(), "collision.bmd", s.ToArray()).Added);
            }
        }

        [Test]
        public void RepeatedPatchAndDuplicatePathsAreByteIdentical()
        {
            var fixture = World();
            var input = new PwMapPatcher.Input { RelativePath = "1/new.bmd", Bmd = Bmd(-20, 20) };
            var first = PwMapPatcher.Build(fixture.Item1, fixture.Item2, new[] { input, input }, Encoding.Decoder, Encoding.Encode);
            Assert.AreEqual(1, first.Added);
            Assert.AreEqual(1, first.Skipped);
            input.RelativePath = "1\\NEW.BMD";
            var second = PwMapPatcher.Build(first.World, first.SharedData, new[] { input }, Encoding.Decoder, Encoding.Encode);
            Assert.AreEqual(0, second.Added);
            CollectionAssert.AreEqual(first.World, second.World);
            CollectionAssert.AreEqual(first.SharedData, second.SharedData);
        }

        [Test]
        public void RejectsOutOfMapUnsupportedAndCorruptInputsWithoutMutatingSource()
        {
            var fixture = World();
            byte[] original = (byte[])fixture.Item1.Clone();
            Assert.Throws<InvalidDataException>(() => Patch(fixture, "outside.bmd", Bmd(200, 200)));
            Assert.Throws<InvalidDataException>(() => Patch(fixture, "bad.bmd", new byte[3]));
            CollectionAssert.AreEqual(original, fixture.Item1);
            byte[] broken = (byte[])fixture.Item1.Clone();
            Write(broken, 4, 13);
            Assert.Throws<InvalidDataException>(() => Patch(Tuple.Create(broken, fixture.Item2), "new.bmd", Bmd(0, 0)));
            Write(broken, 4, 12); Write(broken, 116, 1);
            Assert.Throws<InvalidDataException>(() => Patch(Tuple.Create(broken, fixture.Item2), "new.bmd", Bmd(0, 0)));
            byte[] truncated = Bmd(0, 0).Take(Bmd(0, 0).Length - 1).ToArray();
            Assert.Throws<InvalidDataException>(() => Patch(fixture, "new.bmd", truncated));
        }

        [TestCase("../escape.bmd")]
        [TestCase("C:\\game\\model.bmd")]
        [TestCase("\\server\\model.bmd")]
        [TestCase("1\\model.ecm")]
        [TestCase("1\\..\\model.bmd")]
        public void RejectsInvalidRelativePaths(string path)
        {
            Assert.Throws<InvalidDataException>(() => PwMapPatcher.NormalizePath(path));
        }

        [Test]
        public void GbkEncoderRoundTripsChineseAndRejectsUnsupportedCharacters()
        {
            var table = new byte[126 * 191 * 2];
            int index = ((0xd6 - 0x81) * 191 + 0xd0 - 0x40) * 2;
            table[index] = 0x2d; table[index + 1] = 0x4e;
            var codec = new MapPatcherGbk(table);
            CollectionAssert.AreEqual(new byte[] { 0xd6, 0xd0 }, codec.Encode("中"));
            Assert.AreEqual("中\\x.bmd", codec.Decoder.DecodeGbk(codec.Encode("中\\x.bmd")));
            Assert.Throws<InvalidDataException>(() => codec.Encode("😀.bmd"));
        }

        [Test]
        public void FilePatchFindsPairBacksUpAndDoesNotTouchBmdOrTerrain()
        {
            WithFiles((root, map, model, world, bsd, trn) =>
            {
                byte[] originalBmd = File.ReadAllBytes(model), originalTrn = File.ReadAllBytes(trn);
                var report = MapPatcherFiles.Patch(map, new[] { @"LitModels\sample\1\new.bmd" }, "", Encoding);
                Assert.AreEqual(1, report.Added);
                CollectionAssert.AreEqual(world, File.ReadAllBytes(Path.Combine(report.BackupFolder, "sample.ecwld")));
                CollectionAssert.AreEqual(bsd, File.ReadAllBytes(Path.Combine(report.BackupFolder, "sample.ecbsd")));
                CollectionAssert.AreEqual(originalBmd, File.ReadAllBytes(model));
                CollectionAssert.AreEqual(originalTrn, File.ReadAllBytes(trn));
                byte[] patched = File.ReadAllBytes(report.WorldPath);
                var repeat = MapPatcherFiles.Patch(map, new[] { @"LitModels\sample\1\new.bmd" }, root, Encoding);
                Assert.AreEqual(0, repeat.Added);
                Assert.IsNull(repeat.BackupFolder);
                CollectionAssert.AreEqual(patched, File.ReadAllBytes(report.WorldPath));
                Assert.IsEmpty(Directory.GetFiles(map, "*.tmp"));
            });
        }

        [Test]
        public void FilePreflightRejectsWrongMapMissingModelAndAmbiguousWorldWithoutWrites()
        {
            WithFiles((root, map, model, world, bsd, trn) =>
            {
                Assert.Throws<InvalidDataException>(() => MapPatcherFiles.Patch(map, new[] { @"LitModels\other\new.bmd" }, root, Encoding));
                Assert.Throws<FileNotFoundException>(() => MapPatcherFiles.Patch(map, new[] { @"LitModels\sample\missing.bmd" }, root, Encoding));
                File.WriteAllBytes(Path.Combine(map, "other.ecwld"), world);
                Assert.Throws<InvalidDataException>(() => MapPatcherFiles.Patch(map, new[] { @"LitModels\sample\1\new.bmd" }, root, Encoding));
                CollectionAssert.AreEqual(world, File.ReadAllBytes(Path.Combine(map, "sample.ecwld")));
                CollectionAssert.AreEqual(bsd, File.ReadAllBytes(Path.Combine(map, "sample.ecbsd")));
                Assert.IsEmpty(Directory.GetDirectories(map));
            });
        }

        private static PwMapPatcher.Result Patch(Tuple<byte[], byte[]> fixture, string path, byte[] bmd) =>
            PwMapPatcher.Build(fixture.Item1, fixture.Item2,
                new[] { new PwMapPatcher.Input { RelativePath = path, Bmd = bmd } }, Encoding.Decoder, Encoding.Encode);

        private static Tuple<byte[], byte[]> World()
        {
            byte[] bsd;
            using (var s = new MemoryStream())
            using (var w = new BinaryWriter(s))
            {
                w.Write(0x42584f4d); w.Write(0x42534446); w.Write(12); w.Write(0); w.Write(new byte[60]);
                w.Write(new byte[40]); String(w, "existing.bmd"); bsd = s.ToArray();
            }
            using (var s = new MemoryStream())
            using (var w = new BinaryWriter(s))
            {
                w.Write(0x45435744); w.Write(12); w.Write(128f); w.Write(128f); w.Write(64f);
                w.Write(32); w.Write(4); w.Write(2); w.Write(2); w.Write(new byte[80]); w.Write(new byte[16]);
                var offsets = new int[4];
                for (int i = 0; i < 4; i++)
                {
                    offsets[i] = (int)s.Position;
                    w.Write((ushort)(i / 2)); w.Write((ushort)(i % 2));
                    for (int k = 0; k < 10; k++) w.Write(1);
                    w.Write(0); w.Write(new byte[12]); // tree
                    foreach (int id in new[] { 2, 7, 3, 4 }) { w.Write(id); w.Write(76); }
                    w.Write(5); w.Write(new byte[56]); String(w, "scene\\fire.gfx");
                    w.Write(6); w.Write(new byte[36]); String(w, "models\\bird.ecm"); String(w, "fly");
                    foreach (int id in new[] { 8, 9 }) { w.Write(id); w.Write(76); }
                    w.Write(1000); w.Write(new byte[28]); String(w, "wind.wav");
                }
                int treeOffset = (int)s.Position;
                String(w, "tree.spt"); String(w, "tree.dds");
                int grassOffset = (int)s.Position;
                w.Write(new byte[24]); String(w, "grass.mox");
                s.Position = 36; w.Write(treeOffset); w.Write(grassOffset); w.Write(1); w.Write(1);
                s.Position = 116; foreach (int offset in offsets) w.Write(offset);
                return Tuple.Create(s.ToArray(), bsd);
            }
        }

        private static byte[] Bmd(float x, float z, float extentX = 2, float extentZ = 2, bool rotate = false, float scaleZ = 1)
        {
            using (var s = new MemoryStream())
            using (var w = new BinaryWriter(s))
            {
                w.Write(0x42584f4d); w.Write(0x80000001u); w.Write(false); w.Write(0x10000002);
                Vector(w, 1, 1, scaleZ); Vector(w, rotate ? 1 : 0, 0, rotate ? 0 : 1);
                Vector(w, 0, 1, 0); Vector(w, x, 10, z); w.Write(1);
                w.Write(0x10000004); w.Write(new byte[320]); w.Write(3); w.Write(1);
                w.Write(new byte[72]); w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)2);
                w.Write(new byte[36 + 24]);
                Vector(w, 0, 0, 0); Vector(w, extentX, 1, extentZ);
                Vector(w, -extentX, -1, -extentZ); Vector(w, extentX, 1, extentZ);
                w.Write(0); // no hulls
                return s.ToArray();
            }
        }

        private static void WithFiles(Action<string, string, string, byte[], byte[], string> action)
        {
            string root = Path.Combine(Path.GetTempPath(), "AngelicaMapPatcherTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var fixture = World();
                string map = Path.Combine(root, "maps", "sample"), models = Path.Combine(root, "LitModels", "sample", "1");
                Directory.CreateDirectory(map); Directory.CreateDirectory(models);
                File.WriteAllBytes(Path.Combine(map, "sample.ecwld"), fixture.Item1);
                File.WriteAllBytes(Path.Combine(map, "sample.ecbsd"), fixture.Item2);
                string model = Path.Combine(models, "new.bmd"), terrain = Path.Combine(map, "sample.trn2");
                File.WriteAllBytes(model, Bmd(-20, 20));
                using (var w = new BinaryWriter(File.Create(terrain)))
                { w.Write(0x54524e32); w.Write(1); w.Write(128f); w.Write(128f); w.Write(new byte[140]); }
                action(root, map, model, fixture.Item1, fixture.Item2, terrain);
            }
            finally { Directory.Delete(root, true); } // only this test's freshly created GUID directory
        }

        private static void String(BinaryWriter w, string value) { var bytes = System.Text.Encoding.ASCII.GetBytes(value); w.Write(bytes.Length); w.Write(bytes); }
        private static void Vector(BinaryWriter w, float x, float y, float z) { w.Write(x); w.Write(y); w.Write(z); }
        private static int I32(byte[] b, int offset) => BitConverter.ToInt32(b, offset);
        private static uint U32(byte[] b, int offset) => BitConverter.ToUInt32(b, offset);
        private static void Write(byte[] b, int offset, int value) => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, b, offset, 4);
    }
}
