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
using UnityTerrainLayer = UnityEngine.TerrainLayer;

namespace SilverEcho.ForsakenWorld.Editor
{
    public static class TerrainAssetImporter
    {
        public const string ShaderName = "Forsaken World/Angelica Terrain";
        public const string HdrpShaderName = AngelicaRenderPipeline.HdrpTerrain;
        public const string UrpUnityTerrainShaderName = "Universal Render Pipeline/Terrain/Lit";
        public const string HdrpUnityTerrainShaderName = "HDRP/TerrainLit";
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
            var result = new Result();
            string folder = GetOutputFolder(file.RelativePath);
            GameObject root = null;
            try
            {
                // Match Godot import_map: warn and skip unreadable subs instead of aborting the map.
                string relativeDir = (Path.GetDirectoryName(file.RelativePath) ?? "").Replace('\\', '/');
                EnsureFolder(folder);
                Progress(showProgress, "Reading ground textures", 0);
                if (AssetDatabase.LoadAssetAtPath<Texture2DArray>(folder + "/Layers.asset") != null)
                    AssetDatabase.DeleteAsset(folder + "/Layers.asset");
                var textures = ResolveGroundTextures(t, file, index, result.Warnings);
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

                    if (index.TryResolve(Join(relativeDir, t.Subs[s].Name + ".t2lm"), out _, out _) &&
                        !result.Warnings.Exists(w => w.Contains("Daytime lightmaps are not applied")))
                        result.Warnings.Add("Daytime lightmaps are not applied to Unity Terrain. Replace a ground DDS to update the linked texture.");
                    var sectorObject = BuildUnityTerrain(t, s, blocks, masks, textures, folder, result);
                    if (sectorObject == null)
                    {
                        result.HiddenAreas += masks.Areas.Length;
                        continue;
                    }
                    sectorObject.transform.SetParent(root.transform, false);
                    result.Areas += masks.Areas.Length;
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

        private static GameObject BuildUnityTerrain(
            TerrainDocument terrain, int sector, Dictionary<long, TerrainBlock> blocks, TerrainMasks masks,
            Texture2D[] textures, string folder, Result result)
        {
            int areasPerSide = terrain.AreasPerSubSide;
            int cell = terrain.MaskGrid;
            int sourceResolution = areasPerSide * cell + 1;
            if (sourceResolution < 2)
                return null;
            var source = new float[sourceResolution, sourceResolution];
            var solid = new bool[sourceResolution - 1, sourceResolution - 1];
            int sectorRow = sector / terrain.SubColumns;
            int sectorColumn = sector % terrain.SubColumns;
            int blockGrid = terrain.BlockGrid;
            bool anySolid = false;
            for (int area = 0; area < masks.Areas.Length; area++)
            {
                int areaRow = sectorRow * areasPerSide + area / areasPerSide;
                int areaColumn = sectorColumn * areasPerSide + area % areasPerSide;
                int blocksPerArea = terrain.MaskBlock;
                for (int bi = 0; bi < blocksPerArea; bi++)
                    for (int bj = 0; bj < blocksPerArea; bj++)
                    {
                        if (!blocks.TryGetValue(TerrainBlock.Key(areaRow * blocksPerArea + bi, areaColumn * blocksPerArea + bj), out var block))
                            continue;
                        for (int vi = 0; vi <= blockGrid; vi++)
                            for (int vj = 0; vj <= blockGrid; vj++)
                            {
                                // Mask areas walk row first. Local row is area / side, column is area % side.
                                int i = (area / areasPerSide) * cell + bi * blockGrid + vi;
                                int j = (area % areasPerSide) * cell + bj * blockGrid + vj;
                                if (i >= sourceResolution || j >= sourceResolution)
                                    continue;
                                int vertex = vi * (blockGrid + 1) + vj;
                                if (block.Heights != null && vertex < block.Heights.Length)
                                    source[i, j] = block.Heights[vertex];
                            }
                        if (block.NoRender)
                            continue;
                        anySolid = true;
                        int row0 = (area / areasPerSide) * cell + bi * blockGrid;
                        int col0 = (area % areasPerSide) * cell + bj * blockGrid;
                        for (int vi = 0; vi < blockGrid; vi++)
                            for (int vj = 0; vj < blockGrid; vj++)
                                if (row0 + vi < solid.GetLength(0) && col0 + vj < solid.GetLength(1))
                                    solid[row0 + vi, col0 + vj] = true;
                    }
            }

            if (!anySolid)
                return null;

            float minHeight = float.PositiveInfinity;
            float maxHeight = float.NegativeInfinity;
            for (int i = 0; i < sourceResolution; i++)
                for (int j = 0; j < sourceResolution; j++)
                {
                    minHeight = Mathf.Min(minHeight, source[i, j]);
                    maxHeight = Mathf.Max(maxHeight, source[i, j]);
                }
            float heightRange = Mathf.Max(0.01f, maxHeight - minHeight);
            int heightResolution = UnityHeightResolution(sourceResolution);
            int alphaResolution = UnityAlphaResolution(Math.Max(cell, 16));
            float worldSize = areasPerSide * cell * terrain.GridSize;
            var data = new TerrainData { name = terrain.Subs[sector].Name };
            data.heightmapResolution = heightResolution;
            data.size = new Vector3(worldSize, heightRange, worldSize);
            var normalized = new float[heightResolution, heightResolution];
            for (int z = 0; z < heightResolution; z++)
                for (int x = 0; x < heightResolution; x++)
                {
                    float sourceColumn = x * (sourceResolution - 1f) / (heightResolution - 1f);
                    float sourceRow = (sourceResolution - 1f) - z * (sourceResolution - 1f) / (heightResolution - 1f);
                    normalized[z, x] = Mathf.Clamp01((SampleGrid(source, sourceRow, sourceColumn) - minHeight) / heightRange);
                }
            data.SetHeights(0, 0, normalized);

            var layerAssets = BuildTerrainLayers(terrain, sector, masks, textures, folder, result.Warnings);
            data.terrainLayers = layerAssets;
            data.alphamapResolution = alphaResolution;
            if (layerAssets.Length > 0)
                data.SetAlphamaps(0, 0, BuildAlphamaps(terrain, sector, masks, alphaResolution, layerAssets.Length));
            var holes = new bool[heightResolution - 1, heightResolution - 1];
            for (int z = 0; z < holes.GetLength(0); z++)
                for (int x = 0; x < holes.GetLength(1); x++)
                {
                    float sourceColumn = x * (sourceResolution - 1f) / Math.Max(1, holes.GetLength(1));
                    float sourceRow = (sourceResolution - 1f) - (z + 1f) * (sourceResolution - 1f) / Math.Max(1, holes.GetLength(0));
                    int row = Mathf.Clamp(Mathf.FloorToInt(sourceRow), 0, solid.GetLength(0) - 1);
                    int column = Mathf.Clamp(Mathf.FloorToInt(sourceColumn), 0, solid.GetLength(1) - 1);
                    holes[z, x] = solid[row, column];
                }
            data.SetHoles(0, 0, holes);

            string path = folder + "/" + terrain.Subs[sector].Name + "_Terrain.asset";
            data = SaveTerrain(data, path);
            var sectorObject = new GameObject(terrain.Subs[sector].Name);
            var unityTerrain = sectorObject.AddComponent<Terrain>();
            unityTerrain.terrainData = data;
            unityTerrain.materialTemplate = EnsureUnityTerrainMaterial(folder, result.Warnings);
            unityTerrain.shadowCastingMode = ShadowCastingMode.Off;
            unityTerrain.allowAutoConnect = false;
            float minX = -terrain.Width * 0.5f + sectorColumn * worldSize;
            float minZ = terrain.Length * 0.5f - (sectorRow + 1) * worldSize;
            sectorObject.transform.position = new Vector3(minX, minHeight, minZ);
            result.Vertices += (long)heightResolution * heightResolution;
            return sectorObject;
        }

        private static Material EnsureUnityTerrainMaterial(string folder, List<string> warnings)
        {
            var shaderName = AngelicaRenderPipeline.IsHdrp ? HdrpUnityTerrainShaderName : UrpUnityTerrainShaderName;
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                warnings.Add("Terrain shader '" + shaderName +
                    "' was not found. Install the active render pipeline terrain shader; the old Angelica Terrain shader is for meshes and does not drive a Terrain component.");
                return null;
            }

            var path = folder + "/TerrainLit.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null && existing.shader == shader)
                return existing;
            var material = new Material(shader) { name = "TerrainLit" };
            return Save(material, path);
        }

