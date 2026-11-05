using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxPhysTests
    {
        private const string PhysGfx =
            "MOXTVersion: 99\n" +
            "GFXELEMENTID: 220 Mist\n" +
            "Name: Mist\n" +
            "ParWidth: 0.5\n" +
            "ParHeight: 0.5\n" +
            "ScaleMin: 1\n" +
            "ScaleMax: 2\n" +
            "RotMin: 0\n" +
            "RotMax: 0\n" +
            "ColorMin: -1\n" +
            "ColorMax: -1\n" +
            "IsFaceToView: 1\n" +
            "PhysDesc: MistFluid\n" +
            "TexFile: mist\\a.dds\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 2000\n" +
            "Position: 0, 1, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n";

        [Test]
        public void ParamsParseDefaultsWithoutGphy()
        {
            var doc = new GfxParser().Parse(PhysGfx);
            var warnings = new List<string>();
            var p = GfxPhysParams.FromProperties(
                doc.Elements[0].Properties, "220", 0, "Mist", "", warnings);
            Assert.AreEqual(0.5f, p.ParWidth, 0.001f);
            Assert.AreEqual(1, p.ParType);
            Assert.AreEqual("MistFluid", p.PhysDesc);
            Assert.AreEqual(100f, p.Rate, 0.001f);
            Assert.AreEqual(5f, p.Velocity, 0.001f);
            Assert.IsTrue(p.GroundCollision);
            Assert.IsTrue(warnings.Exists(w => w.Contains("MistFluid")));
            Assert.Greater(p.PoolSize(), 0);
            Assert.LessOrEqual(p.PoolSize(), GfxPhysParams.MaxPool);
        }

        [Test]
        public void GphyReaderFindsNamedFluidEntry()
        {
            var path = WriteMinimalGphy("Spray");
            try
            {
                Dictionary<string, object> entry;
                string err;
                Assert.IsTrue(GfxPhysGphy.TryRead(path, "Spray", out entry, out err));
                Assert.IsNull(err);
                Assert.AreEqual(200, (int)entry["max_particles"]);
                Assert.AreEqual(50f, (float)entry["rate"], 0.01f);
                Assert.AreEqual(2, (int)entry["shape"]);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void SimEmitsAtRateAndRespectsLifetime()
        {
            var p = new GfxPhysParams
            {
                Rate = 60f,
                Lifetime = 0.5f,
                MaxParticles = 100,
                Velocity = 0f,
                Gravity = GfxMath.Vec3Zero(),
                GroundCollision = false,
                EmitterEnabled = true,
                RandomSeed = 1
            };
            var sim = new GfxPhysSim(p);
            Func<float, float[]> at = _ => new float[] { 0f, 0f, 0f, 0f, 0f, 0f, 1f };
            sim.SimulateTo(0.2f, at); // 12 steps @ 1/60 → floor(60*12/60)=12 emitted, all alive
            Assert.AreEqual(12, sim.ParticleCount);
            sim.SimulateTo(1.0f, at); // old particles die after 0.5s
            Assert.Greater(sim.ParticleCount, 0);
            Assert.LessOrEqual(sim.ParticleCount, p.PoolSize());
        }

        [Test]
        public void RegistryPorts220And230()
        {
            GfxElementRegistry.ClearPortedForTests();
            Assert.IsTrue(GfxElementRegistry.IsPorted("220"));
            Assert.IsTrue(GfxElementRegistry.IsPorted("230"));
            Assert.IsFalse(GfxElementRegistry.IsPorted("999"));
        }

        [Test]
        public void SceneBuilderAttachesPhysDriver()
        {
            GfxElementRegistry.ClearPortedForTests();
            var result = GfxSceneBuilder.Build(
                new GfxParser().Parse(PhysGfx), "PhysTest", "", null);
            var phys = result.Root.GetComponentsInChildren<GfxPhysDriver>(true);
            Assert.AreEqual(1, phys.Length);
            Assert.AreEqual("220", phys[0].SystemId);
            Assert.AreEqual(0, result.Root.GetComponentsInChildren<GfxElementStub>(true).Length);
            Assert.IsTrue(result.Warnings.Exists(w => w.Contains("MistFluid") || w.Contains("Phys")));
        }

        [Test]
        public void DriverEmitsParticlesOnTick()
        {
            var go = new GameObject("phys");
            var driver = go.AddComponent<GfxPhysDriver>();
            var p = new GfxPhysParams
            {
                Rate = 120f,
                Lifetime = 2f,
                MaxParticles = 64,
                Velocity = 1f,
                Gravity = GfxMath.Vec3(0f, -1f, 0f),
                GroundCollision = true,
                GroundHeight = -10f,
                RandomSeed = 42,
                ParWidth = 0.25f,
                ParHeight = 0.25f
            };
            driver.LocalStartTime = 0f;
            driver.LocalTotalTime = 3000f;
            driver.GlobalLoopTime = 3000f;
            driver.Configure(p, null, false, new List<string>());
            driver.SetKeypoints(new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec
                {
                    TimeSpan = 3000f,
                    Pos = GfxMath.Vec3(0f, 2f, 0f),
                    Dir = GfxMath.QuatIdentity(),
                    Color = GfxMath.ColorWhite(),
                    Scale = 1f
                }
            });
            driver.TimeManual = true;
            driver.OnGfxTick(500f, 1f); // 0.5 s → ~60 particles
            Assert.Greater(driver.ParticleCount, 10);
            var ps = go.GetComponent<ParticleSystem>();
            Assert.IsNotNull(ps);
            Assert.Greater(ps.particleCount, 10);
        }

        private static string WriteMinimalGphy(string entryName)
        {
            // yhpg + version + count=1 + fluid(type0) + name + fluid132 + emitter104 + objectType0
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(System.Text.Encoding.ASCII.GetBytes("yhpg"));
                bw.Write(1); // version
                bw.Write(1); // count
                bw.Write(0); // syncType fluid
                var nameBytes = System.Text.Encoding.UTF8.GetBytes(entryName);
                bw.Write(nameBytes.Length);
                bw.Write(nameBytes);
                var fluid = new byte[GfxPhysGphy.FluidParamBytes];
                // max_particles @ u0 = 200
                BitConverter.GetBytes(200u).CopyTo(fluid, 0);
                // rest_per_meter @ f2
                BitConverter.GetBytes(10f).CopyTo(fluid, 8);
                // motion_limit_mult @ f5
                BitConverter.GetBytes(3.6f).CopyTo(fluid, 20);
                // collision_dist_mult @ f6
                BitConverter.GetBytes(0.12f).CopyTo(fluid, 24);
                // sim_method @ u29 = 2 (no SPH)
                BitConverter.GetBytes(2u).CopyTo(fluid, 116);
                // collision_method @ u30 = 1 (static/ground)
                BitConverter.GetBytes(1u).CopyTo(fluid, 120);
                // fluid_flags @ u32 = 0
                BitConverter.GetBytes(0u).CopyTo(fluid, 128);
                bw.Write(fluid);
                var emitter = new byte[GfxPhysGphy.EmitterParamBytes];
                // pose_pos f9..11
                BitConverter.GetBytes(0f).CopyTo(emitter, 36);
                BitConverter.GetBytes(0f).CopyTo(emitter, 40);
                BitConverter.GetBytes(0f).CopyTo(emitter, 44);
                // emitter_max u13
                BitConverter.GetBytes(0u).CopyTo(emitter, 52);
                // shape u14 = 2 ellipse
                BitConverter.GetBytes(2u).CopyTo(emitter, 56);
                BitConverter.GetBytes(0.25f).CopyTo(emitter, 60); // dim_x
                BitConverter.GetBytes(0.25f).CopyTo(emitter, 64); // dim_y
                BitConverter.GetBytes(0.05f).CopyTo(emitter, 68);
                BitConverter.GetBytes(0.05f).CopyTo(emitter, 72);
                BitConverter.GetBytes(0.05f).CopyTo(emitter, 76);
                BitConverter.GetBytes(0.5f).CopyTo(emitter, 80); // random_angle
                BitConverter.GetBytes(5f).CopyTo(emitter, 84); // velocity
                BitConverter.GetBytes(50f).CopyTo(emitter, 88); // rate
                BitConverter.GetBytes(10f).CopyTo(emitter, 92); // lifetime
                // emitter_flags u25 = 16|8 enabled+body vel
                BitConverter.GetBytes((uint)(16 | 8)).CopyTo(emitter, 100);
                bw.Write(emitter);
                bw.Write(0); // objectType normal
                var path = Path.Combine(Path.GetTempPath(), "gfxphys_" + Guid.NewGuid().ToString("N") + ".gphy");
                File.WriteAllBytes(path, ms.ToArray());
                return path;
            }
        }
    }
}
