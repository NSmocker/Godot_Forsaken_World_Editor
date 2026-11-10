using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEditor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class BuildingImportBatchTests
    {
        [Test]
        public void MapReusesPreparedDocumentsAndReadsLaterBatchesOnDemand()
        {
            string name = "building_batch_" + Guid.NewGuid().ToString("N");
            string sourceRoot = Path.Combine(Path.GetTempPath(), name);
            string buildingRoot = "litmodels/" + name;
            string textureRelative = name + ".tga";
            string textureOutput = AssembledTextureImporter.GetTextureAssetPath(textureRelative);
            var decoder = new GfxTextDecoder(() => Encoding.GetEncoding(28591));
            Directory.CreateDirectory(Path.Combine(sourceRoot, buildingRoot));
            try
            {
                File.WriteAllBytes(Path.Combine(sourceRoot, name + ".trn2"), TerrainParserTests.BuildTrn2());
                File.WriteAllBytes(Path.Combine(sourceRoot, "sub0.t2bk"), TerrainParserTests.BuildT2bk(1, false, false));
                File.WriteAllBytes(Path.Combine(sourceRoot, "sub0.t2mk"), TerrainParserTests.BuildT2mk(false));
                File.WriteAllBytes(Path.Combine(sourceRoot, "sub0.t2lm"), TerrainParserTests.BuildT2lm(false));
                var texture = new byte[18 + 16];
                texture[2] = 2; texture[12] = 2; texture[14] = 2;
                texture[16] = 32; texture[17] = 8;
                for (int i = 18; i < texture.Length; i++) texture[i] = 255;
                File.WriteAllBytes(Path.Combine(sourceRoot, textureRelative), texture);
                var bytes = BmdParserTests.BuildBmd(true, false, BmdParser.MeshVersion04);
                int textureOffset = Encoding.ASCII.GetString(bytes).IndexOf("building/wall.dds", StringComparison.Ordinal);
                Assert.GreaterOrEqual(textureOffset, 0);
                Array.Clear(bytes, textureOffset, 256);
                Encoding.ASCII.GetBytes(textureRelative).CopyTo(bytes, textureOffset);
                for (int i = 0; i < 33; i++)
                    File.WriteAllBytes(Path.Combine(sourceRoot, buildingRoot, i.ToString("D2") + ".bmd"), bytes);

                var index = new GameDataIndex(sourceRoot); index.Scan();
                var map = index.Files.Single(f => f.Extension == "trn2");
                var ordered = index.Files.Where(f => f.Extension == "bmd" &&
                    f.RelativePath.StartsWith(buildingRoot + "/", StringComparison.OrdinalIgnoreCase)).ToArray();
                bool changed = false;
                BuildingBatchTestPostprocessor.WatchedPath = textureOutput;
                BuildingBatchTestPostprocessor.AfterImport = () =>
                {
                    // The current batch must keep its parsed snapshot, while a later
                    // batch must see this invalid source and report it independently.
                    File.WriteAllBytes(ordered[0].FullPath, new byte[] { 0 });
                    File.WriteAllBytes(ordered[32].FullPath, new byte[] { 0 });
                    changed = true;
                };
                var result = MapAssetImporter.Import(map, index, decoder, showProgress: false);
                Assert.IsTrue(changed, "The test must change sources during native texture prefetch.");
                Assert.AreEqual(32, result.Buildings, string.Join("\n", result.Warnings));
                Assert.IsFalse(result.Warnings.Any(w => w.Contains(ordered[0].RelativePath)));
                Assert.IsTrue(result.Warnings.Any(w => w.Contains(ordered[32].RelativePath)));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
                Assert.IsNotNull(prefab);
                Assert.AreEqual(32, prefab.GetComponentsInChildren<LitModelInstance>().Length);
                Assert.IsTrue(prefab.GetComponentsInChildren<LitModelInstance>()
                    .Select(b => b.GetComponent<MeshRenderer>()).All(r => r != null &&
                    r.sharedMaterial != null && r.sharedMaterial.GetTexture("_Albedo") != null));
            }
            finally
            {
                BuildingBatchTestPostprocessor.AfterImport = null;
                BuildingBatchTestPostprocessor.WatchedPath = null;
                // Remove the parent before its nested prefab dependencies.
                AssetDatabase.DeleteAsset(MapAssetImporter.GetOutputFolder(name + ".trn2"));
                for (int i = 0; i < 33; i++)
                    AssetDatabase.DeleteAsset(BuildingAssetImporter.GetOutputFolder(
                        buildingRoot + "/" + i.ToString("D2") + ".bmd"));
                AssetDatabase.DeleteAsset(TerrainAssetImporter.GetOutputFolder(name + ".trn2"));
                AssetDatabase.DeleteAsset(textureOutput);
                if (Directory.Exists(sourceRoot)) Directory.Delete(sourceRoot, true);
            }
        }
    }

    internal sealed class BuildingBatchTestPostprocessor : AssetPostprocessor
    {
        internal static string WatchedPath;
        internal static Action AfterImport;

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (AfterImport == null || !importedAssets.Contains(WatchedPath)) return;
            var callback = AfterImport;
            AfterImport = null;
            callback();
        }
    }
}