        private static int UnityHeightResolution(int samples)
        {
            if (samples < 2)
                return 33;
            int resolution = 32;
            while (resolution + 1 < samples && resolution < 4096)
                resolution *= 2;
            return Mathf.Clamp(resolution + 1, 33, 4097);
        }

        private static int UnityAlphaResolution(int samples)
        {
            int resolution = 16;
            while (resolution < samples && resolution < 2048)
                resolution *= 2;
            return Mathf.Clamp(resolution, 16, 2048);
        }

        private static float SampleGrid(float[,] source, float row, float column)
        {
            int rows = source.GetLength(0);
            int columns = source.GetLength(1);
            row = Mathf.Clamp(row, 0f, rows - 1f);
            column = Mathf.Clamp(column, 0f, columns - 1f);
            int row0 = Mathf.FloorToInt(row);
            int column0 = Mathf.FloorToInt(column);
            int row1 = Mathf.Min(row0 + 1, rows - 1);
            int column1 = Mathf.Min(column0 + 1, columns - 1);
            float rowT = row - row0;
            float columnT = column - column0;
            float a = Mathf.Lerp(source[row0, column0], source[row0, column1], columnT);
            float b = Mathf.Lerp(source[row1, column0], source[row1, column1], columnT);
            return Mathf.Lerp(a, b, rowT);
        }

