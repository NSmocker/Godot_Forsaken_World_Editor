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
                File.WriteAllBytes(Path.Combine(sourceRoot, "sub" + s + ".t2lm1"),
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
            // A scan cache written before T2LM1 support omits the night maps.
            index.ReplaceFromRelativePaths(index.Files.Where(file => file.Extension != "t2lm1")
                .Select(file => file.RelativePath).ToArray());

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
                width: 4, length: 4, subRows: 2, subColumns: 2, textures: 24, textureName: relative.Replace('/', '\\')));
            File.WriteAllBytes(Path.Combine(sourceRoot, "sub0.t2mk"),
                TerrainParserTests.BuildT2mk(false, textureIndex: 23));
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
                Assert.IsFalse(result.Warnings.Any(w => w.Contains("Ground texture")), string.Join("\n", result.Warnings));
                var terrainObject = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
                var material = terrainObject.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                var linked = (Texture2D)material.GetTexture("_Layer0");
                Assert.IsNotNull(linked);
                Assert.AreSame(linked, material.GetTexture("_Layer1"));
                var parameters = (Texture2D)material.GetTexture("_LayerParams");
                Assert.AreEqual(new Color(0, 1, 1, 1), parameters.GetPixel(0, 0));
                Assert.AreEqual(new Color(1, 2, 2, 1), parameters.GetPixel(1, 0));
                Assert.IsFalse(UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(linked.graphicsFormat));
                Assert.AreEqual(new Color32(255, 0, 0, 255), linked.GetPixels32()[0]);
                Assert.AreEqual(new Color32(0, 255, 0, 255), linked.GetPixels32()[15]);
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
            Assert.IsNull(TerrainTextureBuilder.FindPackedTexture(path, Array.Empty<string>()));
        }

        [TestCase("textures.pck")]
        [TestCase("textures . pck")]
        public void GroundMaterialLinksExactPackedTextureWithoutCopyingIt(string folderName)
        {
            string fixture = "Assets/" + mapFile.Name;
            string relative = "Textures/Maps/" + mapFile.Name + "/ground.dds";
            string asset = fixture + "/" + folderName + "/" + relative.Substring("Textures/".Length);
            string wrongDirectory = fixture + "/" + folderName + "/ground.dds";
            string legacy = AssembledTextureImporter.GetTextureAssetPath(relative);
            var dds = DdsHeaderParserTests.BuildDds(4, 4, "DXT1", 1, 8);
            dds[129] = 248;
            try
            {
                WriteFile(AngelicaAssetFolders.AssetPathToAbsolute(legacy), dds);
                WriteFile(AngelicaAssetFolders.AssetPathToAbsolute(wrongDirectory), dds);
                WriteFile(AngelicaAssetFolders.AssetPathToAbsolute(asset), dds);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                WriteFile(Path.Combine(sourceRoot, relative), new byte[] { 0 });
                File.WriteAllBytes(mapFile.FullPath, TerrainParserTests.BuildTrn2(
                    width: 4, length: 4, subRows: 2, subColumns: 2, textures: 1, textureName: relative.Replace('/', '\\')));
                index.Scan();

                var graph = new GameDataDependencyGraph(index, decoder);
                graph.AddRoot(mapFile.FullPath);
                Assert.IsFalse(graph.ImportOrder.Any(node => node.File.Extension == "dds"));
                Assert.IsFalse(graph.Roots.Single().Warnings.Any(w => w.Contains("Ground texture")));

                var result = TerrainAssetImporter.Import(mapFile, index, decoder, false, selectedSectorIndices: new[] { 0 });
                Assert.IsFalse(result.Warnings.Any(w => w.Contains("Ground texture")), string.Join("\n", result.Warnings));
                var terrain = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
                var material = terrain.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                Assert.AreEqual(asset, AssetDatabase.GetAssetPath(material.GetTexture("_Layer0")));
                Assert.IsFalse(AssetDatabase.IsValidFolder(TerrainAssetImporter.GetOutputFolder(mapFile.RelativePath) + "/GroundTextures"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(TerrainAssetImporter.GetOutputFolder(mapFile.RelativePath));
                AssetDatabase.DeleteAsset(fixture);
                AssetDatabase.DeleteAsset(Path.GetDirectoryName(legacy).Replace('\\', '/'));
            }
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
        public void SectorImportPrefersProjectBmdsAndFallsBackByFullRelativePath()
        {
            string projectRoot = "Assets/" + mapFile.Name;
            string mapFolder = "litmodels/" + mapFile.Name;
            string shadowed = mapFolder + "/36/wall.bmd";
            string fallback = mapFolder + "/37/wall.bmd";
            string projectFolder = projectRoot + "/LitModels/" + mapFile.Name.ToUpperInvariant() + "/36/";
            string projectModel = projectFolder + "Wall.bmd";
            string projectOnly = projectFolder + "custom.bmd";

            try
            {
                WriteFile(Path.Combine(sourceRoot, shadowed), Model(8f));
                WriteFile(Path.Combine(sourceRoot, fallback), Model(-1f));
                WriteFile(AngelicaAssetFolders.AssetPathToAbsolute(projectModel), Model(-1.75f));
                WriteFile(AngelicaAssetFolders.AssetPathToAbsolute(projectOnly), Model(-1.25f));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                index.Scan();

                var result = ImportMap(0);
                Assert.AreEqual(3, result.Buildings, string.Join("\n", result.Warnings));
                Assert.AreEqual(0, result.SkippedOutsideSectors);
                CollectionAssert.AreEquivalent(new[]
                {
                    projectModel, projectOnly, BuildingAssetImporter.GetOutputFolder(fallback) + "/wall.prefab"
                }, result.BuildingPaths);
                Assert.IsFalse(AssetDatabase.IsValidFolder(BuildingAssetImporter.GetOutputFolder(shadowed)));

                var sector = AssetDatabase.LoadAssetAtPath<GameObject>(result.SectorPrefabPaths.Single());
                var wall = sector.transform.Find("Buildings/36/Wall");
                Assert.IsNotNull(wall);
                Assert.AreEqual(-1.75f, wall.localPosition.x);
                Assert.AreEqual(-1f, sector.transform.Find("Buildings/37/wall").localPosition.x);
                var placed = wall.GetComponents<LitModelInstance>().Single();
                Assert.AreEqual(projectModel, AssetDatabase.GetAssetPath(placed.SourceData));
                Assert.AreEqual(projectModel, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(wall));
            }
            finally
            {
                AssetDatabase.DeleteAsset(MapAssetImporter.GetOutputFolder(mapFile.RelativePath));
                AssetDatabase.DeleteAsset(BuildingAssetImporter.GetOutputFolder(fallback));
                AssetDatabase.DeleteAsset(projectRoot);
            }

            byte[] Model(float x) => BmdParserTests.BuildBmd(true, false, BmdParser.MeshVersion04,
                posX: x, posY: 0f, posZ: 1f, scaleX: 0.1f);
        }

        [Test]
        public void DroppedMapDoesNotReadShadowedExternalBmd()
        {
            string projectRoot = "Assets/" + mapFile.Name;
            string mapDirectory = Path.Combine(sourceRoot, mapFile.Name);
            string rawAssetFolder = GameDataDropImporter.ImportRoot + "/Sources/" + mapFile.Name;
            string model = "LitModels/" + mapFile.Name + "/36/wall.bmd";
            string projectPath = projectRoot + "/" + model;
            string fallback = "LitModels/" + mapFile.Name + "/37/wall.bmd";
            try
            {
                Directory.CreateDirectory(mapDirectory);
                foreach (var file in Directory.GetFiles(sourceRoot))
                    File.Move(file, Path.Combine(mapDirectory, Path.GetFileName(file)));

                WriteFile(Path.Combine(sourceRoot, model), new byte[] { 0 });
                var modelBytes = BmdParserTests.BuildBmd(true, false, BmdParser.MeshVersion04, posX: -1f, posZ: 1f);
                WriteFile(AngelicaAssetFolders.AssetPathToAbsolute(projectPath), modelBytes);
                WriteFile(Path.Combine(sourceRoot, fallback), modelBytes);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                index.Scan();
                mapFile = index.Files.Single(f => f.Extension == "trn2");

                var dropped = GameDataDropImporter.Import(index, new[] { mapFile.FullPath }, decoder, false);
                Assert.IsEmpty(dropped.Errors);
                Assert.AreEqual(fallback, dropped.Graph.ImportOrder.Single(node => node.File.Extension == "bmd").File.RelativePath);
                CollectionAssert.AreEquivalent(new[] { projectPath, BuildingAssetImporter.GetNativeAssetPath(fallback) },
                    dropped.Maps.Single().BuildingPaths);
                Assert.IsFalse(AssetDatabase.IsValidFolder(BuildingAssetImporter.GetOutputFolder(model)));
            }
            finally
            {
                AssetDatabase.DeleteAsset(MapAssetImporter.GetOutputFolder(mapFile.RelativePath));
                AssetDatabase.DeleteAsset(rawAssetFolder);
                AssetDatabase.DeleteAsset(BuildingAssetImporter.GetOutputFolder(fallback));
                AssetDatabase.DeleteAsset(projectRoot);
            }
        }

        private static void WriteFile(string path, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
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
            Assert.AreEqual(1f, material.GetFloat("_UseNightLightmap"));
            Assert.IsNotNull(material.GetTexture("_NightLightmap"));
        }
    }
}
