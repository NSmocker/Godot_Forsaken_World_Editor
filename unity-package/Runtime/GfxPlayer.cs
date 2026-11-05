using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// GFX file root: shared clock, alpha, face-to-viewer, shake (gfx_data_node.gd).
    /// Runs in play mode and edit mode ([ExecuteAlways]).
    /// </summary>
    [ExecuteAlways]
    public sealed class GfxPlayer : MonoBehaviour
    {
        private static readonly List<GfxPlayer> registered = new List<GfxPlayer>();

        /// <summary>Live GfxPlayer instances (edit + play). Used by GFX-Player dock.</summary>
        public static IReadOnlyList<GfxPlayer> RegisteredPlayers => registered;
        [SerializeField] private bool headerControl = true;
        [SerializeField] private bool playing = true;
        [SerializeField] private bool timeManual;
        [SerializeField] private float timeMs;
        [SerializeField] private float playSpeed = 1f;
        [SerializeField] private float alpha = 1f;
        [SerializeField] private bool shakeInEditor;
        [SerializeField] private bool playerPaused;
        [SerializeField] private string sourcePath = string.Empty;
        [SerializeField] private string[] headerKeys = Array.Empty<string>();
        [SerializeField] private string[] headerValues = Array.Empty<string>();

        private GfxHeaderDriver driver;
        private readonly List<GfxElementDriver> elements = new List<GfxElementDriver>();
        private bool hierarchyDirty = true;
        private Vector3 appliedShake;
        private Camera shakeCamera;
        private bool hasShake;

        public bool HeaderControl
        {
            get { return headerControl; }
            set
            {
                headerControl = value;
                if (driver != null)
                    driver.HeaderControl = value;
            }
        }

        public bool Playing
        {
            get { return playing; }
            set
            {
                playing = value;
                if (driver != null)
                    driver.Playing = value;
            }
        }

        public bool TimeManual
        {
            get { return timeManual; }
            set
            {
                timeManual = value;
                if (driver != null)
                    driver.TimeManual = value;
            }
        }

        public float TimeMs
        {
            get { return timeMs; }
            set
            {
                timeMs = value;
                if (driver != null)
                    driver.TimeMs = value;
            }
        }

        public float PlaySpeed
        {
            get { return playSpeed; }
            set
            {
                playSpeed = value;
                if (driver != null)
                    driver.PlaySpeed = value;
            }
        }

        public float Alpha
        {
            get { return alpha; }
            set
            {
                alpha = value;
                if (driver != null)
                    driver.Alpha = value;
            }
        }

        public bool ShakeInEditor
        {
            get { return shakeInEditor; }
            set { shakeInEditor = value; }
        }

        public bool PlayerPaused
        {
            get { return playerPaused; }
            set
            {
                playerPaused = value;
                if (driver != null)
                    driver.PlayerPaused = value;
            }
        }

        public string SourcePath
        {
            get { return sourcePath; }
            set { sourcePath = value ?? string.Empty; }
        }

        public GfxHeaderDriver Driver => driver;

        public void SetHeader(IDictionary<string, string> header)
        {
            if (header == null)
            {
                headerKeys = Array.Empty<string>();
                headerValues = Array.Empty<string>();
            }
            else
            {
                headerKeys = new string[header.Count];
                headerValues = new string[header.Count];
                var i = 0;
                foreach (var pair in header)
                {
                    headerKeys[i] = pair.Key;
                    headerValues[i] = pair.Value;
                    i++;
                }
            }

            EnsureDriver();
            driver.Config = GfxHeaderConfig.FromHeader(HeaderDictionary());
            hierarchyDirty = true;
        }

        public Dictionary<string, string> HeaderDictionary()
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            var n = Math.Min(headerKeys.Length, headerValues.Length);
            for (var i = 0; i < n; i++)
            {
                if (!string.IsNullOrEmpty(headerKeys[i]))
                    dict[headerKeys[i]] = headerValues[i] ?? string.Empty;
            }

            return dict;
        }

        public void Restart()
        {
            EnsureDriver();
            driver.Restart();
            timeMs = 0f;
            ClearShake();
        }

        public void PlayerSeek(float realMs)
        {
            EnsureDriver();
            driver.PlayerSeek(realMs);
            timeMs = driver.GfxTime;
            ClearShake();
        }

        public float PlayerTimeMs()
        {
            EnsureDriver();
            return driver.PlayerTimeMs();
        }

        public float PlayerLengthMs()
        {
            EnsureDriver();
            RefreshElements();
            return driver.PlayerLengthMs();
        }

        public void MarkHierarchyDirty() => hierarchyDirty = true;

        public override void OnEnable()
        {
            if (!registered.Contains(this))
                registered.Add(this);
            EnsureDriver();
            hierarchyDirty = true;
        }

        public override void OnDisable()
        {
            registered.Remove(this);
            ClearShake();
        }

        public override void Update()
        {
            Tick(Time.deltaTime * 1000f);
        }

        public void Tick(float dtMs)
        {
            EnsureDriver();
            SyncDriverFields();
            if (hierarchyDirty)
                RefreshElements();

            var cam = Camera.main;
            float[] gfxPos = null;
            float[] camPos = null;
            float[] camFwd = null;
            if (transform != null)
            {
                var p = transform.localPosition;
                gfxPos = new[] { p.x, p.y, p.z };
            }

            if (cam != null && cam.transform != null)
            {
                var cp = cam.transform.localPosition;
                camPos = new[] { cp.x, cp.y, cp.z };
                // Approximate forward as +Z until full camera basis exists in stubs.
                camFwd = new[] { 0f, 0f, 1f };
            }

            var allowShake = shakeInEditor || ApplicationIsPlaying();
            GfxShakeSample shake;
            var t = driver.Tick(dtMs, gfxPos, camPos, camFwd, allowShake && cam != null, out shake);
            timeMs = t;

            ApplyFace(camPos);
            var a = driver.ActualAlpha(gfxPos, camPos);
            if (a <= GfxHeaderDriver.HideAlpha)
                a = 0f;

            foreach (var element in elements)
            {
                if (element == null)
                    continue;
                element.TimeManual = true;
                element.OnGfxTick(t, a);
            }

            ApplyShake(cam, shake);
        }

        private void ApplyFace(float[] camPos)
        {
            if (transform == null)
                return;
            var p = transform.localPosition;
            var basis = driver.FaceBasis(new[] { p.x, p.y, p.z }, camPos);
            if (basis == null)
                return;
            var q = GfxMath.QuatFromAxes(basis[0], basis[1], basis[2]);
            var scale = transform.localScale;
            transform.localRotation = new Quaternion(q[0], q[1], q[2], q[3]);
            transform.localScale = scale;
        }

        private void ApplyShake(Camera cam, GfxShakeSample shake)
        {
            if (cam == null || cam.transform == null)
            {
                ClearShake();
                return;
            }

            if (shakeCamera != null && shakeCamera != cam)
                ClearShake();

            shakeCamera = cam;
            if (shake == null || !shake.Active)
            {
                ClearShake();
                return;
            }

            // Undo previous applied shake, then apply new absolute offset for type-1,
            // or delta for type-2 (driver already returns the right form).
            if (hasShake)
            {
                var cur = cam.transform.localPosition;
                cam.transform.localPosition = new Vector3(
                    cur.x - appliedShake.x,
                    cur.y - appliedShake.y,
                    cur.z - appliedShake.z);
            }

            appliedShake = new Vector3(
                shake.WorldOffset[0],
                shake.WorldOffset[1],
                shake.WorldOffset[2]);
            {
                var cur = cam.transform.localPosition;
                cam.transform.localPosition = new Vector3(
                    cur.x + appliedShake.x,
                    cur.y + appliedShake.y,
                    cur.z + appliedShake.z);
            }
            hasShake = true;
        }

        private void ClearShake()
        {
            if (hasShake && shakeCamera != null && shakeCamera.transform != null)
            {
                var cur = shakeCamera.transform.localPosition;
                shakeCamera.transform.localPosition = new Vector3(
                    cur.x - appliedShake.x,
                    cur.y - appliedShake.y,
                    cur.z - appliedShake.z);
            }

            appliedShake = new Vector3(0f, 0f, 0f);
            hasShake = false;
            shakeCamera = null;
        }

        private void EnsureDriver()
        {
            if (driver != null)
                return;
            driver = new GfxHeaderDriver(GetInstanceID());
            driver.Config = GfxHeaderConfig.FromHeader(HeaderDictionary());
            SyncDriverFields();
        }

        private void SyncDriverFields()
        {
            driver.HeaderControl = headerControl;
            driver.Playing = playing;
            driver.TimeManual = timeManual;
            driver.TimeMs = timeMs;
            driver.PlaySpeed = playSpeed;
            driver.Alpha = alpha;
            driver.PlayerPaused = playerPaused;
        }

        private void RefreshElements()
        {
            hierarchyDirty = false;
            elements.Clear();
            var loop = 0f;
            Collect(transform);
            if (loop <= 0f)
                loop = 3000f;
            driver.LoopMs = loop;

            void Collect(Transform node)
            {
                if (node == null)
                    return;
                for (var i = 0; i < node.childCount; i++)
                {
                    var child = node.GetChild(i);
                    if (child == null || child.gameObject == null)
                        continue;
                    var driverComp = child.gameObject.GetComponent<GfxElementDriver>();
                    if (driverComp != null)
                    {
                        elements.Add(driverComp);
                        if (driverComp.GlobalLoopTime > loop)
                            loop = driverComp.GlobalLoopTime;
                        // Container drives Sub_GFX itself (gfx_container_200._prepare).
                        if (driverComp is GfxContainerDriver)
                            continue;
                    }

                    Collect(child);
                }
            }
        }

        private static bool ApplicationIsPlaying()
        {
            try { return Application.isPlaying; }
            catch { return true; }
        }

        private int GetInstanceID()
        {
            return GetHashCode();
        }
    }
}
