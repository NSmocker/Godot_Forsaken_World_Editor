using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class EcmModelResolverTests
    {
        private string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "ForsakenEcmResolve", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "models", "npc"));
            File.WriteAllBytes(Path.Combine(root, "models", "npc", "Guard.smd"), SmdParserTests.BuildFixture());
            File.WriteAllBytes(Path.Combine(root, "models", "npc", "Skeleton.bon"), BonParserTests.BuildFixture());
            File.WriteAllBytes(Path.Combine(root, "models", "npc", "Body.ski"), SkiParserTests.BuildFixture());
            File.WriteAllBytes(Path.Combine(root, "models", "npc", "Helmet.ski"), SkiParserTests.BuildFixture());
            File.WriteAllBytes(Path.Combine(root, "models", "npc", "Run.stck"), StckParserTests.BuildFixture());
            File.WriteAllText(
                Path.Combine(root, "models", "npc", "Guard.ecm"),
                "SkinModelPath: Models\\NPC\\Guard.smd\nAddiSkinPath: NPC\\Helmet.ski\n");

            // Beside-ECM fallback: basename Star.smd is ambiguous via suffix, so only the
            // exact path next to the .ecm should succeed (Godot _find_model fallback).
            Directory.CreateDirectory(Path.Combine(root, "models", "pet", "star"));
            Directory.CreateDirectory(Path.Combine(root, "models", "other", "star2"));
            File.WriteAllBytes(Path.Combine(root, "models", "pet", "star", "Star.smd"), SmdParserTests.BuildFixture());
            File.WriteAllBytes(Path.Combine(root, "models", "other", "star2", "Star.smd"), SmdParserTests.BuildFixture());
            File.WriteAllBytes(Path.Combine(root, "models", "pet", "star", "Skeleton.bon"), BonParserTests.BuildFixture());
            File.WriteAllBytes(Path.Combine(root, "models", "pet", "star", "Body.ski"), SkiParserTests.BuildFixture());
            File.WriteAllText(
                Path.Combine(root, "models", "pet", "star", "Star.ecm"),
                "SkinModelPath: Models\\Missing\\Deep\\Star.SMD\n");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        [Test]
        public void PrepareResolvesSkinModelAndAdditionalSkins()
        {
            var index = new GameDataIndex(root);
            index.Scan();
            var ecm = new EcmParser().Parse(
                "SkinModelPath: Models\\NPC\\Guard.smd\nAddiSkinPath: NPC\\Helmet.ski\n");
            var preparation = new EcmModelResolver(index).Prepare(ecm, "models/npc/Guard.ecm");

            Assert.IsFalse(preparation.HasBlockingErrors);
            Assert.IsTrue(preparation.SkinModel.IsResolved);
            Assert.AreEqual("models/npc/Guard.smd", preparation.SkinModel.File.RelativePath);
            Assert.AreEqual(1, preparation.AdditionalSkins.Count);
            Assert.IsTrue(preparation.AdditionalSkins[0].IsResolved);
            Assert.AreEqual("models/npc/Helmet.ski", preparation.AdditionalSkins[0].File.RelativePath);
        }

        [Test]
        public void PrepareFallsBackToSmdBesideEcmWithWarning()
        {
            var index = new GameDataIndex(root);
            index.Scan();
            var ecm = new EcmParser().Parse("SkinModelPath: Models\\Missing\\Deep\\Star.SMD\n");
            var preparation = new EcmModelResolver(index).Prepare(ecm, "models/pet/star/Star.ecm");

            Assert.IsFalse(preparation.HasBlockingErrors);
            Assert.IsTrue(preparation.UsedBesideEcmFallback);
            Assert.AreEqual("models/pet/star/Star.smd", preparation.SkinModel.File.RelativePath);
            Assert.IsTrue(preparation.Warnings.Exists(warning =>
                warning.IndexOf("beside the .ecm", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        [Test]
        public void ResolveSmdWithAdditionalSkinsMergesWithoutDuplicates()
        {
            var index = new GameDataIndex(root);
            index.Scan();
            var decoder = new GfxTextDecoder(() => Encoding.GetEncoding(28591));
            var smd = new SmdParser(decoder).Parse(
                SmdParserTests.BuildFixture(),
                "models/npc/Guard.smd");
            var resolution = new EcmModelResolver(index).ResolveSmdWithAdditionalSkins(
                smd,
                "models/npc/Guard.smd",
                new[] { "NPC\\Body.ski", "NPC\\Helmet.ski" });

            Assert.IsFalse(resolution.HasBlockingErrors);
            Assert.AreEqual(2, resolution.Skins.Count);
            Assert.AreEqual("models/npc/Body.ski", resolution.Skins[0].File.RelativePath);
            Assert.AreEqual("models/npc/Helmet.ski", resolution.Skins[1].File.RelativePath);
        }

        [Test]
        public void PrepareReportsMissingSkinModel()
        {
            var index = new GameDataIndex(root);
            index.Scan();
            var ecm = new EcmParser().Parse("SkinModelPath: Models\\NoSuch\\Ghost.smd\n");
            var preparation = new EcmModelResolver(index).Prepare(ecm, "models/npc/Guard.ecm");

            Assert.IsTrue(preparation.HasBlockingErrors);
            StringAssert.Contains("Ghost.smd", preparation.Errors[0]);
        }
    }
}
