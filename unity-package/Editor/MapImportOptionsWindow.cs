using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SilverEcho.ForsakenWorld;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Діалогове вікно, яке з'являється при імпорті мапи через Game Data Browser.
    /// Дозволяє вибрати, які сектори ландшафту (sub-terrain) імпортувати.
    /// Drag & Drop цей вікно ігнорує.
    /// </summary>
    public class MapImportOptionsWindow : EditorWindow
    {
        public TerrainDocument TerrainDoc;
        public GameDataFile Trn2File;
        public GameDataIndex Index;
        public GfxTextDecoder Decoder;

        private bool[] selectedSectors;
        private Vector2 scrollPos;
        private System.Action<MapImportOptions> onComplete;

        public static void ShowWindow(TerrainDocument terrainDoc, GameDataFile trn2File, GameDataIndex index, GfxTextDecoder decoder, System.Action<MapImportOptions> onComplete, EditorWindow owner = null)
        {
            if (terrainDoc == null) throw new System.ArgumentNullException(nameof(terrainDoc));
            if (onComplete == null) throw new System.ArgumentNullException(nameof(onComplete));
            var window = CreateInstance<MapImportOptionsWindow>();
            window.titleContent = new GUIContent("Map Import Options - Select Sectors");
            window.minSize = new Vector2(540, 480);
            var center = owner != null ? owner.position.center : new Vector2(400, 350);
            window.position = new Rect(center.x - 300, center.y - 250, 600, 500);
            window.TerrainDoc = terrainDoc;
            window.Trn2File = trn2File;
            window.Index = index;
            window.Decoder = decoder;
            window.onComplete = onComplete;
            window.selectedSectors = new bool[terrainDoc.Subs.Length];
            for (int i = 0; i < window.selectedSectors.Length; i++)
                window.selectedSectors[i] = true; // за замовчуванням вибрано всі

            window.ShowUtility();
            window.Focus();
            // onComplete буде викликано при натисканні "Import Selected Sectors"
        }

        private void OnGUI()
        {
            if (TerrainDoc == null || selectedSectors == null || onComplete == null)
            {
                EditorGUILayout.HelpBox("Reopen map import options from Game Data Browser.", MessageType.Info);
                if (GUILayout.Button("Close")) Close();
                return;
            }
            EditorGUILayout.LabelField("Map Import Options", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Terrain: {Trn2File?.DisplayName}", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Select sectors to import:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All", GUILayout.Height(22)))
                SetAllSectors(true);
            if (GUILayout.Button("Deselect All", GUILayout.Height(22)))
                SetAllSectors(false);
            EditorGUILayout.EndHorizontal();
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(300));

            for (int i = 0; i < TerrainDoc.Subs.Length; i++)
            {
                var sub = TerrainDoc.Subs[i];
                selectedSectors[i] = EditorGUILayout.ToggleLeft(
                    $"Sector {i}: {sub.Name} (Height: {sub.MinHeight:F1}..{sub.MaxHeight:F1})",
                    selectedSectors[i]);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            int selectedCount = selectedSectors.Count(selected => selected);
            EditorGUILayout.LabelField($"Selected: {selectedCount} / {selectedSectors.Length}");
            if (selectedCount == 0)
                EditorGUILayout.HelpBox("Select at least one terrain sector.", MessageType.Info);
            bool importRequested;
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(selectedCount == 0))
                importRequested = GUILayout.Button("Import Selected Sectors", GUILayout.Height(30));
            bool cancelRequested = GUILayout.Button("Cancel", GUILayout.Height(30));
            EditorGUILayout.EndHorizontal();

            if (importRequested)
                ConfirmSelection();
            else if (cancelRequested)
            {
                Close();
            }
        }

        private void SetAllSectors(bool value)
        {
            if (selectedSectors == null) return;
            for (int i = 0; i < selectedSectors.Length; i++)
                selectedSectors[i] = value;
        }

        private void ConfirmSelection()
        {
            if (onComplete == null || selectedSectors == null || !selectedSectors.Any(selected => selected))
                return;
            var options = new MapImportOptions
            {
                SelectedSectorIndices = Enumerable.Range(0, selectedSectors.Length)
                    .Where(i => selectedSectors[i]).ToList()
            };
            var complete = onComplete;
            onComplete = null;
            Debug.Log($"[Angelica 2.x] Importing {options.SelectedSectorIndices.Count} sectors out of {TerrainDoc.Subs.Length}");
            Close();
            complete(options);
        }
    }

    public class MapImportOptions
    {
        // Null preserves full-map import for callers without a selection. An explicit
        // empty list means no sectors and is rejected instead of importing everything.
        public List<int> SelectedSectorIndices;
        public bool ImportFullMap => SelectedSectorIndices == null;
    }
}
