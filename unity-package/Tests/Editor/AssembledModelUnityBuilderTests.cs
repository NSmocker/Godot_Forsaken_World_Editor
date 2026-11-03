using System.Text;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class AssembledModelUnityBuilderTests
    {
        [Test]
        public void BuildCombinedMeshCreatesOneSubmeshPerSurface()
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

            Assert.Greater(model.Skins.Count, 0);
            var skin = model.Skins[0];
            Assert.Greater(skin.Surfaces.Count, 0);
            var mesh = AssembledModelUnityBuilder.BuildCombinedMesh(skin);
            Assert.IsNotNull(mesh);
            Assert.AreEqual(skin.Surfaces.Count, mesh.subMeshCount);

            var materials = AssembledModelUnityBuilder.CreateSurfaceMaterials(skin);
            Assert.AreEqual(skin.Surfaces.Count, materials.Length);
        }
    }
}
