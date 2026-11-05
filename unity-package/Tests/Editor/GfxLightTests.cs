using System.Collections.Generic;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxLightTests
    {
        private const string PointGfx =
            "MOXTVersion: 92\n" +
            "DedaultScale: 1\n" +
            "2DRender: 0\n" +
            "GFXELEMENTID: 130 PointLamp\n" +
            "Name: PointLamp\n" +
            "LightType: 1\n" +
            "Diffuse: -1\n" +
            "Specular: 0\n" +
            "Ambient: 0\n" +
            "Range: 8\n" +
            "FallOff: 1\n" +
            "Attenuation0: 0\n" +
            "Attenuation0: 0.1\n" +
            "Attenuation0: 0\n" +
            "Theta: 40\n" +
            "Phi: 90\n" +
            "InnerUse: 0\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 2000\n" +
            "Position: 1, 2, 3\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n";

        private const string SpotInnerGfx =
            "MOXTVersion: 92\n" +
            "GFXELEMENTID: 130 SpotInner\n" +
            "Name: SpotInner\n" +
            "LightType: 2\n" +
            "Range: 12\n" +
            "FallOff: 0\n" +
            "Attenuation0: 1\n" +
            "Attenuation0: 0\n" +
            "Attenuation0: 0\n" +
            "Phi: 60\n" +
            "Theta: 20\n" +
            "InnerUse: 1\n" +
            "Specular: -16777216\n" +
            "StartTime: 100\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 1000\n" +
            "Position: 0, 1, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n";

        private const string DirectionalGfx =
            "MOXTVersion: 92\n" +
            "GFXELEMENTID: 130 DirSun\n" +
            "Name: DirSun\n" +
            "LightType: 3\n" +
            "Range: 100\n" +
            "Attenuation0: 0\n" +
            "Attenuation0: 0.1\n" +
            "Attenuation0: 0\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 500\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n";

        [Test]
        public void ParamsParsePointAttenuationAndFit()
        {
            var doc = new GfxParser().Parse(PointGfx);
            Assert.AreEqual(1, doc.Elements.Count);
            var p = GfxLightParams.FromProperties(doc.Elements[0].Properties, "130", 0);
            Assert.AreEqual(GfxLightParams.LightPoint, p.LightType);
            Assert.AreEqual(8f, p.Range, 0.001f);
            Assert.AreEqual(0f, p.Attenuation0, 0.001f);
            Assert.AreEqual(0.1f, p.Attenuation1, 0.001f);
            Assert.AreEqual(0f, p.Attenuation2, 0.001f);
            Assert.IsFalse(p.InnerUse);
            Assert.IsTrue(p.UseAlphaEnergy);
            Assert.AreEqual(45f, p.SpotAngleDeg, 0.001f); // Phi*0.5 even for point (stored)

            var fit = GfxLightParams.FitAttenuation(8f, 0f, 0.1f, 0f);
            Assert.AreEqual(fit.Decay, p.Decay, 0.0001f);
            Assert.AreEqual(fit.Energy, p.EnergyBase, 0.0001f);
            // d=4, denom=0.4, decay=(0.1*4)/0.4=1, energy=4^1/0.4=10
            Assert.AreEqual(1f, fit.Decay, 0.001f);
            Assert.AreEqual(10f, fit.Energy, 0.001f);
        }

        [Test]
        public void ParamsInnerUseOverridesAttenuationAndDisablesAlphaEnergy()
        {
            var doc = new GfxParser().Parse(SpotInnerGfx);
            var p = GfxLightParams.FromProperties(doc.Elements[0].Properties, "130", 0);
            Assert.AreEqual(GfxLightParams.LightSpot, p.LightType);
            Assert.IsTrue(p.InnerUse);
            Assert.IsFalse(p.UseAlphaEnergy);
            Assert.AreEqual(30f, p.SpotAngleDeg, 0.001f); // Phi 60 / 2
            Assert.AreEqual(64f, p.SpotAngleAttenuation, 0.001f); // FallOff 0
            // att forced to (0, 6/12, 0) = (0, 0.5, 0); d=6, denom=3, decay=1, energy=2
            var expected = GfxLightParams.FitAttenuation(12f, 0f, 0.5f, 0f);
            Assert.AreEqual(expected.Decay, p.Decay, 0.001f);
            Assert.AreEqual(expected.Energy, p.EnergyBase, 0.001f);
        }

        [Test]
        public void ParamsDirectionalEnergyBaseIsOne()
        {
            var doc = new GfxParser().Parse(DirectionalGfx);
            var p = GfxLightParams.FromProperties(doc.Elements[0].Properties, "130", 0);
            Assert.AreEqual(GfxLightParams.LightDirectional, p.LightType);
            Assert.AreEqual(1f, p.EnergyBase, 0.001f);
        }

        [Test]
        public void SpotAttenuationMatchesGodot()
        {
            Assert.AreEqual(64f, GfxLightParams.SpotAttenuation(0f), 0.001f);
            Assert.AreEqual(64f, GfxLightParams.SpotAttenuation(0.0005f), 0.001f);
            var v = GfxLightParams.SpotAttenuation(1f);
            Assert.Greater(v, 0.01f);
            Assert.Less(v, 64f);
        }

        [Test]
        public void RegistryMarks130Ported()
        {
            Assert.IsTrue(GfxElementRegistry.IsPorted("130"));
            Assert.IsTrue(GfxElementRegistry.IsPorted("140"));
            Assert.IsTrue(GfxElementRegistry.IsPorted("190"));
            Assert.IsTrue(GfxElementRegistry.IsPorted("150"));
            Assert.IsTrue(GfxElementRegistry.IsPorted("151"));
            Assert.IsTrue(GfxElementRegistry.IsPorted("152"));
        }

        [Test]
        public void EvalInactiveOutsideLocalWindow()
        {
            var p = new GfxLightParams
            {
                LightType = GfxLightParams.LightPoint,
                EnergyBase = 2f,
                UseAlphaEnergy = true
            };
            var kps = new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec
                {
                    TimeSpan = 1000f,
                    Pos = GfxMath.Vec3(1f, 0f, 0f),
                    Color = GfxMath.Color(1f, 0f, 0f, 0.5f),
                    Dir = GfxMath.QuatIdentity()
                }
            };
            var off = GfxLightEval.Evaluate(p, kps, 50f, 100f, 1100f, false);
            Assert.IsFalse(off.Active);

            var on = GfxLightEval.Evaluate(p, kps, 200f, 100f, 1100f, false);
            Assert.IsTrue(on.Active);
            Assert.AreEqual(1f, on.Pos[0], 0.001f);
            Assert.AreEqual(1f, on.ColorRgb[0], 0.001f);
            // EnergyBase * alpha * 2 = 2 * 0.5 * 2 = 2
            Assert.AreEqual(2f, on.Intensity, 0.001f);
        }

        [Test]
        public void EvalInnerUseIgnoresAlphaForIntensity()
        {
            var p = new GfxLightParams
            {
                LightType = GfxLightParams.LightPoint,
                EnergyBase = 3f,
                UseAlphaEnergy = false
            };
            var kps = new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec
                {
                    TimeSpan = 500f,
                    Pos = GfxMath.Vec3Zero(),
                    Color = GfxMath.Color(1f, 1f, 1f, 0.25f),
                    Dir = GfxMath.QuatIdentity()
                }
            };
            var frame = GfxLightEval.Evaluate(p, kps, 10f, 0f, 500f, false);
            Assert.IsTrue(frame.Active);
            Assert.AreEqual(3f, frame.Intensity, 0.001f);
        }

        [Test]
        public void EvalSpotSetsForwardFromKeypointDirection()
        {
            var p = new GfxLightParams
            {
                LightType = GfxLightParams.LightSpot,
                EnergyBase = 1f,
                UseAlphaEnergy = true
            };
            var kps = new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec
                {
                    TimeSpan = 1000f,
                    Pos = GfxMath.Vec3Zero(),
                    Color = GfxMath.ColorWhite(),
                    Dir = GfxMath.QuatIdentity()
                }
            };
            var frame = GfxLightEval.Evaluate(p, kps, 0f, 0f, 1000f, false);
            Assert.IsTrue(frame.Active);
            Assert.AreEqual(0f, frame.Forward[0], 0.001f);
            Assert.AreEqual(0f, frame.Forward[1], 0.001f);
            Assert.AreEqual(1f, frame.Forward[2], 0.001f);
        }

        [Test]
        public void SceneBuilderAttachesLightDriver()
        {
            var doc = new GfxParser().Parse(PointGfx + SpotInnerGfx + DirectionalGfx);
            var result = GfxSceneBuilder.Build(doc, "LightsGfx", "lights.gfx");
            Assert.IsNotNull(result.Root);
            Assert.IsNotNull(result.Player);

            var drivers = result.Root.GetComponentsInChildren<GfxLightDriver>(true);
            Assert.AreEqual(3, drivers.Length);

            GfxLightDriver point = null, spot = null, dir = null;
            foreach (var d in drivers)
            {
                if (d.Params.LightType == GfxLightParams.LightPoint) point = d;
                else if (d.Params.LightType == GfxLightParams.LightSpot) spot = d;
                else if (d.Params.LightType == GfxLightParams.LightDirectional) dir = d;
            }

            Assert.IsNotNull(point);
            Assert.IsNotNull(spot);
            Assert.IsNotNull(dir);
            Assert.IsNotNull(point.UnityLight);
            Assert.AreEqual(LightType.Point, point.UnityLight.type);
            Assert.AreEqual(LightType.Spot, spot.UnityLight.type);
            Assert.AreEqual(LightType.Directional, dir.UnityLight.type);
            Assert.AreEqual(8f, point.UnityLight.range, 0.001f);
            Assert.AreEqual(30f, spot.UnityLight.spotAngle, 0.001f);
            Assert.AreEqual(LightShadows.None, point.UnityLight.shadows);

            // Before LocalStartTime → disabled (Godot visible=false).
            point.TimeManual = true;
            point.GlobalLoopTime = 10000f;
            point.LocalStartTime = 500f;
            point.LocalTotalTime = 2500f;
            point.RepeatCount = 1;
            point.RepeatDelay = 0f;
            point.IsEndless = false;
            point.OnGfxTick(100f, 1f);
            Assert.IsFalse(point.UnityLight.enabled);

            // Inside window: white Color:-1 → a=1 → intensity = EnergyBase * 2
            point.OnGfxTick(600f, 1f);
            Assert.IsTrue(point.UnityLight.enabled);
            Assert.AreEqual(1f, point.transform.localPosition.x, 0.001f);
            Assert.AreEqual(2f, point.transform.localPosition.y, 0.001f);
            Assert.AreEqual(3f, point.transform.localPosition.z, 0.001f);
            Assert.AreEqual(point.Params.EnergyBase * 2f, point.UnityLight.intensity, 0.001f);

            var stubbed = result.Root.GetComponentsInChildren<GfxElementStub>(true);
            Assert.AreEqual(0, stubbed.Length);
        }

        [Test]
        public void PlannerMarks130PortedNotStub()
        {
            var doc = new GfxParser().Parse(PointGfx);
            var plan = GfxRuntimePlanner.Build(doc, "t.gfx");
            Assert.AreEqual(1, plan.Elements.Count);
            Assert.IsTrue(GfxElementRegistry.IsPorted(plan.Elements[0].SystemId));
        }
    }
}
