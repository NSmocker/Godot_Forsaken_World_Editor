using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    /// <summary>
    /// GFX-00001: BindEle emitter binding for ID120-125 particles
    /// (gfx/gfx/场景/符文阵效果2改.gfx: ID123 Unnamed18 BindEle → ID110 trails).
    /// Covers birth-time sampling, birth-position rest for IsBind=0, current-pose
    /// follow for IsBind=1, self-emission transform preservation, target StartTime
    /// gating, and binding survival across serialization/reload.
    /// </summary>
    public sealed class GfxParticleBindEleTests
    {
        private const int Rate = 2;
        private const float Ttl = 2f;

        private static GfxParticleParams PointParams(bool isBind)
        {
            return new GfxParticleParams
            {
                SystemId = "120",
                Quota = 4,
                EmissionRate = Rate,
                TTL = Ttl,
                Speed = 0f,
                Angle = 0f,
                IsBind = isBind,
                EmitterEnabled = true,
                RandomSeed = 5,
                ColorMin = GfxMath.ColorWhite(),
                ColorMax = GfxMath.ColorWhite()
            };
        }

        private static GfxKeypointSpec Keypoint(float x, float scale)
        {
            return new GfxKeypointSpec
            {
                TimeSpan = 2000f,
                Pos = GfxMath.Vec3(x, 0f, 0f),
                Dir = GfxMath.QuatIdentity(),
                Color = GfxMath.ColorWhite(),
                Scale = scale
            };
        }

        private static GfxKeypointSpec MoveXKeypoint(float vel)
        {
            var kp = Keypoint(0f, 1f);
            kp.Ctrls.Add(new GfxCtrlSpec
            {
                Type = 100,
                Start = 0f,
                End = -1f,
                HasVelocity = true,
                Vel = vel,
                Dir = GfxMath.Vec3(1f, 0f, 0f),
                Pos = GfxMath.Vec3Zero()
            });
            return kp;
        }

        private static List<GfxKeypointSpec> OneKp(GfxKeypointSpec kp)
        {
            return new List<GfxKeypointSpec> { kp };
        }

        private static GfxParticleEmitterState Pose(float x, float scale)
        {
            return new GfxParticleEmitterState
            {
                Pos = GfxMath.Vec3(x, 0f, 0f),
                Rot = GfxMath.QuatIdentity(),
                Scale = scale
            };
        }

        [Test]
        public void UnboundParticlesSampleTargetAtBirthTimeAndStayThere()
        {
            var p = PointParams(false);
            var sim = new GfxParticleSim(p, OneKp(Keypoint(0f, 1f)), null);
            // Target moves 1 unit per second of shared GFX active time.
            sim.BindEleSampler = activeMs => Pose(activeMs * 0.001f, 1f);
            var slots = sim.Simulate(1500f, 3000f, 0f, 3000f, 1, 0f, false);
            // Births: slot 0 at 500ms, slot 1 at 1000ms, slot 2 at 1500ms.
            Assert.IsTrue(slots[0].Alive);
            Assert.AreEqual(0.5f, slots[0].Pos[0], 0.01f);
            Assert.AreEqual(1.0f, slots[1].Pos[0], 0.01f);
            Assert.AreEqual(1.5f, slots[2].Pos[0], 0.01f);
            // IsBind=0: the older particle keeps its birth position even though
            // the bound element has moved on to x=1.5 by now.
            Assert.AreNotEqual(slots[2].Pos[0], slots[0].Pos[0]);
        }

        [Test]
        public void WithoutBindingParticlesStayAtOrigin()
        {
            // The reported GFX-00001 defect: no BindEle resolution → emitter pose
            // is the element's own (origin) for every birth time.
            var p = PointParams(false);
            var sim = new GfxParticleSim(p, OneKp(Keypoint(0f, 1f)), null);
            var slots = sim.Simulate(1500f, 3000f, 0f, 3000f, 1, 0f, false);
            Assert.IsTrue(slots[0].Alive);
            Assert.AreEqual(0f, slots[0].Pos[0], 0.01f);
            Assert.AreEqual(0f, slots[2].Pos[0], 0.01f);
        }

        [Test]
        public void BoundParticlesFollowCurrentTargetPose()
        {
            var p = PointParams(true);
            var sim = new GfxParticleSim(p, OneKp(Keypoint(0f, 1f)), null);
            sim.BindEleSampler = activeMs => Pose(activeMs * 0.001f, 1f);
            var slots = sim.Simulate(1500f, 3000f, 0f, 3000f, 1, 0f, false);
            // IsBind=1: every live particle rides the emitter's current pose.
            Assert.AreEqual(1.5f, slots[0].Pos[0], 0.01f);
            Assert.AreEqual(1.5f, slots[1].Pos[0], 0.01f);
        }

        [Test]
        public void IdentityBindPoseLeavesSlotsUnchanged()
        {
            var p = PointParams(false);
            var kps = OneKp(Keypoint(0f, 1f));
            var plain = new GfxParticleSim(p, kps, null);
            var bound = new GfxParticleSim(p, kps, null);
            bound.BindEleSampler = activeMs => new GfxParticleEmitterState();
            var plainSlots = plain.Simulate(1500f, 3000f, 0f, 3000f, 1, 0f, false);
            var boundSlots = bound.Simulate(1500f, 3000f, 0f, 3000f, 1, 0f, false);
            for (var i = 0; i < plainSlots.Length; i++)
            {
                Assert.AreEqual(plainSlots[i].Alive, boundSlots[i].Alive);
                Assert.AreEqual(plainSlots[i].Pos[0], boundSlots[i].Pos[0], 0.0001f);
                Assert.AreEqual(plainSlots[i].Pos[2], boundSlots[i].Pos[2], 0.0001f);
                Assert.AreEqual(plainSlots[i].KeyScale, boundSlots[i].KeyScale, 0.0001f);
            }
        }

        [Test]
        public void BindingReplacesSelfPositionAndPreservesOwnScale()
        {
            // Native GetCurKeyPoint замінює Pos/Dir, не множить scale target.
            var p = PointParams(false);
            var sim = new GfxParticleSim(p, OneKp(KeypointWithSelfOffset()), null);
            sim.BindEleSampler = activeMs => Pose(activeMs * 0.001f, 2f);
            var slots = sim.Simulate(1500f, 3000f, 0f, 3000f, 1, 0f, false);
            // eeWorld = (0,0,5)*2 + rot(self) * ((birthX,0,0) * 2):
            // slot 0 born at 500ms → (1, 0, 10); slot 2 born at 1500ms → (3, 0, 10).
            Assert.AreEqual(1f, slots[0].Pos[0], 0.01f);
            Assert.AreEqual(0f, slots[0].Pos[2], 0.01f);
            Assert.AreEqual(3f, slots[2].Pos[0], 0.01f);
            // KeyScale = self current scale * bound scale = 1 * 2.
            Assert.AreEqual(2f, slots[0].KeyScale, 0.01f);
        }

        private static GfxKeypointSpec KeypointWithSelfOffset()
        {
            return new GfxKeypointSpec
            {
                TimeSpan = 2000f,
                Pos = GfxMath.Vec3(0f, 0f, 5f),
                Dir = GfxMath.QuatIdentity(),
                Color = GfxMath.ColorWhite(),
                Scale = 2f
            };
        }

        private static GfxElementDriver NewTarget(GameObject parent, float vel)
        {
            var go = new GameObject("Ring");
            go.transform.SetParent(parent.transform, false);
            var target = go.AddComponent<GfxElementDriver>();
            target.ElementName = "Ring";
            target.SetKeypoints(OneKp(MoveXKeypoint(vel)));
            return target;
        }

        private static GfxParticleDriver NewParticleDriver(GameObject parent)
        {
            var go = new GameObject("Dots");
            go.transform.SetParent(parent.transform, false);
            var driver = go.AddComponent<GfxParticleDriver>();
            driver.Configure(PointParams(false), null, false, null, null, null);
            return driver;
        }

        private static ParticleSystem.Particle[] EmittedParticles(GfxParticleDriver driver)
        {
            var ps = driver.GetComponent<ParticleSystem>();
            var parts = new ParticleSystem.Particle[ps.particleCount];
            ps.GetParticles(parts);
            return parts;
        }

        [Test]
        public void DriverSamplesTargetWithItsStartTimePerParticle()
        {
            var root = new GameObject("GfxRoot");
            try
            {
                var target = NewTarget(root, 1f);
                var driver = NewParticleDriver(root);
                driver.SetBindEleTarget(target, 250f);
                Assert.IsTrue(driver.BindEleTargetActive);
                Assert.AreEqual(250f, driver.BindEleTargetStartMs);
                driver.OnGfxTick(1250f, 1f);
                var parts = EmittedParticles(driver);
                Assert.AreEqual(2, parts.Length);
                // Births at 500ms and 1000ms of shared time → target spans of
                // 250ms and 750ms after its 250ms StartTime → x 0.25 / 0.75.
                Assert.AreEqual(0.25f, parts[0].position.x, 0.01f);
                Assert.AreEqual(0.75f, parts[1].position.x, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DriverTreatsNotYetStartedTargetAsIdentity()
        {
            var root = new GameObject("GfxRoot");
            try
            {
                var target = NewTarget(root, 1f);
                var driver = NewParticleDriver(root);
                driver.SetBindEleTarget(target, 1000f);
                driver.OnGfxTick(750f, 1f);
                var parts = EmittedParticles(driver);
                Assert.AreEqual(1, parts.Length);
                // Birth at 500ms is 500ms before the target starts → identity pose.
                Assert.AreEqual(0f, parts[0].position.x, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BindEleTargetApiGuardsAndClear()
        {
            var root = new GameObject("GfxRoot");
            try
            {
                var driver = NewParticleDriver(root);
                Assert.IsFalse(driver.BindEleTargetActive);
                Assert.IsNull(driver.BindEleTarget);
                // Null target must leave the driver unbound, not crash.
                driver.SetBindEleTarget(null, 250f);
                Assert.IsFalse(driver.BindEleTargetActive);
                // A target without keypoints samples identity (no crash, no shift).
                var empty = NewTarget(root, 0f);
                empty.SetKeypoints(new List<GfxKeypointSpec>());
                driver.SetBindEleTarget(empty, 0f);
                driver.OnGfxTick(1250f, 1f);
                var parts = EmittedParticles(driver);
                Assert.Greater(parts.Length, 0);
                Assert.AreEqual(0f, parts[0].position.x, 0.01f);
                // Clear detaches the sampler again.
                var target = NewTarget(root, 1f);
                driver.SetBindEleTarget(target, 250f);
                Assert.IsTrue(driver.BindEleTargetActive);
                driver.ClearBindEleTarget();
                Assert.IsFalse(driver.BindEleTargetActive);
                Assert.IsNull(driver.BindEleTarget);
                Assert.AreEqual(0f, driver.BindEleTargetStartMs);
                driver.OnGfxTick(1250f, 1f);
                parts = EmittedParticles(driver);
                Assert.Greater(parts.Length, 0);
                Assert.AreEqual(0f, parts[0].position.x, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BindingSurvivesSerializationRoundtripByName()
        {
            var root = new GameObject("GfxRoot");
            GameObject restoredRoot = null;
            try
            {
                var target = NewTarget(root, 1f);
                var driver = NewParticleDriver(root);
                driver.SetBindEleTarget(target, 250f);
                var json = UnityEditor.EditorJsonUtility.ToJson(driver);
                restoredRoot = new GameObject("GfxRootRestored");
                var restoredTargetGo = new GameObject("Ring");
                restoredTargetGo.transform.SetParent(restoredRoot.transform, false);
                var restoredTarget = restoredTargetGo.AddComponent<GfxElementDriver>();
                restoredTarget.ElementName = "Ring";
                restoredTarget.SetKeypoints(OneKp(MoveXKeypoint(1f)));
                var restoredGo = new GameObject("Dots");
                restoredGo.transform.SetParent(restoredRoot.transform, false);
                var restored = restoredGo.AddComponent<GfxParticleDriver>();
                UnityEditor.EditorJsonUtility.FromJsonOverwrite(json, restored);
                // Serialized primitives keep the binding; the live reference is
                // re-resolved from the serialized target name after reload.
                Assert.IsTrue(restored.BindEleTargetActive);
                Assert.AreEqual(250f, restored.BindEleTargetStartMs);
                var resolved = restored.BindEleTarget;
                Assert.IsNotNull(resolved);
                Assert.AreEqual("Ring", resolved.ElementName);
                restored.OnGfxTick(1250f, 1f);
                var ps = restoredGo.GetComponent<ParticleSystem>();
                Assert.Greater(ps.particleCount, 0);
                var parts = new ParticleSystem.Particle[ps.particleCount];
                ps.GetParticles(parts);
                Assert.AreEqual(0.25f, parts[0].position.x, 0.01f);
                Assert.AreEqual(0.75f, parts[1].position.x, 0.01f);
            }
            finally
            {
                if (restoredRoot != null) Object.DestroyImmediate(restoredRoot);
                Object.DestroyImmediate(root);
            }
        }
    }
}
