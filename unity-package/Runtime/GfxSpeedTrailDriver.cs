using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// CPU SpeedTrail element for GFX ID 110 (create_id_110.gd).
    /// Rebuilds a ribbon mesh each tick from GfxKeypointEval samples.
    /// </summary>
    [ExecuteAlways]
    public sealed class GfxSpeedTrailDriver : GfxElementDriver
    {
        [SerializeField] private GfxSpeedTrailParamsSerial paramsSerial =
            new GfxSpeedTrailParamsSerial();
        [SerializeField] private bool visualActive = true;
        [SerializeField] private bool warpUnsupportedLogged;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Material runtimeMaterial;
        private Mesh runtimeMesh;
        private GfxSpeedTrailParams cachedParams;
        private bool header2dRender;

        public GfxSpeedTrailParams Params =>
            cachedParams ?? (cachedParams = paramsSerial.ToParams());

        public void Configure(
            GfxSpeedTrailParams trailParams,
            Texture texture,
            bool gfx2dRender,
            IList<string> warnings)
        {
            if (trailParams == null)
                throw new ArgumentNullException(nameof(trailParams));

            cachedParams = trailParams;
            paramsSerial = GfxSpeedTrailParamsSerial.FromParams(trailParams);
            header2dRender = gfx2dRender;
            SystemId = trailParams.SystemId;
            EnsureComponents();
            runtimeMaterial = GfxTrailMaterialFactory.Create(
                string.IsNullOrEmpty(trailParams.Name)
                    ? "SpeedTrail_" + trailParams.SystemId
                    : trailParams.Name,
                trailParams.IsAdditiveBlend,
                trailParams.ZEnable,
                gfx2dRender,
                texture);
            meshRenderer.sharedMaterial = runtimeMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            if (trailParams.Warp && warnings != null && !warpUnsupportedLogged)
            {
                warpUnsupportedLogged = true;
                // create_id_110.gd adds screen_texture but fragment ignores Warp ("komplett ignoriert").
                warnings.Add(
                    "GFX ID 110 Warp is ignored in Godot fragment too; blend forced to mix.");
            }

            if (texture == null && warnings != null &&
                !string.IsNullOrEmpty(trailParams.TexFile))
            {
                warnings.Add("GFX ID 110 TexFile not bound: " + trailParams.TexFile);
            }

            // Seed empty mesh so hierarchy shows a MeshFilter immediately.
            ApplyMesh(GfxSpeedTrailRibbon.Build(
                trailParams, GetKeypoints(), LocalStartTime, LocalTotalTime,
                LocalStartTime, 1f));
        }

        public override void OnGfxTick(float gfxTimeMs, float alpha)
        {
            GfxAlpha = alpha;
            if (TimeManual)
                TimeMs = gfxTimeMs;

            var active = TimeManual ? TimeMs : gfxTimeMs;
            var elementTime = GfxKeypointEval.ElementTime(
                active, GlobalLoopTime, LocalStartTime, LocalTotalTime,
                RepeatCount, RepeatDelay);

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

            var data = GfxSpeedTrailRibbon.Build(
                Params, GetKeypoints(), LocalStartTime, LocalTotalTime,
                elementTime, alpha);
            ApplyMesh(data);

            if (runtimeMaterial != null && runtimeMaterial.HasProperty("_GfxAlpha"))
                runtimeMaterial.SetFloat("_GfxAlpha", alpha);

            // Priority Z-fighting nudge like create_id_110 face_dir * index * 0.001
            var kps = GetKeypoints();
            float[] dir = GfxMath.QuatIdentity();
            if (kps.Count > 0)
                dir = kps[0].Dir ?? dir;
            var face = GfxMath.RotateVec3(dir, GfxMath.Vec3(0f, 0f, 1f));
            if (GfxMath.Length3(face) < GfxMath.Epsilon)
                face = GfxMath.Vec3(0f, 0f, 1f);
            else
                face = GfxMath.Normalize3(face);
            var zOff = GfxMath.Scale3(face, Params.ElementIndex * 0.001f);
            transform.localPosition = new Vector3(zOff[0], zOff[1], zOff[2]);
        }

        protected override void ApplyState(GfxKeypointState state)
        {
            // Trail path rebuilds mesh in OnGfxTick.
        }

        private void ApplyMesh(GfxSpeedTrailRibbon.MeshData data)
        {
            EnsureComponents();
            if (runtimeMesh == null)
            {
                runtimeMesh = new Mesh { name = "GfxSpeedTrail" };
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
            if (meshFilter == null)
                meshFilter = gameObject.GetComponent<MeshFilter>() ??
                             gameObject.AddComponent<MeshFilter>();
            if (meshRenderer == null)
                meshRenderer = gameObject.GetComponent<MeshRenderer>() ??
                               gameObject.AddComponent<MeshRenderer>();
        }
    }

    [Serializable]
    public sealed class GfxSpeedTrailParamsSerial
    {
        public string SystemId = "110";
        public string Name = string.Empty;
        public string TexFile = string.Empty;
        public float Width = 2f;
        public float Height = 0.5f;
        public Vector3 OrgPos1;
        public Vector3 OrgPos2;
        public int SegLife = 1000;
        public float FadeLength = 0.15f;
        public float SpeedVel = 1f;
        public bool Bind = true;
        public bool Spline;
        public int TexRow = 1;
        public int TexCol = 1;
        public float TexInterval = 50f;
        public float TexSpeed1;
        public float TexSpeed2;
        public bool TileMode;
        public bool UReverse;
        public bool VReverse;
        public bool UvExchange;
        public bool Warp;
        public bool ZEnable = true;
        public int Priority;
        public int SrcBlend;
        public int DestBlend = 6;
        public int ElementIndex = 1;
        public int SegmentCount = 50;

        public GfxSpeedTrailParams ToParams()
        {
            return new GfxSpeedTrailParams
            {
                SystemId = SystemId,
                Name = Name,
                TexFile = TexFile,
                Width = Width,
                Height = Height,
                OrgPos1 = new[] { OrgPos1.x, OrgPos1.y, OrgPos1.z },
                OrgPos2 = new[] { OrgPos2.x, OrgPos2.y, OrgPos2.z },
                SegLife = SegLife,
                FadeLength = FadeLength,
                SpeedVel = SpeedVel,
                Bind = Bind,
                Spline = Spline,
                TexRow = TexRow,
                TexCol = TexCol,
                TexInterval = TexInterval,
                TexSpeed1 = TexSpeed1,
                TexSpeed2 = TexSpeed2,
                TileMode = TileMode,
                UReverse = UReverse,
                VReverse = VReverse,
                UvExchange = UvExchange,
                Warp = Warp,
                ZEnable = ZEnable,
                Priority = Priority,
                SrcBlend = SrcBlend,
                DestBlend = DestBlend,
                ElementIndex = ElementIndex,
                SegmentCount = SegmentCount
            };
        }

        public static GfxSpeedTrailParamsSerial FromParams(GfxSpeedTrailParams p)
        {
            if (p == null)
                return new GfxSpeedTrailParamsSerial();
            var o1 = p.OrgPos1 ?? GfxMath.Vec3Zero();
            var o2 = p.OrgPos2 ?? GfxMath.Vec3Zero();
            return new GfxSpeedTrailParamsSerial
            {
                SystemId = p.SystemId,
                Name = p.Name,
                TexFile = p.TexFile,
                Width = p.Width,
                Height = p.Height,
                OrgPos1 = new Vector3(o1[0], o1[1], o1[2]),
                OrgPos2 = new Vector3(o2[0], o2[1], o2[2]),
                SegLife = p.SegLife,
                FadeLength = p.FadeLength,
                SpeedVel = p.SpeedVel,
                Bind = p.Bind,
                Spline = p.Spline,
                TexRow = p.TexRow,
                TexCol = p.TexCol,
                TexInterval = p.TexInterval,
                TexSpeed1 = p.TexSpeed1,
                TexSpeed2 = p.TexSpeed2,
                TileMode = p.TileMode,
                UReverse = p.UReverse,
                VReverse = p.VReverse,
                UvExchange = p.UvExchange,
                Warp = p.Warp,
                ZEnable = p.ZEnable,
                Priority = p.Priority,
                SrcBlend = p.SrcBlend,
                DestBlend = p.DestBlend,
                ElementIndex = p.ElementIndex,
                SegmentCount = p.SegmentCount
            };
        }
    }
}
