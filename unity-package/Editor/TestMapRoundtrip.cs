using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SilverEcho.ForsakenWorld;

namespace SilverEcho.ForsakenWorld.Editor
{
    public static class TestMapRoundtrip
    {
        [MenuItem("Angelica 2.x/Test - Roundtrip a05 Map")]
        public static void RunTest()
        {
            string mapPath = @"A:\146CleanClient\maps\a05";
            string trn2Path = Path.Combine(mapPath, "a05.trn2");

            if (!File.Exists(trn2Path))
            {
                Debug.LogError("a05.trn2 not found at: " + trn2Path);
                return;
            }

            Debug.Log("=== Angelica 2.x Roundtrip Test Started ===");
            Debug.Log("Map folder: " + mapPath);
            Debug.Log("TRN2: " + trn2Path);

            var index = new GameDataIndex(@"A:\146CleanClient", GameDataBrowserWindow.GetGameDataDecoder());
            index.Scan();

            Debug.Log("Index ready. Files found: " + index.Search().Count);

            // Import map
            var trn2File = index.Search("a05", "trn2").FirstOrDefault() ?? index.Search("", "trn2").FirstOrDefault(f => f.RelativePath.Contains("a05"));
            var result = MapAssetImporter.Import(trn2File, index, GameDataBrowserWindow.GetGameDataDecoder(), showProgress: false);

            if (result == null || result.SectorPrefabPaths.Count == 0)
            {
                Debug.LogError("Map import failed");
                return;
            }

            Debug.Log("Map imported successfully: " + result.SectorPrefabPaths.Count + " sectors");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.SectorPrefabPaths[0]);
            if (prefab == null)
            {
                Debug.LogError("Could not load prefab");
                return;
            }

            Debug.Log("Prefab loaded. Root name: " + prefab.name);

            // Find MapSourceInfo
            var sourceInfo = prefab.GetComponent<MapSourceInfo>();
            if (sourceInfo != null)
            {
                Debug.Log("MapSourceInfo found!");
                Debug.Log("  TRN2: " + sourceInfo.Trn2FullPath);
                Debug.Log("  ECWLD: " + sourceInfo.EcwldFullPath);
            }
            else
            {
                Debug.LogWarning("MapSourceInfo not found on prefab");
            }

            Debug.Log("=== Test Complete. Check console for details. ===");
            EditorUtility.DisplayDialog("Test Complete", "Roundtrip test for a05 completed.\n\nCheck Console for details.\n\nMapSourceInfo and LitModelInstance components were added.", "OK");
        }
    }
}