        private sealed class LayerKey
        {
            public int Texture;
            public float U;
            public float V;
        }

        private static UnityTerrainLayer[] BuildTerrainLayers(
            TerrainDocument terrain, int sector, TerrainMasks masks, Texture2D[] textures, string folder, List<string> warnings)
        {
            var keys = new List<LayerKey>();
            bool warnedAxis = false;
            for (int area = 0; area < masks.Areas.Length; area++)
            {
                var entries = masks.Areas[area] ?? Array.Empty<TerrainAreaLayer>();
                int count = Math.Min(entries.Length, MaxLayers);
                for (int i = 0; i < count; i++)
                {
                    var layer = ResolveLayer(masks, entries[i].LayerIndex);
                    if (layer.Axis != 1 && !warnedAxis)
                    {
                        warnings.Add(terrain.Subs[sector].Name +
                            ": Unity Terrain tiles textures on XZ only. Vertical projection axes are not applied.");
                        warnedAxis = true;
                    }
                    int texture = ClampTextureIndex(layer.TextureIndex, textures.Length);
                    if (keys.Exists(k => k.Texture == texture && Mathf.Approximately(k.U, layer.UScale) && Mathf.Approximately(k.V, layer.VScale)))
                        continue;
                    keys.Add(new LayerKey { Texture = texture, U = layer.UScale, V = layer.VScale });
                }
            }

            if (keys.Count == 0)
                keys.Add(new LayerKey { Texture = 0, U = 1f, V = 1f });
            var assets = new UnityTerrainLayer[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                var asset = new UnityTerrainLayer { name = terrain.Subs[sector].Name + "_Layer_" + i.ToString("D2") };
                if (key.Texture >= 0 && key.Texture < textures.Length)
                    asset.diffuseTexture = textures[key.Texture];
                asset.tileSize = new Vector2(
                    Mathf.Approximately(key.U, 0f) ? 1f : 1f / key.U,
                    Mathf.Approximately(key.V, 0f) ? 1f : 1f / key.V);
                assets[i] = Save(asset, folder + "/" + asset.name + ".terrainlayer");
            }

            return assets;
        }

