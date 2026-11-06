using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SilverEcho.ForsakenWorld.Editor
{
    [InitializeOnLoad]
    public static class GameDataDragAndDrop
    {
        private const string RootPreference = "SilverEcho.ForsakenWorld.GameDataRoot";
        private static GameDataIndex index;
        private static bool scanning;
        private static bool importing;
        private static string attemptedRoot;
        private static GameDataFile dragCandidate;
        private static Vector2 dragStart;

        static GameDataDragAndDrop()
        {
            DragAndDrop.AddDropHandler(OnProjectDrop);
            DragAndDrop.AddDropHandler(OnSceneDrop);
            DragAndDrop.AddDropHandler(OnHierarchyDrop);
        }

        internal static void SetIndex(GameDataIndex value, bool isScanning = false)
        {
            index = value;
            scanning = isScanning;
            attemptedRoot = value?.Root;
        }

        public static GameDataIndex GetIndex()
        {
            var root = EditorPrefs.GetString(RootPreference, "");
            if (scanning) return null;
            if (index != null && !string.IsNullOrWhiteSpace(root) &&
                string.Equals(Path.GetFullPath(root), index.Root, StringComparison.OrdinalIgnoreCase)) return index;
            if (string.IsNullOrWhiteSpace(root) || root == attemptedRoot) return null;
            attemptedRoot = root;
            index = null;
            if (!GameDataScanCache.TryLoad(GameDataScanCache.DefaultCachePath(Application.dataPath), root, out var snapshot)) return null;
            index = new GameDataIndex(root, GameDataBrowserWindow.GetGameDataDecoder());
            index.ReplaceFromRelativePaths(snapshot.RelativePaths);
            return index;
        }

        // Also used by tests and hosts: hover only classifies; it never imports or modifies Assets.
        public static DragAndDropVisualMode Classify(GameDataIndex data, string[] paths, out string error)
        {
            error = null;
            if (paths == null || paths.Length == 0) return DragAndDropVisualMode.None;
            if (!paths.Any(GameDataDependencyGraph.Supports)) return DragAndDropVisualMode.None;
            if (data == null) { error = "Open Tools/Forsaken World/Game Data Browser and scan the extracted game data root first."; return DragAndDropVisualMode.Rejected; }
            try
            {
                var graph = new GameDataDependencyGraph(data);
                foreach (var path in paths)
                {
                    if (!GameDataDependencyGraph.Supports(path)) throw new NotSupportedException("Unsupported file in drop: " + path);
                    graph.FileFromPath(path, requireIndexed: true);
                }
                return DragAndDropVisualMode.Copy;
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException || e is NotSupportedException || e is UnauthorizedAccessException)
            { error = e.Message; return DragAndDropVisualMode.Rejected; }
        }

        private static string[] Paths()
        {
            var sourcePaths = new List<string>();
            foreach (var obj in DragAndDrop.objectReferences)
            {
                string source = null;
                switch (obj)
                {
                    case SmdAsset smd: source = smd.SourceRelativePath; break;
                    case EcmAsset ecm: source = ecm.SourceRelativePath; break;
                    case GfxAsset gfx: source = gfx.SourceRelativePath; break;
                    case SkiAsset ski: source = ski.SourceRelativePath; break;
                    case BonAsset bon: source = bon.SourceRelativePath; break;
                    case StckAsset stck: source = stck.SourceRelativePath; break;
                    default: return Array.Empty<string>(); // Preserve ordinary Unity asset/object drags.
                }
                if (!string.IsNullOrEmpty(source)) sourcePaths.Add(source);
            }
            if (sourcePaths.Count > 0) return sourcePaths.ToArray();
            var paths = DragAndDrop.paths ?? Array.Empty<string>();
            // Native textures/audio/XML from elsewhere keep Unity's normal copy behavior.
            var root = EditorPrefs.GetString(RootPreference, "").TrimEnd('/', '\\') + Path.DirectorySeparatorChar;
            var proprietary = new[] { ".smd", ".bon", ".ski", ".stck", ".ecm", ".gfx", ".trn2", ".ecwld", ".ecbsd", ".bmd", ".dcf", ".stf", ".t2bk", ".t2mk", ".t2lm", ".gphy" };
            return paths.Any(p => proprietary.Contains(Path.GetExtension(p).ToLowerInvariant()) ||
                (Path.IsPathRooted(p) && Path.GetFullPath(p).StartsWith(root, StringComparison.OrdinalIgnoreCase))) ? paths : Array.Empty<string>();
        }

        private static DragAndDropVisualMode OnProjectDrop(int id, string path, bool perform) =>
            Handle(perform, false, null, Vector3.zero, default);

        private static DragAndDropVisualMode OnSceneDrop(Object upon, Vector3 position, Vector2 viewport, Transform parent, bool perform) =>
            Handle(perform, true, parent, position, SceneManager.GetActiveScene());

        private static DragAndDropVisualMode OnHierarchyDrop(int id, HierarchyDropFlags flags, Transform parent, bool perform)
        {
            var target = EditorUtility.InstanceIDToObject(id) as GameObject;
            var dropParent = parent;
            if (target != null) dropParent = (flags & HierarchyDropFlags.DropUpon) != 0 ? target.transform : target.transform.parent;
            return Handle(perform, true, dropParent, Vector3.zero, target != null ? target.scene : SceneManager.GetActiveScene());
        }

        private static DragAndDropVisualMode Handle(bool perform, bool instantiate, Transform parent, Vector3 position, Scene scene)
        {
            var paths = Paths();
            if (paths.Length == 0) return DragAndDropVisualMode.None;
            if (importing || EditorApplication.isPlayingOrWillChangePlaymode) return DragAndDropVisualMode.Rejected;
            var data = GetIndex();
            var mode = Classify(data, paths, out var error);
            if (perform)
            {
                if (mode == DragAndDropVisualMode.Copy)
                {
                    DragAndDrop.AcceptDrag();
                    QueueImport(data, paths, instantiate, parent, position, scene);
                }
                else if (!string.IsNullOrEmpty(error)) Debug.LogWarning("Forsaken World drop: " + error);
            }
            return mode;
        }

        internal static void QueueImport(GameDataIndex data, string[] paths, bool instantiate = false,
            Transform parent = null, Vector3 position = default, Scene scene = default)
        {
            if (importing) return;
            importing = true;
            // Run outside the current GUI event; asset refresh/reload must not corrupt its layout stack.
            EditorApplication.delayCall += () =>
            {
                try
                {
                    var result = GameDataDropImporter.Import(data, paths);
                    var selection = new List<Object>();
                    Undo.IncrementCurrentGroup();
                    var undoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Drop Forsaken World assets");
                    foreach (var asset in result.Assets)
                    {
                        if (instantiate && asset is GameObject prefab)
                        {
                            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab,
                                scene.IsValid() && scene.isLoaded ? scene : SceneManager.GetActiveScene());
                            if (parent != null) instance.transform.SetParent(parent, false);
                            else instance.transform.position = position;
                            Undo.RegisterCreatedObjectUndo(instance, "Drop Forsaken World asset");
                            selection.Add(instance);
                        }
                        else selection.Add(asset);
                    }
                    Undo.CollapseUndoOperations(undoGroup);
                    if (selection.Count > 0) { Selection.objects = selection.ToArray(); EditorGUIUtility.PingObject(selection[0]); }
                    foreach (var warning in result.Warnings.Distinct()) Debug.LogWarning("Forsaken World drop: " + warning);
                    foreach (var failure in result.Errors.Distinct()) Debug.LogError("Forsaken World drop: " + failure);
                    Debug.Log("Forsaken World drop: " + result.ImportedFiles + " files imported; " + result.Assets.Count +
                        " root assets; " + result.Errors.Count + " errors." + (result.Cancelled ? " Cancelled." : ""));
                }
                catch (Exception e) { Debug.LogException(e); }
                finally { importing = false; }
            };
        }

        internal static void DrawDropArea(GameDataIndex data)
        {
            var rect = GUILayoutUtility.GetRect(0, 38, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "Drop game files here to import with dependencies\nOr drag a browser row to Project / Scene / Hierarchy", EditorStyles.helpBox);
            var evt = Event.current;
            if (!rect.Contains(evt.mousePosition) || (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)) return;
            var paths = Paths();
            if (paths.Length == 0) return;
            DragAndDrop.visualMode = Classify(data, paths, out var error);
            if (evt.type == EventType.DragPerform)
            {
                if (DragAndDrop.visualMode == DragAndDropVisualMode.Copy)
                { DragAndDrop.AcceptDrag(); QueueImport(data, paths); }
                else Debug.LogWarning("Forsaken World drop: " + error);
            }
            evt.Use();
        }

        internal static void HandleRowDrag(GameDataFile file, Rect rect)
        {
            var evt = Event.current;
            if (file != null && evt.rawType == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            { dragCandidate = file; dragStart = evt.mousePosition; }
            if (file == dragCandidate && evt.rawType == EventType.MouseDrag && (evt.mousePosition - dragStart).sqrMagnitude > 25)
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = Array.Empty<Object>();
                DragAndDrop.paths = new[] { file.FullPath };
                DragAndDrop.StartDrag(file.DisplayName);
                dragCandidate = null;
                evt.Use();
            }
            if (evt.rawType == EventType.MouseUp) dragCandidate = null;
        }
    }
}
