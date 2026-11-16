using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Base for GFX elements driven by the header clock (Time_Manual / Time_Ms),
    /// matching gfx_element_*.gd + gfx_data_node driver collection.
    /// </summary>
    public class GfxElementDriver : MonoBehaviour
    {
        [SerializeField] private string systemId = string.Empty;
        [SerializeField] private string elementName = string.Empty;
        [SerializeField] private bool timeManual;
        [SerializeField] private float timeMs;
        [SerializeField] private float globalLoopTime = 3000f;
        [SerializeField] private float localStartTime;
        [SerializeField] private float localTotalTime = 3000f;
        [SerializeField] private int repeatCount = 1;
        [SerializeField] private float repeatDelay;
        [SerializeField] private bool isEndless;
        [SerializeField] private float gfxAlpha = 1f;
        [SerializeField] private bool bindEleActive;
        [SerializeField] private float bindEleStartMs;

        private List<GfxKeypointSpec> keypoints = new List<GfxKeypointSpec>();
        private GfxKeypointState lastState;
        [SerializeField] private GfxElementAsset animationSource;
        // Unity hot reload serializes private primitive fields, but not the runtime
        // keypoint graph. Restore both together instead of retaining a stale flag.
        [NonSerialized] private bool animationRestored;

        /// <summary>Dummy flag from the persisted source, also for drivers without typed dummy params.</summary>
        public bool IsDummySource
        {
            get
            {
                if (animationSource == null) return false;
                foreach (var property in animationSource.Properties)
                    if (property.Key == "IsDummy")
                        return GfxPropertyValues.ParseBool(property.Value, false);
                return false;
            }
        }

        public void SetAnimationSource(GfxElement source)
        {
            animationSource = GfxElementAsset.FromElement(source);
            animationRestored = false;
        }

        protected GfxElement RestoreAnimationSource()
        {
            if (animationSource == null) return null;
            var source = animationSource.ToElement();
            if (!animationRestored)
            {
                keypoints = GfxKeypointBuilder.Build(source);
                animationRestored = true;
            }
            return source;
        }

        /// <summary>The element's own "Name:" value (parsed_data["Name"] in Godot).</summary>
        public string ElementName
        {
            get { return elementName; }
            set { elementName = value ?? string.Empty; }
        }

        public string SystemId
        {
            get { return systemId; }
            set { systemId = value ?? string.Empty; }
        }

        public bool TimeManual
        {
            get { return timeManual; }
            set { timeManual = value; }
        }

        public float TimeMs
        {
            get { return timeMs; }
            set { timeMs = value; }
        }

        public float GlobalLoopTime
        {
            get { return globalLoopTime; }
            set { globalLoopTime = value; }
        }

        public float LocalStartTime
        {
            get { return localStartTime; }
            set { localStartTime = value; }
        }

        public float LocalTotalTime
        {
            get { return localTotalTime; }
            set { localTotalTime = value; }
        }

        public int RepeatCount
        {
            get { return repeatCount; }
            set { repeatCount = value; }
        }

        public float RepeatDelay
        {
            get { return repeatDelay; }
            set { repeatDelay = value; }
        }

        public bool IsEndless
        {
            get { return isEndless; }
            set { isEndless = value; }
        }

        public float GfxAlpha
        {
            get { return gfxAlpha; }
            set { gfxAlpha = value; }
        }

        /// <summary>
        /// BindEle resolved at build time (create_id_100/101.gd:591–677). When set,
        /// transform controllers run on the bound clock instead of the element's own
        /// loop time; local controllers (105–109/111) keep their own time.
        /// </summary>
        public bool BindEleActive
        {
            get { return bindEleActive; }
        }

        /// <summary>Target element StartTime in ms (Godot b_start).</summary>
        public float BindEleStartMs
        {
            get { return bindEleStartMs; }
        }

        [SerializeField] private GfxElementDriver boundTransformTarget;
        public GfxElementDriver BoundTransformTarget => boundTransformTarget;

        public void SetBoundTransformTarget(GfxElementDriver target)
        {
            boundTransformTarget = target == this ? null : target;
        }

        // A3DGFXElement::GetCurKeyPoint: BindEle замінює позицію й орієнтацію,
        // зберігаючи власні колір і масштаб. Обмеження захищає від циклів у файлі.
        public GfxKeypointState EvaluateBoundKeypoint(float activeMs, int depth = 0)
        {
            var t = IsEndless ? activeMs : GfxKeypointEval.ElementTime(activeMs,
                GlobalLoopTime, LocalStartTime, LocalTotalTime, RepeatCount, RepeatDelay);
            var state = GfxKeypointEval.Evaluate(GetKeypoints(), Math.Max(0f, t - LocalStartTime));
            if (boundTransformTarget != null && depth < 16)
            {
                var target = boundTransformTarget.EvaluateBoundKeypoint(activeMs, depth + 1);
                state.Pos = target.Pos;
                state.Dir = target.Dir;
            }
            return state;
        }

        public void SetBindEle(float targetStartMs)
        {
            bindEleActive = true;
            bindEleStartMs = targetStartMs;
        }

        public void ClearBindEle()
        {
            boundTransformTarget = null;
            bindEleActive = false;
            bindEleStartMs = 0f;
        }

        public GfxKeypointState LastState => lastState;

        public void SetKeypoints(IList<GfxKeypointSpec> specs)
        {
            keypoints = specs != null
                ? new List<GfxKeypointSpec>(specs)
                : new List<GfxKeypointSpec>();
        }

        public List<GfxKeypointSpec> GetKeypoints()
        {
            if (!animationRestored) RestoreAnimationSource();
            return new List<GfxKeypointSpec>(keypoints);
        }

        /// <summary>
        /// Called by GfxPlayer each tick with the shared GFX time.
        /// </summary>
        public virtual void OnGfxTick(float gfxTimeMs, float alpha)
        {
            if (!animationRestored) RestoreAnimationSource();
            gfxAlpha = alpha;
            if (timeManual)
                timeMs = gfxTimeMs;

            if (keypoints.Count == 0)
                return;

            var active = timeManual ? timeMs : gfxTimeMs;
            var elementTime = GfxKeypointEval.ElementTime(
                active,
                globalLoopTime,
                localStartTime,
                localTotalTime,
                repeatCount,
                repeatDelay);
            var span = elementTime - localStartTime;
            if (span < 0f)
                return;
            lastState = GfxKeypointEval.Evaluate(keypoints, span);
            ApplyState(lastState);
        }

        protected virtual void ApplyState(GfxKeypointState state)
        {
            if (state == null)
                return;
            transform.localPosition = new Vector3(state.Pos[0], state.Pos[1], state.Pos[2]);
            transform.localRotation = new Quaternion(
                state.Dir[0], state.Dir[1], state.Dir[2], state.Dir[3]);
            var s = state.Scale;
            transform.localScale = new Vector3(s, s, s);
        }
    }
}
