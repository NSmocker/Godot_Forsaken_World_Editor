using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SilverEcho.ForsakenWorld;

namespace SilverEcho.ForsakenWorld.Editor
{
    public static class MapExporter
    {
        [MenuItem("Tools/Angelica 2.x/Export Map Changes Back", true)]
        private static bool ValidateExportMapChanges()
        {
            return Selection.activeGameObject != null &&
                   Selection.activeGameObject.GetComponentInParent<MapSourceInfo>() != null;
        }

        [MenuItem("Tools/Angelica 2.x/Export Map Changes Back")]
        private static void ExportMapChanges()
        {
            var root = Selection.activeGameObject;
            var info = root.GetComponentInParent<MapSourceInfo>();

            if (info == null || !info.IsValid)
            {
                EditorUtility.DisplayDialog("Angelica 2.x", "Selected object is not an imported map with source tracking.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Export Map Changes",
                $"Export changes back to original files?\n\n" +
                $"TRN2: {info.Trn2FullPath}\n" +
                $"ECWLD: {info.EcwldFullPath}\n\n" +
                "This will overwrite original game files!\n\n" +
                "Make sure you have a backup.", "Export", "Cancel"))
                return;

            try
            {
                EditorUtility.DisplayProgressBar("Angelica 2.x", "Exporting map changes...", 0.1f);

                // TODO: Implement actual export logic
                // Phase 1: Export moved/rotated buildings and GFX parameters
                bool success = ExportObjectTransforms(info, root);

                EditorUtility.ClearProgressBar();

                if (success)
                {
                    info.HasChanges = false;
                    EditorUtility.SetDirty(info);
                    AssetDatabase.SaveAssets();

                    EditorUtility.DisplayDialog("Success", 
                        "Map changes exported back to original game files.\n\n" +
                        "Buildings positions and GFX parameters were updated.", "OK");
                }
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Error", "Failed to export map:\n\n" + e.Message, "OK");
            }
        }

        private static bool ExportObjectTransforms(MapSourceInfo info, GameObject root)
        {
            Debug.Log($"[Angelica 2.x] Starting export for map: {info.Trn2FullPath}");

            var litInstances = root.GetComponentsInChildren<LitModelInstance>(true)
                .Where(l => l.HasBeenModified)
                .ToList();

            if (litInstances.Count == 0)
            {
                EditorUtility.DisplayDialog("No Changes", "No modified buildings found to export.", "OK");
                return true;
            }

            var message = $"Found {litInstances.Count} modified building(s).\n\nThese changes will be written directly into the original .ecwld file.\n\nContinue?";

            if (!EditorUtility.DisplayDialog("Export to .ecwld", message, "Yes, Overwrite .ecwld", "Cancel"))
                return false;

            // Load original document
            var decoder = GameDataBrowserWindow.GetGameDataDecoder();
            var parser = new EcwldParser(decoder);
            EcwldDocument doc;

            try
            {
                doc = parser.Parse(File.ReadAllBytes(info.EcwldFullPath));
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", "Failed to parse original .ecwld:\n" + e.Message, "OK");
                return false;
            }

            bool success = EcwldWriter.WriteChanges(doc, info.EcwldFullPath, litInstances, out var error);

            if (success)
            {
                // Clear modified flags
                foreach (var lit in litInstances)
                    lit.HasBeenModified = false;

                EditorUtility.DisplayDialog("Success", 
                    $"Successfully updated {litInstances.Count} buildings in original .ecwld file.\n\n" +
                    $"Backup was created.\n\n" +
                    $"File: {info.EcwldFullPath}", "OK");
                return true;
            }
            else
            {
                EditorUtility.DisplayDialog("Export Failed", error ?? "Unknown error", "OK");
                return false;
            }
        }
    }
}
