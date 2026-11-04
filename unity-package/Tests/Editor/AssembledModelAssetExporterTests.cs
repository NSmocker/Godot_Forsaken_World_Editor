using System.IO;
using System.Text;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEditor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class AssembledModelAssetExporterTests
    {
        [Test]
        public void MeshAndPrefabPathsMatchGodotSuffixConvention()
        {
            Assert.AreEqual(
                "Assets/ForsakenWorld/Imported/Meshes/models/npc/Body_mesh.asset",
                AssembledModelAssetExporter.GetMeshAssetPath("models/npc/Body.ski"));
            Assert.AreEqual(
                "Assets/ForsakenWorld/Imported/Models/models/npc/Guard_model.prefab",
                AssembledModelAssetExporter.GetPrefabAssetPath("models/npc/Guard.smd", fromEcm: false));
            Assert.AreEqual(
                "Assets/ForsakenWorld/Imported/ECM/models/npc/Guard.prefab",
                AssembledModelAssetExporter.GetPrefabAssetPath("models/npc/Guard.ecm", fromEcm: true));
            Assert.AreEqual(
                "Assets/ForsakenWorld/Imported/Animations/models/npc/Guard/Run.anim",
                AssembledModelAssetExporter.GetAnimationClipAssetPath("models/npc/Guard.smd", "Run"));
        }

        [Test]
        public void ExportWritesPrefabAndMeshThroughAssetDatabase()
        {
            var decoder = new GfxTextDecoder(() => Encoding.GetEncoding(28591));
            var ski = new SkiParser(decoder).Parse(
                SkiParserTests.BuildFixture(),
                "models/npc/Body.ski");
            var smd = new SmdParser(decoder).Parse(SmdParserTests.BuildFixture());
            var bon = new BonParser(decoder).Parse(BonParserTests.BuildFixture());
            var model = new ModelAssembler().Assemble(
                smd,
                "models/npc/Guard.smd",
                bon,
                "models/npc/Skeleton.bon",
                new[] { ski },
                new[] { "models/npc/Body.ski" });
            Assert.IsTrue(model.IsValid);

            // Minimal temp index so texture import reports missing rather than throwing.
            var root = Path.Combine(Path.GetTempPath(), "ForsakenPrefabExport", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var index = new GameDataIndex(root);
                index.Scan();
                var result = AssembledModelAssetExporter.Export(
                    model,
                    index,
                    "models/npc/Guard.smd",
                    fromEcm: false,
                    rootName: "Guard");

                Assert.IsTrue(result.Succeeded);
                Assert.IsTrue(string.IsNullOrEmpty(result.Error));
                Assert.AreEqual(
                    "Assets/ForsakenWorld/Imported/Models/models/npc/Guard_model.prefab",
                    result.PrefabPath);
                Assert.Greater(result.MeshPaths.Count, 0);
                Assert.IsTrue(result.PrefabCreated || result.PrefabUpdated);
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath));
                foreach (var meshPath in result.MeshPaths)
                    Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Mesh>(meshPath));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }
    }
}
