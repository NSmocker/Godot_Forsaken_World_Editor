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
    public sealed class GameDataDropTests
    {
        private string root;
        private string prefix;
        private readonly GfxTextDecoder decoder = new GfxTextDecoder(() => Encoding.GetEncoding(28591));

        [SetUp]
        public void SetUp()
        {
            prefix = "__drop_tests/" + Guid.NewGuid().ToString("N");
            root = Path.Combine(Path.GetTempPath(), "ForsakenDrop", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var category in new[] { "Models", "Skeletons", "Skins", "Tracks", "ECM", "GFX", "Textures", "Sounds", "Sources", "Generated", "Meshes", "Materials", "Animations" })
                AssetDatabase.DeleteAsset(GameDataDropImporter.ImportRoot + "/" + category + "/" + prefix);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private void Write(string path, byte[] data)
        {
            path = Path.Combine(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }
        private void Text(string path, string text) => Write(path, Encoding.UTF8.GetBytes(text));
        private GameDataIndex Index() { var index = new GameDataIndex(root, decoder); index.Scan(); return index; }
        private GameDataDependencyGraph Graph(params string[] paths)
        {
            var graph = new GameDataDependencyGraph(Index(), decoder);
            foreach (var path in paths) graph.AddRoot(path);
            return graph;
        }
        private void Model(string folder)
        {
            Write(folder + "/Guard.smd", SmdParserTests.BuildFixture());
            Write(folder + "/Skeleton.bon", BonParserTests.BuildFixture());
            Write(folder + "/Body.ski", SkiParserTests.BuildFixture());
            Write(folder + "/Run.stck", StckParserTests.BuildFixture());
            Write(folder + "/Textures/body.dds", Tga());
        }
        private static byte[] Tga()
        {
            var bytes = new byte[18 + 16];
            bytes[2] = 2; bytes[12] = 2; bytes[14] = 2; bytes[16] = 32; bytes[17] = 8;
            for (var i = 18; i < bytes.Length; i += 4) { bytes[i + 2] = 255; bytes[i + 3] = 255; }
            return bytes;
        }

        [Test]
        public void ModelClosureIsOutgoingDeduplicatedAndDependenciesPrecedeRoot()
        {
            Model("models/hero");
            Text("models/hero/other.ecm", "SkinModelPath: models/hero/Guard.smd");
            var graph = Graph("models/hero/Guard.smd", "models/hero/Guard.smd");
            Assert.AreEqual(1, graph.Roots.Count);
            CollectionAssert.AreEquivalent(new[] { "smd", "bon", "ski", "stck", "dds" }, graph.ImportOrder.Select(n => n.File.Extension));
            Assert.AreEqual("smd", graph.ImportOrder.Last().File.Extension);
            Assert.IsTrue(graph.Roots[0].CanImport, string.Join("; ", graph.Roots[0].Errors));
        }

        [TestCase("Skeleton.bon", 1)]
        [TestCase("Run.stck", 1)]
        [TestCase("Body.ski", 2)]
        [TestCase("Textures/body.dds", 1)]
        public void LeafAndSkinDropsNeverGuessParentModelOrSkeleton(string name, int count)
        {
            Model("models/hero");
            Assert.AreEqual(count, Graph("models/hero/" + name).ImportOrder.Count);
        }

        [Test]
        public void CorruptRequiredSkeletonBlocksEcmButMissingSkinIsSoft()
        {
            Model("models/hero");
            Text("models/hero/Guard.ecm", "SkinModelPath: models/hero/Guard.smd");
            File.Delete(Path.Combine(root, "models/hero/Body.ski"));
            var soft = Graph("models/hero/Guard.ecm");
            Assert.IsTrue(soft.Roots[0].CanImport);
            Write("models/hero/Skeleton.bon", new byte[] { 1 });
            var hard = Graph("models/hero/Guard.ecm");
            Assert.IsFalse(hard.Roots[0].CanImport);
            Assert.IsTrue(hard.Roots[0].Errors.Any(e => e.Contains("Required dependency")));
        }

        [Test]
        public void GfxCyclesAndUnknownElementsStillCollectKnownReferences()
        {
            Text("gfx/a.gfx", "GFXELEMENTID: 200\nGfxPath: b.gfx\n");
            Text("gfx/b.gfx", "GFXELEMENTID: 999\nGfxPath: a.gfx\nTexFile: image.tga\n");
            Write("gfx/textures/image.tga", Tga());
            var graph = Graph("gfx/a.gfx", "gfx/b.gfx");
            Assert.AreEqual(3, graph.ImportOrder.Count);
            Assert.IsTrue(graph.ImportOrder.All(n => n.CanImport));
        }

        [Test]
        public void EcmGfxCycleRetainsOneCopyOfModelAndEffect()
        {
            Model("models/hero");
            Text("models/hero/Guard.ecm", "SkinModelPath: models/hero/Guard.smd\nCoGfxNum: 1\nEventType: 0\nFxFilePath: gfx/a.gfx\n");
            Text("gfx/a.gfx", "GFXELEMENTID: 230\nECMPath: models/hero/Guard.ecm\n");
            var graph = Graph("models/hero/Guard.ecm");
            Assert.AreEqual(7, graph.ImportOrder.Count);
            Assert.AreEqual(1, graph.ImportOrder.Count(n => n.File.Extension == "gfx"));
        }

        [Test]
        public void GfxImportsCustomAudioAndBesidePhysicsButSkipsShaders()
        {
            Text("gfx/test.gfx", "GFXELEMENTID: 170\nPathNum: 1\nPath: hit.wav\nUseCustom: 1\nPath: event.ogg\n" +
                "GFXELEMENTID: 220\nPhysDesc: fluid\nTexFile: shader.sdr\n");
            Write("sfx/hit.wav", new byte[] { 0 });
            Write("sfx/event.ogg", new byte[] { 0 });
            Write("gfx/test.gphy", new byte[] { 0 });
            Text("gfx/textures/shader.sdr", "shader");
            var graph = Graph("gfx/test.gfx");
            CollectionAssert.AreEquivalent(new[] { "gfx", "wav", "ogg", "gphy" }, graph.ImportOrder.Select(n => n.File.Extension));
            Assert.IsTrue(graph.Roots[0].Warnings.Any(w => w.Contains(".sdr")));
        }

        [Test]
        public void AmbiguousTextureDoesNotFallThroughToUnrelatedCandidate()
        {
            Text("gfx/a.gfx", "GFXELEMENTID: 100\nTexFile: shared.tga\n");
            Write("client1/gfx/textures/shared.tga", Tga());
            Write("client2/gfx/textures/shared.tga", Tga());
            Write("shared.tga", Tga());
            var graph = Graph("gfx/a.gfx");
            Assert.AreEqual(1, graph.ImportOrder.Count);
            Assert.IsTrue(graph.Roots[0].Warnings.Any(w => w.IndexOf("ambiguous", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        [Test]
        public void TerrainFindsOnlyNamedExactSubFilesAndKeepsMissingSubsSoft()
        {
            Write("maps/demo/demo.trn2", TerrainParserTests.BuildTrn2(textureName: "ground.tga"));
            Write("maps/demo/sub0.t2bk", TerrainParserTests.BuildT2bk(6, false, false));
            Write("other/sub0.t2mk", TerrainParserTests.BuildT2mk(false));
            Write("ground.tga", Tga());
            var graph = Graph("maps/demo/demo.trn2");
            CollectionAssert.AreEquivalent(new[] { "trn2", "t2bk", "tga" }, graph.ImportOrder.Select(n => n.File.Extension));
            Assert.IsTrue(graph.Roots[0].CanImport);
            Assert.IsTrue(graph.Roots[0].Warnings.Any(w => w.Contains("sub0.t2mk")));
        }

        [Test]
        public void WorldAndTerrainPairClosesWithoutRecursingForever()
        {
            Write("maps/demo/demo.trn2", TerrainParserTests.BuildTrn2());
            var world = new byte[116];
            Buffer.BlockCopy(BitConverter.GetBytes(EcwldParser.EcwdMagic), 0, world, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(14u), 0, world, 4, 4);
            Write("maps/demo/demo.ecwld", world);
            var graph = Graph("maps/demo/demo.ecwld");
            CollectionAssert.AreEquivalent(new[] { "ecwld", "trn2" }, graph.ImportOrder.Select(n => n.File.Extension));
        }

        [Test]
        public void BsdWithoutWorldImportsItsTableWithoutInventingTerrain()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("MOXB")); writer.Write(EcbsdParser.BsdfMagic);
                writer.Write(0); writer.Write(1); writer.Write(new byte[60]);
                var name = Encoding.ASCII.GetBytes("BSData\\0.dat"); writer.Write(name.Length); writer.Write(name);
                Write("maps/demo/demo.ecbsd", stream.ToArray());
            }
            Write("maps/demo/BSData/0.dat", new byte[] { 1, 2 });
            Write("maps/demo/demo.trn2", TerrainParserTests.BuildTrn2());
            var graph = Graph("maps/demo/demo.ecbsd");
            CollectionAssert.AreEquivalent(new[] { "ecbsd", "dat" }, graph.ImportOrder.Select(n => n.File.Extension));
        }

        [Test]
        public void UiLayoutResolvesLanguageSurfacesNestedDialogSoundAndFont()
        {
            Text("resources/Eng/interfaces/loginui.dcf", "main.xml 0 0 1");
            Text("resources/Eng/interfaces/loginui.stf", "1 \"Unknown face\" 2 \"10\"");
            Text("resources/Eng/interfaces/main.xml", "<DIALOG Name=\"Main\" SoundEffect=\"click.wav\"><Resource><FrameImage FileName=\"frame.tga\"/></Resource><SUBDIALOG Name=\"Sub\" DialogFile=\"child.xml\"/></DIALOG>");
            Text("resources/Eng/interfaces/child.xml", "<DIALOG Name=\"Child\"/>");
            Write("resources/Eng/surfaces/frame.tga", Tga());
            Write("resources/click.wav", new byte[] { 0 });
            Write("font/fzxh1jw.ttf", new byte[] { 0 });
            var graph = Graph("resources/Eng/interfaces/loginui.dcf");
            Assert.AreEqual(2, graph.ImportOrder.Count(n => n.File.Extension == "xml"));
            foreach (var ext in new[] { "tga", "wav", "ttf", "stf" }) Assert.IsTrue(graph.ImportOrder.Any(n => n.File.Extension == ext), ext);
        }

        [Test]
        public void UiCannotFollowAbsoluteReferencesOutsideGameData()
        {
            var outside = root + "-outside.xml";
            File.WriteAllText(outside, "<DIALOG Name=\"Outside\"/>");
            try
            {
                var ui = new AngelicaUi(root);
                Assert.AreEqual(string.Empty, ui.InterfaceFile(outside));
                Assert.AreEqual(string.Empty, ui.Sound(outside));
            }
            finally { File.Delete(outside); }
        }

        [Test]
        public void HoverIsReadOnlyAndRejectsOutsideOrUnscannedFiles()
        {
            Write(prefix + "/a.bon", BonParserTests.BuildFixture());
            var index = Index();
            Assert.AreEqual(DragAndDropVisualMode.Copy, GameDataDragAndDrop.Classify(index, new[] { prefix + "/a.bon" }, out _));
            Assert.IsNull(AssetDatabase.LoadMainAssetAtPath(GameDataDropImporter.ImportRoot + "/Skeletons/" + prefix + "/a.asset"));
            Write(prefix + "/later.bon", BonParserTests.BuildFixture());
            Assert.AreEqual(DragAndDropVisualMode.Rejected, GameDataDragAndDrop.Classify(index, new[] { prefix + "/later.bon" }, out _));
            Assert.AreEqual(DragAndDropVisualMode.Rejected, GameDataDragAndDrop.Classify(index, new[] { "../escape.bon" }, out _));
            Assert.AreEqual(DragAndDropVisualMode.Rejected, GameDataDragAndDrop.Classify(null, new[] { "a.bon" }, out _));
            Assert.AreEqual(DragAndDropVisualMode.None, GameDataDragAndDrop.Classify(index, new[] { "a.fbx" }, out _));
        }

        [Test]
        public void ImportCreatesModelPrefabAndReimportPreservesGuid()
        {
            Model(prefix);
            var index = Index();
            var first = GameDataDropImporter.Import(index, new[] { prefix + "/Guard.smd", prefix + "/Guard.smd" }, decoder, false);
            Assert.IsEmpty(first.Errors, string.Join("; ", first.Errors));
            Assert.AreEqual(5, first.ImportedFiles);
            Assert.AreEqual(1, first.Assets.Count);
            Assert.IsInstanceOf<GameObject>(first.Assets[0]);
            var path = AssetDatabase.GetAssetPath(first.Assets[0]);
            var guid = AssetDatabase.AssetPathToGUID(path);
            var second = GameDataDropImporter.Import(index, new[] { prefix + "/Guard.smd" }, decoder, false);
            Assert.IsEmpty(second.Errors, string.Join("; ", second.Errors));
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(second.Assets[0])));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsTrue(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).All(r => r.sharedMesh != null && EditorUtility.IsPersistent(r.sharedMesh)));
        }

        [Test]
        public void MissingRequiredFileProducesNoRootAssetButOtherRootsStillImport()
        {
            Model(prefix);
            File.Delete(Path.Combine(root, prefix, "Skeleton.bon"));
            var result = GameDataDropImporter.Import(Index(), new[] { prefix + "/Guard.smd", prefix + "/Run.stck" }, decoder, false);
            Assert.AreEqual(1, result.Assets.Count);
            Assert.IsInstanceOf<StckAsset>(result.Assets[0]);
            Assert.IsNotEmpty(result.Errors);
            Assert.IsNull(AssetDatabase.LoadMainAssetAtPath(GameDataDropImporter.ImportRoot + "/Models/" + prefix + "/Guard.asset"));
        }

        [Test]
        public void GfxPrefabRetainsPersistentMaterialsAndMeshesAfterReload()
        {
            Text(prefix + "/effect.gfx", "MOXTVersion: 96\nGFXELEMENTID: 100\nName: Sprite\nTexFile: " + prefix + "/image.tga\nKEYPOINTCOUNT: 1\nTimeSpan: 1000\nScale: 1\nColor: -1\n");
            Write(prefix + "/image.tga", Tga());
            var result = GameDataDropImporter.Import(Index(), new[] { prefix + "/effect.gfx" }, decoder, false);
            Assert.IsEmpty(result.Errors, string.Join("; ", result.Errors));
            Assert.AreEqual(1, result.Assets.Count);
            var path = AssetDatabase.GetAssetPath(result.Assets[0]);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(renderers);
            foreach (var renderer in renderers)
                foreach (var material in renderer.sharedMaterials) Assert.IsTrue(material != null && EditorUtility.IsPersistent(material));
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true)) Assert.IsTrue(filter.sharedMesh != null && EditorUtility.IsPersistent(filter.sharedMesh));
        }
    }
}
