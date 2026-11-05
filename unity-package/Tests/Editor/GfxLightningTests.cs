using System;
using System.Collections.Generic;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxLightningTests
    {
        private const string StraightGfx =
            "MOXTVersion: 92\n" +
            "DedaultScale: 1\n" +
            "2DRender: 0\n" +
            "GFXELEMENTID: 150 Bolt\n" +
            "Name: Bolt\n" +
            "DestBlend: 2\n" +
            "TexFile: fx\\ltn.dds\n" +
            "ZEnable: 1\n" +
            "BufLen: 256\n" +
            "Amplitude: 1\n" +
            "WaveLen: 1\n" +
            "Persistence: 0.5\n" +
            "OctaveNum: 2\n" +
            "StartPos: 0, 0, 0\n" +
            "EndPos: 0, 10, 0\n" +
            "Segs: 4\n" +
            "LightNum: 1\n" +
            "WaveLen_2: 2\n" +
            "Interval: 100\n" +
            "Width: 0.5\n" +
            "Width_2: 0.1\n" +
            "Width_3: 0.3\n" +
            "Alpha: 1\n" +
            "Alpha_2: 0.2\n" +
            "Alpha_3: 0.6\n" +
            "Amplitude_2: 0.4\n" +
            "Filter: 1\n" +
            "WaveMoving: 0\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 2000\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n";

        private const string ZigzagGfx =
            "MOXTVersion: 92\n" +
            "GFXELEMENTID: 150 Zig\n" +
            "Name: Zig\n" +
            "DestBlend: 6\n" +
            "StartPos: 0, 0, 0\n" +
            "EndPos: 10, 0, 0\n" +
            "Segs: 3\n" +
            "LightNum: 2\n" +
            "WaveLen_2: 1\n" +
            "Interval: 50\n" +
            "Width: 1\n" +
            "Width_2: 1\n" +
            "Alpha: 1\n" +
            "Alpha_2: 1\n" +
            "Amplitude_2: 0\n" +
            "Filter: 1\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 1000\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 2\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n";

        private const string ExGfx =
            "MOXTVersion: 92\n" +
            "GFXELEMENTID: 152 Ex\n" +
            "Name: Ex\n" +
            "DestBlend: 2\n" +
            "StartPos: 0, 0, 0\n" +
            "EndPos: 0, 5, 0\n" +
            "Segs: 4\n" +
            "LightNum: 1\n" +
            "WaveLen_2: 1\n" +
            "Interval: 100\n" +
            "Width: 0.2\n" +
            "Width_2: 0.2\n" +
            "Alpha: 1\n" +
            "Alpha_2: 1\n" +
            "Amplitude_2: 0.1\n" +
            "Filter: 2\n" +
            "isappend: 1\n" +
            "renderside: 1\n" +
            "istaildisappear: 1\n" +
            "vertslife: 500\n" +
            "tailfadeout: 1\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 2000\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n";

        private const string BoltGfx =
            "MOXTVersion: 92\n" +
            "GFXELEMENTID: 151 Star\n" +
            "Name: Star\n" +
            "DestBlend: 2\n" +
            "TexFile: fx\\bolt.dds\n" +
            "Deviation: 0.523599\n" +
            "StepMin: 5\n" +
            "StepMax: 6\n" +
            "WidthStart: 0.15\n" +
            "WidthEnd: 0.03\n" +
            "AlphaStart: 1\n" +
            "AlphaEnd: 0\n" +
            "StrokeAmp: 0.4\n" +
            "MaxSteps: 2\n" +
            "MaxBranches: 1\n" +
            "Interval: 100\n" +
            "PerBolts: 2\n" +
            "Circles: 1\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 1000\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n";

        private const string DestNumGfx =
            "MOXTVersion: 102\n" +
            "GFXELEMENTID: 150 Amp\n" +
            "Name: Amp\n" +
            "DestBlend: 2\n" +
            "StartPos: 0, 0, 0\n" +
            "EndPos: 0, 1, 0\n" +
            "Segs: 2\n" +
            "LightNum: 1\n" +
            "WaveLen_2: 1\n" +
            "Interval: 100\n" +
            "Width: 0.1\n" +
            "Width_2: 0.1\n" +
            "Alpha: 1\n" +
            "Alpha_2: 1\n" +
            "DestNum: 1\n" +
            "DestVal: 0.1\n" +
            "DestVal_2: 0.9\n" +
            "TransTime: 500\n" +
            "StartTime: 100\n" +
            "StartTime_2: 200\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 1000\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n";

        [Test]
        public void ParamsParseStraightLightning()
        {
            var doc = new GfxParser().Parse(StraightGfx);
            var p = GfxLightningParams.FromProperties(doc.Elements[0].Properties, "150", 0);
            Assert.AreEqual("150", p.SystemId);
            Assert.AreEqual(4, p.Segs);
            Assert.AreEqual(1, p.LightNum);
            Assert.IsTrue(p.IsValid);
            Assert.AreEqual(5, p.PairCount); // 4*1+1
            Assert.AreEqual(0.5f, p.Width, 0.001f);
            Assert.AreEqual(0.3f, p.EffectiveWidth3, 0.001f);
            Assert.IsTrue(p.IsAdditiveBlend);
            Assert.AreEqual(0.4f, p.Amplitude2, 0.001f);
            Assert.AreEqual(10f, p.EndPos[1], 0.001f);
        }

        [Test]
        public void ParamsParseExFlags()
        {
            var doc = new GfxParser().Parse(ExGfx);
            var p = GfxLightningParams.FromProperties(doc.Elements[0].Properties, "152", 0);
            Assert.IsTrue(p.IsEx);
            Assert.IsTrue(p.IsAppend);
            Assert.AreEqual(1, p.RenderSide);
            Assert.IsTrue(p.IsTailDisappear);
            Assert.AreEqual(500f, p.VertsLife, 0.001f);
            Assert.IsTrue(p.TailFadeOut);
            Assert.AreEqual(2, p.Filter);
        }

        [Test]
        public void DestNumUsesStartTime2AndAmpCurve()
        {
            var doc = new GfxParser().Parse(DestNumGfx);
            var p = GfxLightningParams.FromProperties(doc.Elements[0].Properties, "150", 0);
            Assert.IsTrue(p.HasAmpCurve);
            Assert.AreEqual(1, p.AmpDestNum);
            Assert.AreEqual(100f, p.AmpStartTime, 0.001f);
            Assert.AreEqual(200f, p.ElementStartTime, 0.001f);
            Assert.AreEqual(0.1f, p.AmpValues[0], 0.001f);
            Assert.AreEqual(0.9f, p.AmpValues[1], 0.001f);
            Assert.AreEqual(0.5f, GfxLightningEval.LtnAmplitude(p, 350f), 0.05f); // mid of 100..600

            var plan = GfxRuntimePlanner.Build(doc);
            Assert.AreEqual(200f, plan.Elements[0].LocalStartTime, 0.001f);
        }

        [Test]
        public void InvalidWhenSegsTooLow()
        {
            var p = new GfxLightningParams { Segs = 1, LightNum = 1 };
            Assert.IsFalse(p.IsValid);
            var mesh = GfxLightningEval.Build(
                p,
                new GfxKeypointState { Scale = 1f, Color = GfxMath.ColorWhite(), Dir = GfxMath.QuatIdentity(), Pos = GfxMath.Vec3Zero() },
                0f, 0f, 1f, null);
            Assert.AreEqual(1, mesh.PairCount);
        }

        [Test]
        public void StraightMeshPairCountAndEndpoints()
        {
            var doc = new GfxParser().Parse(ZigzagGfx);
            var p = GfxLightningParams.FromProperties(doc.Elements[0].Properties, "150", 0);
            p.RandomSeed = 1;
            p.Amplitude2 = 0f; // no noise offset
            var kp = new GfxKeypointState
            {
                Pos = GfxMath.Vec3Zero(),
                Dir = GfxMath.QuatIdentity(),
                Color = GfxMath.ColorWhite(),
                Scale = 2f
            };
            var mesh = GfxLightningEval.Build(p, kp, 0f, 0f, 1f, GfxMath.Vec3(0f, 0f, -10f));
            Assert.AreEqual(7, mesh.PairCount); // 3*2+1
            Assert.AreEqual(14, mesh.Positions.Length / 3);
            // first pair centered on StartPos (0,0,0); width*scale=2 → side verts ±view_up
            var mx = (mesh.Positions[0] + mesh.Positions[3]) * 0.5f;
            var my = (mesh.Positions[1] + mesh.Positions[4]) * 0.5f;
            var mz = (mesh.Positions[2] + mesh.Positions[5]) * 0.5f;
            Assert.AreEqual(0f, mx, 0.05f);
            Assert.AreEqual(0f, my, 0.05f);
            Assert.AreEqual(0f, mz, 0.05f);
            var dx = mesh.Positions[0] - mesh.Positions[3];
            var dy = mesh.Positions[1] - mesh.Positions[4];
            var dz = mesh.Positions[2] - mesh.Positions[5];
            var widthDist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            Assert.AreEqual(4.0, widthDist, 0.15); // 2 * width * scale
        }

        [Test]
        public void FilterShapesAndMid()
        {
            Assert.AreEqual(1f, GfxLightningEval.LtnFilter(1, 0.3f, 1f), 0.001f); // rect
            Assert.AreEqual(0.6f, GfxLightningEval.LtnFilter(0, 0.3f, 1f), 0.001f); // tri 2*0.3
            Assert.Greater(GfxLightningEval.LtnFilter(2, 0.5f, 1f), 0.9f); // sin peak
            Assert.AreEqual(0.5f, GfxLightningEval.LtnMid(0.25f, 0f, 1f, 2f), 0.001f);
            Assert.AreEqual(1.5f, GfxLightningEval.LtnMid(0.75f, 0f, 1f, 2f), 0.001f);
        }

        [Test]
        public void BoltStrokeDirsPerBoltsCircles()
        {
            // PerBolts=2 Circles=1 → nBolts=4 base only
            var dirs = GfxLtnBoltBuilder.StrokeDirs(2, 1);
            Assert.AreEqual(4, dirs.Count);
            // PerBolts=3 Circles=2 → 6 base + (circles-1)*(nBolts-2 skipped poles) = 6 + 1*(6-2? wait j!=perBolts skip one)
            // nBolts=6, for c in 1..1: j=1..5 skip j==3 → 4 dirs → total 6+4=10
            var d2 = GfxLtnBoltBuilder.StrokeDirs(3, 2);
            Assert.AreEqual(10, d2.Count);
            Assert.AreEqual(0, GfxLtnBoltBuilder.StrokeDirs(0, 1).Count);
        }

        [Test]
        public void BoltBuildStrokesDeterministic()
        {
            var doc = new GfxParser().Parse(BoltGfx);
            var bolt = GfxLtnBoltParams.FromProperties(doc.Elements[0].Properties, "151", 0);
            bolt.RandomSeed = 42;
            var a = GfxLtnBoltBuilder.BuildStrokes(bolt, 1f, 42);
            var b = GfxLtnBoltBuilder.BuildStrokes(bolt, 1f, 42);
            Assert.AreEqual(a.Count, b.Count);
            Assert.Greater(a.Count, 0);
            Assert.AreEqual(a[0].Start[0], b[0].Start[0], 1e-5f);
            Assert.AreEqual(a[0].End[1], b[0].End[1], 1e-5f);
            // MaxSteps=2, MaxBranches=1, PerBolts=2 Circles=1 → 4 dirs * 2 steps = 8 (no branch chance doubling always continues)
            Assert.AreEqual(8, a.Count);
            var lp = GfxLtnBoltBuilder.ToLightningParams(bolt, a[0], doc.Elements[0].Properties);
            Assert.AreEqual("150", lp.SystemId);
            Assert.AreEqual(8, lp.Segs);
            Assert.IsTrue(lp.HasDeviation);
            Assert.AreEqual(a[0].StepLen / 3f, lp.WaveLen2, 0.001f);
        }

        [Test]
        public void RegistryMarks150To152Ported()
        {
            GfxElementRegistry.ClearPortedForTests();
            Assert.IsTrue(GfxElementRegistry.IsPorted("150"));
            Assert.IsTrue(GfxElementRegistry.IsPorted("151"));
            Assert.IsTrue(GfxElementRegistry.IsPorted("152"));
        }

        [Test]
        public void SceneBuilderAttachesLightningAndBolt()
        {
            var text =
                "MOXTVersion: 92\nDedaultScale: 1\n2DRender: 0\n" +
                StraightGfx.Substring(StraightGfx.IndexOf("GFXELEMENTID", StringComparison.Ordinal)) +
                BoltGfx.Substring(BoltGfx.IndexOf("GFXELEMENTID", StringComparison.Ordinal)) +
                ExGfx.Substring(ExGfx.IndexOf("GFXELEMENTID", StringComparison.Ordinal));
            var doc = new GfxParser().Parse(text);
            var plan = GfxRuntimePlanner.Build(doc, "LtnTest");
            Assert.AreEqual(3, plan.Elements.Count);
            var result = GfxSceneBuilder.Build(plan, null);
            Assert.IsNotNull(result.Root);
            var ltns = result.Root.GetComponentsInChildren<GfxLightningDriver>(true);
            var bolts = result.Root.GetComponentsInChildren<GfxLtnBoltDriver>(true);
            Assert.AreEqual(1, bolts.Length);
            Assert.AreEqual(8, bolts[0].Strokes.Count);
            Assert.GreaterOrEqual(ltns.Length, 2 + 8);
        }

        [Test]
        public void WidthScaledByKeypoint()
        {
            var p = GfxLightningParams.FromProperties(
                new GfxParser().Parse(StraightGfx).Elements[0].Properties, "150", 0);
            p.Amplitude2 = 0f;
            p.RandomSeed = 0;
            var kp = new GfxKeypointState
            {
                Pos = GfxMath.Vec3Zero(),
                Dir = GfxMath.QuatIdentity(),
                Color = GfxMath.ColorWhite(),
                Scale = 3f
            };
            // Cam off the lightning Y-axis so cross(dir, pos-cam) is nonzero.
            var mesh = GfxLightningEval.Build(p, kp, 0f, 0f, 1f, GfxMath.Vec3(5f, 0f, 0f));
            // At start, width = 0.5 * 3 = 1.5; side ± view_up → distance 3
            var dx = mesh.Positions[0] - mesh.Positions[3];
            var dy = mesh.Positions[1] - mesh.Positions[4];
            var dz = mesh.Positions[2] - mesh.Positions[5];
            var dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            Assert.AreEqual(3.0, dist, 0.15);
        }
    }
}
