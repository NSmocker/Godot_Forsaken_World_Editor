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

        private List<GfxKeypointSpec> keypoints = new List<GfxKeypointSpec>();
        private GfxKeypointState lastState;

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

        public GfxKeypointState LastState => lastState;

        public void SetKeypoints(IList<GfxKeypointSpec> specs)
        {
            keypoints = specs != null
                ? new List<GfxKeypointSpec>(specs)
                : new List<GfxKeypointSpec>();
        }

        public List<GfxKeypointSpec> GetKeypoints() =>
            new List<GfxKeypointSpec>(keypoints);

        /// <summary>
        /// Called by GfxPlayer each tick with the shared GFX time.
        /// </summary>
        public virtual void OnGfxTick(float gfxTimeMs, float alpha)
        {
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
