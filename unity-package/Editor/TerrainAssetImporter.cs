using System;
using System.Collections.Generic;
using System.IO;
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

        /// <summary>
        /// Generated terrain data lives beside grass/effects/sectors:
        /// Assets/ForsakenWorld/Imported/Maps/&lt;map&gt;/terrains/&lt;sector&gt;.
        /// Color DDS files stay in the textures folder and are linked, not copied.
        /// </summary>
        public static string GetOutputFolder(string relativePath) =>
            MapAssetImporter.GetOutputFolder(relativePath) + "/terrains";

        public static string GetSectorFolder(string relativePath, string sectorName) =>
            GetOutputFolder(relativePath) + "/" + Sanitize(sectorName);

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
            var result = new Result();
            string folder = GetOutputFolder(file.RelativePath);
            GameObject root = null;
            try
            {
                // Match Godot import_map: warn and skip unreadable subs instead of aborting the map.
                string relativeDir = (Path.GetDirectoryName(file.RelativePath) ?? "").Replace('\\', '/');
                EnsureFolder(folder);
                Progress(showProgress, "Reading ground textures", 0);
                var textures = ResolveGroundTextures(t, file, index, result.Warnings);
                var shader = AngelicaRenderPipeline.Find(HdrpShaderName, ShaderName);
                if (shader == null)
                {
                    result.Warnings.Add("Terrain shader '" + ShaderName + "' was not found.");
                    shader = Shader.Find("Hidden/InternalErrorShader");
                }
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

                    TerrainLightmap[] lightmaps = null;
                    if (index.TryResolve(Join(relativeDir, t.Subs[s].Name + ".t2lm"), out var lightFile, out _))
                    {
                        try
                        {
                            lightmaps = parser.ReadLightmaps(File.ReadAllBytes(lightFile.FullPath), lightFile.RelativePath);
                        }
                        catch (Exception e) when (e is InvalidDataException || e is IOException)
                        {
                            result.Warnings.Add(t.Subs[s].Name + ": daytime lightmap was not applied (" + e.Message + ")");
                        }
                    }
                    else
                        result.Warnings.Add(t.Subs[s].Name + ": no daytime lightmap, terrain is unlit.");

                    var sectorObject = BuildSector(t, s, blocks, masks, lightmaps, textures, shader, file.RelativePath, result);
                    if (sectorObject == null)
                        continue;
                    sectorObject.transform.SetParent(root.transform, false);
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

        private static Texture2D[] ResolveGroundTextures(
            TerrainDocument terrain, GameDataFile map, GameDataIndex index, List<string> warnings)
        {
            var textures = new Texture2D[terrain.Textures.Length];
            PrefetchGroundTextures(terrain, map, index);
            for (int i = 0; i < terrain.Textures.Length; i++)
            {
                var name = terrain.Textures[i];
                var project = TerrainTextureBuilder.FindProjectTexture(name);
                if (project != null)
                {
                    var assetPath = AssembledTextureImporter.GetTextureAssetPath(project.RelativePath);
                    textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (textures[i] != null)
                        continue;
                }

                var file = ResolveTexture(index, map.RelativePath, name);
                if (file == null)
                {
                    warnings.Add("Ground texture not found: " + name + " (magenta fallback)");
                    continue;
                }

                var imported = AssembledTextureImporter.ImportOrUpdate(file.FullPath, file.RelativePath);
                textures[i] = imported.Texture;
                if (textures[i] == null)
                    warnings.Add((string.IsNullOrEmpty(imported.Error) ? "Ground texture import failed: " + name : imported.Error) +
                        " (magenta fallback)");
            }

            return textures;
        }

        private static GameObject BuildSector(
            TerrainDocument terrain, int sector, Dictionary<long, TerrainBlock> blocks, TerrainMasks masks,
            TerrainLightmap[] lightmaps, Texture2D[] textures, Shader shader, string mapRelativePath, Result result)
        {
            int areasPerSide = terrain.AreasPerSubSide;
            int sectorRow = sector / terrain.SubColumns;
            int sectorColumn = sector % terrain.SubColumns;
            var sectorObject = new GameObject(terrain.Subs[sector].Name);
            string folder = GetSectorFolder(mapRelativePath, terrain.Subs[sector].Name);
            int visible = 0;
            for (int area = 0; area < masks.Areas.Length; area++)
            {
                int row = sectorRow * areasPerSide + area / areasPerSide;
                int column = sectorColumn * areasPerSide + area % areasPerSide;
                TerrainMeshData data;
                try
                {
                    data = TerrainMeshAssembler.BuildArea(terrain, blocks, row, column);
                }
                catch (InvalidDataException e)
                {
                    result.Warnings.Add(e.Message);
                    result.HiddenAreas++;
                    continue;
                }

                if (data.Indices == null || data.Indices.Length == 0)
                {
                    result.HiddenAreas++;
                    continue;
                }

                EnsureFolder(folder);
                string prefix = folder + "/Area_" + area.ToString("D4");
                var mesh = Save(BuildMesh(data, "Area_" + area.ToString("D4")), prefix + "_Mesh.asset");
                var lightmap = lightmaps != null && area < lightmaps.Length ? lightmaps[area] : null;
                var material = BuildMaterial(terrain, masks, area, sector, textures, shader, prefix, data, lightmap, result.Warnings);
                var areaObject = new GameObject("Area_" + area.ToString("D4"));
                areaObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = areaObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                areaObject.transform.SetParent(sectorObject.transform, false);
                result.Vertices += mesh.vertexCount;
                result.Triangles += data.Indices.Length / 3;
                result.Areas++;
                visible++;
            }

            if (visible == 0)
            {
                Object.DestroyImmediate(sectorObject);
                return null;
            }

            return sectorObject;
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
            Texture2D[] textures, Shader shader, string prefix, TerrainMeshData mesh, TerrainLightmap lightmap,
            List<string> warnings)
        {
            var entries = masks.Areas != null && area >= 0 && area < masks.Areas.Length && masks.Areas[area] != null
                ? masks.Areas[area]
                : Array.Empty<TerrainAreaLayer>();
            if (entries.Length == 0)
                entries = new[] { new TerrainAreaLayer { LayerIndex = 0 } };
            // Godot truncates to MaxLayers with a warning instead of failing the area.
            if (entries.Length > MaxLayers)
            {
                warnings.Add(prefix + ": mask area has " + entries.Length + " layers, using first " + MaxLayers);
                var truncated = new TerrainAreaLayer[MaxLayers];
                Array.Copy(entries, truncated, MaxLayers);
                entries = truncated;
            }

            if (shader == null)
                shader = Shader.Find("Hidden/InternalErrorShader");
            if (shader == null)
                throw new InvalidOperationException("No shader for material.");

            Texture2D parameters = null;
            Texture2D height = null;
            Texture2D light = null;
            var splats = new Texture2D[4];
            Material mat = null;
            try
            {
                parameters = new Texture2D(MaxLayers, 1, TextureFormat.RGBAFloat, false, true)
                {
                    name = "Parameters",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point
                };
                int textureSlots = Math.Min(t.Textures.Length, MaxLayers);
                for (int i = 0; i < entries.Length; i++)
                {
                    var layer = ResolveLayer(masks, entries[i].LayerIndex);
                    parameters.SetPixel(i, 0, new Color(
                        ClampTextureIndex(layer.TextureIndex, textureSlots), layer.UScale, layer.VScale, layer.Axis));
                }
                parameters.Apply(false, false);

                height = BuildHeightMap(mesh, t.MaskGrid);
                PackSplats(masks, entries, splats);
                if (lightmap != null)
                {
                    try
                    {
                        light = TerrainTextureBuilder.Lightmap(lightmap, prefix + " lightmap");
                        light.wrapMode = TextureWrapMode.Clamp;
                        light.filterMode = FilterMode.Bilinear;
                    }
                    catch (Exception e) when (e is InvalidDataException || e is IOException || e is NotSupportedException)
                    {
                        warnings.Add(prefix + ": daytime lightmap was not applied (" + e.Message + ")");
                        if (light != null) Object.DestroyImmediate(light);
                        light = null;
                    }
                }

                mat = new Material(shader) { name = Path.GetFileName(prefix) };
                int linked = Math.Min(textures == null ? 0 : textures.Length, MaxLayers);
                for (int i = 0; i < linked; i++)
                    if (textures[i] != null)
                        mat.SetTexture("_Layer" + i, textures[i]);
                if (textures != null && textures.Length > MaxLayers)
                    warnings.Add(prefix + ": shader links the first " + MaxLayers + " ground textures.");
                for (int i = 0; i < splats.Length; i++)
                {
                    if (splats[i] == null) continue;
                    mat.SetTexture("_Splat" + i, Save(splats[i], prefix + "_Splat" + i + ".asset"));
                    splats[i] = null;
                }
                mat.SetTexture("_LayerParams", Save(parameters, prefix + "_Parameters.asset")); parameters = null;
                mat.SetTexture("_HeightMap", Save(height, prefix + "_Height.asset")); height = null;
                if (light != null)
                {
                    mat.SetTexture("_Lightmap", Save(light, prefix + "_Lightmap.asset"));
                    light = null;
                    mat.SetFloat("_UseLightmap", 1f);
                }
                else
                    mat.SetFloat("_UseLightmap", 0f);
                mat.SetFloat("_LayerCount", entries.Length);
                mat.SetFloat("_MinHeight", t.Subs[sub].MinHeight);
                mat.SetFloat("_Brightness", BuildingAssetImporter.DefaultBrightness);
                var saved = Save(mat, prefix + ".mat");
                mat = null;
                return saved;
            }
            finally
            {
                if (mat != null) Object.DestroyImmediate(mat);
                if (parameters != null) Object.DestroyImmediate(parameters);
                if (height != null) Object.DestroyImmediate(height);
                if (light != null) Object.DestroyImmediate(light);
                for (int i = 0; i < splats.Length; i++)
                    if (splats[i] != null) Object.DestroyImmediate(splats[i]);
            }
        }

        // Row 0 of the mesh is +Z. Pixel (column, row) stores that vertex height in R.
        private static Texture2D BuildHeightMap(TerrainMeshData mesh, int maskGrid)
        {
            int side = Math.Max(1, maskGrid) + 1;
            var texture = new Texture2D(side, side, TextureFormat.RGBAFloat, false, true)
            {
                name = "Height",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            int count = mesh.Positions.Length / 3;
            for (int i = 0; i < side; i++)
                for (int j = 0; j < side; j++)
                {
                    int vertex = i * side + j;
                    float y = vertex < count ? mesh.Positions[vertex * 3 + 1] : 0f;
                    texture.SetPixel(j, i, new Color(y, 0f, 0f, 1f));
                }
            texture.Apply(false, false);
            return texture;
        }

        // t2_4layer_batch_14.txt packs four overlay masks as alpha, red, green, blue.
        // Layer 0 stays opaque, matching angelica_terrain.gdshader.
        private static void PackSplats(TerrainMasks masks, TerrainAreaLayer[] entries, Texture2D[] splats)
        {
            int overlayCount = Math.Max(0, entries.Length - 1);
            int splatCount = Math.Min(splats.Length, (overlayCount + 3) / 4);
            for (int splat = 0; splat < splatCount; splat++)
            {
                int size = 1;
                for (int channel = 0; channel < 4; channel++)
                {
                    int layer = splat * 4 + channel + 1;
                    if (layer >= entries.Length) break;
                    size = Math.Max(size, ResolveLayer(masks, entries[layer].LayerIndex).MaskSize);
                }
                if (size < 1) size = 1;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
                {
                    name = "Splat" + splat,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var pixels = new Color32[size * size];
                for (int channel = 0; channel < 4; channel++)
                {
                    int layer = splat * 4 + channel + 1;
                    if (layer >= entries.Length) continue;
                    var source = ResolveLayer(masks, entries[layer].LayerIndex);
                    var mask = entries[layer].Mask;
                    bool valid = source.MaskSize > 0 && mask != null && mask.Length == source.MaskSize * source.MaskSize;
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            byte value = valid
                                ? mask[(y * source.MaskSize / size) * source.MaskSize + x * source.MaskSize / size]
                                : (byte)0;
                            var pixel = pixels[y * size + x];
                            if (channel == 0) pixel.a = value;
                            else if (channel == 1) pixel.r = value;
                            else if (channel == 2) pixel.g = value;
                            else pixel.b = value;
                            pixels[y * size + x] = pixel;
                        }
                }
                texture.SetPixels32(pixels);
                texture.Apply(true, false);
                splats[splat] = texture;
            }
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
        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "sector";
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }
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
