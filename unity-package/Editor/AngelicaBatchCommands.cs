using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>Unity CLI entry points. Invoke with -executeMethod.</summary>
    public static class AngelicaBatchCommands
    {
        public const string DefaultMapRelative = "maps/a37/a37.trn2";

        public static void RebuildA37Grass()
        {
            var code = 1;
            try
            {
                code = RebuildGrass(DefaultMapRelative) ? 0 : 1;
            }
            catch (Exception exception)
            {
                Debug.LogError("A37_GRASS_FAIL " + exception);
                code = 1;
            }

            EditorApplication.Exit(code);
        }

        public static void ImportA37()
        {
            var code = 1;
            try
            {
                code = ImportMap(DefaultMapRelative) ? 0 : 1;
            }
            catch (Exception exception)
            {
                Debug.LogError("A37_IMPORT_FAIL " + exception);
                code = 1;
            }

            EditorApplication.Exit(code);
        }

        private static bool ImportMap(string relative)
        {
            var cachePath = GameDataScanCache.DefaultCachePath(Application.dataPath);
            if (!File.Exists(cachePath))
                throw new FileNotFoundException("Game data scan cache is missing: " + cachePath);

            var header = File.ReadAllLines(cachePath);
            if (header.Length == 0 || !header[0].StartsWith("#root=", StringComparison.Ordinal))
                throw new InvalidDataException("Scan cache has no #root= line.");
            var root = header[0].Substring("#root=".Length).Trim();
            GameDataScanCache.Snapshot snapshot;
            if (!GameDataScanCache.TryLoad(cachePath, root, out snapshot))
                throw new InvalidDataException("Could not load scan cache for " + root);

            var index = new GameDataIndex(root);
            index.ReplaceFromRelativePaths(snapshot.RelativePaths);
            if (!index.TryResolve(relative, out var file, out var error))
                throw new FileNotFoundException(error ?? relative);

            Debug.Log("A37_IMPORT_START " + file.FullPath + " index=" + index.Files.Count);
            var result = MapAssetImporter.Import(file, index, GameDataBrowserWindow.GetGameDataDecoder(), showProgress: false);
            foreach (var warning in result.Warnings)
                Debug.LogWarning("A37_IMPORT_WARN " + warning);
            Debug.Log(
                "A37_IMPORT_OK prefab=" + result.PrefabPath +
                " terrainAreas=" + result.TerrainAreas +
                " buildings=" + result.Buildings +
                " water=" + result.Water +
                " effects=" + result.Effects +
                " models=" + result.Models +
                " grass=" + result.GrassBlades +
                " warnings=" + result.Warnings.Count);
            return !string.IsNullOrEmpty(result.PrefabPath);
        }

        private static bool RebuildGrass(string relative)
        {
            var cachePath = GameDataScanCache.DefaultCachePath(Application.dataPath);
            var header = File.ReadAllLines(cachePath);
            var root = header[0].Substring("#root=".Length).Trim();
            GameDataScanCache.Snapshot snapshot;
            if (!GameDataScanCache.TryLoad(cachePath, root, out snapshot))
                throw new InvalidDataException("Could not load scan cache for " + root);
            var index = new GameDataIndex(root);
            index.ReplaceFromRelativePaths(snapshot.RelativePaths);
            if (!index.TryResolve(relative, out var file, out var error))
                throw new FileNotFoundException(error ?? relative);

            var folder = MapAssetImporter.GetOutputFolder(file.RelativePath);
            var prefabPath = folder + "/" + file.Name + ".prefab";
            Debug.Log("A37_GRASS_START " + prefabPath);
            var blades = MapAssetImporter.RebuildGrass(
                file, index, GameDataBrowserWindow.GetGameDataDecoder(), prefabPath, folder);
            Debug.Log("A37_GRASS_OK blades=" + blades + " prefab=" + prefabPath);
            return blades > 0;
        }
    }
}
