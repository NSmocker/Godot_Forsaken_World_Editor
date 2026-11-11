using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class TerrainSectorImportTests
    {
        private string sourceRoot;
        private GameDataFile mapFile;
        private GameDataIndex index;
        private readonly GfxTextDecoder decoder = new GfxTextDecoder(() => Encoding.GetEncoding(28591));

        [SetUp]
        public void SetUp()
        {
            string name = "sector_test_" + Guid.NewGuid().ToString("N");
            sourceRoot = Path.Combine(Path.GetTempPath(), name);
            Directory.CreateDirectory(sourceRoot);
            File.WriteAllBytes(Path.Combine(sourceRoot, name + ".trn2"),
                TerrainParserTests.BuildTrn2(width: 4, length: 4, subRows: 2, subColumns: 2));
            for (int s = 0; s < 4; s++)
            {
                File.WriteAllBytes(Path.Combine(sourceRoot, "sub" + s + ".t2bk"),
                    TerrainParserTests.BuildT2bk(1, false, false, row: s / 2, column: s % 2));
                File.WriteAllBytes(Path.Combine(sourceRoot, "sub" + s + ".t2mk"),
                    TerrainParserTests.BuildT2mk(false));
                File.WriteAllBytes(Path.Combine(sourceRoot, "sub" + s + ".t2lm"),
                    TerrainParserTests.BuildT2lm(false));
            }
            index = new GameDataIndex(sourceRoot);
            index.Scan();
            mapFile = index.Files.Single(f => f.Extension == "trn2");
        }

        [TearDown]
        public void TearDown()
        {
            if (mapFile != null)
            {
                AssetDatabase.DeleteAsset(MapAssetImporter.GetOutputFolder(mapFile.RelativePath));
                AssetDatabase.DeleteAsset(TerrainAssetImporter.GetOutputFolder(mapFile.RelativePath));
            }
            if (Directory.Exists(sourceRoot)) Directory.Delete(sourceRoot, true);
        }

        [Test]
        public void MapImportSkipsUnselectedFilesAndKeepsSourceCoordinatesAndMaterials()
        {
            // The skipped sectors are missing/corrupt. They must not be read or warned about.
            File.Delete(Path.Combine(sourceRoot, "sub0.t2bk"));
            File.Delete(Path.Combine(sourceRoot, "sub0.t2mk"));
            File.WriteAllBytes(Path.Combine(sourceRoot, "sub2.t2bk"), new byte[] { 0 });
            File.WriteAllBytes(Path.Combine(sourceRoot, "sub2.t2lm"), new byte[] { 0 });
            index.Scan();

            var result = ImportMap(3, 1, 1); // Duplicate and unordered input must not duplicate sectors.
            Assert.AreEqual(2, result.TerrainAreas);
            Assert.AreEqual(2, result.SectorPrefabPaths.Count);
            var sectorOne = AssetDatabase.LoadAssetAtPath<GameObject>(result.SectorPrefabPaths[0]);
            Assert.IsNull(sectorOne.transform.Find("Terrain/sub1").GetComponent<Terrain>());
            Assert.IsNotNull(sectorOne.transform.Find("Terrain/sub1").GetComponentInChildren<MeshFilter>());
            Assert.IsNull(sectorOne.transform.Find("Terrain/sub3"));
            CollectionAssert.AreEqual(new[] { "sub1", "sub3" }, TerrainChildNames(result));
            AssertTerrainCorner(result, "sub1", new Vector3(0f, 1.5f, 2f), 0f);
            AssertTerrainCorner(result, "sub3", new Vector3(0f, 1.5f, 0f), 2f);
            Assert.IsFalse(result.Warnings.Any(w => w.Contains("sub0") || w.Contains("sub2")),
                string.Join("\n", result.Warnings));
            string sub1 = TerrainAssetImporter.GetSectorFolder(mapFile.RelativePath, "sub1");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Mesh>(sub1 + "/Area_0000_Mesh.asset"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(sub1 + "/Area_0000_Splat0.asset"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(sub1 + "/Area_0000_Height.asset"));
            Assert.IsFalse(AssetDatabase.IsValidFolder(TerrainAssetImporter.GetSectorFolder(mapFile.RelativePath, "sub0")));
            Assert.IsFalse(AssetDatabase.IsValidFolder(TerrainAssetImporter.GetSectorFolder(mapFile.RelativePath, "sub2")));
        }

        [TestCase(0)] // No external texture at all.
        [TestCase(1)] // Corrupt external texture must never be read.
        [TestCase(2)] // Different external content must not overwrite the project copy.
        [TestCase(3)] // No project copy: retain source fallback.
        public void GroundTexturesPreferProjectFilesBeforeExternalSources(int sourceMode)
        {
            string relative = "textures/" + mapFile.Name + "/ground.dds";
            string assetPath = AssembledTextureImporter.GetTextureAssetPath(relative);
            string absolute = AssembledTextureImporter.AssetPathToAbsolute(assetPath);
            var bytes = DdsHeaderParserTests.BuildDds(4, 4, "DXT1", 1, 8);
            bytes[128] = 0; bytes[129] = 248; // RGB565 red endpoint.
            bytes[130] = 224; bytes[131] = 7; // Green endpoint.
            bytes[134] = 85; bytes[135] = 85; // Last two rows use green.
            var other = (byte[])bytes.Clone();
            other[128] = 31; other[129] = 0;
            string external = Path.Combine(sourceRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(external));
            if (sourceMode != 0)
                File.WriteAllBytes(external, sourceMode == 1 ? new byte[] { 0 } : sourceMode == 3 ? bytes : other);
            File.WriteAllBytes(mapFile.FullPath, TerrainParserTests.BuildTrn2(
                width: 4, length: 4, subRows: 2, subColumns: 2, textureName: relative.Replace('/', '\\')));
            index.Scan();
            try
            {
                if (sourceMode != 3)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(absolute));
                    File.WriteAllBytes(absolute, bytes);
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                    var project = TerrainTextureBuilder.FindProjectTexture(relative.Replace('/', '\\'));
                    Assert.IsNotNull(project);
                    var loaded = TerrainTextureBuilder.LoadProjectTexture(project);
                    var expected = TerrainTextureBuilder.DecodeDds(bytes, relative);
                    try { CollectionAssert.AreEqual(expected.GetPixels32(), loaded.GetPixels32()); }
                    finally { UnityEngine.Object.DestroyImmediate(loaded); UnityEngine.Object.DestroyImmediate(expected); }
                }
                var result = TerrainAssetImporter.Import(mapFile, index, decoder, false,
                    selectedSectorIndices: new[] { 0 });
                Assert.AreEqual(1, result.Areas);
                Assert.IsFalse(result.Warnings.Any(w => w.Contains("magenta fallback")), string.Join("\n", result.Warnings));
                var terrainObject = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
                var linked = terrainObject.GetComponentInChildren<MeshRenderer>().sharedMaterial.GetTexture("_Layer0");
                Assert.IsNotNull(linked);
                if (sourceMode != 3)
                    Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath), linked);
                if (sourceMode != 3) CollectionAssert.AreEqual(bytes, File.ReadAllBytes(absolute));
            }
            finally { AssetDatabase.DeleteAsset(Path.GetDirectoryName(assetPath).Replace('\\', '/')); }
        }

        [TestCase("../outside.dds")]
        [TestCase("/outside.dds")]
        [TestCase("C:/outside.dds")]
        public void ProjectGroundTextureLookupRejectsNonRelativePaths(string path)
        {
            Assert.IsNull(TerrainTextureBuilder.FindProjectTexture(path));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MapImportWithoutSelectionImportsAllSectors(bool defaultOptions)
        {
            var result = MapAssetImporter.Import(mapFile, index, decoder, showProgress: false,
                options: defaultOptions ? new MapImportOptions() : null);
            Assert.AreEqual(4, result.TerrainAreas);
            Assert.AreEqual(4, result.SectorPrefabPaths.Count);
            Assert.AreEqual(4, TerrainChildNames(result).Length);
        }

        [Test]
        public void ReimportReplacesSectorHierarchyAndPreservesAssetGuids()
        {
            var first = ImportMap(1);
            string sectorPath = first.SectorPrefabPaths.Single();
            string sectorGuid = AssetDatabase.AssetPathToGUID(sectorPath);
            string terrainGuid = AssetDatabase.AssetPathToGUID(first.TerrainPrefabPath);
            string meshPath = TerrainAssetImporter.GetSectorFolder(mapFile.RelativePath, "sub1") + "/Area_0000_Mesh.asset";
            string meshGuid = AssetDatabase.AssetPathToGUID(meshPath);
            var full = MapAssetImporter.Import(mapFile, index, decoder, showProgress: false);
            Assert.AreEqual(4, full.SectorPrefabPaths.Count);
            Assert.AreEqual(meshGuid, AssetDatabase.AssetPathToGUID(meshPath));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>(
                MapAssetImporter.GetOutputFolder(mapFile.RelativePath) + "/" + mapFile.Name + ".prefab"));

            var last = ImportMap(3);
            Assert.AreEqual(sectorPath, first.SectorPrefabPaths.Single());
            Assert.AreEqual(sectorGuid, AssetDatabase.AssetPathToGUID(sectorPath));
            Assert.AreEqual(terrainGuid, AssetDatabase.AssetPathToGUID(last.TerrainPrefabPath));
            Assert.AreEqual(1, last.TerrainAreas);
            Assert.AreEqual(1, last.SectorPrefabPaths.Count);
            CollectionAssert.AreEqual(new[] { "sub3" }, TerrainChildNames(last));
        }

        [Test]
        public void ExplicitEmptySelectionDoesNotFallBackToFullImport()
        {
            Assert.IsTrue(new MapImportOptions().ImportFullMap);
            Assert.IsFalse(new MapImportOptions { SelectedSectorIndices = new List<int>() }.ImportFullMap);
            Assert.Throws<ArgumentException>(() => ImportMap());
            Assert.IsFalse(AssetDatabase.IsValidFolder(TerrainAssetImporter.GetOutputFolder(mapFile.RelativePath)));
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void InvalidSectorFailsBeforeGeneratingTerrainAssets(int invalidSector)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ImportMap(1, invalidSector));
            Assert.IsFalse(AssetDatabase.IsValidFolder(TerrainAssetImporter.GetOutputFolder(mapFile.RelativePath)));
        }

        [Test]
        public void SectorImportSkipsBuildingsOutsideTheSelectionAndKeepsBorderOverlap()
        {
            string mapName = mapFile.Name;
            string insideRel = "litmodels/" + mapName + "/inside.bmd";
            string outsideRel = "litmodels/" + mapName + "/outside.bmd";
            Directory.CreateDirectory(Path.Combine(sourceRoot, "litmodels", mapName));
            File.WriteAllBytes(Path.Combine(sourceRoot, insideRel.Replace('/', Path.DirectorySeparatorChar)),
                BmdParserTests.BuildBmd(true, false, BmdParser.MeshVersion04,
                    posX: -1.5f, posY: 0f, posZ: 1f, scaleX: 0.1f, scaleY: 1f, scaleZ: 1f));
            File.WriteAllBytes(Path.Combine(sourceRoot, outsideRel.Replace('/', Path.DirectorySeparatorChar)),
                BmdParserTests.BuildBmd(true, false, BmdParser.MeshVersion04,
                    posX: 8f, posY: 0f, posZ: 8f, scaleX: 1f, scaleY: 1f, scaleZ: 1f));
            index.Scan();
            mapFile = index.Files.Single(f => f.Extension == "trn2");
            string insideFolder = BuildingAssetImporter.GetOutputFolder(insideRel);
            string outsideFolder = BuildingAssetImporter.GetOutputFolder(outsideRel);
            try
            {
                var result = ImportMap(0);
                Assert.AreEqual(1, result.Buildings, string.Join("\n", result.Warnings));
                Assert.AreEqual(1, result.SkippedOutsideSectors);
                Assert.IsTrue(result.Warnings.Any(w => w.Contains("outside the selected sectors")));
                Assert.IsFalse(AssetDatabase.IsValidFolder(outsideFolder));
                Assert.IsTrue(AssetDatabase.IsValidFolder(insideFolder));
            }
            finally
            {
                // Delete the parent first so removing its nested building prefab does
                // not trigger a missing-prefab error during test cleanup.
                AssetDatabase.DeleteAsset(MapAssetImporter.GetOutputFolder(mapFile.RelativePath));
                AssetDatabase.DeleteAsset(insideFolder);
                AssetDatabase.DeleteAsset(outsideFolder);
            }
        }

        [Test]
        public void UnreadableSelectionDoesNotImportOtherSectors()
        {
            File.WriteAllBytes(Path.Combine(sourceRoot, "sub3.t2bk"), new byte[] { 0 });
            var error = Assert.Throws<InvalidDataException>(() => ImportMap(3));
            StringAssert.Contains("Selected terrain sectors", error.Message);
            string folder = TerrainAssetImporter.GetSectorFolder(mapFile.RelativePath, "sub3");
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/Area_0000_Mesh.asset"));
        }

        private MapAssetImporter.Result ImportMap(params int[] sectors)
        {
            return MapAssetImporter.Import(mapFile, index, decoder, showProgress: false,
                options: new MapImportOptions { SelectedSectorIndices = sectors.ToList() });
        }

        [TestCase("ImportSelectedMap", true)]
        [TestCase("InstantiateSelectedMap", true)]
        [TestCase("ImportSelectedTerrain", true)]
        [TestCase("ImportSelectedMap", false)]
        [TestCase("InstantiateSelectedMap", false)]
        [TestCase("ImportSelectedTerrain", false)]
        public void BrowserButtonsWaitForSelectionAndImportOnlyConfirmedSectors(string action, bool confirm)
        {
            var previousSelection = Selection.activeObject;
            var previousScene = SceneManager.GetActiveScene();
            var testScene = default(Scene);
            GameDataBrowserWindow browser = null;
            MapImportOptionsWindow dialog = null;
            try
            {
                if (action == "InstantiateSelectedMap" && confirm)
                {
                    testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    SceneManager.SetActiveScene(testScene);
                }
                browser = ScriptableObject.CreateInstance<GameDataBrowserWindow>();
                SetField(browser, "selectedFile", mapFile);
                SetField(browser, "index", index);
                SetField(browser, "selectedTerrain",
                    new TerrainParser(decoder).Parse(File.ReadAllBytes(mapFile.FullPath)));
                Invoke(browser, action);
                dialog = Resources.FindObjectsOfTypeAll<MapImportOptionsWindow>()
                    .SingleOrDefault(w => w.Trn2File == mapFile);
                Assert.IsNotNull(dialog, action + " must open the sector dialog before importing.");
                Assert.IsFalse(AssetDatabase.IsValidFolder(TerrainAssetImporter.GetOutputFolder(mapFile.RelativePath)));
                Assert.IsFalse(AssetDatabase.IsValidFolder(MapAssetImporter.GetOutputFolder(mapFile.RelativePath)));

                if (!confirm)
                {
                    dialog.Close();
                    Assert.IsFalse(AssetDatabase.IsValidFolder(TerrainAssetImporter.GetOutputFolder(mapFile.RelativePath)));
                    Assert.AreEqual(previousSelection, Selection.activeObject);
                    return;
                }

                // Empty confirmation does nothing. Then pick only the last sector.
                SetField(dialog, "selectedSectors", new[] { false, false, false, false });
                Invoke(dialog, "ConfirmSelection");
                Assert.IsFalse(AssetDatabase.IsValidFolder(TerrainAssetImporter.GetOutputFolder(mapFile.RelativePath)));
                SetField(dialog, "selectedSectors", new[] { false, false, false, true });
                // Changing the browser while the dialog is open must not change the imported map.
                SetField(browser, "selectedFile", null);
                SetField(browser, "selectedTerrain", null);
                SetField(browser, "index", null);
                Invoke(dialog, "ConfirmSelection");

                if (action == "ImportSelectedTerrain")
                {
                    var imported = Selection.activeObject as GameObject;
                    Assert.IsNotNull(imported);
                    Assert.IsTrue(EditorUtility.IsPersistent(imported));
                    Assert.AreEqual(1, imported.transform.childCount);
                    Assert.AreEqual("sub3", imported.transform.GetChild(0).name);
                    return;
                }

                if (action == "InstantiateSelectedMap")
                {
                    var imported = Selection.activeObject as GameObject;
                    Assert.IsNotNull(imported);
                    Assert.IsFalse(EditorUtility.IsPersistent(imported));
                    Assert.AreEqual(testScene, imported.scene);
                    var sector = imported.transform.Find("Sector_0003");
                    Assert.IsNotNull(sector);
                    Assert.AreEqual("sub3", sector.Find("Terrain").GetChild(0).name);
                    Assert.AreEqual(0, imported.GetComponentsInChildren<Light>(true).Length);
                    return;
                }

                var sectorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    MapAssetImporter.GetOutputFolder(mapFile.RelativePath) + "/Sectors/Sector_0003.prefab");
                Assert.IsNotNull(sectorPrefab);
                Assert.AreEqual("sub3", sectorPrefab.transform.Find("Terrain").GetChild(0).name);
                Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>(
                    MapAssetImporter.GetOutputFolder(mapFile.RelativePath) + "/" + mapFile.Name + ".prefab"));
            }
            finally
            {
                // Asset refresh during import can drop an unshown window's container.
                // Close() then throws; the import itself already finished.
                SafeClose(dialog);
                SafeClose(browser);
                if (testScene.IsValid())
                {
                    SceneManager.SetActiveScene(previousScene);
                    EditorSceneManager.CloseScene(testScene, true);
                }
                Selection.activeObject = previousSelection;
            }
        }

        private static void SafeClose(EditorWindow window)
        {
            if (window == null)
                return;
            try
            {
                window.Close();
            }
            catch (NullReferenceException)
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static void SetField(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static void Invoke(object target, string method)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        private static string[] TerrainChildNames(MapAssetImporter.Result result)
        {
            return result.SectorPrefabPaths
                .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path))
                .SelectMany(prefab => prefab.transform.Find("Terrain").Cast<Transform>())
                .Select(child => child.name)
                .OrderBy(name => name)
                .ToArray();
        }

        private static void AssertTerrainCorner(MapAssetImporter.Result result, string subName, Vector3 corner, float minHeight)
        {
            MeshFilter filter = null;
            foreach (var sectorPath in result.SectorPrefabPaths)
            {
                var sector = AssetDatabase.LoadAssetAtPath<GameObject>(sectorPath);
                var child = sector.transform.Find("Terrain/" + subName);
                if (child == null)
                    continue;
                filter = child.GetComponentInChildren<MeshFilter>();
                break;
            }
            Assert.IsNotNull(filter, subName);
            Assert.IsNull(filter.GetComponent<Terrain>());
            var world = filter.transform.TransformPoint(filter.sharedMesh.vertices[0]);
            Assert.AreEqual(corner.x, world.x, 0.001f);
            Assert.AreEqual(corner.y, world.y, 0.02f);
            Assert.AreEqual(corner.z, world.z, 0.001f);
            var material = filter.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.AreEqual(TerrainAssetImporter.ShaderName, material.shader.name);
            Assert.AreEqual(minHeight, material.GetFloat("_MinHeight"), 0.001f);
            Assert.AreEqual(1f, material.GetFloat("_UseLightmap"), 0.001f);
            Assert.IsNotNull(material.GetTexture("_Splat0"));
            Assert.IsNotNull(material.GetTexture("_HeightMap"));
            Assert.IsNotNull(material.GetTexture("_Lightmap"));
        }
    }
}
