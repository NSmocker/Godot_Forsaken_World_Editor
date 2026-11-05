using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// CPU lightning trail for GFX ID 180 (create_id_180.gd / gfx_ltntrail_180.gd).
    /// Bind=1: strip in GFX space. Bind=0: ring-buffer of parent world poses (Hist_*).
    /// </summary>
    [ExecuteAlways]
    public sealed class GfxLtnTrailDriver : GfxElementDriver
    {
        [SerializeField] private GfxLtnTrailParamsSerial paramsSerial =
            new GfxLtnTrailParamsSerial();
        [SerializeField] private bool visualActive = true;
        [SerializeField] private bool warpUnsupportedLogged;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Material runtimeMaterial;
        private Mesh runtimeMesh;
        private GfxLtnTrailParams cachedParams;
        private bool header2dRender;

        // Bind=0 history (gfx_ltntrail_180.gd)
        private float[][] histOrigins;
        private float[][] histRots;
        private float[] histScales;
        private float writtenTick = float.PositiveInfinity;
        private float cycle = -1f;
        private float prevTime = float.NegativeInfinity;
        private Vector3 prevOrigin;
        private Quaternion prevRot = Quaternion.identity;
        private float prevScale = 1f;
        private bool hasPrev;

        public GfxLtnTrailParams Params =>
            cachedParams ?? (cachedParams = paramsSerial.ToParams());

        public void Configure(
            GfxLtnTrailParams trailParams,
            Texture texture,
            bool gfx2dRender,
            IList<string> warnings)
        {
            if (trailParams == null)
                throw new ArgumentNullException(nameof(trailParams));

            cachedParams = trailParams;
            paramsSerial = GfxLtnTrailParamsSerial.FromParams(trailParams);
            header2dRender = gfx2dRender;
            SystemId = trailParams.SystemId;
            EnsureComponents();
            EnsureHistory(trailParams.HistorySlots);
            runtimeMaterial = GfxTrailMaterialFactory.Create(
                string.IsNullOrEmpty(trailParams.Name)
                    ? "LtnTrail_" + trailParams.SystemId
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
                warnings.Add(
                    "GFX ID 180 Warp heat-distortion absent in Godot too; blend forced to mix.");
            }

            if (texture == null && warnings != null &&
                !string.IsNullOrEmpty(trailParams.TexFile))
            {
                warnings.Add("GFX ID 180 TexFile not bound: " + trailParams.TexFile);
            }

            // EnableMat/EnablePos cannot be remade without game transforms (Godot note).
            if (warnings != null && (trailParams.EnableMat || trailParams.EnablePos ||
                                     trailParams.EnablePos2))
            {
                warnings.Add(
                    "GFX ID 180 EnableMat/EnablePos follow keypoints only " +
                    "(Pos/Pos_2 from file); game-side live mats not available.");
            }
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

            var p = Params;
            float[] curO;
            float[] curR;
            float curS;
            UpdateHistory(active, out curO, out curR, out curS);

            Func<float, float[]> histO = null;
            Func<float, float[]> histR = null;
            Func<float, float> histS = null;
            if (!p.Bind)
            {
                histO = k =>
                {
                    var slot = (int)GfxMath.Fmod(k, histOrigins.Length);
                    if (slot < 0) slot += histOrigins.Length;
                    return histOrigins[slot];
                };
                histR = k =>
                {
                    var slot = (int)GfxMath.Fmod(k, histRots.Length);
                    if (slot < 0) slot += histRots.Length;
                    return histRots[slot];
                };
                histS = k =>
                {
                    var slot = (int)GfxMath.Fmod(k, histScales.Length);
                    if (slot < 0) slot += histScales.Length;
                    return histScales[slot];
                };
            }

            var data = GfxLtnTrailEval.Build(
                p, GetKeypoints(),
                LocalStartTime, LocalTotalTime, GlobalLoopTime,
                RepeatCount, RepeatDelay, IsEndless,
                active, alpha,
                histO, histR, histS,
                curO, curR, curS);
            ApplyMesh(data);

            if (runtimeMaterial != null && runtimeMaterial.HasProperty("_GfxAlpha"))
                runtimeMaterial.SetFloat("_GfxAlpha", alpha);
        }

        protected override void ApplyState(GfxKeypointState state)
        {
            // Lightning trail rebuilds mesh in OnGfxTick.
        }

        private void UpdateHistory(
            float activeTime, out float[] curO, out float[] curR, out float curS)
        {
            var xf = transform;
            // Prefer world pose when available; harness Transform may only expose local.
            var origin = xf.position;
            var rot = xf.rotation;
            var scale = AverageScale(xf.lossyScale);
            curO = new[] { origin.x, origin.y, origin.z };
            curR = new[] { rot.x, rot.y, rot.z, rot.w };
            curS = scale;

            var p = Params;
            if (p.Bind)
                return;

            EnsureHistory(p.HistorySlots);
            var g = GfxLtnTrailEval.BuildGrid(
                activeTime, LocalStartTime, LocalTotalTime, GlobalLoopTime,
                RepeatCount, RepeatDelay, IsEndless, p.TickMs);
            var now = g.Now;

            if (g.Cycle != cycle || activeTime < prevTime || now < writtenTick ||
                now - writtenTick > histOrigins.Length)
            {
                for (var k = 0; k < histOrigins.Length; k++)
                    Store(k, curO, curR, curS);
                writtenTick = now;
            }
            else
            {
                var k = writtenTick + 1f;
                while (k <= now)
                {
                    var tK = activeTime - (g.TauSinceStart - GfxLtnTrailEval.TickTime(
                        k, g.J, g.Full, g.T));
                    var f = 1f;
                    if (activeTime > prevTime && hasPrev)
                        f = GfxMath.Clamp((tK - prevTime) / (activeTime - prevTime), 0f, 1f);
                    var o = hasPrev
                        ? GfxMath.Lerp3(
                            new[] { prevOrigin.x, prevOrigin.y, prevOrigin.z }, curO, f)
                        : curO;
                    var r = hasPrev
                        ? GfxMath.SlerpQuat(
                            new[] { prevRot.x, prevRot.y, prevRot.z, prevRot.w }, curR, f)
                        : curR;
                    var s = hasPrev ? GfxMath.Lerp(prevScale, curS, f) : curS;
                    Store(k, o, r, s);
                    k += 1f;
                }

                writtenTick = now;
            }

            Store(now + 1f, curO, curR, curS);
            Store(now + 2f, curO, curR, curS);
            cycle = g.Cycle;
            prevTime = activeTime;
            prevOrigin = origin;
            prevRot = rot;
            prevScale = curS;
            hasPrev = true;
        }

        private void Store(float k, float[] o, float[] r, float s)
        {
            var slot = (int)GfxMath.Fmod(k, histOrigins.Length);
            if (slot < 0)
                slot += histOrigins.Length;
            histOrigins[slot] = GfxMath.Clone3(o);
            histRots[slot] = GfxMath.Clone4(r);
            histScales[slot] = s;
        }

        private void EnsureHistory(int slots)
        {
            if (slots < 1)
                slots = 1;
            if (histOrigins != null && histOrigins.Length == slots)
                return;
            histOrigins = new float[slots][];
            histRots = new float[slots][];
            histScales = new float[slots];
            for (var i = 0; i < slots; i++)
            {
                histOrigins[i] = GfxMath.Vec3Zero();
                histRots[i] = GfxMath.QuatIdentity();
                histScales[i] = 1f;
            }

            writtenTick = float.PositiveInfinity;
        }

        private static float AverageScale(Vector3 s) =>
            (Math.Abs(s.x) + Math.Abs(s.y) + Math.Abs(s.z)) / 3f;

        private void ApplyMesh(GfxLtnTrailEval.MeshData data)
        {
            EnsureComponents();
            if (runtimeMesh == null)
            {
                runtimeMesh = new Mesh { name = "GfxLtnTrail" };
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
    public sealed class GfxLtnTrailParamsSerial
    {
        public string SystemId = "180";
        public string Name = string.Empty;
        public string TexFile = string.Empty;
        public Vector3 Pos;
        public Vector3 Pos2;
        public int SegLife = 1000;
        public float Amp = 0.05f;
        public float Amp2 = 0.08f;
        public bool Bind = true;
        public bool EnableMat;
        public bool EnablePos;
        public bool EnablePos2;
        public bool Warp;
        public bool ZEnable = true;
        public int Priority;
        public int SrcBlend;
        public int DestBlend = 6;
        public int ElementIndex = 1;
        public float TickMs = 40f;
        public int RandomSeed;

        public GfxLtnTrailParams ToParams()
        {
            return new GfxLtnTrailParams
            {
                SystemId = SystemId,
                Name = Name,
                TexFile = TexFile,
                Pos = new[] { Pos.x, Pos.y, Pos.z },
                Pos2 = new[] { Pos2.x, Pos2.y, Pos2.z },
                SegLife = SegLife,
                Amp = Amp,
                Amp2 = Amp2,
                Bind = Bind,
                EnableMat = EnableMat,
                EnablePos = EnablePos,
                EnablePos2 = EnablePos2,
                Warp = Warp,
                ZEnable = ZEnable,
                Priority = Priority,
                SrcBlend = SrcBlend,
                DestBlend = DestBlend,
                ElementIndex = ElementIndex,
                TickMs = TickMs,
                RandomSeed = RandomSeed
            };
        }

        public static GfxLtnTrailParamsSerial FromParams(GfxLtnTrailParams p)
        {
            if (p == null)
                return new GfxLtnTrailParamsSerial();
            var a = p.Pos ?? GfxMath.Vec3Zero();
            var b = p.Pos2 ?? GfxMath.Vec3Zero();
            return new GfxLtnTrailParamsSerial
            {
                SystemId = p.SystemId,
                Name = p.Name,
                TexFile = p.TexFile,
                Pos = new Vector3(a[0], a[1], a[2]),
                Pos2 = new Vector3(b[0], b[1], b[2]),
                SegLife = p.SegLife,
                Amp = p.Amp,
                Amp2 = p.Amp2,
                Bind = p.Bind,
                EnableMat = p.EnableMat,
                EnablePos = p.EnablePos,
                EnablePos2 = p.EnablePos2,
                Warp = p.Warp,
                ZEnable = p.ZEnable,
                Priority = p.Priority,
                SrcBlend = p.SrcBlend,
                DestBlend = p.DestBlend,
                ElementIndex = p.ElementIndex,
                TickMs = p.TickMs,
                RandomSeed = p.RandomSeed
            };
        }
    }
}
