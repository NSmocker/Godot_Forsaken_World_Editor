using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SilverEcho.ForsakenWorld.Editor
{
    public static class TerrainAssetImporter
    {
        public const string ShaderName = "Forsaken World/Angelica Terrain";
        public const int MaxLayers = 16;
        public sealed class Result
        {
            public string PrefabPath;
            public int Areas, HiddenAreas;
            public long Vertices, Triangles;
            public readonly List<string> Warnings = new List<string>();
        }

        // Deterministic per-source folder, including a hash to avoid sanitization collisions.
        public static string GetOutputFolder(string relativePath)
        {
            string name = Path.GetFileNameWithoutExtension(relativePath);
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(relativePath.Replace('\\', '/').ToLowerInvariant()));
                return "Assets/ForsakenWorld/Imported/Terrain/" + name + "_" + BitConverter.ToString(hash, 0, 6).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>Imports a full terrain prefab. Does not modify the current scene.</summary>
        public static Result Import(GameDataFile file, GameDataIndex index, GfxTextDecoder decoder,
            bool showProgress = true)
        {
            if (file == null || index == null) throw new ArgumentNullException();
            if (!SystemInfo.supports2DArrayTextures) throw new NotSupportedException("Terrain requires Texture2DArray support.");
            var shader = Shader.Find(ShaderName);
            if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Angelica Terrain shader is unavailable. Use a URP project and resolve shader compilation errors.");
            var parser = new TerrainParser(decoder);
            var t = parser.Parse(File.ReadAllBytes(file.FullPath), file.RelativePath);
            var result = new Result();
            string folder = GetOutputFolder(file.RelativePath);
            GameObject root = null;
            try
            {
                // Match Godot import_map: warn and skip unreadable subs instead of aborting the map.
                string relativeDir = (Path.GetDirectoryName(file.RelativePath) ?? "").Replace('\\', '/');
                EnsureFolder(folder);
                Progress(showProgress, "Reading ground textures", 0);
                var layers = Save(BuildLayers(t, file, index, result.Warnings), folder + "/Layers.asset");
                root = new GameObject(file.Name + "_Terrain");
                for (int s = 0; s < t.Subs.Length; s++)
                {
                    Progress(showProgress, "Reading " + t.Subs[s].Name, (float)s / Math.Max(1, t.Subs.Length));
                    GameDataFile blockFile;
                    GameDataFile maskFile;
                    try
                    {
                        blockFile = ResolveRequired(index, Join(relativeDir, t.Subs[s].Name + ".t2bk"));
                        maskFile = ResolveRequired(index, Join(relativeDir, t.Subs[s].Name + ".t2mk"));
                    }
                    catch (FileNotFoundException e)
                    {
                        result.Warnings.Add(e.Message);
                        continue;
                    }

                    Dictionary<long, TerrainBlock> blocks;
                    TerrainMasks masks;
                    int perSide;
                    try
                    {
                        blocks = parser.ReadBlocks(File.ReadAllBytes(blockFile.FullPath), t.BlockGrid, blockFile.RelativePath);
                        masks = parser.ReadMasks(File.ReadAllBytes(maskFile.FullPath), maskFile.RelativePath);
                        perSide = t.AreasPerSubSide;
                        if (masks.Areas.Length != perSide * perSide ||
                            blocks.Count != (t.SubGrid / t.BlockGrid) * (t.SubGrid / t.BlockGrid))
                            throw new InvalidDataException(t.Subs[s].Name + ": block/mask counts do not match TRN2");
                    }
                    catch (InvalidDataException e)
                    {
                        result.Warnings.Add(e.Message);
                        continue;
                    }

                    var lightmaps = Array.Empty<TerrainLightmap>();
                    string lmPath = Join(relativeDir, t.Subs[s].Name + ".t2lm");
                    if (index.TryResolve(lmPath, out var lmFile, out _))
                    {
                        try { lightmaps = parser.ReadLightmaps(File.ReadAllBytes(lmFile.FullPath), lmFile.RelativePath); }
                        catch (InvalidDataException e) { result.Warnings.Add(e.Message); }
                    }
                    if (lightmaps.Length != masks.Areas.Length)
                        result.Warnings.Add(t.Subs[s].Name + ": missing/incomplete daytime lightmaps; affected areas use unlit textures.");
                    var subRoot = new GameObject(t.Subs[s].Name);
                    subRoot.transform.SetParent(root.transform, false);
                    for (int a = 0; a < masks.Areas.Length; a++)
                    {
                        Progress(showProgress, t.Subs[s].Name + ": area " + (a + 1) + "/" + masks.Areas.Length,
                            (s + (float)a / Math.Max(1, masks.Areas.Length)) / Math.Max(1, t.Subs.Length));
                        int row = s / t.SubColumns * perSide + a / perSide, col = s % t.SubColumns * perSide + a % perSide;
                        string areaName = "Area_" + (row * t.AreaColumns + col).ToString("D4"), prefix = folder + "/" + areaName;
                        TerrainMeshData meshData;
                        try { meshData = TerrainMeshAssembler.BuildArea(t, blocks, row, col); }
                        catch (InvalidDataException e)
                        {
                            result.Warnings.Add(e.Message);
                            continue;
                        }
                        if (meshData.Indices.Length == 0) { result.HiddenAreas++; continue; }
                        if (masks.Areas[a].Length == 0)
                        {
                            result.Warnings.Add(areaName + ": visible terrain has no texture layers");
                            continue;
                        }
                        var mesh = Save(BuildMesh(meshData, areaName), prefix + "_Mesh.asset");
                        var mat = BuildMaterial(t, masks, a, s, layers, shader, prefix, result.Warnings);
                        if (a < lightmaps.Length)
                        {
                            try
                            {
                                var lm = TerrainTextureBuilder.Lightmap(lightmaps[a], lmPath + " area " + a);
                                lm.wrapMode = TextureWrapMode.Clamp; lm.filterMode = FilterMode.Bilinear;
                                mat.SetTexture("_Lightmap", Save(lm, prefix + "_Lightmap.asset"));
                                mat.SetFloat("_UseLightmap", 1);
                            }
                            catch (InvalidDataException e) { result.Warnings.Add(e.Message); }
                        }
                        mat = Save(mat, prefix + ".mat");
                        var go = new GameObject(areaName);
                        go.transform.SetParent(subRoot.transform, false);
                        go.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = mat;
                        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                        result.Areas++; result.Vertices += mesh.vertexCount; result.Triangles += meshData.Indices.Length / 3;
                    }
                }
                if (result.Areas == 0) throw new InvalidDataException("Terrain contains no visible areas.");
                result.PrefabPath = folder + "/" + root.name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, result.PrefabPath, out bool saved);
                if (!saved) throw new IOException("Could not save terrain prefab: " + result.PrefabPath);
                AssetDatabase.SaveAssets();
                return result;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                if (showProgress) EditorUtility.ClearProgressBar();
            }
        }

        public static Mesh BuildMesh(TerrainMeshData data, string name)
        {
            int count = data.Positions.Length / 3;
            var vertices = new Vector3[count]; var normals = new Vector3[count]; var uv = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(data.Positions[i * 3], data.Positions[i * 3 + 1], data.Positions[i * 3 + 2]);
                normals[i] = new Vector3(data.Normals[i * 3], data.Normals[i * 3 + 1], data.Normals[i * 3 + 2]);
                uv[i] = new Vector2(data.Uvs[i * 2], data.Uvs[i * 2 + 1]);
            }
            var mesh = new Mesh { name = name, indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.triangles = data.Indices; mesh.RecalculateBounds();
            return mesh;
        }

        private static Material BuildMaterial(TerrainDocument t, TerrainMasks masks, int area, int sub,
            Texture2DArray layers, Shader shader, string prefix, List<string> warnings)
        {
            var entries = masks.Areas[area];
            // Godot truncates to MaxLayers with a warning instead of failing the area.
            if (entries.Length > MaxLayers)
            {
                warnings.Add(prefix + ": mask area has " + entries.Length + " layers, using first " + MaxLayers);
                var truncated = new TerrainAreaLayer[MaxLayers];
                Array.Copy(entries, truncated, MaxLayers);
                entries = truncated;
            }
            int size = 1;
            for (int i = 1; i < entries.Length; i++) size = Math.Max(size, masks.Layers[entries[i].LayerIndex].MaskSize);
            var array = new Texture2DArray(size, size, Math.Max(1, entries.Length - 1), TextureFormat.RGBA32, true, true)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var parameters = new Texture2D(MaxLayers, 1, TextureFormat.RGBAFloat, false, true)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            try
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    var layer = masks.Layers[entries[i].LayerIndex];
                    if (layer.TextureIndex < 0 || layer.TextureIndex >= t.Textures.Length)
                        throw new InvalidDataException(prefix + ": invalid layer texture index " + layer.TextureIndex);
                    parameters.SetPixel(i, 0, new Color(layer.TextureIndex, layer.UScale, layer.VScale, layer.Axis));
                    if (i == 0) continue;
                    var pixels = new Color32[size * size];
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            byte value = entries[i].Mask[(y * layer.MaskSize / size) * layer.MaskSize + x * layer.MaskSize / size];
                            pixels[y * size + x] = new Color32(value, value, value, 255);
                        }
                    array.SetPixels32(pixels, i - 1);
                }
                if (entries.Length == 1) array.SetPixels32(new[] { new Color32(0, 0, 0, 255) }, 0);
                array.Apply(true, false); parameters.Apply();
                // Texture assets serialize all layer parameters, unlike Material.SetVectorArray.
                var mat = new Material(shader) { name = Path.GetFileName(prefix) };
                mat.SetTexture("_Layers", layers);
                mat.SetTexture("_Masks", Save(array, prefix + "_Masks.asset")); array = null;
                mat.SetTexture("_LayerParams", Save(parameters, prefix + "_Parameters.asset")); parameters = null;
                mat.SetFloat("_LayerCount", entries.Length); mat.SetFloat("_MinHeight", t.Subs[sub].MinHeight);
                mat.SetFloat("_UseLightmap", 0); mat.SetFloat("_Brightness", 1);
                return mat;
            }
            finally { if (array != null) Object.DestroyImmediate(array); if (parameters != null) Object.DestroyImmediate(parameters); }
        }

        private static Texture2DArray BuildLayers(TerrainDocument t, GameDataFile map, GameDataIndex index, List<string> warnings)
        {
            var images = new List<Texture2D>();
            Texture2DArray array = null;
            try
            {
                int size = 4;
                foreach (string texture in t.Textures)
                {
                    Texture2D image = null;
                    try
                    {
                        var file = ResolveTexture(index, map.RelativePath, texture);
                        if (file == null) throw new FileNotFoundException("Ground texture not found: " + texture);
                        image = TerrainTextureBuilder.LoadSource(file);
                    }
                    catch (Exception e) when (e is IOException || e is NotSupportedException)
                    { warnings.Add(e.Message + " (magenta fallback)"); }
                    if (image == null)
                    {
                        image = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                        image.SetPixels(new[] { Color.magenta, Color.black, Color.black, Color.magenta }); image.Apply();
                    }
                    images.Add(image); size = Math.Max(size, Math.Max(image.width, image.height));
                }
                size = Math.Min(size, 1024);
                if ((long)size * size * images.Count > 134217728)
                    throw new NotSupportedException("Terrain texture array exceeds the 512 MiB uncompressed import limit.");
                array = new Texture2DArray(size, size, images.Count, TextureFormat.RGBA32, true, true)
                    { name = "Terrain Layers", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
                for (int i = 0; i < images.Count; i++) array.SetPixels32(TerrainTextureBuilder.Resample(images[i], size, size), i);
                array.Apply(true, false); return array;
            }
            catch { if (array != null) Object.DestroyImmediate(array); throw; }
            finally { foreach (var image in images) Object.DestroyImmediate(image); }
        }

        private static GameDataFile ResolveTexture(GameDataIndex index, string map, string texture)
        {
            // Keep an enclosing client's textures ahead of similarly named assets in another client.
            string normalized = map.Replace('\\', '/'); int maps = normalized.IndexOf("/maps/", StringComparison.OrdinalIgnoreCase);
            if (maps >= 0 && index.TryResolve(normalized.Substring(0, maps + 1) + texture, out var local, out _)) return local;
            return index.TryResolve(texture, out var file, out _, true) ? file : null;
        }
        private static GameDataFile ResolveRequired(GameDataIndex index, string path)
        {
            if (!index.TryResolve(path, out var file, out var error)) throw new FileNotFoundException(error);
            return file;
        }
        private static string Join(string directory, string file) => string.IsNullOrEmpty(directory) ? file : directory + "/" + file;
        private static void Progress(bool show, string text, float value)
        {
            if (show && EditorUtility.DisplayCancelableProgressBar("Import Angelica Terrain", text, value))
                throw new OperationCanceledException("Terrain import cancelled. Existing generated assets may have been updated; import again to finish.");
        }
        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
        private static T Save<T>(T value, string path) where T : Object
        {
            value.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(value);
            return existing;
        }
    }
}
