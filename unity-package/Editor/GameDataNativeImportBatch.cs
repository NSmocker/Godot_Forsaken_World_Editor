using System;
using System.Collections.Generic;
using UnityEditor;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Copies are staged by the texture and audio importers. This flushes the queued
    /// asset paths through one or more <see cref="AssetDatabase.StartAssetEditing"/> chunks
    /// so Unity does not refresh the project after every file.
    /// Load the assets only after <see cref="Import"/> returns. A null load during the
    /// pause must not be treated as a failed native import.
    /// </summary>
    public static class GameDataNativeImportBatch
    {
        // Large enough to collapse a sector's unique files into a few refreshes,
        // small enough to keep a cancellation point and a bounded import spike.
        public const int ChunkSize = 64;

        private static readonly HashSet<string> importedPaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static void Reset()
        {
            importedPaths.Clear();
        }

        internal static bool WasImported(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) && importedPaths.Contains(assetPath);
        }

        internal static void NoteImported(string assetPath)
        {
            if (!string.IsNullOrEmpty(assetPath))
                importedPaths.Add(assetPath);
        }

        public static int Import(ICollection<string> assetPaths, string progressLabel = null)
        {
            if (assetPaths == null || assetPaths.Count == 0)
                return 0;
            if (GameDataAssetBatch.IsActive)
                throw new InvalidOperationException(
                    "Import native files before starting the asset write batch.");

            var unique = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in assetPaths)
            {
                if (string.IsNullOrEmpty(path) || importedPaths.Contains(path) || !seen.Add(path))
                    continue;
                unique.Add(path);
            }

            if (unique.Count == 0)
                return 0;

            var imported = 0;
            for (var offset = 0; offset < unique.Count; offset += ChunkSize)
            {
                if (!string.IsNullOrEmpty(progressLabel) &&
                    EditorUtility.DisplayCancelableProgressBar(
                        "Import Angelica assets",
                        progressLabel + " (" + (offset + 1) + "-" +
                        Math.Min(offset + ChunkSize, unique.Count) + "/" + unique.Count + ")",
                        unique.Count == 0 ? 1f : (float)offset / unique.Count))
                    throw new OperationCanceledException(
                        "Native import cancelled. Copied files are left in place; import again to finish.");

                var count = Math.Min(ChunkSize, unique.Count - offset);
                AssetDatabase.StartAssetEditing();
                try
                {
                    for (var i = 0; i < count; i++)
                        AssetDatabase.ImportAsset(unique[offset + i], ImportAssetOptions.ForceUpdate);
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                for (var i = 0; i < count; i++)
                    NoteImported(unique[offset + i]);
                imported += count;
                if (GameDataImportSession.Metrics != null)
                {
                    GameDataImportSession.Metrics.NativeImports += count;
                    GameDataImportSession.Metrics.NativeImportBatches++;
                }
            }

            return imported;
        }
    }
}
