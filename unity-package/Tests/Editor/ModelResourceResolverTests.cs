using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class ModelResourceResolverTests
    {
        private string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "ForsakenModelResolve", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "models", "npc"));
            Directory.CreateDirectory(Path.Combine(root, "models", "tracks"));
            File.WriteAllBytes(Path.Combine(root, "models", "npc", "Guard.smd"), SmdParserTests.BuildFixture());
            File.WriteAllBytes(Path.Combine(root, "models", "npc", "Skeleton.bon"), BonParserTests.BuildFixture());
            File.WriteAllBytes(Path.Combine(root, "models", "npc", "Body.ski"), SkiParserTests.BuildFixture());
            File.WriteAllBytes(Path.Combine(root, "models", "npc", "Run.stck"), StckParserTests.BuildFixture());
            File.WriteAllBytes(Path.Combine(root, "models", "tracks", "Alt.stck"), StckParserTests.BuildFixture());
            Directory.CreateDirectory(Path.Combine(root, "models", "dup_a"));
            Directory.CreateDirectory(Path.Combine(root, "models", "dup_b"));
            File.WriteAllText(Path.Combine(root, "models", "dup_a", "DupA.ski"), "a");
            File.WriteAllText(Path.Combine(root, "models", "dup_b", "DupA.ski"), "b");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        [Test]
        public void ResolvesSkeletonSkinsAndTracksRelativeToSmd()
        {
            var index = new GameDataIndex(root);
            index.Scan();
            var smd = new SmdParser(new GfxTextDecoder(() => Encoding.GetEncoding(28591)))
                .Parse(SmdParserTests.BuildFixture(), "models/npc/Guard.smd");
            var resolution = new ModelResourceResolver(index).Resolve(smd, "models/npc/Guard.smd");

            Assert.IsFalse(resolution.HasBlockingErrors);
            Assert.AreEqual("models/npc/Skeleton.bon", resolution.Skeleton.File.RelativePath);
            Assert.AreEqual(1, resolution.Skins.Count);
            Assert.AreEqual("models/npc/Body.ski", resolution.Skins[0].File.RelativePath);
            Assert.IsTrue(resolution.ActionTracks[0].Track.IsResolved);
            Assert.AreEqual("models/npc/Run.stck", resolution.ActionTracks[0].Track.File.RelativePath);
        }

        [Test]
        public void ReportsMissingAndAmbiguousResourcesExplicitly()
        {
            var index = new GameDataIndex(root);
            index.Scan();
            var smd = new SmdDocument
            {
                SkeletonPath = "Missing.bon"
            };
            smd.SkinPaths.Add("DupA.ski");
            smd.Actions.Add(new SmdAction
            {
                Name = "Wave",
                TrackPath = "Missing.stck"
            });

            var resolution = new ModelResourceResolver(index).Resolve(smd, "models/npc/Guard.smd");
            Assert.IsTrue(resolution.HasBlockingErrors);
            StringAssert.Contains("Missing.bon", resolution.Errors[0]);
            Assert.AreEqual(1, resolution.Skins.Count);
            StringAssert.Contains("ambiguous", resolution.Skins[0].Error.ToLowerInvariant());
            Assert.IsTrue(resolution.Warnings.Exists(warning =>
                warning.IndexOf("Missing.stck", StringComparison.OrdinalIgnoreCase) >= 0));
        }
    }
}
