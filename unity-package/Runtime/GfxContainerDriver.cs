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

        private GfxContainerParams cachedParams;
        [NonSerialized] private Transform subGfx;
        private readonly List<GfxElementDriver> subDrivers = new List<GfxElementDriver>();
        private GfxKeypointPlaybackClock playbackClock;
        private float lastPlaybackTime;
        private float childPlaybackTime;

        public GfxContainerParams Params =>
            cachedParams ?? (cachedParams = paramsSerial.ToParams());

        public float SubGfxSpeed => subGfxSpeed;
        public float SubGfxScale => subGfxScale;
        public Transform SubGfx { get { RestoreRuntimeReferences(); return subGfx; } }

        private void RestoreRuntimeReferences()
        {
            if (subGfx != null) return;
            subGfx = transform.Find(SubGfxNodeName);
            if (subGfx != null) PrepareSubGfx();
        }

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
            playbackClock = null;
            if (subGfx == null)
                return;
            subGfx.localPosition = Vector3.zero;
            subGfx.localRotation = Quaternion.identity;
            subGfx.localScale = Vector3.one;
            CollectPrepared(subGfx);
        }

        public override void OnGfxTick(float gfxTimeMs, float alpha)
        {
            if (playbackClock != null)
                ResetPlayback();
            var activeTime = gfxTimeMs;
            var elementTime = GfxKeypointEval.ElementTime(activeTime, GlobalLoopTime, LocalStartTime,
                LocalTotalTime, RepeatCount, RepeatDelay);
            var childTime = GfxContainerEval.SubGfxMs(activeTime, LocalStartTime, LocalTotalTime,
                GlobalLoopTime, RepeatCount, RepeatDelay, IsEndless, Params.LoopFlag) * ChildSpeed;
            ApplyTick(gfxTimeMs, alpha, elementTime, IsEndless ||
                (elementTime >= LocalStartTime && elementTime <= LocalTotalTime), childTime, null, false, null);
        }

        private float ChildSpeed => Params.PlaySpeed * subGfxSpeed;

        internal void OnGfxPlaybackTick(float gfxTimeMs, float alpha, float step, bool advance,
            GfxParticleSim.EmissionRandom random)
        {
            RestoreRuntimeReferences();
            var priorTime = gfxTimeMs - step;
            if (playbackClock == null || Math.Abs(lastPlaybackTime - priorTime) > 0.01f)
            {
                ResetPlayback();
                playbackClock = new GfxKeypointPlaybackClock();
                if (priorTime > 0f)
                {
                    playbackClock.Tick(priorTime, LocalStartTime, LocalTotalTime, RepeatCount, RepeatDelay, IsEndless);
                    childPlaybackTime = GfxContainerEval.SubGfxMs(priorTime, LocalStartTime,
                        LocalTotalTime, GlobalLoopTime, RepeatCount, RepeatDelay, IsEndless, Params.LoopFlag) * ChildSpeed;
                }
            }

            lastPlaybackTime = gfxTimeMs;
            var looped = advance && playbackClock.Tick(step, LocalStartTime, LocalTotalTime,
                RepeatCount, RepeatDelay, IsEndless);
            if (looped && Params.LoopFlag)
                ResetChildPlayback();

            // A3DGFXContainer::TickAnimation gives the child the entire tick,
            // even if this tick just reached the container's start time.
            var childStep = advance && playbackClock.Active ? GfxHeaderDriver.ScaleTick(step, ChildSpeed) : 0f;
            childPlaybackTime += childStep;
            ApplyTick(gfxTimeMs, alpha, playbackClock.TimeMs, playbackClock.Active, childPlaybackTime,
                childStep, advance, random);
        }

        internal void ResetPlayback()
        {
            playbackClock = null;
            ResetChildPlayback();
        }

        private void ResetChildPlayback()
        {
            childPlaybackTime = 0f;
            foreach (var child in subDrivers)
            {
                if (child is GfxParticleDriver particle)
                    particle.ResetParticlePlayback();
                else if (child is GfxSpriteDriver sprite)
                    sprite.ResetPlayback();
                else if (child is GfxContainerDriver container)
                    container.ResetPlayback();
                else if (child is GfxGridDriver grid)
                    grid.ResetPlayback();
            }
        }

        private void ApplyTick(float gfxTimeMs, float alpha, float elementTime, bool active, float childTime,
            float? childStep, bool advance, GfxParticleSim.EmissionRandom random)
        {
            RestoreRuntimeReferences();
            GfxAlpha = alpha;
            if (TimeManual)
                TimeMs = gfxTimeMs;

            if (Params.IsDummy || subGfx == null)
            {
                SetVisualActive(false);
                return;
            }

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

            var keyAlpha = state.Color != null && state.Color.Length > 3 ? state.Color[3] : 1f;
            SetVisualActive(keyAlpha > 0.001f);

            PushTime(childTime, alpha * keyAlpha, childStep, advance, random);
        }

        private void PushTime(float ms, float alpha, float? step, bool advance, GfxParticleSim.EmissionRandom random)
        {
            for (var i = 0; i < subDrivers.Count; i++)
            {
                var d = subDrivers[i];
                if (d == null)
                    continue;
                d.TimeManual = true;
                d.TimeMs = ms;
                d.GlobalLoopTime = NoLoopMs;
                if (step.HasValue && d is GfxParticleDriver particle)
                    particle.OnGfxPlaybackTick(ms, alpha, step.Value, random);
                else if (step.HasValue && d is GfxSpriteDriver sprite)
                    sprite.OnGfxPlaybackTick(ms, alpha, step.Value, advance);
                else if (step.HasValue && d is GfxContainerDriver container)
                    container.OnGfxPlaybackTick(ms, alpha, step.Value, advance, random);
                else if (step.HasValue && d is GfxGridDriver grid)
                    grid.OnGfxPlaybackTick(ms, alpha, step.Value, advance);
                else
                    d.OnGfxTick(ms, alpha);
            }
        }

        private void CollectPrepared(Transform node)
        {
            if (node == null || node.GetComponent<GfxDummyParticlesDriver>() != null)
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
            // The container gates the whole child GFX. Each child element
            // still owns its renderer's visibility for its own keypoint clock.
            if (subGfx != null && subGfx.gameObject.activeSelf != active)
                subGfx.gameObject.SetActive(active);
        }

        private void ClearSub()
        {
            if (subGfx != null)
            {
                UnityEngine.Object.DestroyImmediate(subGfx.gameObject);
                subGfx = null;
            }
            subDrivers.Clear();
        }

        private void OnDestroy()
        {
            // Removing the component must not delete the user's nested hierarchy.
            subGfx = null;
            subDrivers.Clear();
        }
    }
}
