using System.IO;
using System.Text;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class ModelMaterialAssemblerTests
    {
        [Test]
        public void ResolvesSkiTexturesAndBuildsUrpMaterialSlots()
        {
            var root = Path.Combine(Path.GetTempPath(), "forsaken-mat-" + Path.GetRandomFileName());
            Directory.CreateDirectory(Path.Combine(root, "models", "npc", "Textures"));
            try
            {
                var dds = DdsHeaderParserTests.BuildDds(4, 4, "DXT5", 1, 16);
                File.WriteAllBytes(
                    Path.Combine(root, "models", "npc", "Textures", "body.dds"),
                    dds);

                var decoder = new GfxTextDecoder(() => Encoding.GetEncoding(28591));
                var ski = new SkiParser(decoder).Parse(
                    SkiParserTests.BuildFixture(),
                    "models/npc/Body.ski");
                var smd = new SmdParser(decoder).Parse(SmdParserTests.BuildFixture());
                var bon = new BonParser(decoder).Parse(BonParserTests.BuildFixture());
                var model = new ModelAssembler().Assemble(
                    smd,
                    "models/npc/Guard.smd",
                    bon,
                    "models/npc/Skeleton.bon",
                    new[] { ski },
                    new[] { "models/npc/Body.ski" });

                var index = new GameDataIndex(root);
                index.Scan();
                new ModelMaterialAssembler(new SkiTextureResolver(index)).BuildForModel(model);
                Assert.Greater(model.Skins[0].SurfaceMaterials.Count, 0);
                var slot = model.Skins[0].SurfaceMaterials[0];
                Assert.IsTrue(slot.TextureResolved);
                Assert.AreEqual(AssembledAlphaMode.AlphaClip, slot.AlphaMode);
                Assert.AreEqual(DdsCompression.Dxt5, slot.TextureDds.Compression);

                var material = AssembledUrpMaterialBuilder.CreateMaterial(slot);
                Assert.IsNotNull(material);
                Assert.IsTrue(material.HasProperty("_BaseColor"));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [Test]
        public void ReportsMissingTextureWithoutThrowing()
        {
            var root = Path.Combine(Path.GetTempPath(), "forsaken-mat-miss-" + Path.GetRandomFileName());
            Directory.CreateDirectory(Path.Combine(root, "models", "npc"));
            try
            {
                var decoder = new GfxTextDecoder(() => Encoding.GetEncoding(28591));
                var ski = new SkiParser(decoder).Parse(
                    SkiParserTests.BuildFixture(),
                    "models/npc/Body.ski");
                var smd = new SmdParser(decoder).Parse(SmdParserTests.BuildFixture());
                var bon = new BonParser(decoder).Parse(BonParserTests.BuildFixture());
                var model = new ModelAssembler().Assemble(
                    smd,
                    "models/npc/Guard.smd",
                    bon,
                    "models/npc/Skeleton.bon",
                    new[] { ski },
                    new[] { "models/npc/Body.ski" });

                var index = new GameDataIndex(root);
                index.Scan();
                new ModelMaterialAssembler(new SkiTextureResolver(index)).BuildForModel(model);
                Assert.Greater(model.Skins[0].SurfaceMaterials.Count, 0);
                Assert.IsFalse(model.Skins[0].SurfaceMaterials[0].TextureResolved);
                Assert.Greater(model.Warnings.Count, 0);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }
    }
}
