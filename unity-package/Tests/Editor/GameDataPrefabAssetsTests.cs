using System;
using System.Linq;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GameDataPrefabAssetsTests
    {
        [Test]
        public void PersistsTransientAssetsUnderOriginalNamesTypeExtensionsAndSourceFolders()
        {
            string folder = "Assets/__prefab_assets_tests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            var root = new GameObject("Root");
            try
            {
                var body = Child(root, "Body");
                var texture = new Texture2D(2, 2) { name = "GrassTex" };
                var material = new Material(Shader.Find("Sprites/Default")) { name = "GrassMat", mainTexture = texture };
                var mesh = new Mesh { name = "GrassMesh" };
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
                mesh.triangles = new[] { 0, 1, 2 };
                var renderer = body.AddComponent<MeshRenderer>();
                body.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer.sharedMaterial = material;

                new GameDataPrefabAssets(root, folder);

                Assert.AreEqual(folder + "/Body/GrassMat.mat", AssetDatabase.GetAssetPath(renderer.sharedMaterial));
                Assert.AreEqual(folder + "/Body/GrassMesh.asset", AssetDatabase.GetAssetPath(body.GetComponent<MeshFilter>().sharedMesh));
                Assert.AreEqual(folder + "/Body/GrassTex.asset", AssetDatabase.GetAssetPath(renderer.sharedMaterial.mainTexture));
                Assert.IsTrue(EditorUtility.IsPersistent(renderer.sharedMaterial));
            }
            finally { Object.DestroyImmediate(root); AssetDatabase.DeleteAsset(folder); }
        }

        [Test]
        public void ReusesTheSameFilesOnRepeatedRuns()
        {
            string folder = "Assets/__prefab_assets_tests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            var root = new GameObject("Root");
            try
            {
                var renderer = Child(root, "Body").AddComponent<MeshRenderer>();
                renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Shared" };

                new GameDataPrefabAssets(root, folder);
                var firstPath = AssetDatabase.GetAssetPath(renderer.sharedMaterial);
                var firstGuid = AssetDatabase.AssetPathToGUID(firstPath);
                var firstCount = AssetDatabase.FindAssets("", new[] { folder }).Length;

                renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Shared" };
                new GameDataPrefabAssets(root, folder);

                Assert.AreEqual(firstPath, AssetDatabase.GetAssetPath(renderer.sharedMaterial));
                Assert.AreEqual(firstGuid, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(renderer.sharedMaterial)));
                var secondCount = AssetDatabase.FindAssets("", new[] { folder }).Length;
                Assert.AreEqual(firstCount, secondCount, "Repeated imports must reuse files instead of accumulating siblings.");
            }
            finally { Object.DestroyImmediate(root); AssetDatabase.DeleteAsset(folder); }
        }

        [Test]
        public void SameNameInDifferentSourcesLandsInDifferentFolders()
        {
            string folder = "Assets/__prefab_assets_tests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            var root = new GameObject("Root");
            try
            {
                var first = Child(Child(root, "Source1"), "Body").AddComponent<MeshRenderer>();
                var second = Child(Child(root, "Source2"), "Body").AddComponent<MeshRenderer>();
                first.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Shared" };
                second.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Shared" };

                new GameDataPrefabAssets(root, folder);

                Assert.AreEqual(folder + "/Source1/Shared.mat", AssetDatabase.GetAssetPath(first.sharedMaterial));
                Assert.AreEqual(folder + "/Source2/Shared.mat", AssetDatabase.GetAssetPath(second.sharedMaterial));
                Assert.AreNotSame(first.sharedMaterial, second.sharedMaterial);
            }
            finally { Object.DestroyImmediate(root); AssetDatabase.DeleteAsset(folder); }
        }

        [Test]
        public void SameNameInsideOneSourceGetsNumberedSiblings()
        {
            string folder = "Assets/__prefab_assets_tests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            var root = new GameObject("Root");
            try
            {
                var body = Child(root, "Body");
                var first = Child(body, "A").AddComponent<MeshFilter>();
                var second = Child(body, "B").AddComponent<MeshFilter>();
                first.sharedMesh = new Mesh { name = "Unnamed" };
                second.sharedMesh = new Mesh { name = "Unnamed" };

                new GameDataPrefabAssets(root, folder);

                Assert.AreEqual(folder + "/Body/Unnamed.asset", AssetDatabase.GetAssetPath(first.sharedMesh));
                Assert.AreEqual(folder + "/Body/Unnamed_1.asset", AssetDatabase.GetAssetPath(second.sharedMesh));
                Assert.AreNotSame(first.sharedMesh, second.sharedMesh);
            }
            finally { Object.DestroyImmediate(root); AssetDatabase.DeleteAsset(folder); }
        }

        [Test]
        public void LeftoverAssetOfAnotherKindDoesNotAbortPersist()
        {
            string folder = "Assets/__prefab_assets_tests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            AssetDatabase.CreateFolder(folder, "Body");
            var root = new GameObject("Root");
            try
            {
                // Simulate an older import that saved a Material where the current run persists a Mesh.
                var stale = new Material(Shader.Find("Sprites/Default")) { name = "Dummy" };
                AssetDatabase.CreateAsset(stale, folder + "/Body/Dummy.asset");
                var staleGuid = AssetDatabase.AssetPathToGUID(folder + "/Body/Dummy.asset");

                var filter = Child(Child(root, "Body"), "Chunk").AddComponent<MeshFilter>();
                filter.sharedMesh = new Mesh { name = "Dummy" };
                filter.sharedMesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
                filter.sharedMesh.triangles = new[] { 0, 1, 2 };

                Assert.DoesNotThrow(() => new GameDataPrefabAssets(root, folder));
                Assert.AreEqual(folder + "/Body/Dummy_1.asset", AssetDatabase.GetAssetPath(filter.sharedMesh));
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>(folder + "/Body/Dummy.asset"),
                    "The leftover asset must stay intact for older prefabs.");
                Assert.AreEqual(staleGuid, AssetDatabase.AssetPathToGUID(folder + "/Body/Dummy.asset"));

                // The sibling becomes the stable location for this kind on later runs too.
                filter.sharedMesh = new Mesh { name = "Dummy" };
                new GameDataPrefabAssets(root, folder);
                Assert.AreEqual(folder + "/Body/Dummy_1.asset", AssetDatabase.GetAssetPath(filter.sharedMesh));
            }
            finally { Object.DestroyImmediate(root); AssetDatabase.DeleteAsset(folder); }
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var child = new GameObject(name); child.transform.SetParent(parent.transform, false); return child;
        }
    }
}
