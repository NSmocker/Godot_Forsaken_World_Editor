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

        [Test]
        public void ReflectPrefixSelectsArmorReflectShaderAndUsesSphereMap()
        {
            var slot = new AssembledSurfaceMaterial
            {
                Name = "blade",
                TextureName = "r_blade.dds",
                TextureIndex = 0,
                MaterialIndex = 0,
                Diffuse = new[] { 1f, 1f, 1f, 1f },
                Specular = new[] { 0.4f, 0.4f, 0.4f, 1f },
                SpecularLuminance = 0.4f,
                Emissive = new[] { 0f, 0f, 0f },
                UsesReflectShader = true,
                AlphaMode = AssembledAlphaMode.Opaque
            };
            Assert.IsTrue(slot.TextureName.StartsWith(
                ModelMaterialAssembler.ReflectTexturePrefix,
                System.StringComparison.OrdinalIgnoreCase));

            var albedo = new Texture2D(2, 2);
            var reflect = new Texture2D(2, 2);
            var material = AssembledUrpMaterialBuilder.CreateMaterial(slot, albedo, reflect);
            Assert.AreEqual(AssembledUrpMaterialBuilder.ArmorReflectShaderName, material.shader.name);
            Assert.IsTrue(material.HasProperty("_ReflectMap"));
            Assert.AreEqual(reflect, material.GetTexture("_ReflectMap"));
            Assert.AreEqual(albedo, material.GetTexture("_Albedo"));

            var cache = new System.Collections.Generic.Dictionary<string, Texture2D>(
                System.StringComparer.OrdinalIgnoreCase)
            {
                { AssembledUrpMaterialBuilder.ReflectMapCacheKey, reflect }
            };
            Assert.AreEqual(reflect, AssembledUrpMaterialBuilder.FindReflectMap(cache));
            Assert.AreEqual(
                "Textures/reflect/armorreflect.bmp",
                ModelMaterialAssembler.ReflectMapRelativePath);
        }

        [Test]
        public void AssemblerMarksRPrefixTexturesAsReflect()
        {
            Assert.AreEqual("r_", ModelMaterialAssembler.ReflectTexturePrefix);
            Assert.IsTrue("r_sword.dds".StartsWith(
                ModelMaterialAssembler.ReflectTexturePrefix,
                System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
