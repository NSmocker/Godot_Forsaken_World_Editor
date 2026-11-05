using System.IO;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GameDataFolderTreeTests
    {
        [Test]
        public void BuildsLazyFolderCountsAndFilters()
        {
            var paths = new[]
            {
                "gfx/effects/Spark.gfx",
                "gfx/effects/Fire.gfx",
                "models/npc/Guard.ecm",
                "maps/world/a01.trn2",
                "models/npc/Guard.smd"
            };

            var all = GameDataFolderTree.Build(paths);
            Assert.AreEqual(2, all.GfxCount);
            Assert.AreEqual(1, all.EcmCount);
            Assert.AreEqual(1, all.MapCount);
            Assert.AreEqual(5, all.ShownCount);
            Assert.IsTrue(all.Root.Dirs.ContainsKey("gfx"));
            Assert.AreEqual(2, all.Root.Dirs["gfx"].Count);
            Assert.AreEqual(2, all.Root.Dirs["gfx"].Dirs["effects"].Files.Count);

            var gfxOnly = GameDataFolderTree.Build(paths, "gfx", "");
            Assert.AreEqual(2, gfxOnly.ShownCount);
            Assert.AreEqual(0, gfxOnly.EcmCount);

            var query = GameDataFolderTree.Build(paths, "", "guard");
            Assert.AreEqual(2, query.ShownCount);
            Assert.IsTrue(query.ExpandAll);
        }

        [Test]
        public void ScanCacheRoundTripsRootTimeAndPaths()
        {
            var cache = Path.Combine(Path.GetTempPath(), "forsaken-cache-" + Path.GetRandomFileName());
            var root = Path.Combine(Path.GetTempPath(), "forsaken-data-" + Path.GetRandomFileName());
            Directory.CreateDirectory(root);
            try
            {
                var time = GameDataScanCache.FormatScanTime(System.DateTime.Now);
                GameDataScanCache.Save(
                    cache,
                    root,
                    time,
                    new[] { "gfx/a.gfx", "models/b.ecm" });

                GameDataScanCache.Snapshot snap;
                Assert.IsTrue(GameDataScanCache.TryLoad(cache, root, out snap));
                Assert.AreEqual(time, snap.ScanTime);
                Assert.AreEqual(2, snap.RelativePaths.Count);
                Assert.AreEqual("gfx/a.gfx", snap.RelativePaths[0]);

                var other = Path.Combine(Path.GetTempPath(), "other-" + Path.GetRandomFileName());
                Assert.IsFalse(GameDataScanCache.TryLoad(cache, other, out snap));

                var index = new GameDataIndex(root);
                index.ReplaceFromRelativePaths(new[] { "gfx/a.gfx", "models/b.ecm" });
                Assert.AreEqual(2, index.Files.Count);
                Assert.AreEqual("gfx/a.gfx", index.Files[0].RelativePath);
                Assert.IsTrue(index.Files[0].FullPath.Replace('\\', '/').EndsWith("gfx/a.gfx"));
            }
            finally
            {
                if (File.Exists(cache))
                    File.Delete(cache);
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [Test]
        public void DefaultCachePathUsesLibraryForsakenWorld()
        {
            var path = GameDataScanCache.DefaultCachePath("/proj/Assets");
            Assert.IsTrue(path.Replace('\\', '/').EndsWith("Library/ForsakenWorld/ecm_gfx_index3.txt"));
        }
    }
}
