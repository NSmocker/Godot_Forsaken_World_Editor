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
    public sealed class ImportPerformanceTests
    {
        private string directory, relative, source, output;

        [SetUp]
        public void SetUp()
        {
            string id = "performance_" + Guid.NewGuid().ToString("N");
            directory = Path.Combine(Path.GetTempPath(), id);
            Directory.CreateDirectory(directory);
            relative = "__tests/" + id + "/texture.tga";
            source = Path.Combine(directory, "texture.tga");
            output = AssembledTextureImporter.GetTextureAssetPath(relative);
            File.WriteAllBytes(source, Tga(255));
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Path.GetDirectoryName(output).Replace('\\', '/'));
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void NestedSessionsShareDependenciesAndDoNotReimportTextures()
        {
            using (var session = new GameDataImportSession())
            {
                var first = AssembledTextureImporter.ImportOrUpdate(source, relative);
                Assert.IsNotNull(first.Texture, first.Error);
                using (new GameDataImportSession())
                    Assert.AreSame(first, AssembledTextureImporter.ImportOrUpdate(source, relative.Replace('/', '\\')));
                Assert.AreSame(first, AssembledTextureImporter.ImportOrUpdate(source, relative));
                Assert.AreEqual(1, session.Statistics.NativeImports);
                Assert.AreEqual(2, session.Statistics.CacheHits);
            }
        }

        [Test]
        public void UnchangedSourceIsReusedButEqualSizeEqualTimestampChangesAreImported()
        {
            var first = AssembledTextureImporter.ImportOrUpdate(source, relative);
            Assert.IsNotNull(first.Texture, first.Error);
            string guid = AssetDatabase.AssetPathToGUID(output);
            var stamp = File.GetLastWriteTimeUtc(source);
            using (var session = new GameDataImportSession())
            {
                Assert.IsTrue(AssembledTextureImporter.ImportOrUpdate(source, relative).Unchanged);
                Assert.AreEqual(0, session.Statistics.NativeImports);
                Assert.AreEqual(1, session.Statistics.UnchangedFiles);
            }
            File.WriteAllBytes(source, Tga(32));
            File.SetLastWriteTimeUtc(source, stamp);
            using (var session = new GameDataImportSession())
            {
                var changed = AssembledTextureImporter.ImportOrUpdate(source, relative);
                Assert.IsNotNull(changed.Texture, changed.Error);
                Assert.IsTrue(changed.Updated);
                Assert.AreEqual(1, session.Statistics.NativeImports);
            }
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(output));
            CollectionAssert.AreEqual(File.ReadAllBytes(source), File.ReadAllBytes(AssembledTextureImporter.AssetPathToAbsolute(output)));
        }

        [Test]
        public void ForceReimportOverridesUnchangedContent()
        {
            AssembledTextureImporter.ImportOrUpdate(source, relative);
            using (var session = new GameDataImportSession(forceReimport: true))
            {
                var result = AssembledTextureImporter.ImportOrUpdate(source, relative);
                Assert.IsNotNull(result.Texture, result.Error);
                Assert.IsFalse(result.Unchanged);
                Assert.AreEqual(1, session.Statistics.NativeImports);
            }
        }

        [Test]
        public void ExceptionRestoresOuterSessionAndLaterSessionsDoNotReuseStaleResults()
        {
            using (var outer = new GameDataImportSession())
            {
                var first = AssembledTextureImporter.ImportOrUpdate(source, relative);
                Assert.Throws<InvalidOperationException>(() =>
                {
                    using (new GameDataImportSession())
                        throw new InvalidOperationException("test interruption");
                });
                Assert.AreSame(first, AssembledTextureImporter.ImportOrUpdate(source, relative));
            }
            File.WriteAllBytes(source, Tga(64));
            using (var next = new GameDataImportSession())
            {
                Assert.IsTrue(AssembledTextureImporter.ImportOrUpdate(source, relative).Updated);
                Assert.AreEqual(1, next.Statistics.NativeImports);
            }
        }

        [Test]
        public void BuildingBatchPersistsReferencesReusesTextureAndPreservesGuids()
        {
            var decoder = new GfxTextDecoder(() => Encoding.GetEncoding(28591));
            var document = new BmdParser(decoder).Parse(BmdParserTests.BuildBmd(true, false, BmdParser.MeshVersion04));
            document.Meshes[0].Texture = "texture.tga";
            string bmdName = Path.GetFileName(directory) + ".bmd";
            File.WriteAllBytes(Path.Combine(directory, bmdName), new byte[] { 0 });
            var index = new GameDataIndex(directory); index.Scan();
            var file = index.Files.Single(f => f.Extension == "bmd");
            // The parsed-document overload must not read the deliberately invalid source again.
            var folder = BuildingAssetImporter.GetOutputFolder(file.RelativePath);
            string texturePath = AssembledTextureImporter.GetTextureAssetPath("texture.tga");
            // Do not touch another user's asset with this common fixture path.
            string uniqueTexture = Path.GetFileName(directory) + ".tga";
            File.Copy(source, Path.Combine(directory, uniqueTexture));
            document.Meshes[0].Texture = uniqueTexture;
            texturePath = AssembledTextureImporter.GetTextureAssetPath(uniqueTexture);
            index.Scan();
            try
            {
                string prefabGuid, meshGuid;
                using (var session = new GameDataImportSession())
                {
                    var result = BuildingAssetImporter.Import(file, index, decoder, false, document);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
                    Assert.IsNotNull(prefab);
                    Assert.IsNotNull(prefab.GetComponent<MeshFilter>().sharedMesh);
                    Assert.IsNotNull(prefab.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_Albedo"));
                    prefabGuid = AssetDatabase.AssetPathToGUID(result.PrefabPath);
                    meshGuid = AssetDatabase.AssetPathToGUID(result.MeshPath);
                    Assert.AreEqual(1, session.Statistics.WriteBatches);
                    Assert.AreEqual(1, session.Statistics.NativeImports);
                    Assert.AreEqual(0, session.Statistics.SaveCalls, "Nested building import must defer global save.");
                }
                var again = BuildingAssetImporter.Import(file, index, decoder, false, document);
                Assert.AreEqual(prefabGuid, AssetDatabase.AssetPathToGUID(again.PrefabPath));
                Assert.AreEqual(meshGuid, AssetDatabase.AssetPathToGUID(again.MeshPath));
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
                AssetDatabase.DeleteAsset(texturePath);
            }
        }

        private static byte[] Tga(byte red)
        {
            var bytes = new byte[18 + 16];
            bytes[2] = 2; bytes[12] = 2; bytes[14] = 2; bytes[16] = 32; bytes[17] = 8;
            for (int i = 18; i < bytes.Length; i += 4) { bytes[i + 2] = red; bytes[i + 3] = 255; }
            return bytes;
        }
    }
}
