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
        public const string HdrpShaderName = AngelicaRenderPipeline.HdrpTerrain;
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

        /// <summary>
        /// Imports a terrain prefab without modifying the current scene. Null sector indices
        /// import all sub-terrains; an explicit selection must contain at least one valid index.
        /// </summary>
        public static Result Import(GameDataFile file, GameDataIndex index, GfxTextDecoder decoder,
            bool showProgress = true, IReadOnlyCollection<int> selectedSectorIndices = null)
        {
            using (new GameDataImportSession())
            using (GameDataImportSession.Stage("Terrain"))
                return ImportCore(file, index, decoder, showProgress, selectedSectorIndices);
        }

        private static Result ImportCore(GameDataFile file, GameDataIndex index, GfxTextDecoder decoder,
            bool showProgress, IReadOnlyCollection<int> selectedSectorIndices)
        {
            if (file == null || index == null) throw new ArgumentNullException();
            var parser = new TerrainParser(decoder);
            var t = parser.Parse(File.ReadAllBytes(file.FullPath), file.RelativePath);
            HashSet<int> selectedSectors = null;
            if (selectedSectorIndices != null)
            {
                selectedSectors = new HashSet<int>(selectedSectorIndices);
                if (selectedSectors.Count == 0)
                    throw new ArgumentException("Select at least one terrain sector.", nameof(selectedSectorIndices));
                foreach (int sector in selectedSectors)
                    if (sector < 0 || sector >= t.Subs.Length)
                        throw new ArgumentOutOfRangeException(nameof(selectedSectorIndices), sector,
                            file.RelativePath + ": sector index must be between 0 and " + (t.Subs.Length - 1) + ".");
            }
            if (!SystemInfo.supports2DArrayTextures) throw new NotSupportedException("Terrain requires Texture2DArray support.");
            var shader = AngelicaRenderPipeline.Find(HdrpShaderName, ShaderName);
            if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Angelica Terrain shader is unavailable. Use a URP project and resolve shader compilation errors.");
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
                int sectorCount = selectedSectors?.Count ?? t.Subs.Length;
                int sectorOrdinal = 0;
                for (int s = 0; s < t.Subs.Length; s++)
                {
                    // Keep s as the source index: area coordinates, names and height offsets
                    // belong to the full map even when earlier sectors are omitted.
                    if (selectedSectors != null && !selectedSectors.Contains(s)) continue;
                    int progressIndex = sectorOrdinal++;
                    Progress(showProgress, "Reading " + t.Subs[s].Name, (float)progressIndex / sectorCount);
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
                    using (new GameDataAssetBatch())
                    {
                        var subRoot = new GameObject(t.Subs[s].Name);
                        subRoot.transform.SetParent(root.transform, false);
                        for (int a = 0; a < masks.Areas.Length; a++)
                        {
                            Progress(showProgress, t.Subs[s].Name + ": area " + (a + 1) + "/" + masks.Areas.Length,
                                (progressIndex + (float)a / Math.Max(1, masks.Areas.Length)) / sectorCount);
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
                            // Godot _area_material still builds the area with layer_count 0 (black).
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
                    GameDataImportSession.SaveAssets();
                    GameDataImportSession.FlushSave();
                }
                if (result.Areas == 0) throw new InvalidDataException(
                    selectedSectors == null ? "Terrain contains no visible areas." :
                    "Selected terrain sectors contain no readable visible areas. Check their block and mask files.");
                result.PrefabPath = folder + "/" + root.name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, result.PrefabPath, out bool saved);
                if (!saved) throw new IOException("Could not save terrain prefab: " + result.PrefabPath);
                GameDataImportSession.SaveAssets();
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
            for (int i = 1; i < entries.Length; i++) size = Math.Max(size, ResolveLayer(masks, entries[i].LayerIndex).MaskSize);
            var array = new Texture2DArray(size, size, Math.Max(1, entries.Length - 1), TextureFormat.RGBA32, true, true)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var parameters = new Texture2D(MaxLayers, 1, TextureFormat.RGBAFloat, false, true)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            try
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    var layer = ResolveLayer(masks, entries[i].LayerIndex);
                    parameters.SetPixel(i, 0, new Color(
                        ClampTextureIndex(layer.TextureIndex, t.Textures.Length), layer.UScale, layer.VScale, layer.Axis));
                    if (i == 0) continue;
                    var pixels = new Color32[size * size];
                    var mask = entries[i].Mask;
                    // Godot: mask data of the wrong length becomes an empty (black) mask image.
                    bool validMask = layer.MaskSize > 0 && mask != null && mask.Length == layer.MaskSize * layer.MaskSize;
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            byte value = validMask
                                ? mask[(y * layer.MaskSize / size) * layer.MaskSize + x * layer.MaskSize / size]
                                : (byte)0;
                            pixels[y * size + x] = new Color32(value, value, value, 255);
                        }
                    array.SetPixels32(pixels, i - 1);
                }
                if (entries.Length <= 1) array.SetPixels32(new[] { new Color32(0, 0, 0, 255) }, 0);
                array.Apply(true, false); parameters.Apply();
                // Texture assets serialize all layer parameters, unlike Material.SetVectorArray.
                if (shader == null)
                    shader = Shader.Find("Hidden/InternalErrorShader");

                if (shader == null)
                    throw new System.InvalidOperationException("No shader for material.");

                var mat = new Material(shader) { name = Path.GetFileName(prefix) };
                mat.SetTexture("_Layers", layers);
                mat.SetTexture("_Masks", Save(array, prefix + "_Masks.asset")); array = null;
                mat.SetTexture("_LayerParams", Save(parameters, prefix + "_Parameters.asset")); parameters = null;
                mat.SetFloat("_LayerCount", entries.Length); mat.SetFloat("_MinHeight", t.Subs[sub].MinHeight);
                mat.SetFloat("_UseLightmap", 0); mat.SetFloat("_Brightness", BuildingAssetImporter.DefaultBrightness);
                return mat;
            }
            finally { if (array != null) Object.DestroyImmediate(array); if (parameters != null) Object.DestroyImmediate(parameters); }
        }

        /// <summary>
        /// Godot _area_material: an out-of-range layer index uses {} (texture 0, u/v scale 1,
        /// axis 1, no mask).
        /// </summary>
        public static TerrainLayer ResolveLayer(TerrainMasks masks, int layerIndex)
        {
            if (masks != null && masks.Layers != null && layerIndex >= 0 && layerIndex < masks.Layers.Length &&
                masks.Layers[layerIndex] != null)
                return masks.Layers[layerIndex];
            return new TerrainLayer { TextureIndex = 0, MaskSize = 0, Axis = 1, UScale = 1f, VScale = 1f };
        }

        /// <summary>Godot: clampi(texture, 0, maxi(0, num_texture - 1)).</summary>
        public static int ClampTextureIndex(int textureIndex, int textureCount) =>
            Math.Max(0, Math.Min(textureIndex, Math.Max(0, textureCount - 1)));

        private static Texture2DArray BuildLayers(TerrainDocument t, GameDataFile map, GameDataIndex index, List<string> warnings)
        {
            var images = new List<Texture2D>();
            Texture2DArray array = null;
            try
            {
                PrefetchGroundTextures(t, map, index);
                int size = 4;
                foreach (string texture in t.Textures)
                {
                    Texture2D image = null;
                    try
                    {
                        var projectTexture = TerrainTextureBuilder.FindProjectTexture(texture);
                        if (projectTexture != null)
                            image = TerrainTextureBuilder.LoadProjectTexture(projectTexture);
                        else
                        {
                            var file = ResolveTexture(index, map.RelativePath, texture);
                            if (file == null) throw new FileNotFoundException("Ground texture not found: " + texture);
                            image = TerrainTextureBuilder.LoadSource(file);
                        }
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
                array = AngelicaTextureArrayBuilder.Build(
                    images, name: "Terrain Layers", maxEdge: 1024, linear: true, warnings: warnings);
                return array;
            }
            catch { if (array != null) Object.DestroyImmediate(array); throw; }
            finally { foreach (var image in images) Object.DestroyImmediate(image); }
        }

        internal static GameDataFile ResolveTexture(GameDataIndex index, string map, string texture)
        {
            // Keep an enclosing client's textures ahead of similarly named assets in another client.
            string normalized = map.Replace('\\', '/'); int maps = normalized.IndexOf("/maps/", StringComparison.OrdinalIgnoreCase);
            if (maps >= 0 && index.TryResolve(normalized.Substring(0, maps + 1) + texture, out var local, out _)) return local;
            return index.TryResolve(texture, out var file, out _, true) ? file : null;
        }
        private static void PrefetchGroundTextures(TerrainDocument terrain, GameDataFile map, GameDataIndex index)
        {
            if (terrain == null || terrain.Textures == null || index == null)
                return;
            var files = new List<GameDataFile>();
            foreach (var texture in terrain.Textures)
            {
                if (TerrainTextureBuilder.FindProjectTexture(texture) != null) continue;
                var file = ResolveTexture(index, map.RelativePath, texture);
                if (file == null || TextureFileFormat.DetectFile(file.FullPath) == ".dds")
                    continue;
                files.Add(file);
            }

            AssembledTextureImporter.ImportFiles(files);
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
            AngelicaAssetFolders.Ensure(folder);
        }
        private static T Save<T>(T value, string path) where T : Object
        {
            value.name = Path.GetFileNameWithoutExtension(path);
            var existing = GameDataAssetBatch.Load(path) as T;
            if (existing == null) { GameDataAssetBatch.Create(value, path); return value; }
            EditorUtility.CopySerialized(value, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(value);
            return existing;
        }
    }
}
