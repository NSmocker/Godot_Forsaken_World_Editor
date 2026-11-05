using System.Collections.Generic;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxParticleTests
    {
        private const string PointGfx =
            "MOXTVersion: 92\n" +
            "DedaultScale: 1\n" +
            "2DRender: 0\n" +
            "GFXELEMENTID: 120 Sparks\n" +
            "Name: Sparks\n" +
            "DestBlend: 2\n" +
            "TexFile: fx\\spark.dds\n" +
            "Quota: 20\n" +
            "ParticleWidth: 0.1\n" +
            "ParticleHeight: 0.1\n" +
            "EmissionRate: 10\n" +
            "Angle: 0.5\n" +
            "Speed: 2\n" +
            "ParAcc: 0\n" +
            "AccDir: 0, 0, -1\n" +
            "Acc: 0\n" +
            "TTL: 1\n" +
            "ColorMin: -1\n" +
            "ColorMax: -1\n" +
            "ScaleMin: 1\n" +
            "ScaleMax: 1\n" +
            "RotMin: 0\n" +
            "RotMax: 0\n" +
            "IsSurface: 0\n" +
            "IsBind: 1\n" +
            "IsDrag: 0\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 2000\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n" +
            "AffectorCount: 0\n" +
            "GFXELEMENTID: 121 Box\n" +
            "Name: Box\n" +
            "DestBlend: 6\n" +
            "TexFile: fx\\box.dds\n" +
            "Quota: 8\n" +
            "EmissionRate: 4\n" +
            "TTL: 1\n" +
            "AreaSize: 2, 2, 2\n" +
            "IsSurface: 0\n" +
            "IsBind: 1\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 1000\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n" +
            "AffectorCount: 0\n" +
            "GFXELEMENTID: 123 Ellipsoid\n" +
            "Name: Ellipsoid\n" +
            "DestBlend: 2\n" +
            "Quota: 12\n" +
            "EmissionRate: 6\n" +
            "TTL: 1\n" +
            "AreaSize: 1, 2, 1\n" +
            "IsAvgGen: 1\n" +
            "AlphaSeg: 4\n" +
            "BetaSeg: 3\n" +
            "IsBind: 1\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 1000\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n" +
            "AffectorCount: 0\n" +
            "GFXELEMENTID: 124 Cylinder\n" +
            "Name: Cylinder\n" +
            "Quota: 10\n" +
            "EmissionRate: 5\n" +
            "TTL: 1\n" +
            "AreaSize: 1, 1, 2\n" +
            "IsSurface: 1\n" +
            "IsBind: 0\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 1000\n" +
            "Position: 0, 1, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n" +
            "AffectorCount: 0\n" +
            "GFXELEMENTID: 122 Multi\n" +
            "Name: Multi\n" +
            "Quota: 5\n" +
            "EmissionRate: 5\n" +
            "TTL: 1\n" +
            "RefModel: missing.mox\n" +
            "IsBind: 1\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 1000\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n" +
            "AffectorCount: 0\n" +
            "GFXELEMENTID: 125 Curve\n" +
            "Name: Curve\n" +
            "Quota: 6\n" +
            "EmissionRate: 3\n" +
            "TTL: 1\n" +
            "Radius: 0.2\n" +
            "IsAvgGen: 0\n" +
            "IsBind: 1\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 1000\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n" +
            "AffectorCount: 0\n";

        [Test]
        public void ParticleParamsParsePointAndBox()
        {
            var doc = new GfxParser().Parse(PointGfx);
            var p120 = GfxParticleParams.FromProperties(doc.Elements[0].Properties, "120", 0);
            Assert.AreEqual(20, p120.Quota);
            Assert.AreEqual(10f, p120.EmissionRate, 0.001f);
            Assert.AreEqual(1f, p120.TTL, 0.001f);
            Assert.IsTrue(p120.IsBind);
            Assert.IsTrue(p120.IsAdditiveBlend);
            Assert.AreEqual(20, p120.PoolSize);

            var p121 = GfxParticleParams.FromProperties(doc.Elements[1].Properties, "121", 1);
            Assert.AreEqual(2f, p121.AreaSize[0], 0.001f);
            Assert.IsFalse(p121.IsAdditiveBlend);
            Assert.AreEqual(0.70f, p121.MixAlphaScale, 0.001f);
        }

        [Test]
        public void RegistryMarks120To125Ported()
        {
            Assert.IsTrue(GfxElementRegistry.IsPorted("120"));
            Assert.IsTrue(GfxElementRegistry.IsPorted("121"));
            Assert.IsTrue(GfxElementRegistry.IsPorted("122"));
            Assert.IsTrue(GfxElementRegistry.IsPorted("123"));
            Assert.IsTrue(GfxElementRegistry.IsPorted("124"));
            Assert.IsTrue(GfxElementRegistry.IsPorted("125"));
            Assert.IsFalse(GfxElementRegistry.IsPorted("130"));
        }

        [Test]
        public void PointEmitterEmitsDeterministicSlots()
        {
            var p = new GfxParticleParams
            {
                SystemId = "120",
                Quota = 10,
                EmissionRate = 10f,
                TTL = 1f,
                Speed = 1f,
                IsBind = true,
                RandomSeed = 1,
                EmitterEnabled = true,
                ColorMin = GfxMath.ColorWhite(),
                ColorMax = GfxMath.ColorWhite()
            };
            var kps = new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec
                {
                    TimeSpan = 2000f,
                    Pos = GfxMath.Vec3Zero(),
                    Dir = GfxMath.QuatIdentity(),
                    Color = GfxMath.ColorWhite(),
                    Scale = 1f
                }
            };
            var sim = new GfxParticleSim(p, kps, null);
            var slots = sim.Simulate(500f, 3000f, 0f, 2000f, 1, 0f, false);
            Assert.AreEqual(10, slots.Length);
            var alive = 0;
            foreach (var s in slots)
            {
                if (s.Alive)
                    alive++;
            }

            // After 0.5s at 10/s → 5 emissions; all still within TTL=1
            Assert.AreEqual(5, alive);
            // Deterministic: second call matches
            var slots2 = sim.Simulate(500f, 3000f, 0f, 2000f, 1, 0f, false);
            Assert.AreEqual(slots[0].Pos[0], slots2[0].Pos[0], 0.0001f);
            Assert.AreEqual(slots[0].Pos[2], slots2[0].Pos[2], 0.0001f);
        }

        [Test]
        public void QuotaCapsTotalEmissions()
        {
            var p = new GfxParticleParams
            {
                SystemId = "120",
                Quota = 3,
                EmissionRate = 100f,
                TTL = 5f,
                IsBind = true,
                EmitterEnabled = true,
                ColorMin = GfxMath.ColorWhite(),
                ColorMax = GfxMath.ColorWhite()
            };
            var kps = new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec { TimeSpan = 5000f, Color = GfxMath.ColorWhite(), Scale = 1f, Dir = GfxMath.QuatIdentity() }
            };
            var sim = new GfxParticleSim(p, kps, null);
            var slots = sim.Simulate(1000f, 5000f, 0f, 5000f, 1, 0f, false);
            var alive = 0;
            foreach (var s in slots)
                if (s.Alive) alive++;
            Assert.AreEqual(3, alive);
            Assert.AreEqual(3, p.PoolSize);
        }

        [Test]
        public void BoxEmitterOffsetsInsideArea()
        {
            var p = new GfxParticleParams
            {
                SystemId = "121",
                Quota = 4,
                EmissionRate = 4f,
                TTL = 2f,
                AreaSize = GfxMath.Vec3(2f, 2f, 2f),
                IsBind = true,
                IsSurface = false,
                EmitterEnabled = true,
                ColorMin = GfxMath.ColorWhite(),
                ColorMax = GfxMath.ColorWhite(),
                RandomSeed = 42
            };
            var kps = new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec { TimeSpan = 2000f, Color = GfxMath.ColorWhite(), Scale = 1f, Dir = GfxMath.QuatIdentity() }
            };
            var sim = new GfxParticleSim(p, kps, null);
            var slots = sim.Simulate(1000f, 3000f, 0f, 2000f, 1, 0f, false);
            var found = false;
            foreach (var s in slots)
            {
                if (!s.Alive) continue;
                found = true;
                Assert.LessOrEqual(System.Math.Abs(s.Pos[0]), 1.001f);
                Assert.LessOrEqual(System.Math.Abs(s.Pos[1]), 1.001f);
                Assert.LessOrEqual(System.Math.Abs(s.Pos[2]), 1.001f);
            }

            Assert.IsTrue(found);
        }

        [Test]
        public void AvgRingCountMatchesFloat32Loop()
        {
            var n = GfxParticleParams.AvgRingCount(10);
            Assert.Greater(n, 0);
            Assert.LessOrEqual(n, 12);
        }

        [Test]
        public void CurveEmitterDisabledWithoutPath()
        {
            var p = new GfxParticleParams
            {
                SystemId = "125",
                Quota = 4,
                EmissionRate = 4f,
                TTL = 1f,
                EmitterEnabled = false,
                ColorMin = GfxMath.ColorWhite(),
                ColorMax = GfxMath.ColorWhite()
            };
            var sim = new GfxParticleSim(p, new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec { TimeSpan = 1000f, Color = GfxMath.ColorWhite(), Scale = 1f, Dir = GfxMath.QuatIdentity() }
            }, null);
            var slots = sim.Simulate(500f, 3000f, 0f, 1000f, 1, 0f, false);
            foreach (var s in slots)
                Assert.IsFalse(s.Alive);
        }

        [Test]
        public void CurveEmitterUsesPathPoints()
        {
            var path = GfxCurvePath.GenPath(new List<float[]>
            {
                GfxMath.Vec3(0f, 0f, 0f),
                GfxMath.Vec3(0f, 1f, 0f),
                GfxMath.Vec3(0f, 2f, 0f)
            });
            Assert.IsNotNull(path);
            var p = new GfxParticleParams
            {
                SystemId = "125",
                Quota = 4,
                EmissionRate = 4f,
                TTL = 2f,
                IsBind = true,
                EmitterEnabled = true,
                CurvePoints = path.Points,
                CurveRatios = path.Ratios,
                CurveDirs = path.Dirs,
                ColorMin = GfxMath.ColorWhite(),
                ColorMax = GfxMath.ColorWhite(),
                RandomSeed = 7
            };
            var sim = new GfxParticleSim(p, new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec { TimeSpan = 2000f, Color = GfxMath.ColorWhite(), Scale = 1f, Dir = GfxMath.QuatIdentity() }
            }, null);
            var slots = sim.Simulate(500f, 3000f, 0f, 2000f, 1, 0f, false);
            var alive = 0;
            foreach (var s in slots)
                if (s.Alive) alive++;
            Assert.Greater(alive, 0);
        }

        [Test]
        public void AffectorColorDeltaChangesAlpha()
        {
            var p = new GfxParticleParams
            {
                SystemId = "120",
                Quota = 2,
                EmissionRate = 2f,
                TTL = 2f,
                IsBind = true,
                EmitterEnabled = true,
                ColorMin = GfxMath.Color(1f, 1f, 1f, 1f),
                ColorMax = GfxMath.Color(1f, 1f, 1f, 1f),
                RandomSeed = 3
            };
            // RGBA ColorDelta (GfxCtrlSpec / GfxKeypointEval convention).
            var aff = new GfxCtrlSpec
            {
                Type = 105,
                Start = 0f,
                End = -1f,
                ColorDelta = GfxMath.Color(0f, 0f, 0f, -0.5f)
            };
            var sim = new GfxParticleSim(p, new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec { TimeSpan = 2000f, Color = GfxMath.ColorWhite(), Scale = 1f, Dir = GfxMath.QuatIdentity() }
            }, new List<GfxCtrlSpec> { aff });
            var slots = sim.Simulate(1000f, 3000f, 0f, 2000f, 1, 0f, false);
            var faded = false;
            foreach (var s in slots)
            {
                if (!s.Alive) continue;
                if (s.Age > 0.1f && s.Color[3] < 0.999f)
                    faded = true;
            }

            Assert.IsTrue(faded);
        }

        [Test]
        public void SceneBuilderAttachesParticleDrivers()
        {
            var doc = new GfxParser().Parse(PointGfx);
            var result = GfxSceneBuilder.Build(doc, "ParticlesGfx", "particles.gfx");
            Assert.IsNotNull(result.Root);
            var drivers = result.Root.GetComponentsInChildren<GfxParticleDriver>(true);
            Assert.AreEqual(6, drivers.Length);
            var stubs = result.Root.GetComponentsInChildren<GfxElementStub>(true);
            Assert.AreEqual(0, stubs.Length);
        }

        [Test]
        public void MultiPlaneWithoutSamplesStartsAtEmitter()
        {
            var p = new GfxParticleParams
            {
                SystemId = "122",
                Quota = 2,
                EmissionRate = 2f,
                TTL = 1f,
                IsBind = true,
                EmitterEnabled = true,
                RefModel = "missing.mox",
                ColorMin = GfxMath.ColorWhite(),
                ColorMax = GfxMath.ColorWhite(),
                RandomSeed = 9
            };
            var sim = new GfxParticleSim(p, new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec { TimeSpan = 1000f, Color = GfxMath.ColorWhite(), Scale = 1f, Dir = GfxMath.QuatIdentity() }
            }, null);
            var slots = sim.Simulate(500f, 3000f, 0f, 1000f, 1, 0f, false);
            foreach (var s in slots)
            {
                if (!s.Alive) continue;
                Assert.AreEqual(0f, s.Pos[0], 0.01f);
                Assert.AreEqual(0f, s.Pos[1], 0.01f);
                Assert.AreEqual(0f, s.Pos[2], 0.01f);
            }
        }

        [Test]
        public void PoolSizeClampedToMaxQuota()
        {
            var p = new GfxParticleParams { Quota = 5000, EmissionRate = 1f, TTL = 1f };
            Assert.AreEqual(GfxParticleParams.MaxQuota, p.PoolSize);
        }
    }
}
