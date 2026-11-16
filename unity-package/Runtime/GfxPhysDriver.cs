using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// GFX ID 220 phys emitter (create_id_220.gd / gfx_phys_220.gd).
    /// CPU sim via GfxPhysSim → GfxRuntimeParticleBatch mesh. SPH / scene colliders /
    /// DummyEle / PhysX orientation (ParType 2) not ported (same as Godot).
    /// </summary>
    [ExecuteAlways]
    public sealed class GfxPhysDriver : GfxElementDriver
    {
        [SerializeField] private bool visualActive = true;
        [SerializeField] private bool parType2Logged;

        [SerializeField, HideInInspector] private GfxPhysParams cachedParams;
        private GfxPhysSim sim;
        private MeshFilter meshFilter;
        private MeshRenderer particleRenderer;
        private Material runtimeMaterial;
        private readonly GfxRuntimeParticleBatch batch = new GfxRuntimeParticleBatch();
        private GfxRenderedParticle[] particleBuffer = Array.Empty<GfxRenderedParticle>();
        private GfxPhysSim.Sample[] samples = Array.Empty<GfxPhysSim.Sample>();

        public GfxPhysParams Params => cachedParams;
        public int ParticleCount => sim != null ? sim.ParticleCount : 0;
        public int RenderedParticleCount => batch.Count;

        public int CopyRenderedParticles(GfxRenderedParticle[] destination) =>
            batch.CopyTo(destination);

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
            var pool = Math.Max(1, physParams.PoolSize());
            particleBuffer = new GfxRenderedParticle[pool];
            samples = new GfxPhysSim.Sample[pool];

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
            if (sim == null && cachedParams != null)
            {
                EnsureComponents();
                sim = new GfxPhysSim(cachedParams);
                var pool = Math.Max(1, cachedParams.PoolSize());
                particleBuffer = new GfxRenderedParticle[pool];
                samples = new GfxPhysSim.Sample[pool];
                runtimeMaterial = particleRenderer != null ? particleRenderer.sharedMaterial : null;
            }
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
                batch.Set(particleBuffer, 0);
                batch.Rebuild(transform, null);
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

            var rawCount = sim.CopySamples(samples);
            var half = Math.Max(cachedParams.ParWidth, cachedParams.ParHeight) *
                       Math.Max(0.0001f, kpScale);
            var outCount = 0;
            for (var i = 0; i < rawCount && outCount < particleBuffer.Length; i++)
            {
                var sample = samples[i];
                var a = sample.A * kpA;
                if (a * 255f < 5f)
                    continue;
                var size = sample.Scale * half;
                particleBuffer[outCount++] = new GfxRenderedParticle
                {
                    Position = new Vector3(sample.X, sample.Y, sample.Z),
                    HalfWidth = size,
                    HalfHeight = size,
                    Color = new Color(sample.R, sample.G, sample.B, a),
                    ScreenAngle = sample.Rotation,
                    Orientation = Quaternion.identity,
                    Move = Vector3.forward,
                    Uv = new Rect(0f, 0f, 1f, 1f),
                    Kind = GfxRuntimeParticleKind.Billboard
                };
            }

            batch.Set(particleBuffer, outCount);
            batch.Rebuild(transform, null);
            if (meshFilter != null && meshFilter.sharedMesh != batch.Mesh)
                meshFilter.sharedMesh = batch.Mesh;
            SetVisualActive(outCount > 0);

            if (runtimeMaterial != null && runtimeMaterial.HasProperty("_GfxAlpha"))
                runtimeMaterial.SetFloat("_GfxAlpha", alpha);
        }

        private void EnsureComponents()
        {
            var systems = gameObject.GetComponents<ParticleSystem>();
            for (var i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                    DestroyImmediate(systems[i]);
            }

            var legacy = gameObject.GetComponents<ParticleSystemRenderer>();
            for (var i = 0; i < legacy.Length; i++)
            {
                if (legacy[i] != null)
                    DestroyImmediate(legacy[i]);
            }

            meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();
            particleRenderer = gameObject.GetComponent<MeshRenderer>();
            if (particleRenderer == null)
                particleRenderer = gameObject.AddComponent<MeshRenderer>();
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            batch.EnsureMesh();
            meshFilter.sharedMesh = batch.Mesh;
        }

        private void OnWillRenderObject()
        {
            if (batch.Count == 0)
                return;
            batch.Rebuild(transform, Camera.current);
        }

        private void OnDestroy()
        {
            batch.Release();
        }

        private void SetVisualActive(bool active)
        {
            visualActive = active;
            if (particleRenderer != null)
                particleRenderer.enabled = active;
        }
    }
}
