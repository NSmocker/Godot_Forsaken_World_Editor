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
        public void EmitterCtrlTimeUsesSecondsForCtrlStartEndLikeGodot()
        {
            // gfx_particle_eval._ctrl_time: kp base/dur = TimeSpan*0.001, StartTime/EndTime raw seconds.
            var spec = new GfxCtrlSpec { Start = 0.5f, End = -1f };
            var tr = GfxParticleSim.CtrlTime(spec, 2.0f, 1000f, 2000f);
            Assert.AreEqual(0.5f, tr.Item1, 1e-5f);
            Assert.AreEqual(1.5f, tr.Item2, 1e-5f);
            spec.End = 1.2f;
            tr = GfxParticleSim.CtrlTime(spec, 3.5f, 1000f, 2000f);
            Assert.AreEqual(0.7f, tr.Item1, 1e-5f);
            Assert.AreEqual(0.7f, tr.Item2, 1e-5f);
            spec.End = -1f;
            tr = GfxParticleSim.CtrlTime(spec, 9f, 0f, -1f);
            Assert.AreEqual(8.5f, tr.Item1, 1e-5f);
            Assert.AreEqual(0f, tr.Item2, 1e-5f);
        }

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
            // N03/N04 fix: pool = int(TTL*rate)+1 capped by quota (Godot _calc_pool_size),
            // not quota itself when quota is not the binding constraint.
            Assert.AreEqual(11, p120.PoolSize); // int(1*10)+1 = 11 < Quota 20

            var p121 = GfxParticleParams.FromProperties(doc.Elements[1].Properties, "121", 1);
            Assert.AreEqual(2f, p121.AreaSize[0], 0.001f);
            Assert.IsFalse(p121.IsAdditiveBlend);
            Assert.AreEqual(1f, p121.MixAlphaScale, 0.001f);
        }

        [Test]
        public void SavedParticleRenderSlotPreservesRawFactors()
        {
            var parameters = GfxParticleParamsSerial.FromParams(
                new GfxParticleParams { SrcBlend = 1, DestBlend = 4 }).ToParams();
            var material = GfxParticleMaterialFactory.Create("blend-slot", parameters.SrcBlend,
                parameters.DestBlend, true, false, Texture2D.whiteTexture);
            try
            {
                Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.Zero, material.GetFloat("_SrcBlend"));
                Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.OneMinusSrcColor, material.GetFloat("_DstBlend"));
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void NoiseWithUnitWavelengthUsesOnlyTheFirstOctave()
        {
            var spec = new GfxCtrlSpec { WaveLen = 1f, Amplitude = 2f, NoiseBufferLength = 16, Octaves = 5 };
            var noise = new GfxParticleSim.ParticleNoise(spec, 104729);
            spec.Octaves = 1;
            var single = new GfxParticleSim.ParticleNoise(spec, 104729);
            float value = noise.Sample(50f);
            Assert.Greater(value, 0f);
            Assert.LessOrEqual(value, 2f);
            Assert.AreEqual(single.Sample(50f), value);
            Assert.AreEqual(value, noise.Sample(66f));
        }

        [Test]
        public void PlaybackAgesBirthsInTheirFirstTickAndExpiresBeforeEmitting()
        {
            var p = new GfxParticleParams { EmitterEnabled = true, EmissionRate = 60f, TTL = 0.16f };
            var sim = new GfxParticleSim(p, new List<GfxKeypointSpec>(), null);
            var first = sim.Advance(50f, 50f, 0f, 0f, 0f, 1, 0f, true);
            Assert.AreEqual(3, first.Length);
            Assert.AreEqual(0.05f, first[0].Age, 0.00001f);
            sim.Advance(100f, 50f, 0f, 0f, 0f, 1, 0f, true);
            sim.Advance(150f, 50f, 0f, 0f, 0f, 1, 0f, true);
            var fourth = sim.Advance(200f, 50f, 0f, 0f, 0f, 1, 0f, true);
            Assert.AreEqual(9, fourth.Length);
            Assert.AreEqual(3f, fourth[0].EIdx);
            Assert.AreEqual(0.15f, fourth[0].Age, 0.00001f);
        }

        [Test]
        public void PlaybackIntegratesAffectorTravelFromTheAdvancedAge()
        {
            var parameters = new GfxParticleParams { EmissionRate = 8f, Quota = 1, TTL = 1f };
            var move = new GfxCtrlSpec
            {
                Type = 100,
                Start = 0.125f,
                End = 0.25f,
                Dir = GfxMath.Vec3(1f, 0f, 0f),
                Vel = 2f,
                Acc = 4f
            };
            var sim = new GfxParticleSim(parameters, null, new[] { move });

            float Position(float time, float elapsed) =>
                sim.Advance(time, elapsed, 0f, 0f, 0f, 1, 0f, true)[0].Pos[0];

            Assert.AreEqual(0.34375f, Position(125f, 125f), 0.000001f);
            Assert.AreEqual(0.5234375f, Position(187.5f, 62.5f), 0.000001f);
            Assert.AreEqual(0.71875f, Position(250f, 62.5f), 0.000001f);
            Assert.AreEqual(0.71875f, Position(312.5f, 62.5f), 0.000001f);

            sim.ResetTicks();
            Assert.AreEqual(0.34375f, Position(125f, 125f), 0.000001f);
        }

        [Test]
        public void VolumeEllipsoidsConsumeSharedWindowsBirthSamplesOnlyAtEmission()
        {
            var random = new GfxParticleSim.EmissionRandom(104729);
            GfxParticleSim Emitter(bool variedColor) => new GfxParticleSim(new GfxParticleParams
            {
                SystemId = "123",
                EmissionRate = 20f,
                Quota = 1,
                AreaSize = GfxMath.Vec3(2f, 2f, 2f),
                ScaleMin = 0f,
                RotMax = 1f,
                ColorMax = variedColor ? GfxMath.Color(0f, 0f, 0f, 0f) : GfxMath.ColorWhite()
            }, null, null);
            GfxParticleSlot Step(GfxParticleSim sim, float time) =>
                sim.Advance(time, 50f, 0f, 0f, 0f, 1, 0f, true, random)[0];

            var first = Emitter(false);
            var a = Step(first, 50f);
            Assert.AreEqual(0.43821528f, a.Size, 0.000001f);
            Assert.AreEqual(0.95327616f, a.BillboardAngle, 0.000001f);
            Assert.AreEqual(0.77562792f, a.Pos[0], 0.000001f);
            Assert.AreEqual(-0.51921140f, a.Pos[1], 0.000001f);
            Assert.AreEqual(-0.14358959f, a.Pos[2], 0.000001f);

            var b = Step(Emitter(true), 50f);
            Assert.AreEqual(0.24591815f, b.Size, 0.000001f);
            Assert.AreEqual(0.45933409f, b.BillboardAngle, 0.000001f);
            CollectionAssert.AreEqual(new[] { 131f / 255f, 29f / 255f, 205f / 255f, 100f / 255f }, b.Color);

            var older = Step(first, 100f);
            CollectionAssert.AreEqual(a.Pos, older.Pos);
            Assert.AreEqual(a.BillboardAngle, older.BillboardAngle);
            Assert.AreEqual(0.45204016f, Step(Emitter(false), 50f).Size, 0.000001f);
        }

        [Test]
        public void CylinderBirthConsumesTheSameCrtStreamAsTheNextEllipsoid()
        {
            var random = new GfxParticleSim.EmissionRandom(1);
            GfxParticleSlot Birth(string id) => new GfxParticleSim(new GfxParticleParams
            {
                SystemId = id, EmissionRate = 20f, Quota = 1, ScaleMin = 0f,
                AreaSize = GfxMath.Vec3(2f, 2f, 2f)
            }, null, null).Advance(50f, 50f, 0f, 0f, 0f, 1, 0f, true, random)[0];

            var cylinder = Birth("124");
            Assert.AreEqual(-0.61339152f, cylinder.Pos[0], 0.000001f);
            Assert.AreEqual(0.61748100f, cylinder.Pos[1], 0.000001f);
            Assert.AreEqual(0.17001862f, cylinder.Pos[2], 0.000001f);
            var ellipsoid = Birth("123");
            Assert.AreEqual(0.47987304f, ellipsoid.Size, 0.000001f);
            Assert.AreEqual(-0.24423963f, ellipsoid.Pos[0], 0.000001f);
            Assert.AreEqual(0.06332591f, ellipsoid.Pos[1], 0.000001f);
            Assert.AreEqual(0.14236885f, ellipsoid.Pos[2], 0.000001f);
        }

        [Test]
        public void PlaybackKeyColorUsesWholeTicksAndRetainsBirthAlpha()
        {
            var key = new GfxKeypointSpec { TimeSpan = -1f, Color = GfxMath.Color(1f, 1f, 1f, 79f / 255f) };
            key.Ctrls.Add(new GfxCtrlSpec { Type = 105, Start = 0.2f, ColorDelta = GfxMath.Color(0f, 0f, 0f, -2f) });
            var parameters = new GfxParticleParams
            {
                SystemId = "123", EmissionRate = 20f, ColorMax = GfxMath.Color(0f, 0f, 0f, 1f)
            };
            var random = new GfxParticleSim.EmissionRandom(1);
            var sim = new GfxParticleSim(parameters, new[] { key }, null);
            GfxParticleSlot[] Tick(float time, float step) =>
                sim.Advance(time, step, 0f, 0f, 0f, 1, 0f, true, random);
            Assert.AreEqual(79f / 255f, Tick(150f, 150f)[0].KeyAlpha);
            var threshold = Tick(200f, 50f);
            Assert.AreEqual(54f / 255f, threshold[0].KeyAlpha);
            Assert.AreEqual(79f / 255f, threshold[0].Color[3]);
            Assert.AreEqual(54f / 255f, threshold[3].Color[3]);
            var later = Tick(300f, 100f);
            Assert.AreEqual(3f / 255f, later[0].KeyAlpha);
            Assert.AreEqual(54f / 255f, later[3].Color[3]);
            sim.ResetTicks();
            Assert.AreEqual(79f / 255f, Tick(150f, 150f)[0].KeyAlpha);
        }

        [Test]
        public void ParticlePlaybackResetsPopulationAtRepeatAndAtFinalBoundary()
        {
            var key = new GfxKeypointSpec { TimeSpan = 100f };
            var parameters = new GfxParticleParams { EmissionRate = 100f, Quota = 1, TTL = 10f };
            var sim = new GfxParticleSim(parameters, new[] { key }, null);

            GfxParticleSlot[] Tick(float time, float step) =>
                sim.Advance(time, step, 0f, 0f, 100f, 2, 20f, false);

            Assert.AreEqual(1, Tick(50f, 50f).Length);
            Assert.IsEmpty(Tick(100f, 50f));
            Assert.IsFalse(sim.TickKeyActive);
            Assert.IsEmpty(Tick(119f, 19f));
            var resumed = Tick(120f, 1f);
            Assert.IsTrue(sim.TickKeyActive);
            Assert.IsEmpty(resumed);
            Assert.AreEqual(1, Tick(130f, 10f).Length);
            Assert.IsEmpty(Tick(219f, 89f));
            Assert.IsFalse(sim.TickKeyActive);
            Assert.IsEmpty(Tick(240f, 21f));
        }

        [Test]
        public void ParticleColorAffectorCarriesFractionalBytesBetweenParticles()
        {
            var affector = new GfxCtrlSpec
            {
                Type = 105, ColorDelta = GfxMath.Color(0f, 0f, 0f, -25f / 255f)
            };
            var parameters = new GfxParticleParams { EmissionRate = 80f, Quota = 4, TTL = 1f };
            var sim = new GfxParticleSim(parameters, null, new[] { affector });
            GfxParticleSlot[] Tick(float time) => sim.Advance(time, 50f, 0f, 0f, 0f, 1, 0f, true);

            var first = Tick(50f);
            Assert.AreEqual(4, first.Length);
            Assert.AreEqual(254f / 255f, first[0].Color[3]);
            Assert.AreEqual(254f / 255f, first[2].Color[3]);
            Assert.AreEqual(253f / 255f, first[3].Color[3]);
            var second = Tick(100f);
            Assert.AreEqual(253f / 255f, second[0].Color[3]);
            Assert.AreEqual(251f / 255f, second[3].Color[3]);
            Assert.AreEqual(254f / 255f, first[0].Color[3]);
            sim.ResetTicks();
            Assert.AreEqual(254f / 255f, Tick(50f)[0].Color[3]);
        }

        [Test]
        public void ConstantEmitterColorDoesNotPremultiplyBirthAlpha()
        {
            var p = new GfxParticleParams
            {
                EmitterEnabled = true,
                EmissionRate = 10f,
                TTL = 1f,
                ColorMin = GfxMath.Color(1f, 1f, 1f, 0.8f),
                ColorMax = GfxMath.Color(1f, 1f, 1f, 0.8f)
            };
            var keypoints = new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec { TimeSpan = -1f, Color = GfxMath.Color(1f, 1f, 1f, 0.5f) }
            };
            var slots = new GfxParticleSim(p, keypoints, null).Simulate(500f, 3000f, 0f, 3000f, 1, 0f, false);
            Assert.IsTrue(slots[0].Alive);
            Assert.AreEqual(0.8f, slots[0].Color[3], 0.001f);
            Assert.AreEqual(0.5f, slots[0].KeyAlpha, 0.001f);
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
            Assert.IsTrue(GfxElementRegistry.IsPorted("130"));
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
            // int(TTL*rate)+1 = 1001 must still clamp to MaxQuota (750) when quota
            // is present but non-binding, exactly like Godot clampi(pool, 0, MAX_QUOTA).
            var p = new GfxParticleParams { Quota = 5000, EmissionRate = 1000f, TTL = 1f };
            Assert.AreEqual(GfxParticleParams.MaxQuota, p.PoolSize);
        }

        [Test]
        public void ZeroQuotaSelectsEmptyPoolNotAutoSize()
        {
            // N03: Godot _calc_pool_size clamps the pool to Quota=0; only -1 is automatic.
            var zero = new GfxParticleParams { Quota = 0, EmissionRate = 10f, TTL = 0.7f };
            Assert.AreEqual(0, zero.PoolSize);
            // N04: automatic size is int(TTL*rate)+1 = 8; Ceiling alone gave 7.
            var auto = new GfxParticleParams { Quota = -1, EmissionRate = 10f, TTL = 0.7f };
            Assert.AreEqual(8, auto.PoolSize);
            var capped = new GfxParticleParams { Quota = 3, EmissionRate = 10f, TTL = 0.7f };
            Assert.AreEqual(3, capped.PoolSize);
        }

        [Test]
        public void ZeroQuotaSimulateProducesNoSlotsAtAnyTime()
        {
            var p = new GfxParticleParams
            {
                SystemId = "120",
                Quota = 0,
                EmissionRate = 10f,
                TTL = 0.7f,
                Speed = 3f,
                IsBind = true,
                EmitterEnabled = true,
                RandomSeed = 41,
                ColorMin = GfxMath.ColorWhite(),
                ColorMax = GfxMath.ColorWhite()
            };
            var kps = new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec { TimeSpan = 2000f, Pos = GfxMath.Vec3Zero(), Dir = GfxMath.QuatIdentity(), Color = GfxMath.ColorWhite(), Scale = 1f }
            };
            var sim = new GfxParticleSim(p, kps, null);
            foreach (var t in new[] { 0f, 100f, 500f, 700f, 799.99f, 800f, 1499.99f, 1500f, 3000f })
            {
                var slots = sim.Simulate(t, 3000f, 0f, 3000f, 1, 0f, false);
                Assert.AreEqual(0, slots.Length, "t=" + t);
            }
        }

        [Test]
        public void AutoPoolKeepsFirstEmissionAliveAtWindowEdge()
        {
            // N04: rate=10, TTL=0.7 -> pool 8 (Godot), not 7; at t=799.99ms emission 0
            // still has age 0.69999 < TTL in Godot and must stay alive in Unity too.
            var p = new GfxParticleParams
            {
                SystemId = "120",
                Quota = -1,
                EmissionRate = 10f,
                TTL = 0.7f,
                Speed = 3f,
                IsBind = true,
                EmitterEnabled = true,
                RandomSeed = 41,
                ColorMin = GfxMath.ColorWhite(),
                ColorMax = GfxMath.ColorWhite()
            };
            Assert.AreEqual(8, p.PoolSize);
            var kps = new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec { TimeSpan = 2000f, Pos = GfxMath.Vec3Zero(), Dir = GfxMath.QuatIdentity(), Color = GfxMath.ColorWhite(), Scale = 1f }
            };
            var sim = new GfxParticleSim(p, kps, null);
            var slots = sim.Simulate(799.99f, 3000f, 0f, 3000f, 1, 0f, false);
            var firstAlive = false;
            foreach (var s in slots)
                if (s.Alive && s.EIdx == 0f)
                    firstAlive = true;
            Assert.IsTrue(firstAlive, "emission 0 must survive until age 0.69999");
        }

        [Test]
        public void SerializedParticleRestoresAnimationAndAdvancesGlobalSheet()
        {
            var doc = new GfxParser().Parse(PointGfx);
            var element = doc.Elements[0];
            element.Properties["TexRow"] = "5";
            element.Properties["TexCol"] = "6";
            element.Properties["TexInterval"] = "40";
            element.Properties["RenderLayer"] = "7";
            element.Properties["RotMin"] = "0.7";
            element.Properties["RotMax"] = "0.7";
            element.Keypoints[0].Properties["Position"] = "1, 2, 3";
            var built = GfxSceneBuilder.Build(doc, "SerializationTest");
            var original = built.Root.GetComponentsInChildren<GfxParticleDriver>()[0];
            var go = new GameObject("ReloadedParticle");
            try
            {
                var restored = go.AddComponent<GfxParticleDriver>();
                UnityEditor.EditorJsonUtility.FromJsonOverwrite(
                    UnityEditor.EditorJsonUtility.ToJson(original), restored);
                Assert.AreEqual(1f, restored.GetKeypoints()[0].Pos[0]);
                restored.OnGfxTick(80f, 1f);
                Assert.IsNull(go.GetComponent<ParticleSystem>());
                var mat = go.GetComponent<MeshRenderer>().sharedMaterial;
                Assert.AreEqual(2996, mat.renderQueue);
                var block = new MaterialPropertyBlock();
                restored.GetComponent<Renderer>().GetPropertyBlock(block);
                var first = block.GetVector("_UvRect");
                restored.OnGfxTick(120f, 1f);
                restored.GetComponent<Renderer>().GetPropertyBlock(block);
                var next = block.GetVector("_UvRect");
                Assert.AreEqual(1f / 6f, first.x, 0.0001f);
                Assert.AreEqual(1f / 5f, first.y, 0.0001f);
                Assert.AreNotEqual(first.z, next.z);
                Assert.Greater(restored.RenderedParticleCount, 0);
                var particles = new GfxRenderedParticle[restored.RenderedParticleCount];
                Assert.Greater(restored.CopyRenderedParticles(particles), 0);
                Assert.AreEqual(0.7f, particles[0].ScreenAngle, 0.0001f);
                Assert.AreEqual(1f, mat.GetFloat("_UseVertexColor"));
                Assert.AreEqual(0f, mat.GetFloat("_ZWrite"));
                Object.DestroyImmediate(mat);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(built.Root);
            }
        }
    }
}
