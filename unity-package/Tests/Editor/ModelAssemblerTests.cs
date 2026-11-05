using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class ModelAssemblerTests
    {
        [Test]
        public void LegacySkinUsesSkeletonIndicesAndLegacyActionUsesEmbeddedTracks()
        {
            var bon = new BonParser().Parse(BonParserTests.BuildFixture());
            bon.EmbeddedTracks = new StckDocument { FramesPerSecond = 30, AnimationEnd = 30 };
            bon.EmbeddedTracks.Tracks.Add(new StckJointTrack
            {
                JointIndex = 0,
                Position = new StckChannel { FramesPerSecond = 30, Keys = new[] { 0f, 0f, 0f, 3f, 0f, 0f }, Segments = new[] { 0, 1000, 0, 1 } },
                Rotation = new StckChannel { FramesPerSecond = 30, IsQuaternion = true, Keys = new[] { 0f, 0f, 0f, 1f }, Segments = new[] { 0, 1000, 0, 0 } }
            });
            var ski = new SkiParser().Parse(SkiParserTests.BuildFixture());
            ski.Version = 8; ski.BoneNames.Clear();
            for (int v = 0; v < ski.SkinMeshes[0].BoneIndices.Length; v++) ski.SkinMeshes[0].BoneIndices[v] = 0;
            var smd = new SmdDocument { Version = 5 };
            smd.Actions.Add(new SmdAction { Name = "Move", StartFrame = 0, EndFrame = 30 });
            var model = new ModelAssembler().Assemble(smd, "legacy.smd", bon, "legacy.bon", new[] { ski }, new[] { "legacy.ski" });
            Assert.AreEqual(0, model.Skins[0].BindBones[0].SkeletonBoneIndex);
            Assert.AreEqual(1, model.Animations.Count);
            Assert.AreEqual("legacy.bon", model.Animations[0].TrackSourcePath);
            var keys = model.Animations[0].Bones[0].PositionKeys;
            Assert.AreEqual(3f, keys[keys.Count - 1].X, 0.001f);
        }

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
