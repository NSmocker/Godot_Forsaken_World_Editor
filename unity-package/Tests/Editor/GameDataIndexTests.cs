using System;
using System.IO;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GameDataIndexTests
    {
        [Test]
        public void ResolvesUnpackerEncodingAliasesWithoutChangingRealPaths()
        {
            var table = new byte[126 * 191 * 2];
            int offset = ((0xc5 - 0x81) * 191 + 0xae - 0x40) * 2;
            table[offset] = 0x73; table[offset + 1] = 0x59; // GBK C5 AE -> 女
            var index = new GameDataIndex(Path.GetTempPath(), new GfxTextDecoder(null, table));
            index.ReplaceFromRelativePaths(new[] { "models/Ůnpc/Ůnpc.bon" });
            Assert.IsTrue(index.TryResolve("models/Ůnpc/女npc.bon", out var file, out var error), error);
            Assert.AreEqual("models/Ůnpc/Ůnpc.bon", file.RelativePath);
            Assert.AreEqual("models/女npc/女npc.bon", file.DisplayRelativePath);
            Assert.AreEqual("女npc", file.DisplayName);
            Assert.IsTrue(index.TryResolve("女npc.bon", out file, out error, true), error);
            index.ReplaceFromRelativePaths(new[] { "models/Ůnpc.bon", "other/Ůnpc.bon" });
            Assert.IsFalse(index.TryResolve("女npc.bon", out file, out error, true));
            StringAssert.Contains("ambiguous", error);
        }

        [Test]
        public void CacheLoadRemapsMojibakePathToExistingCjkFile()
        {
            var table = new byte[126 * 191 * 2];
            int offset = ((0xc5 - 0x81) * 191 + 0xae - 0x40) * 2;
            table[offset] = 0x73;
            table[offset + 1] = 0x59; // GBK C5 AE -> 女
            var folder = Path.Combine(Path.GetTempPath(), "fw-mojibake-" + Guid.NewGuid().ToString("N"));
            var realDir = Path.Combine(folder, "models", "女npc21");
            Directory.CreateDirectory(realDir);
            var realFile = Path.Combine(realDir, "女npc21.smd");
            File.WriteAllText(realFile, "smd");
            try
            {
                var index = new GameDataIndex(folder, new GfxTextDecoder(null, table));
                index.ReplaceFromRelativePaths(new[] { "models/Ůnpc21/Ůnpc21.smd" });
                Assert.AreEqual(1, index.RepairedPathCount);
                Assert.AreEqual(1, index.Files.Count);
                Assert.AreEqual("models/女npc21/女npc21.smd", index.Files[0].RelativePath);
                Assert.IsTrue(File.Exists(index.Files[0].FullPath));
                Assert.IsTrue(index.TryResolve("models/女npc21/女npc21.smd", out var resolved, out var error), error);
                Assert.AreEqual(index.Files[0].FullPath, resolved.FullPath);
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }

        private string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(
                Path.GetTempPath(),
                "ForsakenWorldIndexTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "gfx", "effects"));
            Directory.CreateDirectory(Path.Combine(root, "models"));
            File.WriteAllText(Path.Combine(root, "gfx", "effects", "Spark.GFX"), "gfx");
            File.WriteAllText(Path.Combine(root, "models", "Guard.ECM"), "ecm");
            File.WriteAllText(Path.Combine(root, "models", "Guard.SMD"), "smd");
            File.WriteAllText(Path.Combine(root, "models", "Guard.BON"), "bon");
            File.WriteAllText(Path.Combine(root, "models", "Guard.SKI"), "ski");
            File.WriteAllText(Path.Combine(root, "models", "Guard.STCK"), "stck");
            Directory.CreateDirectory(Path.Combine(root, "litmodels", "a01"));
            File.WriteAllText(Path.Combine(root, "litmodels", "a01", "litmodel_0.BMD"), "bmd");
            File.WriteAllText(Path.Combine(root, "readme.txt"), "not indexed");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        [Test]
        public void ScansSupportedFilesAndSearchesByPathAndExtension()
        {
            var index = new GameDataIndex(root);
            var reportedDirectories = 0;
            var reportedFiles = 0;
            index.Scan((directories, files) =>
            {
                reportedDirectories = directories;
                reportedFiles = files;
            });

            Assert.AreEqual(7, index.Files.Count);
            Assert.AreEqual(6, reportedDirectories);
            Assert.AreEqual(7, reportedFiles);
            Assert.AreEqual("Guard", index.Search("guard", ".ecm")[0].Name);
            Assert.AreEqual("Guard", index.Search("guard", "smd")[0].Name);
            Assert.AreEqual("Guard", index.Search("guard", "bon")[0].Name);
            Assert.AreEqual("Guard", index.Search("guard", "ski")[0].Name);
            Assert.AreEqual("Guard", index.Search("guard", "stck")[0].Name);
            Assert.AreEqual("litmodel_0", index.Search("litmodel", "bmd")[0].Name);
            Assert.AreEqual(1, index.Search("EFFECTS", "gfx").Count);
        }

        [Test]
        public void ResolvesPathsCaseInsensitivelyAndByUniqueSuffix()
        {
            var index = new GameDataIndex(root);
            index.Scan();
            GameDataFile file;
            string error;

            Assert.IsTrue(index.TryResolve("GFX\\EFFECTS\\spark.gfx", out file, out error));
            Assert.AreEqual("gfx/effects/Spark.GFX", file.RelativePath);
            Assert.IsTrue(index.TryResolve("effects/spark.gfx", out file, out error, true));
            Assert.AreEqual("gfx/effects/Spark.GFX", file.RelativePath);
        }

        [Test]
        public void RejectsAbsoluteAndParentTraversalPaths()
        {
            var index = new GameDataIndex(root);
            index.Scan();
            GameDataFile file;
            string error;

            Assert.IsFalse(index.TryResolve("../outside.gfx", out file, out error));
            StringAssert.Contains("traverse", error);
            Assert.IsFalse(index.TryResolve("C:\\outside.gfx", out file, out error));
            StringAssert.Contains("relative", error);
        }

        [Test]
        public void ReportsAmbiguousSuffixInsteadOfPickingArbitrarily()
        {
            Directory.CreateDirectory(Path.Combine(root, "other", "effects"));
            File.WriteAllText(Path.Combine(root, "other", "effects", "Spark.GFX"), "gfx");
            var index = new GameDataIndex(root);
            index.Scan();
            GameDataFile file;
            string error;

            Assert.IsFalse(index.TryResolve("effects/spark.gfx", out file, out error, true));
            StringAssert.Contains("ambiguous", error);
        }
    }
}
