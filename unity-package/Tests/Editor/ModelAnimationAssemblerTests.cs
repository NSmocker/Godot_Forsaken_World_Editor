using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class ModelAnimationAssemblerTests
    {
        [Test]
        public void BuildsClipsBoundToSkeletonBonePathsFromSmdActionRange()
        {
            var decoder = new GfxTextDecoder(() => Encoding.GetEncoding(28591));
            var smd = new SmdParser(decoder).Parse(SmdParserTests.BuildFixture());
            var bon = new BonParser(decoder).Parse(BonParserTests.BuildFixture());
            var ski = new SkiParser(decoder).Parse(SkiParserTests.BuildFixture());
            var stck = new StckParser().Parse(StckParserTests.BuildFixture());

            var model = new ModelAssembler().Assemble(
                smd,
                "models/npc/Guard.smd",
                bon,
                "models/npc/Skeleton.bon",
                new[] { ski },
                new[] { "models/npc/Body.ski" });

            Assert.AreEqual(0, model.Bones[0].FirstJointIndex);

            var sources = new List<ActionAnimationSource>
            {
                new ActionAnimationSource
                {
                    ActionName = "Run",
                    StartFrame = 0f,
                    EndFrame = 30f,
                    TrackSourcePath = "models/npc/Run.stck",
                    Track = stck
                }
            };

            var animations = new ModelAnimationAssembler().Build(model, sources);
            Assert.AreEqual(1, animations.Count);
            Assert.AreEqual("Run", animations[0].Name);
            Assert.AreEqual(1f, animations[0].LengthSeconds, 1e-4f);
            Assert.AreEqual(30, animations[0].SampleFramesPerSecond);
            Assert.Greater(animations[0].Bones.Count, 0);
            Assert.AreEqual("Skeleton/root", animations[0].Bones[0].HierarchyPath);
            Assert.Greater(animations[0].Bones[0].PositionKeys.Count, 0);
            Assert.Greater(animations[0].Bones[0].RotationKeys.Count, 0);
            Assert.AreEqual(1, model.Animations.Count);

            var clip = AssembledAnimationClipBuilder.CreateClip(animations[0]);
            Assert.AreEqual("Run", clip.name);
            Assert.AreEqual(30f, clip.frameRate, 1e-4f);

            var root = AssembledModelUnityBuilder.BuildHierarchyWithAnimations(model, animations);
            Assert.IsNotNull(root);
            Assert.IsNotNull(root.GetComponent<Animation>());
        }

        [Test]
        public void WarnsWhenTrackMissingAndSkipsInvalidSources()
        {
            var decoder = new GfxTextDecoder(() => Encoding.GetEncoding(28591));
            var smd = new SmdParser(decoder).Parse(SmdParserTests.BuildFixture());
            var bon = new BonParser(decoder).Parse(BonParserTests.BuildFixture());
            var model = new ModelAssembler().Assemble(
                smd,
                "models/npc/Guard.smd",
                bon,
                "models/npc/Skeleton.bon",
                new SkiDocument[0],
                new string[0]);

            var animations = new ModelAnimationAssembler().Build(
                model,
                new[]
                {
                    new ActionAnimationSource
                    {
                        ActionName = "Missing",
                        StartFrame = 0,
                        EndFrame = 10,
                        TrackSourcePath = "missing.stck",
                        Track = null
                    }
                });

            Assert.AreEqual(0, animations.Count);
            Assert.Greater(model.Warnings.Count, 0);
            StringAssert.Contains("track was not loaded", model.Warnings[0]);
        }

        [Test]
        public void BuildSampleTimesMatchesGodotStepping()
        {
            var times = ModelAnimationAssembler.BuildSampleTimes(0, 1000, 30);
            Assert.AreEqual(0, times[0]);
            Assert.AreEqual(1000, times[times.Count - 1]);
            Assert.Greater(times.Count, 2);
            for (var i = 1; i < times.Count; i++)
                Assert.GreaterOrEqual(times[i], times[i - 1]);
        }
    }
}
