using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Godot <c>gfx_player_dock.gd</c>: Play-GFX / Stop-GFX / time slider for GfxPlayers
    /// in the open scene (selection if it contains GFX, else all HeaderControl roots).
    /// Nested Sub_GFX under containers (HeaderControl=false) are excluded.
    /// </summary>
    public sealed class GfxPlayerWindow : EditorWindow
    {
        private const float MinLengthMs = 100f;
        private readonly List<GfxPlayer> targets = new List<GfxPlayer>();
        private float lengthMs = MinLengthMs;
        private float sliderMs;
        private bool dragging;
        private bool resumeAfterDrag;

        [MenuItem("Tools/Forsaken World/GFX-Player")]
        public static void Open()
        {
            GetWindow<GfxPlayerWindow>("GFX-Player");
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            Repaint();
        }

        private void OnGUI()
        {
            RefreshTargets();
            EditorGUILayout.LabelField(
                targets.Count == 0
                    ? "Keine GFX in der Szene"
                    : targets.Count + " GFX",
                EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(targets.Count == 0))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play-GFX"))
                    Play();
                if (GUILayout.Button("Stop-GFX"))
                    Stop();
            }

            EditorGUI.BeginChangeCheck();
            var next = EditorGUILayout.Slider("Zeit (ms)", sliderMs, 0f, lengthMs);
            if (EditorGUI.EndChangeCheck())
            {
                if (!dragging)
                {
                    dragging = true;
                    resumeAfterDrag = IsPlaying();
                    PauseAll();
                }
                sliderMs = next;
                Seek(sliderMs);
            }
            else if (dragging)
            {
                // Slider released: EndChangeCheck false while value stable.
                dragging = false;
                if (resumeAfterDrag)
                    Play();
            }

            if (!dragging && targets.Count > 0)
            {
                var t = targets[0].PlayerTimeMs();
                if (lengthMs > 0f)
                {
                    sliderMs = t - lengthMs * (float)System.Math.Floor(t / lengthMs);
                    if (sliderMs < 0f)
                        sliderMs += lengthMs;
                }
            }

            EditorGUILayout.LabelField(
                string.Format(
                    "{0:0.00} s / {1:0.00} s",
                    sliderMs / 1000f,
                    lengthMs / 1000f));
        }

        private void Play()
        {
            foreach (var p in ValidTargets())
            {
                p.PlayerSeek(sliderMs);
                p.PlayerPaused = false;
                p.Playing = true;
            }
        }

        private void Stop()
        {
            foreach (var p in ValidTargets())
            {
                p.PlayerPaused = true;
                p.PlayerSeek(0f);
            }
            sliderMs = 0f;
        }

        private void Seek(float ms)
        {
            foreach (var p in ValidTargets())
                p.PlayerSeek(ms);
        }

        private void PauseAll()
        {
            foreach (var p in ValidTargets())
                p.PlayerPaused = true;
        }

        private bool IsPlaying()
        {
            foreach (var p in ValidTargets())
            {
                if (!p.PlayerPaused)
                    return true;
            }
            return false;
        }

        private List<GfxPlayer> ValidTargets()
        {
            var list = new List<GfxPlayer>();
            foreach (var p in targets)
            {
                if (p != null)
                    list.Add(p);
            }
            return list;
        }

        private void RefreshTargets()
        {
            targets.Clear();
            lengthMs = MinLengthMs;

            var fromSelection = false;
            var selected = Selection.gameObjects;
            if (selected != null)
            {
                foreach (var go in selected)
                {
                    if (go == null)
                        continue;
                    CollectUnder(go.transform, targets);
                }
                fromSelection = targets.Count > 0;
            }

            if (!fromSelection)
            {
                foreach (var p in GfxPlayer.RegisteredPlayers)
                {
                    if (IsTarget(p) && !targets.Contains(p))
                        targets.Add(p);
                }
            }

            for (var i = 0; i < targets.Count; i++)
            {
                var len = targets[i].PlayerLengthMs();
                if (len > lengthMs)
                    lengthMs = len;
            }
        }

        private static void CollectUnder(Transform root, List<GfxPlayer> into)
        {
            if (root == null)
                return;
            var p = root.GetComponent<GfxPlayer>();
            if (IsTarget(p) && !into.Contains(p))
                into.Add(p);
            var above = root.parent;
            while (above != null)
            {
                var parentPlayer = above.GetComponent<GfxPlayer>();
                if (IsTarget(parentPlayer) && !into.Contains(parentPlayer))
                    into.Add(parentPlayer);
                above = above.parent;
            }
            for (var i = 0; i < root.childCount; i++)
                CollectUnder(root.GetChild(i), into);
        }

        private static bool IsTarget(GfxPlayer p)
        {
            return p != null && p.HeaderControl;
        }
    }
}
