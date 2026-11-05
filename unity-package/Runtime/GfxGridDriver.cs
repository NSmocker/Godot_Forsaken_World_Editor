using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// GFX ID 210 grid decal 3D / 211 grid decal 2D (create_id_210 / create_id_211).
    /// CPU mesh via GfxGridMesh; grid animation + color compose via GfxGridEval.
    /// 210: KP transform without scale (AffByScl multiplies); RotFromView approximated
    /// as camera-facing basis when Camera.main exists.
    /// 211: screen-parallel local XY placement (full clip-space Fill_Verts_2D needs a
    /// custom shader — not ported; points stay in local XY with Rad_2D).
    /// MatchGrnd / AlwaysOnGround not ported (needs terrain).
    /// </summary>
    [ExecuteAlways]
    public sealed class GfxGridDriver : GfxElementDriver
    {
        [SerializeField] private bool visualActive = true;
        [SerializeField] private bool is2D;
        [SerializeField] private string texFile = string.Empty;
        [SerializeField] private bool affByScl;
        [SerializeField] private bool rotFromView;
        [SerializeField] private int gridW;
        [SerializeField] private int gridH;

        private GfxGridParams cachedParams;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh runtimeMesh;
        private Material runtimeMaterial;

        public GfxGridParams Params => cachedParams;
        public Mesh RuntimeMesh => runtimeMesh;
        public bool Is2D => is2D;

        public void Configure(
            GfxGridParams gridParams,
            Texture texture,
            bool gfx2dRender,
            IList<string> warnings)
        {
            if (gridParams == null)
                throw new ArgumentNullException(nameof(gridParams));

            cachedParams = gridParams;
            is2D = gridParams.Is2D;
            texFile = gridParams.TexFile ?? string.Empty;
            affByScl = gridParams.AffByScl;
            rotFromView = gridParams.RotFromView;
            gridW = gridParams.W;
            gridH = gridParams.H;
            SystemId = gridParams.SystemId;

            EnsureComponents();
            runtimeMaterial = GfxTrailMaterialFactory.Create(
                is2D ? "GridDecal2D" : "GridDecal",
                additive: false,
                zEnable: !gfx2dRender,
                gfx2dRender,
                texture);
            meshRenderer.sharedMaterial = runtimeMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            if (texture == null && warnings != null && !string.IsNullOrEmpty(texFile))
            {
                warnings.Add(
                    "GFX ID " + gridParams.SystemId +
                    " TexFile not bound: " + texFile);
            }

            if (is2D && warnings != null)
            {
                warnings.Add(
                    "GFX ID 211: Fill_Verts_2D clip-space path approximated in local XY " +
                    "(custom POSITION shader not ported).");
            }

            var built = GfxGridMesh.Build(gridParams, gridParams.Positions, gridParams.Colors);
            ApplyMesh(built.Mesh);
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
            if (!windowActive || keypoints == null || keypoints.Count == 0 ||
                cachedParams == null || cachedParams.W < 2 || cachedParams.H < 2)
            {
                SetVisualActive(false);
                return;
            }

            var state = GfxKeypointEval.Evaluate(
                keypoints, Math.Max(0f, elementTime - LocalStartTime));
            var sample = GfxGridEval.Evaluate(
                cachedParams,
                Math.Max(0f, elementTime - LocalStartTime),
                state.Color);
            if (sample.Hidden)
            {
                SetVisualActive(false);
                return;
            }

            var built = GfxGridMesh.Build(cachedParams, sample.Positions, sample.Colors);
            ApplyMesh(built.Mesh);
            ApplyTransform(state);
            SetVisualActive(true);
        }

        private void ApplyTransform(GfxKeypointState state)
        {
            var pos = state.Pos ?? GfxMath.Vec3Zero();
            var dir = state.Dir ?? GfxMath.QuatIdentity();
            if (!is2D && Math.Abs(state.Rad) > 0.000001f)
                dir = GfxMath.MulQuat(dir, GfxMath.QuatFromAxisAngle(GfxMath.Vec3(0f, 0f, 1f), state.Rad));

            if (!is2D && rotFromView)
            {
                // Approximate RotFromView: face camera when available.
                var cam = Camera.main;
                if (cam != null && cam.transform != null)
                {
                    var toCam = GfxMath.Sub3(
                        new[] { cam.transform.position.x, cam.transform.position.y, cam.transform.position.z },
                        pos);
                    if (GfxMath.Length3(toCam) > 0.0001f)
                    {
                        var z = GfxMath.Normalize3(toCam);
                        var up = GfxMath.Vec3(0f, 1f, 0f);
                        var x = GfxMath.Normalize3(GfxMath.Cross3(up, z));
                        if (GfxMath.Length3(x) < 0.0001f)
                            x = GfxMath.Vec3(1f, 0f, 0f);
                        var y = GfxMath.Cross3(z, x);
                        dir = GfxMath.QuatFromAxes(x, y, z);
                    }
                }
            }

            if (is2D)
            {
                // Local XY plane + Rad_2D about Z (screen-parallel approximation).
                if (Math.Abs(state.Rad) > 0.000001f)
                    dir = GfxMath.MulQuat(
                        GfxMath.QuatIdentity(),
                        GfxMath.QuatFromAxisAngle(GfxMath.Vec3(0f, 0f, 1f), state.Rad));
                else
                    dir = GfxMath.QuatIdentity();
            }

            transform.localPosition = new Vector3(pos[0], pos[1], pos[2]);
            transform.localRotation = new Quaternion(dir[0], dir[1], dir[2], dir[3]);
            var s = (!is2D && affByScl) ? state.Scale : 1f;
            if (Math.Abs(s) < 0.000001f)
                s = 0.000001f;
            transform.localScale = new Vector3(s, s, s);
        }

        private void ApplyMesh(Mesh mesh)
        {
            if (runtimeMesh != null && !ReferenceEquals(runtimeMesh, mesh))
                UnityEngine.Object.DestroyImmediate(runtimeMesh);
            runtimeMesh = mesh;
            if (meshFilter != null)
                meshFilter.sharedMesh = runtimeMesh;
        }

        private void EnsureComponents()
        {
            meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        private void SetVisualActive(bool active)
        {
            visualActive = active;
            if (meshRenderer != null)
                meshRenderer.enabled = active;
        }
    }
}
