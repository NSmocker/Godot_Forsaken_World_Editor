using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class MapSectorPrefabTests
    {
        [Test]
        public void ExportsIndependentSectorsWithUniqueObjectsSplitGeometryAndStableGuids()
        {
            string folder = "Assets/__sector_prefab_tests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            var root = new GameObject("Map");
            try
            {
                var terrain = new TerrainParser(new GfxTextDecoder(() => Encoding.GetEncoding(28591))).Parse(
                    TerrainParserTests.BuildTrn2(width: 4, length: 2, subColumns: 2));
                Child(Child(root, "Terrain"), "sub0");
                Child(root.transform.Find("Terrain").gameObject, "sub1");
                var buildings = Child(Child(root, "Buildings"), "Main");
                Child(buildings, "WestBuilding").transform.position = new Vector3(-1, 0, 0);
                Child(buildings, "EastBuilding").transform.position = new Vector3(1, 0, 0);
                var objects = Child(root, "Objects"); objects.AddComponent<WorldObjectsController>();
                var effects = Child(objects, "Effects");
                Child(effects, "WestEffect").transform.position = new Vector3(-1, 0, 0);
                Child(effects, "BorderEffect").transform.position = Vector3.zero;
                Child(root, "Sun").AddComponent<Light>();
                Child(root, "Environment");

                var mesh = new Mesh { name = "AcrossBorder" };
                mesh.vertices = new[] { new Vector3(-1.5f, 0, 0), new Vector3(-0.5f, 0, 0), new Vector3(-1, 0, 0.5f),
                    new Vector3(0.5f, 0, 0), new Vector3(1.5f, 0, 0), new Vector3(1, 0, 0.5f) };
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.zero, Vector2.right, Vector2.up };
                mesh.colors = Enumerable.Repeat(Color.gray, 6).ToArray();
                mesh.triangles = new[] { 0, 1, 2, 3, 4, 5 }; mesh.RecalculateNormals();
                AssetDatabase.CreateAsset(mesh, folder + "/Source.asset");
                var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
                AssetDatabase.CreateAsset(material, folder + "/Source.mat");
                foreach (string category in new[] { "Grass", "Water" })
                {
                    var go = Child(Child(root, category), "AcrossBorder");
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = material;
                }

                var paths = MapSectorPrefabExporter.Export(root, terrain, folder, null, false, out var environment);
                Assert.AreEqual(2, paths.Count);
                Assert.IsNotEmpty(environment);
                var west = AssetDatabase.LoadAssetAtPath<GameObject>(paths[0]);
                var east = AssetDatabase.LoadAssetAtPath<GameObject>(paths[1]);
                Assert.IsNotNull(west.transform.Find("Terrain/sub0"));
                Assert.IsNull(west.transform.Find("Terrain/sub1"));
                Assert.IsNotNull(east.transform.Find("Terrain/sub1"));
                Assert.IsNotNull(west.transform.Find("Buildings/Main/WestBuilding"));
                Assert.IsNull(west.transform.Find("Buildings/Main/EastBuilding"));
                Assert.IsNotNull(east.transform.Find("Buildings/Main/EastBuilding"));
                Assert.AreEqual(1, paths.Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p))
                    .Count(p => p.transform.Find("Objects/Effects/BorderEffect") != null));
                Assert.IsNull(west.GetComponentInChildren<Light>());
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(environment).GetComponentInChildren<Light>());
                foreach (var prefab in new[] { west, east })
                {
                    var water = prefab.transform.Find("Water/AcrossBorder");
                    var saved = water.GetComponent<MeshFilter>().sharedMesh;
                    Assert.AreEqual(3, saved.vertexCount);
                    Assert.AreEqual(3, saved.triangles.Length);
                    CollectionAssert.AreEqual(new[] { Vector2.zero, Vector2.right, Vector2.up }, saved.uv);
                    Assert.AreEqual(Color.gray, saved.colors[0]);
                    Assert.AreSame(material, water.GetComponent<MeshRenderer>().sharedMaterial);
                }
                // Grass without a spawn-sector group stays whole and follows its pivot.
                var grassOwners = new[] { west, east }.Count(p => p.transform.Find("Grass/AcrossBorder") != null);
                Assert.AreEqual(1, grassOwners);
                string guid = AssetDatabase.AssetPathToGUID(paths[1]);
                string geometry = AssetDatabase.GetAssetPath(east.transform.Find("Water/AcrossBorder").GetComponent<MeshFilter>().sharedMesh);
                string meshGuid = AssetDatabase.AssetPathToGUID(geometry);
                var again = MapSectorPrefabExporter.Export(root, terrain, folder, null, false, out _);
                Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(again[1]));
                Assert.AreEqual(meshGuid, AssetDatabase.AssetPathToGUID(geometry));
                var selected = MapSectorPrefabExporter.Export(root, terrain, folder, new[] { 1 }, false, out _);
                CollectionAssert.AreEqual(new[] { paths[1] }, selected);
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(paths[0]), "Unselected assets must remain intact.");
            }
            finally { Object.DestroyImmediate(root); AssetDatabase.DeleteAsset(folder); }
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var child = new GameObject(name); child.transform.SetParent(parent.transform, false); return child;
        }
    }
}
