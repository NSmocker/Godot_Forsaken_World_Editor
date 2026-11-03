using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class ModelAssemblerTests
    {
        [Test]
        public void AssemblesBonesSurfacesAndPreservesSourceWinding()
        {
            var decoder = new GfxTextDecoder(() => Encoding.GetEncoding(28591));
            var smd = new SmdParser(decoder).Parse(SmdParserTests.BuildFixture());
            var bon = new BonParser(decoder).Parse(BonParserTests.BuildFixture());
            var ski = new SkiParser(decoder).Parse(SkiParserTests.BuildFixture());

            var model = new ModelAssembler().Assemble(
                smd,
                "models/npc/Guard.smd",
                bon,
                "models/npc/Skeleton.bon",
                new[] { ski },
                new[] { "models/npc/Body.ski" });

            Assert.IsTrue(model.IsValid);
            Assert.AreEqual("root", model.Bones[0].Name);
            Assert.AreEqual(1, model.HookBones.Count);
            Assert.AreEqual("hand", model.HookBones[0].Name);
            Assert.AreEqual(1, model.Skins.Count);
            Assert.AreEqual(2, model.Skins[0].Surfaces.Count);
            Assert.AreEqual(0, model.Skins[0].Surfaces[0].Indices[0]);
            Assert.AreEqual(1, model.Skins[0].Surfaces[0].Indices[1]);
            Assert.AreEqual(2, model.Skins[0].Surfaces[0].Indices[2]);
            Assert.AreEqual("root", model.Skins[0].BindBones[0].Name);
            Assert.AreEqual(0, model.Skins[0].BindBones[0].SkeletonBoneIndex);
            Assert.AreEqual(1.0f, model.Skins[0].Surfaces[0].BoneWeights[0] +
                model.Skins[0].Surfaces[0].BoneWeights[1] +
                model.Skins[0].Surfaces[0].BoneWeights[2] +
                model.Skins[0].Surfaces[0].BoneWeights[3], 1e-4f);
        }
    }
}
