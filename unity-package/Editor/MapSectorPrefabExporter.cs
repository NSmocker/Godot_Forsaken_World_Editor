using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>Exports sector views of an already assembled map without importing its sources again.</summary>
    public static class MapSectorPrefabExporter
    {
        public static List<string> Export(GameObject map, TerrainDocument terrain, string folder,
            IReadOnlyCollection<int> selection, bool showProgress, out string environmentPath)
        {
            environmentPath = string.Empty;
            var roots = new SortedDictionary<int, GameObject>();
            var paths = new List<string>();
            var transientMeshes = new List<Mesh>();
            GameObject environment = null;
            try
            {
                var sourceTerrain = map.transform.Find("Terrain");
                if (sourceTerrain == null) return paths;
                for (int s = 0; s < terrain.Subs.Length; s++)
                {
                    if (selection != null && !selection.Contains(s)) continue;
                    var sub = sourceTerrain.Find(terrain.Subs[s].Name);
                    if (sub == null) continue;
                    var root = new GameObject("Sector_" + s.ToString("D4"));
                    roots.Add(s, root);
                    Copy(sub, Group(root.transform, "Terrain"));
                    var info = map.GetComponent<MapSourceInfo>();
                    if (info != null) EditorUtility.CopySerialized(info, root.AddComponent<MapSourceInfo>());
                }
                if (roots.Count == 0) return paths;

                int Owner(Vector3 point)
                {
                    var candidates = new List<int>(roots.Keys);
                    return MapSectorFilter.Owner(terrain, candidates, point.x, point.z);
                }

                var buildings = map.transform.Find("Buildings");
                if (buildings != null)
                    foreach (Transform group in buildings)
                        foreach (Transform building in group)
                            Copy(building, Group(Group(roots[Owner(building.position)].transform, "Buildings"), group.name));

                var objects = map.transform.Find("Objects");
                if (objects != null)
                    foreach (Transform category in objects)
                        foreach (Transform item in category)
                        {
                            var target = Group(roots[Owner(item.position)].transform, "Objects");
                            if (target.GetComponent<WorldObjectsController>() == null)
                            {
                                var controller = target.gameObject.AddComponent<WorldObjectsController>();
                                var original = objects.GetComponent<WorldObjectsController>();
                                if (original != null) EditorUtility.CopySerialized(original, controller);
                            }
                            Copy(item, Group(target, category.name));
                        }

                var grass = map.transform.Find("Grass");
                if (grass != null)
                {
                    foreach (Transform item in grass)
                    {
                        int sector;
                        if (TrySectorGroup(item.name, out sector))
                        {
                            if (!roots.ContainsKey(sector))
                            {
                                var bounds = MapSectorFilter.SectorBounds(terrain, sector);
                                sector = Owner(new Vector3(
                                    (bounds.MinX + bounds.MaxX) * 0.5f, 0f, (bounds.MinZ + bounds.MaxZ) * 0.5f));
                            }
                            var target = Group(roots[sector].transform, "Grass");
                            foreach (Transform chunk in item)
                                Copy(chunk, target);
                            continue;
                        }

                        Copy(item, Group(roots[Owner(item.position)].transform, "Grass"));
                    }
                }

                foreach (string category in new[] { "Water" })
                {
                    var source = map.transform.Find(category);
                    if (source == null) continue;
                    foreach (Transform item in source)
                    {
                        CheckCancel(showProgress, "Partitioning " + item.name);
                        var filter = item.GetComponent<MeshFilter>();
                        if (filter == null || filter.sharedMesh == null)
                        {
                            Copy(item, Group(roots[Owner(item.position)].transform, category));
                            continue;
                        }
                        var mesh = filter.sharedMesh;
                        var vertices = mesh.vertices;
                        var partitions = new Dictionary<int, List<int>[]>();
                        for (int sub = 0; sub < mesh.subMeshCount; sub++)
                        {
                            if (mesh.GetTopology(sub) != MeshTopology.Triangles)
                                throw new InvalidDataException("Expected triangles in " + item.name);
                            var indices = mesh.GetTriangles(sub);
                            for (int i = 0; i < indices.Length; i += 3)
                            {
                                var center = (vertices[indices[i]] + vertices[indices[i + 1]] + vertices[indices[i + 2]]) / 3f;
                                int owner = Owner(item.TransformPoint(center));
                                if (!partitions.TryGetValue(owner, out var lists))
                                    partitions[owner] = lists = Enumerable.Range(0, mesh.subMeshCount).Select(_ => new List<int>()).ToArray();
                                lists[sub].Add(indices[i]); lists[sub].Add(indices[i + 1]); lists[sub].Add(indices[i + 2]);
                            }
                        }
                        foreach (var partition in partitions.OrderBy(p => p.Key))
                        {
                            var copy = Copy(item, Group(roots[partition.Key].transform, category));
                            // Reuse meshes wholly inside one sector; split only cross-sector geometry.
                            if (partitions.Count == 1) continue;
                            var split = Extract(mesh, partition.Value);
                            transientMeshes.Add(split);
                            copy.GetComponent<MeshFilter>().sharedMesh = split;
                            var output = folder + "/Sectors/Geometry/" + category + "/" + item.name + "_" + partition.Key.ToString("D4") + ".asset";
                            AngelicaAssetFolders.Ensure(Path.GetDirectoryName(output));
                            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(output);
                            if (existing != null)
                            {
                                EditorUtility.CopySerialized(split, existing); EditorUtility.SetDirty(existing);
                                copy.GetComponent<MeshFilter>().sharedMesh = existing;
                            }
                            else AssetDatabase.CreateAsset(split, output);
                        }
                    }
                }

                // Global lighting must be instantiated once, not once per loaded sector.
                foreach (string name in new[] { "Sun", "Environment" })
                {
                    var child = map.transform.Find(name);
                    if (child == null) continue;
                    if (environment == null) environment = new GameObject(map.name + "_Environment");
                    Copy(child, environment.transform);
                }
                if (environment != null)
                {
                    environmentPath = folder + "/Sectors/Environment.prefab";
                    Save(environment, environmentPath);
                }
                foreach (var pair in roots)
                {
                    CheckCancel(showProgress, "Saving " + pair.Value.name);
                    string path = folder + "/Sectors/" + pair.Value.name + ".prefab";
                    Save(pair.Value, path);
                    paths.Add(path);
                }
                GameDataImportSession.SaveAssets();
                return paths;
            }
            finally
            {
                foreach (var root in roots.Values) Object.DestroyImmediate(root);
                if (environment != null) Object.DestroyImmediate(environment);
                foreach (var mesh in transientMeshes)
                    if (mesh != null && !EditorUtility.IsPersistent(mesh)) Object.DestroyImmediate(mesh);
            }
        }

        private static Mesh Extract(Mesh source, List<int>[] triangles)
        {
            var oldIndices = triangles.SelectMany(t => t).Distinct().ToArray();
            var remap = oldIndices.Select((old, index) => new { old, index }).ToDictionary(p => p.old, p => p.index);
            var result = new Mesh { name = source.name, indexFormat = oldIndices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            try
            {
                var vertices = source.vertices;
                result.vertices = oldIndices.Select(i => vertices[i]).ToArray();
                var normals = source.normals; if (normals.Length > 0) result.normals = oldIndices.Select(i => normals[i]).ToArray();
                var tangents = source.tangents; if (tangents.Length > 0) result.tangents = oldIndices.Select(i => tangents[i]).ToArray();
                var colors = source.colors; if (colors.Length > 0) result.colors = oldIndices.Select(i => colors[i]).ToArray();
                for (int channel = 0; channel < 8; channel++)
                {
                    var uv = new List<Vector4>(); source.GetUVs(channel, uv);
                    if (uv.Count > 0) result.SetUVs(channel, oldIndices.Select(i => uv[i]).ToList());
                }
                result.subMeshCount = triangles.Length;
                for (int sub = 0; sub < triangles.Length; sub++)
                    result.SetTriangles(triangles[sub].Select(i => remap[i]).ToArray(), sub);
                result.RecalculateBounds();
                return result;
            }
            catch { Object.DestroyImmediate(result); throw; }
        }

        private static bool TrySectorGroup(string name, out int sector)
        {
            sector = -1;
            const string prefix = "Sector_";
            return name != null && name.StartsWith(prefix, StringComparison.Ordinal) &&
                int.TryParse(name.Substring(prefix.Length), out sector) && sector >= 0;
        }

        private static Transform Group(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;
            var group = new GameObject(name).transform; group.SetParent(parent, false); return group;
        }

        private static GameObject Copy(Transform source, Transform parent)
        {
            var copy = Object.Instantiate(source.gameObject, parent, false);
            copy.name = source.name;
            copy.transform.SetPositionAndRotation(source.position, source.rotation);
            copy.transform.localScale = source.lossyScale;
            return copy;
        }

        private static void Save(GameObject root, string path)
        {
            AngelicaAssetFolders.Ensure(Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
            if (!saved) throw new IOException("Could not save sector prefab: " + path);
        }

        private static void CheckCancel(bool show, string message)
        {
            if (show && EditorUtility.DisplayCancelableProgressBar("Export map sectors", message, 0.97f))
                throw new OperationCanceledException("Sector export cancelled; completed sector assets are retained.");
        }
    }
}
