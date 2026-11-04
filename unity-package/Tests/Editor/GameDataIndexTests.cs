using System;
using System.IO;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GameDataIndexTests
    {
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
