using System.Collections.Generic;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxTrailTests
    {
        private const string TrailGfx =
            "MOXTVersion: 92\n" +
            "DedaultScale: 1\n" +
            "2DRender: 0\n" +
            "GFXELEMENTID: 110 Speed\n" +
            "Name: Speed\n" +
            "DestBlend: 2\n" +
            "TexFile: fx\\trail.dds\n" +
            "OrgPos1: 0, 0.2, 0\n" +
            "OrgPos2: 0, -0.2, 0\n" +
            "SegLife: 500\n" +
            "FadeLength: 0.2\n" +
            "Width: 2\n" +
            "Height: 0.5\n" +
            "Bind: 1\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 1000\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 1\n" +
            "CtrlType: 100\n" +
            "StartTime: 0\n" +
            "EndTime: -1\n" +
            "Dir: 1, 0, 0\n" +
            "Vel: 2\n" +
            "Acc: 0\n" +
            "GFXELEMENTID: 180 Bolt\n" +
            "Name: Bolt\n" +
            "DestBlend: 6\n" +
            "TexFile: fx\\ltn.dds\n" +
            "Pos: 0, 0.1, 0\n" +
            "Pos_2: 0, -0.1, 0\n" +
            "SegLife: 400\n" +
            "Amp: 0.05\n" +
            "Amp_2: 0.1\n" +
            "Bind: 1\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 800\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n" +
            "GFXELEMENTID: 160 Model\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 0\n";

        [Test]
        public void SpeedTrailParamsParseOrgPosAndSegLife()
        {
            var doc = new GfxParser().Parse(TrailGfx);
            var p = GfxSpeedTrailParams.FromProperties(doc.Elements[0].Properties, "110", 1);
            Assert.AreEqual(500, p.SegLife);
            Assert.AreEqual(0.2f, p.FadeLength, 0.001f);
            Assert.AreEqual(0.2f, p.OrgPos1[1], 0.001f);
            Assert.AreEqual(-0.2f, p.OrgPos2[1], 0.001f);
            Assert.IsTrue(p.IsAdditiveBlend);
            Assert.IsTrue(p.Bind);
        }

        [Test]
        public void LtnTrailParamsParseAmpAndPairCount()
        {
            var doc = new GfxParser().Parse(TrailGfx);
            var p = GfxLtnTrailParams.FromProperties(
                doc.Elements[1].Properties, "180", 2, "Element_2_ID_180");
            Assert.AreEqual(400, p.SegLife);
            Assert.AreEqual(0.05f, p.Amp, 0.001f);
            Assert.AreEqual(0.1f, p.Amp2, 0.001f);
            Assert.AreEqual(0.1f, p.Pos[1], 0.001f);
            Assert.GreaterOrEqual(p.PairCount, 3);
            Assert.LessOrEqual(p.PairCount, GfxLtnTrailParams.MaxEntries);
            Assert.IsFalse(p.IsAdditiveBlend);
            Assert.AreEqual(0.70f, p.MixAlphaScale, 0.001f);
        }

        [Test]
        public void RegistryMarks110And180Ported()
        {
            Assert.IsTrue(GfxElementRegistry.IsPorted("110"));
            Assert.IsTrue(GfxElementRegistry.IsPorted("180"));
            Assert.IsFalse(GfxElementRegistry.IsPorted("160"));
        }

        [Test]
        public void PlannerMarksTrailsPortedAndModelStubbed()
        {
            var doc = new GfxParser().Parse(TrailGfx);
            var plan = GfxRuntimePlanner.Build(doc, "Trails", "trails.gfx");
            Assert.AreEqual(3, plan.Elements.Count);
            Assert.IsFalse(plan.Elements[0].IsStub);
            Assert.IsFalse(plan.Elements[1].IsStub);
            Assert.IsTrue(plan.Elements[2].IsStub);
            Assert.IsTrue(plan.StubbedElementIds.Contains("160"));
            Assert.IsFalse(plan.StubbedElementIds.Contains("110"));
            Assert.IsFalse(plan.StubbedElementIds.Contains("180"));
        }

        [Test]
        public void SpeedTrailRibbonHasSegLifeMaskAndMovingHead()
        {
            var props = new Dictionary<string, string>
            {
                { "OrgPos1", "0, 0.25, 0" },
                { "OrgPos2", "0, -0.25, 0" },
                { "SegLife", "500" },
                { "FadeLength", "0.15" },
                { "DestBlend", "2" }
            };
            var p = GfxSpeedTrailParams.FromProperties(props, "110", 1);
            p.SegmentCount = 10;
            var kps = new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec
                {
                    TimeSpan = 1000f,
                    Pos = GfxMath.Vec3(0f, 0f, 0f),
                    Dir = GfxMath.QuatIdentity(),
                    Color = GfxMath.ColorWhite(),
                    Scale = 1f,
                    Ctrls = new List<GfxCtrlSpec>
                    {
                        new GfxCtrlSpec
                        {
                            Type = 100,
                            Start = 0f,
                            End = -1f,
                            Dir = GfxMath.Vec3(1f, 0f, 0f),
                            Vel = 1f,
                            Acc = 0f,
                            HasVelocity = true
                        }
                    }
                }
            };
            var early = GfxSpeedTrailRibbon.Build(p, kps, 0f, 1000f, 100f, 1f);
            var late = GfxSpeedTrailRibbon.Build(p, kps, 0f, 1000f, 600f, 1f);
            Assert.AreEqual(11 * 2, early.Positions.Length / 3);
            Assert.AreEqual(10 * 6, early.Triangles.Length);
            // Head is near current time: late trail should have non-zero alpha near head
            // and early (t=100ms, SegLife=500) still shows birth near 0.
            var lateAlphaSum = 0f;
            for (var i = 0; i < late.Colors.Length; i += 4)
                lateAlphaSum += late.Colors[i + 3];
            Assert.Greater(lateAlphaSum, 0f);

            // Ctrl 100 moves along +X: last segment center should be ahead of first.
            var firstX = (early.Positions[0] + early.Positions[3]) * 0.5f;
            var lastIdx = 10 * 2;
            var lastX = (early.Positions[lastIdx * 3] + early.Positions[lastIdx * 3 + 3]) * 0.5f;
            Assert.Greater(lastX, firstX);
        }

        [Test]
        public void LtnTrailRndIsDeterministicAndTexUSteps()
        {
            Assert.AreEqual(
                GfxLtnTrailEval.UnitRandom(3f, 1, 42),
                GfxLtnTrailEval.UnitRandom(3f, 1, 42),
                0.000001f);
            Assert.AreEqual(0f, GfxLtnTrailEval.TexU(0f), 0.001f);
            Assert.AreEqual(0.2f, GfxLtnTrailEval.TexU(1f), 0.001f);
            Assert.AreEqual(0.4f, GfxLtnTrailEval.TexU(2f), 0.001f);
        }

        [Test]
        public void LtnTrailBuildProducesStripWithPairs()
        {
            var props = new Dictionary<string, string>
            {
                { "Pos", "0, 0.1, 0" },
                { "Pos_2", "0, -0.1, 0" },
                { "SegLife", "200" },
                { "Amp", "0.05" },
                { "Amp_2", "0.08" },
                { "Bind", "1" },
                { "DestBlend", "6" }
            };
            var p = GfxLtnTrailParams.FromProperties(props, "180", 1, "Bolt");
            var kps = new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec
                {
                    TimeSpan = 1000f,
                    Pos = GfxMath.Vec3(0f, 0f, 0f),
                    Dir = GfxMath.QuatIdentity(),
                    Color = GfxMath.ColorWhite(),
                    Scale = 1f
                }
            };
            var mesh = GfxLtnTrailEval.Build(
                p, kps, 0f, 1000f, 3000f, 1, 0f, false,
                200f, 1f, null, null, null,
                GfxMath.Vec3Zero(), GfxMath.QuatIdentity(), 1f);
            Assert.GreaterOrEqual(mesh.PairCount, 3);
            Assert.AreEqual(mesh.PairCount * 2, mesh.Positions.Length / 3);
            Assert.AreEqual((mesh.PairCount - 1) * 6, mesh.Triangles.Length);
            var alphaSum = 0f;
            for (var i = 0; i < mesh.Colors.Length; i += 4)
                alphaSum += mesh.Colors[i + 3];
            Assert.Greater(alphaSum, 0f);
        }

        [Test]
        public void SceneBuilderCreatesTrailDriversNotStubs()
        {
            GfxElementStub.LoggedIdsThisSession.Clear();
            var doc = new GfxParser().Parse(TrailGfx);
            var built = GfxSceneBuilder.Build(doc, "TrailGfx", "trail.gfx");
            Assert.AreEqual(3, built.Root.transform.childCount);
            var speeds = built.Root.GetComponentsInChildren<GfxSpeedTrailDriver>(true);
            var ltns = built.Root.GetComponentsInChildren<GfxLtnTrailDriver>(true);
            var stubs = built.Root.GetComponentsInChildren<GfxElementStub>(true);
            Assert.AreEqual(1, speeds.Length);
            Assert.AreEqual(1, ltns.Length);
            Assert.AreEqual(1, stubs.Length);
            Assert.AreEqual("110", speeds[0].SystemId);
            Assert.AreEqual("180", ltns[0].SystemId);
            Assert.AreEqual("160", stubs[0].SystemId);

            built.Player.Tick(16f);
            Assert.Greater(built.Player.TimeMs, 0f);
            Assert.IsNotNull(speeds[0].GetComponent<MeshFilter>().sharedMesh);
            Assert.IsNotNull(ltns[0].GetComponent<MeshFilter>().sharedMesh);
        }

        [Test]
        public void SmoothstepMatchesEdges()
        {
            Assert.AreEqual(0f, GfxSpeedTrailRibbon.Smoothstep(0f, 1f, -1f), 0.001f);
            Assert.AreEqual(1f, GfxSpeedTrailRibbon.Smoothstep(0f, 1f, 2f), 0.001f);
            Assert.AreEqual(0.5f, GfxSpeedTrailRibbon.Smoothstep(0f, 1f, 0.5f), 0.05f);
        }
    }
}
