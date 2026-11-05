using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// GFX ID 140 ring (A3DGFXRing) and 190 paraboloid (A3DParaboloid).
    /// CPU mesh via GfxRingMesh; renders with AngelicaGfxSprite (URP) + TexFile.
    /// </summary>
    [ExecuteAlways]
    public sealed class GfxRingDriver : GfxElementDriver
    {
        [SerializeField] private GfxRingParamsSerial paramsSerial = new GfxRingParamsSerial();
        [SerializeField] private bool visualActive = true;
        [SerializeField] private bool warpUnsupportedLogged;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh runtimeMesh;
        private Material runtimeMaterial;
        private GfxRingParams cachedParams;

        public GfxRingParams Params =>
            cachedParams ?? (cachedParams = paramsSerial.ToParams());

        public void Configure(
            GfxRingParams ringParams,
            Texture texture,
            bool gfx2dRender,
            IList<string> warnings)
        {
            if (ringParams == null)
                throw new ArgumentNullException(nameof(ringParams));

            cachedParams = ringParams;
            paramsSerial = GfxRingParamsSerial.FromParams(ringParams);
            SystemId = ringParams.SystemId;

            EnsureComponents();
            // AngelicaGfxSprite preferred (factory falls back via Shader.Find chain).
            runtimeMaterial = GfxTrailMaterialFactory.Create(
                string.IsNullOrEmpty(ringParams.Name)
                    ? "Ring_" + ringParams.SystemId
                    : ringParams.Name,
                ringParams.IsAdditiveBlend,
                ringParams.ZEnable,
                gfx2dRender,
                texture);

            if (runtimeMaterial.HasProperty("_MixAlphaScale"))
                runtimeMaterial.SetFloat("_MixAlphaScale", ringParams.MixAlphaScale);
            meshRenderer.sharedMaterial = runtimeMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            if (ringParams.Warp)
            {
                if (!GfxWarpMaterial.Apply(runtimeMaterial, true) &&
                    warnings != null && !warpUnsupportedLogged)
                {
                    warpUnsupportedLogged = true;
                    warnings.Add(
                        "GFX ID " + ringParams.SystemId +
                        " Warp requested but material has no _Warp (fallback shader); " +
                        "heat-distortion skipped, blend forced to mix.");
                }
            }

            if (texture == null && warnings != null &&
                !string.IsNullOrEmpty(ringParams.TexFile))
            {
                warnings.Add(
                    "GFX ID " + ringParams.SystemId +
                    " TexFile not bound: " + ringParams.TexFile);
            }

            // Seed mesh at first keypoint / identity so the editor sees geometry.
            ApplyMesh(GfxRingMesh.Build(
                ringParams,
                new GfxKeypointState
                {
                    Pos = GfxMath.Vec3Zero(),
                    Dir = GfxMath.QuatIdentity(),
                    Color = GfxMath.ColorWhite(),
                    Scale = 1f
                },
                LocalStartTime,
                1f));
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

            ApplyMesh(GfxRingMesh.Build(Params, state, elementTime, alpha));

            // Color/alpha baked into vertex colors (AngelicaGfxTrail); keep material multipliers neutral.
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
            // Ring/paraboloid rebuilds mesh in OnGfxTick.
        }

        private void ApplyMesh(GfxRingMesh.MeshData data)
        {
            EnsureComponents();
            if (runtimeMesh == null)
            {
                runtimeMesh = new Mesh
                {
                    name = Params.IsParaboloid ? "GfxParaboloid" : "GfxRing"
                };
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
    public sealed class GfxRingParamsSerial
    {
        public string SystemId = "140";
        public string Name = string.Empty;
        public int ElementIndex;
        public string TexFile = string.Empty;
        public int DestBlend = 6;
        public bool ZEnable = true;
        public bool Warp;
        public int Priority;
        public int TexRow = 1;
        public int TexCol = 1;
        public float TexInterval = 50f;
        public bool TileMode;
        public float TexSpeed1;
        public float TexSpeed2;
        public float Radius = 1f;
        public float Height;
        public float Pitch;
        public int Sects;
        public bool NoRadScale;
        public bool NoHeiScale;
        public bool OrgAtCenter = true;
        public float Coeff = 1f;
        public float ParaboloidHeight;

        public static GfxRingParamsSerial FromParams(GfxRingParams p)
        {
            if (p == null)
                return new GfxRingParamsSerial();
            return new GfxRingParamsSerial
            {
                SystemId = p.SystemId,
                Name = p.Name,
                ElementIndex = p.ElementIndex,
                TexFile = p.TexFile,
                DestBlend = p.DestBlend,
                ZEnable = p.ZEnable,
                Warp = p.Warp,
                Priority = p.Priority,
                TexRow = p.TexRow,
                TexCol = p.TexCol,
                TexInterval = p.TexInterval,
                TileMode = p.TileMode,
                TexSpeed1 = p.TexSpeed1,
                TexSpeed2 = p.TexSpeed2,
                Radius = p.Radius,
                Height = p.Height,
                Pitch = p.Pitch,
                Sects = p.Sects,
                NoRadScale = p.NoRadScale,
                NoHeiScale = p.NoHeiScale,
                OrgAtCenter = p.OrgAtCenter,
                Coeff = p.Coeff,
                ParaboloidHeight = p.ParaboloidHeight
            };
        }

        public GfxRingParams ToParams()
        {
            return new GfxRingParams
            {
                SystemId = SystemId,
                Name = Name,
                ElementIndex = ElementIndex,
                TexFile = TexFile,
                DestBlend = DestBlend,
                ZEnable = ZEnable,
                Warp = Warp,
                Priority = Priority,
                TexRow = TexRow,
                TexCol = TexCol,
                TexInterval = TexInterval,
                TileMode = TileMode,
                TexSpeed1 = TexSpeed1,
                TexSpeed2 = TexSpeed2,
                Radius = Radius,
                Height = Height,
                Pitch = Pitch,
                Sects = Sects,
                NoRadScale = NoRadScale,
                NoHeiScale = NoHeiScale,
                OrgAtCenter = OrgAtCenter,
                Coeff = Coeff,
                ParaboloidHeight = ParaboloidHeight
            };
        }
    }
}
