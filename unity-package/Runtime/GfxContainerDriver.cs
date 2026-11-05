using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// GFX ID 200 container (create_id_200.gd / gfx_container_200.gd).
    /// Hosts a nested Sub_GFX hierarchy and drives its clock/transform/alpha.
    /// Dummy containers stay invisible (DummyEle copies: Godot gfx_dummy_particles; Unity pending).
    /// OuterColor tint not applied (absent in Godot too: gfx_container_200.gd).
    /// </summary>
    [ExecuteAlways]
    public sealed class GfxContainerDriver : GfxElementDriver
    {
        public const string SubGfxNodeName = "Sub_GFX";
        public const float NoLoopMs = 1000000000f;

        [SerializeField] private GfxContainerParamsSerial paramsSerial = new GfxContainerParamsSerial();
        [SerializeField] private float subGfxSpeed = 1f;
        [SerializeField] private float subGfxScale = 1f;
        [SerializeField] private bool visualActive = true;

        private GfxContainerParams cachedParams;
        private Transform subGfx;
        private readonly List<GfxElementDriver> subDrivers = new List<GfxElementDriver>();
        private readonly List<Renderer> geometries = new List<Renderer>();
        private float lastAlpha = -1f;

        public GfxContainerParams Params =>
            cachedParams ?? (cachedParams = paramsSerial.ToParams());

        public float SubGfxSpeed => subGfxSpeed;
        public float SubGfxScale => subGfxScale;
        public Transform SubGfx => subGfx;

        public void Configure(
            GfxContainerParams containerParams,
            GameObject subGfxRoot,
            float nestedPlaySpeed,
            float nestedDefaultScale,
            IList<string> warnings)
        {
            if (containerParams == null)
                throw new ArgumentNullException(nameof(containerParams));

            cachedParams = containerParams;
            subGfxSpeed = nestedPlaySpeed <= 0f ? 1f : nestedPlaySpeed;
            subGfxScale = nestedDefaultScale <= 0f ? 1f : nestedDefaultScale;
            paramsSerial = GfxContainerParamsSerial.FromParams(
                containerParams, subGfxSpeed, subGfxScale);
            SystemId = containerParams.SystemId;

            ClearSub();
            if (subGfxRoot != null)
            {
                subGfxRoot.transform.SetParent(transform, false);
                subGfxRoot.name = SubGfxNodeName;
                subGfxRoot.transform.name = SubGfxNodeName;
                subGfxRoot.transform.localPosition = Vector3.zero;
                subGfxRoot.transform.localRotation = Quaternion.identity;
                subGfxRoot.transform.localScale = Vector3.one;
                subGfx = subGfxRoot.transform;
                PrepareSubGfx();
            }
            else if (warnings != null && !string.IsNullOrEmpty(containerParams.GfxPath))
            {
                warnings.Add(
                    "GFX ID 200 GfxPath not bound: " + containerParams.GfxPath +
                    " (place under gfx/ or library, then rebuild).");
            }

            SetVisualActive(!containerParams.IsDummy);
        }

        public void PrepareSubGfx()
        {
            subDrivers.Clear();
            geometries.Clear();
            lastAlpha = -1f;
            if (subGfx == null)
                return;
            subGfx.localPosition = Vector3.zero;
            subGfx.localRotation = Quaternion.identity;
            subGfx.localScale = Vector3.one;
            CollectPrepared(subGfx);
        }

        public override void OnGfxTick(float gfxTimeMs, float alpha)
        {
            GfxAlpha = alpha;
            if (TimeManual)
                TimeMs = gfxTimeMs;

            var p = Params;
            if (p.IsDummy || subGfx == null)
            {
                SetVisualActive(false);
                return;
            }

            var activeTime = TimeManual ? TimeMs : gfxTimeMs;
            var elementTime = GfxKeypointEval.ElementTime(
                activeTime,
                GlobalLoopTime,
                LocalStartTime,
                LocalTotalTime,
                RepeatCount,
                RepeatDelay);

            var active = IsEndless ||
                (elementTime >= LocalStartTime && elementTime <= LocalTotalTime);
            var keypoints = GetKeypoints();
            if (!active || keypoints == null || keypoints.Count == 0)
            {
                SetVisualActive(false);
                return;
            }

            var state = GfxKeypointEval.Evaluate(
                keypoints, Math.Max(0f, elementTime - LocalStartTime));
            var s = state.Scale * subGfxScale;
            if (Math.Abs(s) < 0.000001f)
            {
                SetVisualActive(false);
                return;
            }

            var dir = state.Dir ?? GfxMath.QuatIdentity();
            if (Math.Abs(state.Rad) > 0.000001f)
            {
                var radQ = GfxMath.QuatFromAxisAngle(GfxMath.Vec3(0f, 0f, 1f), state.Rad);
                dir = GfxMath.MulQuat(dir, radQ);
            }

            transform.localPosition = new Vector3(state.Pos[0], state.Pos[1], state.Pos[2]);
            transform.localRotation = new Quaternion(dir[0], dir[1], dir[2], dir[3]);
            transform.localScale = new Vector3(s, s, s);

            ApplyAlpha(state.Color != null && state.Color.Length > 3 ? state.Color[3] : 1f);

            var subMs = GfxContainerEval.SubGfxMs(
                activeTime,
                LocalStartTime,
                LocalTotalTime,
                GlobalLoopTime,
                RepeatCount,
                RepeatDelay,
                IsEndless,
                p.LoopFlag) * p.PlaySpeed * subGfxSpeed;
            PushTime(subMs);
            SetVisualActive(true);
        }

        private void PushTime(float ms)
        {
            for (var i = 0; i < subDrivers.Count; i++)
            {
                var d = subDrivers[i];
                if (d == null)
                    continue;
                d.TimeManual = true;
                d.TimeMs = ms;
                d.GlobalLoopTime = NoLoopMs;
                d.OnGfxTick(ms, GfxAlpha);
            }
        }

        private void ApplyAlpha(float a)
        {
            if (Math.Abs(a - lastAlpha) < 0.001f)
                return;
            lastAlpha = a;
            for (var i = 0; i < geometries.Count; i++)
            {
                var r = geometries[i];
                if (r != null)
                    r.enabled = visualActive && a > 0.001f;
            }
        }

        private void CollectPrepared(Transform node)
        {
            if (node == null)
                return;

            var nestedContainer = node.GetComponent<GfxContainerDriver>();
            if (nestedContainer != null && nestedContainer != this)
            {
                nestedContainer.TimeManual = true;
                nestedContainer.GlobalLoopTime = NoLoopMs;
                subDrivers.Add(nestedContainer);
                return;
            }

            var driver = node.GetComponent<GfxElementDriver>();
            if (driver != null && driver != this)
            {
                driver.TimeManual = true;
                driver.GlobalLoopTime = NoLoopMs;
                subDrivers.Add(driver);
            }

            var renderer = node.GetComponent<Renderer>();
            if (renderer != null)
                geometries.Add(renderer);

            var nestedPlayer = node.GetComponent<GfxPlayer>();
            if (nestedPlayer != null)
            {
                nestedPlayer.enabled = false;
                nestedPlayer.Playing = false;
                nestedPlayer.HeaderControl = false;
            }

            for (var i = 0; i < node.childCount; i++)
                CollectPrepared(node.GetChild(i));
        }

        private void SetVisualActive(bool active)
        {
            visualActive = active;
            if (subGfx == null)
                return;
            for (var i = 0; i < geometries.Count; i++)
            {
                var r = geometries[i];
                if (r != null)
                    r.enabled = active && lastAlpha > 0.001f;
            }
        }

        private void ClearSub()
        {
            if (subGfx != null)
            {
                UnityEngine.Object.DestroyImmediate(subGfx.gameObject);
                subGfx = null;
            }
            subDrivers.Clear();
            geometries.Clear();
        }

        private void OnDestroy()
        {
            ClearSub();
        }
    }
}
