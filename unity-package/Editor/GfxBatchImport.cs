using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Godot tool menu «GFX Ausführen» (<c>gfx_importer.gd</c>): load every .gfx under
    /// <c>Assets/ForsakenWorld/GFX_Load</c> (folder nodes mirrored) into the open scene
    /// on a grid (20×20, 30 per row). Uses <see cref="GfxSceneBuilder"/> for parity
    /// with browser instantiate. Library resolve for containers still needs GameDataIndex
    /// when textures/models are required.
    /// </summary>
    public static class GfxBatchImport
    {
        public const string LoadDirAsset = "Assets/ForsakenWorld/GFX_Load";
        public const float SpawnSpacingX = 20f;
        public const float SpawnSpacingZ = 20f;
        public const int ItemsPerRow = 30;

        [MenuItem("Tools/Forsaken World/GFX Ausführen")]
        public static void Ausfuehren()
        {
            var absolute = ToAbsolute(LoadDirAsset);
            if (!Directory.Exists(absolute))
            {
                Debug.Log(
                    "GFX Ausführen: " + LoadDirAsset +
                    " fehlt. Zu ladende .gfx dort ablegen (wie Godot res://GFX_Load/).");
                return;
            }

            var files = new List<string>();
            CollectGfx(absolute, absolute, files);
            if (files.Count == 0)
            {
                Debug.Log("GFX Ausführen: keine .gfx in " + LoadDirAsset);
                return;
            }

            var root = Selection.activeGameObject;
            if (root == null)
            {
                root = new GameObject("GFX_Batch");
                Undo.RegisterCreatedObjectUndo(root, "GFX Ausführen root");
            }

            var pathToNode = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase)
            {
                [""] = root.transform
            };

            GameDataIndex index = null;
            var gameRoot = EditorPrefs.GetString(
                "SilverEcho.ForsakenWorld.GameDataRoot", string.Empty);
            if (!string.IsNullOrEmpty(gameRoot) && Directory.Exists(gameRoot))
            {
                try
                {
                    index = new GameDataIndex(gameRoot);
                    index.Scan();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("GFX Ausführen: GameDataIndex failed: " + ex.Message);
                }
            }

            var counter = 0;
            foreach (var rel in files)
            {
                var parts = rel.Replace('\\', '/').Split('/');
                var currentPath = "";
                var parentPath = "";
                for (var i = 0; i < parts.Length; i++)
                {
                    var partName = parts[i];
                    var isFile = i == parts.Length - 1;
                    if (isFile)
                        partName = Path.GetFileNameWithoutExtension(partName);

                    parentPath = currentPath;
                    currentPath = string.IsNullOrEmpty(currentPath)
                        ? partName
                        : currentPath + "/" + partName;

                    if (pathToNode.ContainsKey(currentPath))
                        continue;

                    var parent = pathToNode[parentPath];
                    var existing = parent.Find(partName);
                    if (existing != null)
                    {
                        pathToNode[currentPath] = existing;
                        continue;
                    }

                    if (isFile)
                    {
                        var full = Path.Combine(absolute, rel);
                        byte[] bytes;
                        try
                        {
                            bytes = File.ReadAllBytes(full);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning(
                                "GFX Ausführen: read failed " + rel + ": " + ex.Message);
                            continue;
                        }

                        var document = new GfxParser().Parse(bytes);
                        if (document == null)
                        {
                            Debug.LogWarning("GFX Ausführen: parse failed " + rel);
                            continue;
                        }
                        var built = GfxSceneBuilder.Build(document, partName, full, index);
                        if (built == null || built.Root == null)
                        {
                            Debug.LogWarning("GFX Ausführen: build failed " + rel);
                            continue;
                        }

                        built.Root.transform.SetParent(parent, false);
                        built.Root.name = partName;
                        built.Root.transform.localPosition = new Vector3(
                            (counter % ItemsPerRow) * SpawnSpacingX,
                            0f,
                            (counter / ItemsPerRow) * SpawnSpacingZ);
                        Undo.RegisterCreatedObjectUndo(built.Root, "GFX Ausführen");
                        pathToNode[currentPath] = built.Root.transform;
                        counter++;
                    }
                    else
                    {
                        var folder = new GameObject(partName);
                        folder.transform.SetParent(parent, false);
                        Undo.RegisterCreatedObjectUndo(folder, "GFX Ausführen folder");
                        pathToNode[currentPath] = folder.transform;
                    }
                }
            }

            Debug.Log(
                "GFX Ausführen: Import abgeschlossen (" + counter +
                " Dateien aus " + LoadDirAsset + ").");
        }

        private static void CollectGfx(string rootAbs, string dir, List<string> into)
        {
            foreach (var file in Directory.GetFiles(dir, "*.gfx"))
            {
                var rel = file.Substring(rootAbs.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
                into.Add(rel);
            }
            foreach (var sub in Directory.GetDirectories(dir))
                CollectGfx(rootAbs, sub, into);
            into.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private static string ToAbsolute(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return string.Empty;
            if (assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(
                    Path.Combine(
                        Application.dataPath,
                        assetPath.Substring("Assets/".Length)));
            }
            return Path.GetFullPath(assetPath);
        }
    }
}