        private static float[,,] BuildAlphamaps(
            TerrainDocument terrain, int sector, TerrainMasks masks, int resolution, int layerCount)
        {
            var maps = new float[resolution, resolution, layerCount];
            int areasPerSide = Math.Max(1, terrain.AreasPerSubSide);
            int cell = Math.Max(1, terrain.MaskGrid);
            var keys = LayerKeys(terrain, masks);
            for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                {
                    float column = x * (areasPerSide * cell) / (float)resolution;
                    float row = (resolution - 1 - z) * (areasPerSide * cell) / (float)Math.Max(1, resolution - 1);
                    int areaColumn = Mathf.Clamp(Mathf.FloorToInt(column / cell), 0, areasPerSide - 1);
                    int areaRow = Mathf.Clamp(Mathf.FloorToInt(row / cell), 0, areasPerSide - 1);
                    int area = areaRow * areasPerSide + areaColumn;
                    var weights = LayerWeights(masks, area, column - areaColumn * cell, row - areaRow * cell, cell, keys);
                    for (int layer = 0; layer < layerCount && layer < weights.Length; layer++)
                        maps[z, x, layer] = weights[layer];
                }

            return maps;
        }

        private static List<LayerKey> LayerKeys(TerrainDocument terrain, TerrainMasks masks)
        {
            var keys = new List<LayerKey>();
            for (int area = 0; area < masks.Areas.Length; area++)
            {
                var entries = masks.Areas[area] ?? Array.Empty<TerrainAreaLayer>();
                int count = Math.Min(entries.Length, MaxLayers);
                for (int i = 0; i < count; i++)
                {
                    var layer = ResolveLayer(masks, entries[i].LayerIndex);
                    int texture = ClampTextureIndex(layer.TextureIndex, terrain.Textures.Length);
                    if (keys.Exists(k => k.Texture == texture && Mathf.Approximately(k.U, layer.UScale) && Mathf.Approximately(k.V, layer.VScale)))
                        continue;
                    keys.Add(new LayerKey { Texture = texture, U = layer.UScale, V = layer.VScale });
                }
            }

            return keys;
        }

        private static float[] LayerWeights(TerrainMasks masks, int area, float localX, float localZ, int cell, List<LayerKey> keys)
        {
            var weights = new float[Math.Max(1, keys.Count)];
            if (masks.Areas == null || area < 0 || area >= masks.Areas.Length || masks.Areas[area] == null)
            {
                weights[0] = 1f;
                return weights;
            }
            var entries = masks.Areas[area];
            int count = Math.Min(entries.Length, MaxLayers);
            float remaining = 1f;
            for (int i = 0; i < count; i++)
            {
                var source = ResolveLayer(masks, entries[i].LayerIndex);
                int slot = 0;
                for (int k = 0; k < keys.Count; k++)
                    if (keys[k].Texture == source.TextureIndex &&
                        Mathf.Approximately(keys[k].U, source.UScale) && Mathf.Approximately(keys[k].V, source.VScale))
                    {
                        slot = k;
                        break;
                    }
                float mask = i == 0 ? 1f : SampleMask(entries[i].Mask, source.MaskSize, localX, localZ, cell);
                if (i == 0)
                {
                    weights[slot] = remaining;
                    continue;
                }
                float take = remaining * mask;
                for (int w = 0; w < weights.Length; w++)
                    weights[w] *= 1f - mask;
                weights[slot] += take;
                remaining *= 1f - mask;
            }

            return weights;
        }

        private static float SampleMask(byte[] mask, int size, float localX, float localZ, int cell)
        {
            if (mask == null || size <= 0 || mask.Length < size * size)
                return 0f;
            float u = Mathf.Clamp01(localX / Math.Max(1, cell));
            float v = Mathf.Clamp01(localZ / Math.Max(1, cell));
            int x = Mathf.Clamp(Mathf.FloorToInt(u * (size - 1)), 0, size - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(v * (size - 1)), 0, size - 1);
            return mask[y * size + x] / 255f;
        }

        private static TerrainData SaveTerrain(TerrainData data, string path)
        {
            var existing = GameDataAssetBatch.Load(path) as TerrainData;
            if (existing == null)
            {
                GameDataAssetBatch.Create(data, path);
                return data;
            }

            existing.heightmapResolution = data.heightmapResolution;
            existing.size = data.size;
            existing.SetHeights(0, 0, data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution));
            existing.terrainLayers = data.terrainLayers;
            existing.alphamapResolution = data.alphamapResolution;
            existing.SetAlphamaps(0, 0, data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight));
            int holes = data.heightmapResolution - 1;
            existing.SetHoles(0, 0, data.GetHoles(0, 0, holes, holes));
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(data);
            return existing;
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
