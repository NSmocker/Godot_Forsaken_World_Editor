using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class BmdAssetImporterTests
    {
        [Test]
        public void CopiedGameFolderImportsModelAndKeepsBmdBytesAndStableSubassets()
        {
            string folder = TestFolder();
            string path = folder + "/LitModels/world/36/model.bmd";
            string texture = folder + "/Building/textures/wall.tga";
            var source = BuildSource("Building\\textures\\wall.tga", extraPayload: 0xA6);
            try
            {
                WriteAsset(texture, BuildTexture());
                WriteAsset(path, source);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(root);
                var mesh = root.GetComponent<MeshFilter>().sharedMesh;
                var material = root.GetComponent<MeshRenderer>().sharedMaterial;
                var placement = root.GetComponent<LitModelInstance>();
                var model = placement.SourceData;
                Assert.AreEqual("LitModels/world/36/model.bmd", placement.OriginalBmdPath);
                Assert.AreEqual(new Vector3(3f, 4f, 5f), root.transform.localPosition);
                Assert.AreEqual(BmdParser.BuildingFlagsVersion, model.BuildingVersion);
                Assert.AreEqual(1, model.MeshCount);
                CollectionAssert.AreEqual(source, model.CopySourceBytes());
                CollectionAssert.AreEqual(source, File.ReadAllBytes(AngelicaAssetFolders.AssetPathToAbsolute(path)));
                Assert.AreEqual(path, AssetDatabase.GetAssetPath(model));
                Assert.AreEqual(path, AssetDatabase.GetAssetPath(mesh));
                Assert.AreEqual(path, AssetDatabase.GetAssetPath(material));
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(texture), material.GetTexture("_Albedo"));
                Assert.AreEqual(3, mesh.vertexCount);
                Assert.AreEqual(1, mesh.subMeshCount);
                Assert.AreEqual((float)CullMode.Off, material.GetFloat("_Cull"));
                var night = new List<Vector3>();
                mesh.GetUVs(2, night);
                Assert.AreEqual(Vector3.forward, night[0]);
                Assert.AreEqual(1f, model.ReadDocument(GameDataBrowserWindow.GetGameDataDecoder()).Meshes[0].ExtraDayColors[1]);

                var preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(root, preview);
                    Assert.IsNotNull(instance);
                    Assert.AreSame(mesh, instance.GetComponent<MeshFilter>().sharedMesh);
                    Assert.AreSame(model, instance.GetComponent<LitModelInstance>().SourceData);
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }

                var ids = LocalIds(root, mesh, material, model);
                source[source.Length - 1] = 0x39;
                WriteAsset(path, source);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var reloadedData = reloaded.GetComponent<LitModelInstance>().SourceData;
                CollectionAssert.AreEqual(ids, LocalIds(reloaded, reloaded.GetComponent<MeshFilter>().sharedMesh,
                    reloaded.GetComponent<MeshRenderer>().sharedMaterial, reloadedData));
                CollectionAssert.AreEqual(source, reloadedData.CopySourceBytes());
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }

        [Test]
        public void AddingATextureAfterTheModelUpdatesItsMaterial()
        {
            string folder = TestFolder();
            string path = folder + "/LitModels/world/late.bmd";
            string texture = folder + "/Building/textures/late.tga";
            try
            {
                WriteAsset(path, BuildSource("Building/textures/late.tga"));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(root);
                Assert.IsNull(root.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_Albedo"));

                WriteAsset(texture, BuildTexture());
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(root);
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(texture),
                    root.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_Albedo"));
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }

        [Test]
        public void CollideOnlyBmdImportsItsData()
        {
            string folder = TestFolder();
            string path = folder + "/collision.bmd";
            var source = BuildSource("Building/textures/collision.tga", collideOnly: true);
            try
            {
                WriteAsset(path, source);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(root);
                Assert.IsNull(root.GetComponent<Renderer>());
                var data = root.GetComponent<LitModelInstance>().SourceData;
                Assert.IsTrue(data.CollideOnly);
                CollectionAssert.AreEqual(source, data.CopySourceBytes());
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }

        [Test]
        public void IndexedDropImportsTheOriginalBmdAndBindsIndexedTextures()
        {
            var id = Guid.NewGuid().ToString("N");
            string sourceRoot = Path.Combine(Path.GetTempPath(), "bmd-test-" + id);
            string bmd = "LitModels/test/" + id + ".bmd";
            string texture = "Building/textures/" + id + ".tga";
            var bytes = BuildSource(texture);
            try
            {
                WriteFile(Path.Combine(sourceRoot, bmd), bytes);
                WriteFile(Path.Combine(sourceRoot, texture), BuildTexture());
                var index = new GameDataIndex(sourceRoot);
                index.Scan();
                var result = GameDataDropImporter.Import(index, new[] { bmd }, showProgress: false);
                Assert.IsEmpty(result.Errors);
                var root = (GameObject)result.Assets.Single();
                var path = AssetDatabase.GetAssetPath(root);
                Assert.AreEqual(".bmd", Path.GetExtension(path));
                Assert.AreEqual(bmd, root.GetComponent<LitModelInstance>().OriginalBmdPath);
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(AngelicaAssetFolders.AssetPathToAbsolute(path)));
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(AssembledTextureImporter.GetTextureAssetPath(texture)),
                    root.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_Albedo"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(BuildingAssetImporter.GetOutputFolder(bmd));
                AssetDatabase.DeleteAsset(AssembledTextureImporter.GetTextureAssetPath(texture));
                if (Directory.Exists(sourceRoot)) Directory.Delete(sourceRoot, true);
            }
        }

        private static string TestFolder() => "Assets/BmdImportTest_" + Guid.NewGuid().ToString("N");

        private static byte[] BuildSource(string texture, byte extraPayload = 0xAF, bool collideOnly = false)
        {
            var bytes = BmdParserTests.BuildBmd(true, collideOnly, BmdParser.MeshVersion06,
                twoSided: true, extraColors: true);
            var offset = Encoding.ASCII.GetString(bytes).IndexOf("building/wall.dds", StringComparison.Ordinal);
            Assert.GreaterOrEqual(offset, 0);
            Array.Clear(bytes, offset, 256);
            Encoding.ASCII.GetBytes(texture).CopyTo(bytes, offset);
            Array.Resize(ref bytes, bytes.Length + 1);
            bytes[bytes.Length - 1] = extraPayload;
            return bytes;
        }

        private static byte[] BuildTexture()
        {
            var texture = new byte[18 + 16];
            texture[2] = 2;
            texture[12] = texture[14] = 2;
            texture[16] = 32;
            texture[17] = 8;
            for (int i = 18; i < texture.Length; i++) texture[i] = 255;
            return texture;
        }

        private static void WriteAsset(string path, byte[] bytes) =>
            WriteFile(AngelicaAssetFolders.AssetPathToAbsolute(path), bytes);

        private static void WriteFile(string path, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
        }

        private static long[] LocalIds(params Object[] assets) => assets.Select(asset =>
        {
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out _, out long id));
            return id;
        }).ToArray();
    }
}
