using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// GFX ID 220 phys emitter (create_id_220.gd / gfx_phys_220.gd).
    /// CPU sim via GfxPhysSim → Shuriken SetParticles. SPH / scene colliders /
    /// DummyEle / PhysX orientation (ParType 2) not ported (same as Godot).
    /// </summary>
    [ExecuteAlways]
    public sealed class GfxPhysDriver : GfxElementDriver
    {
        [SerializeField] private bool visualActive = true;
        [SerializeField] private bool parType2Logged;

        private GfxPhysParams cachedParams;
        private GfxPhysSim sim;
        private ParticleSystem particleSystem;
        private ParticleSystemRenderer particleRenderer;
        private Material runtimeMaterial;
        private ParticleSystem.Particle[] particleBuffer;

        public GfxPhysParams Params => cachedParams;
        public int ParticleCount => sim != null ? sim.ParticleCount : 0;

        public void Configure(
            GfxPhysParams physParams,
            Texture texture,
            bool gfx2dRender,
            IList<string> warnings)
        {
            if (physParams == null)
                throw new ArgumentNullException(nameof(physParams));

            cachedParams = physParams;
            SystemId = physParams.SystemId;
            EnsureComponents();
            sim = new GfxPhysSim(physParams);
            particleBuffer = new ParticleSystem.Particle[physParams.PoolSize()];

            var main = particleSystem.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = physParams.PoolSize();
            main.gravityModifier = 0f;
            var emission = particleSystem.emission;
            emission.enabled = false;
            var shape = particleSystem.shape;
            shape.enabled = false;

            runtimeMaterial = GfxParticleMaterialFactory.Create(
                "Phys_" + physParams.SystemId,
                false,
                !gfx2dRender,
                gfx2dRender,
                texture);
            particleRenderer.sharedMaterial = runtimeMaterial;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;

            if (physParams.ParType == 2 && warnings != null && !parType2Logged)
            {
                parType2Logged = true;
                warnings.Add(
                    "GFX ID 220 ParType 2 (PhysX orient) treated as world quad (same as Godot).");
            }

            if (texture == null && warnings != null && !string.IsNullOrEmpty(physParams.TexFile))
            {
                warnings.Add("GFX ID 220 TexFile not bound: " + physParams.TexFile);
            }
        }

        public override void OnGfxTick(float gfxTimeMs, float alpha)
        {
            GfxAlpha = alpha;
            if (TimeManual)
                TimeMs = gfxTimeMs;

            var active = TimeManual ? TimeMs : gfxTimeMs;
            var elementTime = GfxKeypointEval.ElementTime(
                active,
                GlobalLoopTime,
                LocalStartTime,
                LocalTotalTime,
                RepeatCount,
                RepeatDelay);

            var windowActive = IsEndless ||
                (elementTime >= LocalStartTime && elementTime <= LocalTotalTime);
            var keypoints = GetKeypoints();
            if (!windowActive || keypoints == null || keypoints.Count == 0 || sim == null)
            {
                if (sim != null && sim.Steps > 0)
                    sim.Reset();
                SetVisualActive(false);
                if (particleSystem != null)
                    particleSystem.SetParticles(particleBuffer, 0);
                return;
            }

            Func<float, float[]> emitterAt = spanMs =>
            {
                var state = GfxKeypointEval.Evaluate(keypoints, Math.Max(0f, spanMs));
                var q = state.Dir ?? GfxMath.QuatIdentity();
                if (Math.Abs(state.Rad) > 1e-8f)
                    q = GfxMath.MulQuat(
                        q, GfxMath.QuatFromAxisAngle(GfxMath.Vec3(0f, 0f, 1f), state.Rad));
                var origin = state.Pos ?? GfxMath.Vec3Zero();
                // Emitter_Pose origin in emitter space (Godot: GetTranMatrix * parent * Emitter_Pose).
                var rel = cachedParams.EmitterPos ?? GfxMath.Vec3Zero();
                origin = GfxMath.Add3(origin, GfxMath.RotateVec3(q, rel));
                return new[]
                {
                    origin[0], origin[1], origin[2],
                    q[0], q[1], q[2], q[3]
                };
            };

            sim.SimulateTo((elementTime - LocalStartTime) / 1000f, emitterAt);

            var stateNow = GfxKeypointEval.Evaluate(
                keypoints, Math.Max(0f, elementTime - LocalStartTime));
            var kpA = stateNow.Color != null && stateNow.Color.Length > 3
                ? stateNow.Color[3]
                : 1f;
            var kpScale = stateNow.Scale;

            int rawCount;
            sim.WriteParticles(particleBuffer, out rawCount);
            var outCount = 0;
            for (var i = 0; i < rawCount; i++)
            {
                var c = particleBuffer[i].startColor;
                var a = (int)Math.Round(c.a * kpA);
                if (a < 5)
                    continue;
                c.a = (byte)Math.Max(0, Math.Min(255, a));
                particleBuffer[outCount] = particleBuffer[i];
                particleBuffer[outCount].startColor = c;
                particleBuffer[outCount].startSize =
                    sim.ParticleScale(i) * Math.Max(cachedParams.ParWidth, cachedParams.ParHeight) *
                    2f * Math.Max(0.0001f, kpScale);
                outCount++;
            }

            // Edit Mode: SetParticles alone may leave particleCount at 0 until the system
            // has been played/simulated at least once (real Unity; harness always counted).
            if (!particleSystem.isPlaying && !particleSystem.isPaused)
            {
                particleSystem.Play(true);
                particleSystem.Pause(true);
            }
            particleSystem.SetParticles(particleBuffer, outCount);
            SetVisualActive(outCount > 0);

            if (runtimeMaterial != null && runtimeMaterial.HasProperty("_GfxAlpha"))
                runtimeMaterial.SetFloat("_GfxAlpha", alpha);
        }

        private void EnsureComponents()
        {
            particleSystem = gameObject.GetComponent<ParticleSystem>();
            if (particleSystem == null)
                particleSystem = gameObject.AddComponent<ParticleSystem>();
            particleRenderer = gameObject.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer == null)
                particleRenderer = gameObject.AddComponent<ParticleSystemRenderer>();
        }

        private void SetVisualActive(bool active)
        {
            visualActive = active;
            if (particleRenderer != null)
                particleRenderer.enabled = active;
        }
    }
}
