using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// CPU particle element for GFX IDs 120–125 (create_id_120–125.gd).
    /// Simulates with GfxParticleSim (Godot gfx_particle_eval parity) and pushes
    /// results into Unity ParticleSystem via SetParticles (Shuriken, not VFX Graph). DummyEle → GfxDummyParticlesDriver node copies (gfx_dummy_particles.gd; no MultiMesh).
    /// </summary>
    [ExecuteAlways]
    public sealed class GfxParticleDriver : GfxElementDriver
    {
        [SerializeField] private GfxParticleParamsSerial paramsSerial =
            new GfxParticleParamsSerial();
        [SerializeField] private bool visualActive = true;
        [SerializeField] private bool warpUnsupportedLogged;
        [SerializeField] private bool dummyUnsupportedLogged;

        private ParticleSystem particleSystem;
        private ParticleSystemRenderer particleRenderer;
        private Material runtimeMaterial;
        private MaterialPropertyBlock propertyBlock;
        private GfxParticleParams cachedParams;
        private GfxParticleSim sim;
        private GfxSpriteParams uvParams;
        private ParticleSystem.Particle[] particleBuffer;
        private GfxDummyParticlesDriver dummyDriver;
        private bool dummyActive;
        private List<GfxCtrlSpec> affectors = new List<GfxCtrlSpec>();
        private List<GfxCtrlSpec> extraEmitterCtrls = new List<GfxCtrlSpec>();
        private bool header2dRender;

        // BindEle emitter binding (GFX-00001, ID120-125 BindEle). The component
        // reference survives scene/prefab serialization; the target name and
        // start time are primitives, so the binding is restorable by name even
        // after a reload that drops the reference.
        [SerializeField] private GfxElementDriver bindEleTarget;
        [SerializeField] private string bindEleTargetName = string.Empty;
        [SerializeField] private float bindEleTargetStartMs;
        [SerializeField] private bool bindEleConfigured;

        public GfxParticleParams Params =>
            cachedParams ?? (cachedParams = paramsSerial.ToParams());

        public void Configure(
            GfxParticleParams particleParams,
            Texture texture,
            bool gfx2dRender,
            IList<GfxCtrlSpec> affectorSpecs,
            IList<GfxCtrlSpec> emitterExtraCtrls,
            IList<string> warnings)
        {
            if (particleParams == null)
                throw new ArgumentNullException(nameof(particleParams));

            cachedParams = particleParams;
            paramsSerial = GfxParticleParamsSerial.FromParams(particleParams);
            header2dRender = gfx2dRender;
            SystemId = particleParams.SystemId;
            affectors = affectorSpecs != null
                ? new List<GfxCtrlSpec>(affectorSpecs)
                : new List<GfxCtrlSpec>();
            extraEmitterCtrls = emitterExtraCtrls != null
                ? new List<GfxCtrlSpec>(emitterExtraCtrls)
                : new List<GfxCtrlSpec>();

            EnsureComponents();
            RebuildSim();
            ConfigureShuriken();

            runtimeMaterial = GfxParticleMaterialFactory.Create(
                string.IsNullOrEmpty(particleParams.Name)
                    ? "Particle_" + particleParams.SystemId
                    : particleParams.Name,
                particleParams.IsAdditiveBlend,
                particleParams.ZEnable,
                gfx2dRender,
                texture);
            if (runtimeMaterial.HasProperty("_MixAlphaScale"))
                runtimeMaterial.SetFloat("_MixAlphaScale", particleParams.MixAlphaScale);
            particleRenderer.sharedMaterial = runtimeMaterial;
            particleRenderer.SetPropertyBlock(null);
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;

            if (particleParams.Warp)
            {
                if (!GfxWarpMaterial.Apply(runtimeMaterial, true) &&
                    warnings != null && !warpUnsupportedLogged)
                {
                    warpUnsupportedLogged = true;
                    warnings.Add(
                        "GFX ID " + particleParams.SystemId +
                        " Warp requested but material has no _Warp (fallback shader); " +
                        "heat-distortion skipped, blend forced to mix.");
                }
            }

            if (!string.IsNullOrEmpty(particleParams.DummyEle))
            {
                dummyActive = true;
                EnsureDummyDriver(particleParams.DummyEle, Math.Max(1, particleParams.PoolSize), warnings);
                // Godot: empty particle mesh — suppress Shuriken quads.
                ApplySlots(Array.Empty<GfxParticleSlot>());
                if (particleRenderer != null)
                    particleRenderer.enabled = false;
            }
            else
            {
                dummyActive = false;
                if (particleRenderer != null)
                    particleRenderer.enabled = true;
            }

            if (texture == null && warnings != null &&
                !string.IsNullOrEmpty(particleParams.TexFile))
            {
                warnings.Add(
                    "GFX ID " + particleParams.SystemId +
                    " TexFile not bound: " + particleParams.TexFile);
            }

            if (particleParams.SystemId == "122" &&
                (particleParams.MultiPlanePoints == null ||
                 particleParams.MultiPlanePoints.Length == 0) &&
                warnings != null &&
                !string.IsNullOrEmpty(particleParams.RefModel))
            {
                warnings.Add(
                    "GFX ID 122 RefModel not sampled ('" + particleParams.RefModel +
                    "'); particles start at emitter (Godot fallback).");
            }

            if (particleParams.SystemId == "125" && !particleParams.EmitterEnabled &&
                warnings != null)
            {
                warnings.Add(
                    "GFX ID 125 has no CtrlType 110 curve; Emitter_Enabled=false (no emission).");
            }

            ApplySlots(Array.Empty<GfxParticleSlot>());
        }

        /// <summary>
        /// BindEle emitter binding for ID120-125 (GFX-00001: 符文阵效果2改.gfx ID123
        /// particles bound to ID110 trails). The emitter origin follows
        /// <paramref name="target"/>'s pose, sampled per particle at its birth
        /// time; particles with IsBind=0 then keep their birth position, while
        /// the element's own emitter transforms still apply on top. Call after
        /// all drivers exist (SceneBuilder ResolveBindEle pass).
        /// <paramref name="targetStartMs"/> is the target element's StartTime in
        /// ms (Godot b_start): the target pose before that time is identity.
        /// </summary>
        public void SetBindEleTarget(GfxElementDriver target, float targetStartMs)
        {
            bindEleTarget = target;
            bindEleTargetName = target != null ? target.ElementName : string.Empty;
            bindEleTargetStartMs = targetStartMs;
            bindEleConfigured = target != null;
            if (sim != null)
                sim.BindEleSampler = BindEleSampler;
        }

        /// <summary>Removes the BindEle emitter binding.</summary>
        public void ClearBindEleTarget()
        {
            bindEleTarget = null;
            bindEleTargetName = string.Empty;
            bindEleTargetStartMs = 0f;
            bindEleConfigured = false;
            if (sim != null)
                sim.BindEleSampler = null;
        }

        /// <summary>Live BindEle target, lazily re-resolved by serialized name.</summary>
        public GfxElementDriver BindEleTarget => ResolveBindEleTarget();

        /// <summary>True once SetBindEleTarget was called with a target.</summary>
        public bool BindEleTargetActive => bindEleConfigured;

        /// <summary>Target element StartTime in ms (Godot b_start).</summary>
        public float BindEleTargetStartMs => bindEleTargetStartMs;

        public override void OnGfxTick(float gfxTimeMs, float alpha)
        {
            GfxAlpha = alpha;
            if (TimeManual)
                TimeMs = gfxTimeMs;

            var active = TimeManual ? TimeMs : gfxTimeMs;
            if (sim == null)
            {
                EnsureComponents();
                var source = RestoreAnimationSource();
                if (source != null)
                {
                    affectors = source.Affectors.ConvertAll(GfxKeypointBuilder.FromDictionary);
                    extraEmitterCtrls = source.ControlMethods.ConvertAll(GfxKeypointBuilder.FromDictionary)
                        .FindAll(c => c != null && c.Type == 110);
                }
                runtimeMaterial = particleRenderer.sharedMaterial;
                RebuildSim();
                ConfigureShuriken();
                dummyActive = !string.IsNullOrEmpty(Params.DummyEle);
                if (dummyActive)
                    EnsureDummyDriver(Params.DummyEle, Math.Max(1, Params.PoolSize), null);
            }

            var slots = sim.Simulate(
                active, GlobalLoopTime, LocalStartTime, LocalTotalTime,
                RepeatCount, RepeatDelay, IsEndless);

            var elementTime = GfxKeypointEval.ElementTime(
                active, GlobalLoopTime, LocalStartTime, LocalTotalTime,
                RepeatCount, RepeatDelay);
            var visible = IsEndless ||
                (elementTime >= LocalStartTime && elementTime <= LocalTotalTime);
            SetVisualActive(visible);
            if (!visible)
            {
                if (dummyActive && dummyDriver != null)
                    dummyDriver.Tick(Array.Empty<GfxParticleSlot>(), alpha, false);
                ApplySlots(Array.Empty<GfxParticleSlot>());
                return;
            }

            if (dummyActive)
            {
                if (dummyDriver != null)
                {
                    dummyDriver.Tick(
                        slots,
                        alpha,
                        Params != null && Params.Facing);
                }
                ApplySlots(Array.Empty<GfxParticleSlot>());
            }
            else
            {
                ApplySlots(slots);
            }
            if (runtimeMaterial != null)
            {
                if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
                particleRenderer.GetPropertyBlock(propertyBlock);
                // Per-particle alpha already contains the header alpha.
                if (runtimeMaterial.HasProperty("_GfxAlpha")) propertyBlock.SetFloat("_GfxAlpha", 1f);
                if (!Params.IsUseParUV && !Params.InitRandomTexture && runtimeMaterial.HasProperty("_UvRect"))
                {
                    var uv = GfxSpriteUvEval.Evaluate(uvParams, elementTime);
                    if (runtimeMaterial.HasProperty("_UvExchange"))
                        propertyBlock.SetFloat("_UvExchange", uv.Exchange ? 1f : 0f);
                    propertyBlock.SetVector("_UvRect", new Vector4(uv.ScaleU, uv.ScaleV, uv.OffsetU, uv.OffsetV));
                }
                particleRenderer.SetPropertyBlock(propertyBlock);
            }
        }


        private void EnsureDummyDriver(string dummyName, int pool, IList<string> warnings)
        {
            var existing = transform.Find(GfxDummyParticlesDriver.NodeName);
            if (existing != null)
            {
                dummyDriver = existing.GetComponent<GfxDummyParticlesDriver>();
                if (dummyDriver == null)
                    dummyDriver = existing.gameObject.AddComponent<GfxDummyParticlesDriver>();
            }
            else
            {
                var go = new GameObject(GfxDummyParticlesDriver.NodeName);
                go.transform.SetParent(transform, false);
                dummyDriver = go.AddComponent<GfxDummyParticlesDriver>();
            }
            dummyDriver.Configure(dummyName, pool);
            dummyDriver.EnsureBuilt();
            if (!string.IsNullOrEmpty(dummyDriver.BuildError) &&
                warnings != null && !dummyUnsupportedLogged)
            {
                dummyUnsupportedLogged = true;
                warnings.Add(
                    "GFX ID " + (Params != null ? Params.SystemId : "?") +
                    " DummyEle='" + dummyName + "': " + dummyDriver.BuildError);
            }
        }

        private void RebuildSim()
        {
            uvParams = new GfxSpriteParams {
                TexCol = Params.TexCol, TexRow = Params.TexRow, TexInterval = Params.TexInterval,
                TileMode = Params.TileMode, TexSpeed1 = Params.TexSpeed1, TexSpeed2 = Params.TexSpeed2,
                UReverse = Params.UReverse, VReverse = Params.VReverse, UvExchange = Params.UvExchange
            };
            sim = new GfxParticleSim(Params, GetKeypoints(), affectors, extraEmitterCtrls);
            sim.BindEleSampler = BindEleSampler;
            var pool = Math.Max(1, Params.PoolSize);
            particleBuffer = new ParticleSystem.Particle[pool];
        }

        private Func<float, GfxParticleEmitterState> BindEleSampler
        {
            get
            {
                if (!bindEleConfigured)
                    return null;
                return SampleBindElePose;
            }
        }

        /// <summary>
        /// BindEle emitter pose at a shared GFX active time (ms): the same
        /// keypoint evaluation GfxElementDriver.OnGfxTick applies to the target
        /// node's transform, with the target StartTime recorded at bind time as
        /// the span base. Before that start (or while the target has no
        /// keypoints) the pose is identity, matching the untouched target node;
        /// Rad_2D is not composed because ApplyState does not apply it either.
        /// </summary>
        private GfxParticleEmitterState SampleBindElePose(float activeTimeMs)
        {
            var target = ResolveBindEleTarget();
            if (target == null)
                return null;
            var kps = target.GetKeypoints();
            if (kps == null || kps.Count == 0)
                return new GfxParticleEmitterState();
            var elementTime = target.IsEndless ? activeTimeMs : GfxKeypointEval.ElementTime(
                activeTimeMs, target.GlobalLoopTime, bindEleTargetStartMs,
                target.LocalTotalTime, target.RepeatCount, target.RepeatDelay);
            var span = elementTime - bindEleTargetStartMs;
            if (span < 0f)
                return new GfxParticleEmitterState();
            var state = GfxKeypointEval.Evaluate(kps, span);
            return new GfxParticleEmitterState
            {
                Pos = GfxMath.Clone3(state.Pos),
                Rot = GfxMath.Clone4(state.Dir),
                Scale = state.Scale
            };
        }

        private GfxElementDriver ResolveBindEleTarget()
        {
            if (bindEleTarget != null)
                return bindEleTarget;
            if (!bindEleConfigured || string.IsNullOrEmpty(bindEleTargetName))
                return null;
            // Reload fallback: the component reference may be gone while the
            // serialized name survives. First ElementName match in hierarchy
            // order reproduces GfxSceneBuilder.ResolveBindEle (document order,
            // first match wins, no self-exclusion).
            var drivers = transform.root.GetComponentsInChildren<GfxElementDriver>(true);
            for (var i = 0; i < drivers.Length; i++)
            {
                if (drivers[i] != null && drivers[i].ElementName == bindEleTargetName)
                {
                    bindEleTarget = drivers[i];
                    return drivers[i];
                }
            }

            return null;
        }

        private void EnsureComponents()
        {
            particleSystem = GetComponent<ParticleSystem>();
            if (particleSystem == null)
                particleSystem = gameObject.AddComponent<ParticleSystem>();
            particleRenderer = GetComponent<ParticleSystemRenderer>();
            if (particleRenderer == null)
                particleRenderer = gameObject.AddComponent<ParticleSystemRenderer>();
        }

        private void ConfigureShuriken()
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particleSystem.main;
            main.startSize3D = true;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = Math.Max(1, Params.PoolSize);
            main.startLifetime = 1f;
            main.startSize = 1f;
            main.startColor = Color.white;
            main.gravityModifier = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = particleSystem.emission;
            emission.enabled = false;
            var shape = particleSystem.shape;
            shape.enabled = false;

            // Billboard for 2D particles; mesh/aligned for 3DParticle/Facing.
            if (Params.Particle3D && Params.Facing)
                particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            else if (Params.Particle3D)
                particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            else
                particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;

            // Global sheets use _UvRect; Shuriken only handles individual particle frames.
            var sheet = particleSystem.textureSheetAnimation;
            if ((Params.IsUseParUV || Params.InitRandomTexture) &&
                (Params.TexCol > 1 || Params.TexRow > 1) && !Params.TileMode)
            {
                sheet.enabled = true;
                sheet.numTilesX = Math.Max(1, Params.TexCol);
                sheet.numTilesY = Math.Max(1, Params.TexRow);
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            }
            else
            {
                sheet.enabled = false;
            }

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            // A deserialized, stopped system has no native simulation buffer in edit mode.
            // Initialize it without advancing; all particle state is supplied by GFX.
            particleSystem.Simulate(0f, false, true, false);
        }

        private void ApplySlots(GfxParticleSlot[] slots)
        {
            if (particleSystem == null)
                return;
            var alive = 0;
            var pool = particleBuffer != null ? particleBuffer.Length : 0;
            if (slots != null)
            {
                for (var i = 0; i < slots.Length && alive < pool; i++)
                {
                    var s = slots[i];
                    if (s == null || !s.Alive)
                        continue;
                    var alpha = s.Color[3] * s.KeyAlpha * GfxAlpha;
                    // Angelica: discard under 5/255
                    if (alpha * 255f < 5f)
                        continue;

                    var size = s.Size;
                    float w = Params.NoScale1
                        ? Params.ParticleWidth
                        : Params.ParticleWidth * size * (Params.Particle3D ? 1f : s.KeyScale);
                    float h = Params.NoScale2
                        ? Params.ParticleHeight
                        : Params.ParticleHeight * size * (Params.Particle3D ? 1f : s.KeyScale);
                    if (Params.Particle3D)
                    {
                        w *= s.KeyScale;
                        h *= s.KeyScale;
                    }

                    var p = new ParticleSystem.Particle();
                    p.position = new Vector3(s.Pos[0], s.Pos[1], s.Pos[2]);
                    p.startColor = new Color(
                        s.Color[0], s.Color[1], s.Color[2], alpha * Params.MixAlphaScale);
                    p.startSize3D = new Vector3(w, h, 1f);

                    p.remainingLifetime = Math.Max(0.0001f, Params.TTL - s.Age);
                    p.startLifetime = Math.Max(0.0001f, Params.TTL);
                    p.rotation3D = QuaternionToEuler(s.Rot);
                    p.angularVelocity = 0f;
                    p.velocity = Vector3.zero;
                    // Encode sheet frame in remainingLifetime ratio for optional custom shaders;
                    // TextureSheetAnimation uses startLifetime when global.
                    if (Params.IsUseParUV || Params.InitRandomTexture)
                    {
                        var total = Math.Max(1, Params.TexCol * Params.TexRow);
                        p.startLifetime = 1f;
                        p.remainingLifetime = Math.Max(0.0001f, 1f - ((s.Frame + 0.01f) / total));
                    }

                    particleBuffer[alive++] = p;
                }
            }

            particleSystem.SetParticles(particleBuffer, alive);
        }

        private static Vector3 QuaternionToEuler(float[] q)
        {
            // Approximate for Mesh/Stretch modes; Billboard ignores rotation3D pitch/yaw.
            if (q == null || q.Length < 4)
                return Vector3.zero;
            var qq = new Quaternion(q[0], q[1], q[2], q[3]);
            return qq.eulerAngles;
        }

        private void SetVisualActive(bool active)
        {
            visualActive = active;
            if (particleRenderer != null)
                particleRenderer.enabled = active;
        }
    }

    [Serializable]
    public sealed class GfxParticleParamsSerial
    {
        public string SystemId = "120";
        public string Name = string.Empty;
        public int ElementIndex;
        public string TexFile = string.Empty;
        public int DestBlend = 6;
        public bool ZEnable = true;
        public bool Warp;
        public bool IsDummy;
        public string DummyEle = string.Empty;
        public int TexRow = 1;
        public int TexCol = 1;
        public float TexInterval = 50f;
        public bool TileMode;
        public float TexSpeed1;
        public float TexSpeed2;
        public bool UReverse;
        public bool VReverse;
        public bool UvExchange;
        public bool IsUseParUV;
        public bool InitRandomTexture;
        public int Quota = -1;
        public float ParticleWidth = 0.25f;
        public float ParticleHeight = 0.25f;
        public bool Particle3D;
        public bool Facing;
        public bool ScaleNoOff;
        public bool NoScale1;
        public bool NoScale2;
        public float OrgPt1;
        public float OrgPt2;
        public float EmissionRate;
        public float Angle;
        public float Speed;
        public float ParAcc;
        public Vector3 AccDir = new Vector3(0f, 0f, -1f);
        public float Acc;
        public float TTL = 1f;
        public Color ColorMin = Color.white;
        public Color ColorMax = Color.white;
        public float ScaleMin = 1f;
        public float ScaleMax = 1f;
        public float RotMin;
        public float RotMax;
        public bool IsSurface;
        public bool IsBind = true;
        public bool IsDrag;
        public float DragPow = 1f;
        public Vector3 ParIniDir;
        public bool IsUseHSVInterp;
        public Vector3 AreaSize = Vector3.one;
        public bool IsAvgGen;
        public int AlphaSeg = 10;
        public int BetaSeg = 10;
        public string RefModel = string.Empty;
        public bool UseNormal;
        public bool PerpToNormal = true;
        public float Radius;
        public int AvgGenSeg = 1;
        public bool IsAvgGenSeq = true;
        public int RandomSeed;
        public bool EmitterEnabled = true;
        public GfxFloatRow[] MultiPlanePoints = Array.Empty<GfxFloatRow>();
        public GfxFloatRow[] MultiPlaneNormals = Array.Empty<GfxFloatRow>();
        public GfxFloatRow[] CurvePoints = Array.Empty<GfxFloatRow>();
        public float[] CurveRatios = Array.Empty<float>();
        public GfxFloatRow[] CurveDirs = Array.Empty<GfxFloatRow>();
        public int AvgStepCount = 1;

        public static GfxParticleParamsSerial FromParams(GfxParticleParams p)
        {
            return new GfxParticleParamsSerial
            {
                SystemId = p.SystemId,
                Name = p.Name,
                ElementIndex = p.ElementIndex,
                TexFile = p.TexFile,
                DestBlend = p.DestBlend,
                ZEnable = p.ZEnable,
                Warp = p.Warp,
                IsDummy = p.IsDummy,
                MultiPlanePoints = GfxFloatRow.Pack(p.MultiPlanePoints),
                MultiPlaneNormals = GfxFloatRow.Pack(p.MultiPlaneNormals),
                CurvePoints = GfxFloatRow.Pack(p.CurvePoints),
                CurveRatios = p.CurveRatios != null ? (float[])p.CurveRatios.Clone() : Array.Empty<float>(),
                CurveDirs = GfxFloatRow.Pack(p.CurveDirs),
                AvgStepCount = p.AvgStepCount,
                DummyEle = p.DummyEle,
                TexRow = p.TexRow,
                TexCol = p.TexCol,
                TexInterval = p.TexInterval,
                TileMode = p.TileMode,
                TexSpeed1 = p.TexSpeed1,
                TexSpeed2 = p.TexSpeed2,
                UReverse = p.UReverse,
                VReverse = p.VReverse,
                UvExchange = p.UvExchange,
                IsUseParUV = p.IsUseParUV,
                InitRandomTexture = p.InitRandomTexture,
                Quota = p.Quota,
                ParticleWidth = p.ParticleWidth,
                ParticleHeight = p.ParticleHeight,
                Particle3D = p.Particle3D,
                Facing = p.Facing,
                ScaleNoOff = p.ScaleNoOff,
                NoScale1 = p.NoScale1,
                NoScale2 = p.NoScale2,
                OrgPt1 = p.OrgPt1,
                OrgPt2 = p.OrgPt2,
                EmissionRate = p.EmissionRate,
                Angle = p.Angle,
                Speed = p.Speed,
                ParAcc = p.ParAcc,
                AccDir = ToV3(p.AccDir),
                Acc = p.Acc,
                TTL = p.TTL,
                ColorMin = ToColor(p.ColorMin),
                ColorMax = ToColor(p.ColorMax),
                ScaleMin = p.ScaleMin,
                ScaleMax = p.ScaleMax,
                RotMin = p.RotMin,
                RotMax = p.RotMax,
                IsSurface = p.IsSurface,
                IsBind = p.IsBind,
                IsDrag = p.IsDrag,
                DragPow = p.DragPow,
                ParIniDir = ToV3(p.ParIniDir),
                IsUseHSVInterp = p.IsUseHSVInterp,
                AreaSize = ToV3(p.AreaSize),
                IsAvgGen = p.IsAvgGen,
                AlphaSeg = p.AlphaSeg,
                BetaSeg = p.BetaSeg,
                RefModel = p.RefModel,
                UseNormal = p.UseNormal,
                PerpToNormal = p.PerpToNormal,
                Radius = p.Radius,
                AvgGenSeg = p.AvgGenSeg,
                IsAvgGenSeq = p.IsAvgGenSeq,
                RandomSeed = p.RandomSeed,
                EmitterEnabled = p.EmitterEnabled
            };
        }

        public GfxParticleParams ToParams()
        {
            var p = new GfxParticleParams
            {
                SystemId = SystemId,
                Name = Name,
                ElementIndex = ElementIndex,
                TexFile = TexFile,
                DestBlend = DestBlend,
                ZEnable = ZEnable,
                Warp = Warp,
                IsDummy = IsDummy,
                MultiPlanePoints = GfxFloatRow.Unpack(MultiPlanePoints),
                MultiPlaneNormals = GfxFloatRow.Unpack(MultiPlaneNormals),
                CurvePoints = GfxFloatRow.Unpack(CurvePoints),
                CurveRatios = CurveRatios != null ? (float[])CurveRatios.Clone() : Array.Empty<float>(),
                CurveDirs = GfxFloatRow.Unpack(CurveDirs),
                AvgStepCount = AvgStepCount,
                DummyEle = DummyEle,
                TexRow = TexRow,
                TexCol = TexCol,
                TexInterval = TexInterval,
                TileMode = TileMode,
                TexSpeed1 = TexSpeed1,
                TexSpeed2 = TexSpeed2,
                UReverse = UReverse,
                VReverse = VReverse,
                UvExchange = UvExchange,
                IsUseParUV = IsUseParUV,
                InitRandomTexture = InitRandomTexture,
                Quota = Quota,
                ParticleWidth = ParticleWidth,
                ParticleHeight = ParticleHeight,
                Particle3D = Particle3D,
                Facing = Facing,
                ScaleNoOff = ScaleNoOff,
                NoScale1 = NoScale1,
                NoScale2 = NoScale2,
                OrgPt1 = OrgPt1,
                OrgPt2 = OrgPt2,
                EmissionRate = EmissionRate,
                Angle = Angle,
                Speed = Speed,
                ParAcc = ParAcc,
                AccDir = FromV3(AccDir),
                Acc = Acc,
                TTL = TTL,
                ColorMin = FromColor(ColorMin),
                ColorMax = FromColor(ColorMax),
                ScaleMin = ScaleMin,
                ScaleMax = ScaleMax,
                RotMin = RotMin,
                RotMax = RotMax,
                IsSurface = IsSurface,
                IsBind = IsBind,
                IsDrag = IsDrag,
                DragPow = DragPow,
                ParIniDir = FromV3(ParIniDir),
                IsUseHSVInterp = IsUseHSVInterp,
                AreaSize = FromV3(AreaSize),
                IsAvgGen = IsAvgGen,
                AlphaSeg = AlphaSeg,
                BetaSeg = BetaSeg,
                RefModel = RefModel,
                UseNormal = UseNormal,
                PerpToNormal = PerpToNormal,
                Radius = Radius,
                AvgGenSeg = AvgGenSeg,
                IsAvgGenSeq = IsAvgGenSeq,
                RandomSeed = RandomSeed,
                EmitterEnabled = EmitterEnabled
            };
            p.ParIniDirQuat = GfxMath.QuatFromEuler(p.ParIniDir);
            return p;
        }

        private static Vector3 ToV3(float[] v) =>
            v == null || v.Length < 3 ? Vector3.zero : new Vector3(v[0], v[1], v[2]);

        private static float[] FromV3(Vector3 v) => GfxMath.Vec3(v.x, v.y, v.z);

        private static Color ToColor(float[] c) =>
            c == null || c.Length < 4 ? Color.white : new Color(c[0], c[1], c[2], c[3]);

        private static float[] FromColor(Color c) =>
            GfxMath.Color(c.r, c.g, c.b, c.a);
    }
}
