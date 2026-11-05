using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// GFX ID 150 lightning / 152 lightning-ex. CPU strip via GfxLightningEval;
    /// Mesh + AngelicaGfxTrail/Sprite URP.
    /// </summary>
    [ExecuteAlways]
    public sealed class GfxLightningDriver : GfxElementDriver
    {
        [SerializeField] private GfxLightningParamsSerial paramsSerial = new GfxLightningParamsSerial();
        [SerializeField] private bool visualActive = true;
        [SerializeField] private bool warpUnsupportedLogged;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh runtimeMesh;
        private Material runtimeMaterial;
        private GfxLightningParams cachedParams;

        public GfxLightningParams Params =>
            cachedParams ?? (cachedParams = paramsSerial.ToParams());

        public void Configure(
            GfxLightningParams lightningParams,
            Texture texture,
            bool gfx2dRender,
            IList<string> warnings)
        {
            if (lightningParams == null)
                throw new ArgumentNullException(nameof(lightningParams));

            cachedParams = lightningParams;
            paramsSerial = GfxLightningParamsSerial.FromParams(lightningParams);
            SystemId = lightningParams.SystemId;

            EnsureComponents();
            runtimeMaterial = GfxTrailMaterialFactory.Create(
                string.IsNullOrEmpty(lightningParams.Name)
                    ? "Lightning_" + lightningParams.SystemId
                    : lightningParams.Name,
                lightningParams.IsAdditiveBlend,
                lightningParams.ZEnable,
                gfx2dRender,
                texture);

            if (runtimeMaterial.HasProperty("_MixAlphaScale"))
                runtimeMaterial.SetFloat("_MixAlphaScale", lightningParams.MixAlphaScale);
            meshRenderer.sharedMaterial = runtimeMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            if (lightningParams.Warp && warnings != null && !warpUnsupportedLogged)
            {
                warpUnsupportedLogged = true;
                warnings.Add(
                    "GFX ID " + lightningParams.SystemId +
                    " Warp heat-distortion absent in Godot create_id_150/152 too; blend forced to mix.");
            }

            if (texture == null && warnings != null &&
                !string.IsNullOrEmpty(lightningParams.TexFile))
            {
                warnings.Add(
                    "GFX ID " + lightningParams.SystemId +
                    " TexFile not bound: " + lightningParams.TexFile);
            }

            if ((lightningParams.Pos1Enable || lightningParams.Pos2Enable) &&
                warnings != null)
            {
                warnings.Add(
                    "GFX ID " + lightningParams.SystemId +
                    " Pos1Enable/Pos2Enable use file StartPos/EndPos " +
                    "(live external binds not available).");
            }

            if (!lightningParams.IsValid && warnings != null)
            {
                warnings.Add(
                    "GFX ID " + lightningParams.SystemId +
                    " InitBuf invalid (Segs < 2 or LightNum < 1); strip hidden.");
            }

            ApplyMesh(GfxLightningEval.Build(
                lightningParams,
                new GfxKeypointState
                {
                    Pos = GfxMath.Vec3Zero(),
                    Dir = GfxMath.QuatIdentity(),
                    Color = GfxMath.ColorWhite(),
                    Scale = 1f
                },
                LocalStartTime,
                LocalStartTime,
                1f,
                null));
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

            var visible = IsEndless ||
                (elementTime >= LocalStartTime && elementTime <= LocalTotalTime);
            SetVisualActive(visible);
            if (!visible)
                return;

            var span = elementTime - LocalStartTime;
            if (span < 0f)
            {
                SetVisualActive(false);
                return;
            }

            var keypoints = GetKeypoints();
            GfxKeypointState state;
            if (keypoints.Count == 0)
            {
                state = new GfxKeypointState
                {
                    Pos = GfxMath.Vec3Zero(),
                    Dir = GfxMath.QuatIdentity(),
                    Color = GfxMath.ColorWhite(),
                    Scale = 1f
                };
            }
            else
            {
                state = GfxKeypointEval.Evaluate(keypoints, span);
            }

            var camLocal = ResolveCamLocal();
            // elapsed for amp/path uses elementTime vs ElementStartTime baked into LocalStartTime
            // when DestNum present; Params.ElementStartTime kept for amp relative clock.
            var p = Params;
            var elapsedClock = elementTime;
            ApplyMesh(GfxLightningEval.Build(
                p, state, elapsedClock, active, alpha, camLocal));

            if (runtimeMaterial != null)
            {
                if (runtimeMaterial.HasProperty("_GfxAlpha"))
                    runtimeMaterial.SetFloat("_GfxAlpha", 1f);
                if (runtimeMaterial.HasProperty("_BaseColor"))
                    runtimeMaterial.SetColor("_BaseColor", Color.white);
            }
        }

        protected override void ApplyState(GfxKeypointState state)
        {
            // Lightning rebuilds mesh in OnGfxTick.
        }

        private static float[] ResolveCamLocal()
        {
            var cam = Camera.main;
            if (cam != null && cam.transform != null)
            {
                var cp = cam.transform.position;
                return new[] { cp.x, cp.y, cp.z };
            }
            return GfxMath.Vec3(0f, 0f, -10f);
        }

        private void ApplyMesh(GfxLightningEval.MeshData data)
        {
            EnsureComponents();
            if (runtimeMesh == null)
            {
                runtimeMesh = new Mesh { name = "GfxLightning_" + SystemId };
                meshFilter.sharedMesh = runtimeMesh;
            }

            var n = data.Positions.Length / 3;
            var verts = new Vector3[n];
            var uvs = new Vector2[n];
            var cols = new Color[n];
            for (var i = 0; i < n; i++)
            {
                verts[i] = new Vector3(
                    data.Positions[i * 3],
                    data.Positions[i * 3 + 1],
                    data.Positions[i * 3 + 2]);
                uvs[i] = new Vector2(data.Uvs[i * 2], data.Uvs[i * 2 + 1]);
                cols[i] = new Color(
                    data.Colors[i * 4],
                    data.Colors[i * 4 + 1],
                    data.Colors[i * 4 + 2],
                    data.Colors[i * 4 + 3]);
            }

            runtimeMesh.Clear();
            runtimeMesh.vertices = verts;
            runtimeMesh.uv = uvs;
            runtimeMesh.colors = cols;
            runtimeMesh.triangles = data.Triangles;
            runtimeMesh.RecalculateBounds();
        }

        private void SetVisualActive(bool active)
        {
            visualActive = active;
            EnsureComponents();
            if (meshRenderer != null)
                meshRenderer.enabled = active;
        }

        private void EnsureComponents()
        {
            // Never use ?? with UnityEngine.Object — fake-null bypasses coalescing
            // and MeshFilter/MeshRenderer setters then throw MissingComponentException.
            if (meshFilter == null)
            {
                meshFilter = gameObject.GetComponent<MeshFilter>();
                if (meshFilter == null)
                    meshFilter = gameObject.AddComponent<MeshFilter>();
            }
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                    meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }
        }
    }

    [Serializable]
    public sealed class GfxLightningParamsSerial
    {
        public string SystemId = "150";
        public string Name = string.Empty;
        public int ElementIndex;
        public string TexFile = string.Empty;
        public int DestBlend = 6;
        public bool ZEnable = true;
        public bool Warp;
        public int BufLen = 256;
        public float Amplitude = 1f;
        public float WaveLen = 1f;
        public float Persistence = 0.5f;
        public int OctaveNum = 1;
        public float[] StartPos = new float[3];
        public float[] EndPos = new float[3];
        public int Segs;
        public int LightNum = 1;
        public float WaveLen2 = 1f;
        public int Interval = 100;
        public float Width;
        public float Width2;
        public float Width3 = float.NaN;
        public float Alpha = 1f;
        public float Alpha2 = 1f;
        public float Alpha3 = float.NaN;
        public float Amplitude2;
        public bool Pos1Enable;
        public bool Pos2Enable;
        public bool UseNormal;
        public float[] Normal = new float[3];
        public int Filter = 1;
        public bool HasDeviation;
        public float Deviation;
        public bool WaveMoving;
        public float WaveMovingSpeed = 1f;
        public bool FixWaveLength;
        public float NumWaves = 5f;
        public bool HasAmpCurve;
        public int AmpDestNum;
        public float AmpStartTime;
        public float[] AmpValues = new float[6];
        public float[] AmpTimes = new float[5];
        public float ElementStartTime;
        public bool IsAppend = true;
        public int RenderSide;
        public bool IsTailDisappear = true;
        public float VertsLife = 1000f;
        public bool TailFadeOut;
        public bool ResetLoopEnd;
        public int RandomSeed;
        public bool Curved;
        public float CurveLen;
        public float Kp0TimeSpan;
        public int TexRow = 1;
        public int TexCol = 1;
        public float TexInterval = 50f;
        public bool TileMode;
        public float TexSpeed1;
        public float TexSpeed2;
        public bool UReverse;
        public bool VReverse;
        public bool UvExchange;

        public static GfxLightningParamsSerial FromParams(GfxLightningParams p)
        {
            if (p == null) return new GfxLightningParamsSerial();
            return new GfxLightningParamsSerial
            {
                SystemId = p.SystemId,
                Name = p.Name,
                ElementIndex = p.ElementIndex,
                TexFile = p.TexFile,
                DestBlend = p.DestBlend,
                ZEnable = p.ZEnable,
                Warp = p.Warp,
                BufLen = p.BufLen,
                Amplitude = p.Amplitude,
                WaveLen = p.WaveLen,
                Persistence = p.Persistence,
                OctaveNum = p.OctaveNum,
                StartPos = GfxMath.Clone3(p.StartPos),
                EndPos = GfxMath.Clone3(p.EndPos),
                Segs = p.Segs,
                LightNum = p.LightNum,
                WaveLen2 = p.WaveLen2,
                Interval = p.Interval,
                Width = p.Width,
                Width2 = p.Width2,
                Width3 = p.Width3,
                Alpha = p.Alpha,
                Alpha2 = p.Alpha2,
                Alpha3 = p.Alpha3,
                Amplitude2 = p.Amplitude2,
                Pos1Enable = p.Pos1Enable,
                Pos2Enable = p.Pos2Enable,
                UseNormal = p.UseNormal,
                Normal = GfxMath.Clone3(p.Normal),
                Filter = p.Filter,
                HasDeviation = p.HasDeviation,
                Deviation = p.Deviation,
                WaveMoving = p.WaveMoving,
                WaveMovingSpeed = p.WaveMovingSpeed,
                FixWaveLength = p.FixWaveLength,
                NumWaves = p.NumWaves,
                HasAmpCurve = p.HasAmpCurve,
                AmpDestNum = p.AmpDestNum,
                AmpStartTime = p.AmpStartTime,
                AmpValues = (float[])p.AmpValues.Clone(),
                AmpTimes = (float[])p.AmpTimes.Clone(),
                ElementStartTime = p.ElementStartTime,
                IsAppend = p.IsAppend,
                RenderSide = p.RenderSide,
                IsTailDisappear = p.IsTailDisappear,
                VertsLife = p.VertsLife,
                TailFadeOut = p.TailFadeOut,
                ResetLoopEnd = p.ResetLoopEnd,
                RandomSeed = p.RandomSeed,
                Curved = p.Curved,
                CurveLen = p.CurveLen,
                Kp0TimeSpan = p.Kp0TimeSpan,
                TexRow = p.TexRow,
                TexCol = p.TexCol,
                TexInterval = p.TexInterval,
                TileMode = p.TileMode,
                TexSpeed1 = p.TexSpeed1,
                TexSpeed2 = p.TexSpeed2,
                UReverse = p.UReverse,
                VReverse = p.VReverse,
                UvExchange = p.UvExchange
            };
        }

        public GfxLightningParams ToParams()
        {
            return new GfxLightningParams
            {
                SystemId = SystemId,
                Name = Name,
                ElementIndex = ElementIndex,
                TexFile = TexFile,
                DestBlend = DestBlend,
                ZEnable = ZEnable,
                Warp = Warp,
                BufLen = BufLen,
                Amplitude = Amplitude,
                WaveLen = WaveLen,
                Persistence = Persistence,
                OctaveNum = OctaveNum,
                StartPos = GfxMath.Clone3(StartPos),
                EndPos = GfxMath.Clone3(EndPos),
                Segs = Segs,
                LightNum = LightNum,
                WaveLen2 = WaveLen2,
                Interval = Interval,
                Width = Width,
                Width2 = Width2,
                Width3 = Width3,
                Alpha = Alpha,
                Alpha2 = Alpha2,
                Alpha3 = Alpha3,
                Amplitude2 = Amplitude2,
                Pos1Enable = Pos1Enable,
                Pos2Enable = Pos2Enable,
                UseNormal = UseNormal,
                Normal = GfxMath.Clone3(Normal),
                Filter = Filter,
                HasDeviation = HasDeviation,
                Deviation = Deviation,
                WaveMoving = WaveMoving,
                WaveMovingSpeed = WaveMovingSpeed,
                FixWaveLength = FixWaveLength,
                NumWaves = NumWaves,
                HasAmpCurve = HasAmpCurve,
                AmpDestNum = AmpDestNum,
                AmpStartTime = AmpStartTime,
                AmpValues = AmpValues != null ? (float[])AmpValues.Clone() : new float[6],
                AmpTimes = AmpTimes != null ? (float[])AmpTimes.Clone() : new float[5],
                ElementStartTime = ElementStartTime,
                IsAppend = IsAppend,
                RenderSide = RenderSide,
                IsTailDisappear = IsTailDisappear,
                VertsLife = VertsLife,
                TailFadeOut = TailFadeOut,
                ResetLoopEnd = ResetLoopEnd,
                RandomSeed = RandomSeed,
                Curved = Curved,
                CurveLen = CurveLen,
                Kp0TimeSpan = Kp0TimeSpan,
                TexRow = TexRow,
                TexCol = TexCol,
                TexInterval = TexInterval,
                TileMode = TileMode,
                TexSpeed1 = TexSpeed1,
                TexSpeed2 = TexSpeed2,
                UReverse = UReverse,
                VReverse = VReverse,
                UvExchange = UvExchange
            };
        }
    }
}
