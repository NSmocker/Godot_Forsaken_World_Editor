using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
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

        private readonly struct GroundTextureBinding
        {
            internal readonly Texture2D Texture;
            internal readonly bool FlipV;
            internal readonly bool IsSrgb;

            internal GroundTextureBinding(Texture2D texture, bool flipV = false, bool isSrgb = false)
            {
                Texture = texture;
                FlipV = flipV;
                IsSrgb = isSrgb;
            }
        }

        /// <summary>
        /// Generated terrain data lives beside grass/effects/sectors:
        /// Assets/ForsakenWorld/Imported/Maps/&lt;map&gt;/terrains/&lt;sector&gt;.
        /// Ground DDS samples are shared across the map as linear, file-oriented textures.
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
                var texturePackages = TerrainTextureBuilder.FindTexturePackageFolders();
                var textures = new GroundTextureBinding[t.Textures.Length];
                var resolved = new bool[textures.Length];
                var shader = AngelicaRenderPipeline.Find(HdrpShaderName, ShaderName);
                if (shader == null)
                {
                    result.Warnings.Add("Terrain shader '" + ShaderName + "' was not found.");
                    shader = Shader.Find("Hidden/InternalErrorShader");
                }
                GroundTextureBinding GroundTexture(int textureIndex)
                {
                    if (textureIndex < 0 || textureIndex >= textures.Length)
                    {
                        result.Warnings.Add("Invalid ground texture index: " + textureIndex);
                        return default;
                    }
                    if (!resolved[textureIndex])
                    {
                        textures[textureIndex] = ImportGroundTexture(t.Textures[textureIndex], textureIndex,
                            file, index, texturePackages, folder, shader != null && shader.name == HdrpShaderName,
                            result.Warnings);
                        resolved[textureIndex] = true;
                    }
                    return textures[textureIndex];
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

                    var day = ReadLightmaps(parser, index, relativeDir, t.Subs[s].Name, ".t2lm", result.Warnings);
                    var night = ReadLightmaps(parser, index, relativeDir, t.Subs[s].Name, ".t2lm1", result.Warnings,
                        Path.ChangeExtension(blockFile.FullPath, ".t2lm1"));
                    var sectorObject = BuildSector(t, s, blocks, masks, day, night, GroundTexture, shader, file.RelativePath, result);
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

        private static GroundTextureBinding ImportGroundTexture(string name, int textureIndex, GameDataFile map,
            GameDataIndex index, string[] texturePackages, string folder, bool direct, List<string> warnings)
        {
            var packed = TerrainTextureBuilder.FindPackedTexture(name, texturePackages);
            var project = packed ?? TerrainTextureBuilder.FindProjectTexture(name);
            if (project != null && (packed != null || direct))
            {
                var linked = AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainTextureBuilder.GetProjectAssetPath(project));
                if (linked != null)
                {
                    // Unity's native DDS importer keeps file row zero at V=0.
                    // TextureImporter images use Unity's bottom-up convention.
                    bool flipV = !Path.GetExtension(project.FullPath).Equals(".dds", StringComparison.OrdinalIgnoreCase);
                    return new GroundTextureBinding(linked, flipV, GraphicsFormatUtility.IsSRGBFormat(linked.graphicsFormat));
                }
            }

            var source = project ?? ResolveTexture(index, map.RelativePath, name);
            if (source == null)
            {
                warnings.Add("Ground texture not found: " + name);
                return default;
            }

            if (direct)
            {
                DdsDocument header = null;
                try
                {
                    if (TextureFileFormat.DetectFile(source.FullPath) == ".dds")
                        header = new DdsHeaderParser().ParseFile(source.FullPath);
                    var imported = AssembledTextureImporter.ImportOrUpdate(source.FullPath, source.RelativePath, header);
                    if (imported.Texture != null)
                        return new GroundTextureBinding(imported.Texture, !imported.UsedNativeDds,
                            GraphicsFormatUtility.IsSRGBFormat(imported.Texture.graphicsFormat));
                    warnings.Add("Ground texture could not be loaded: " + name + " (" + imported.Error + ")");
                }
                catch (Exception e) when (e is InvalidDataException || e is IOException || e is NotSupportedException)
                {
                    warnings.Add("Ground texture could not be loaded: " + name + " (" + e.Message + ")");
                }
                return default;
            }

            Texture2D texture = null;
            try
            {
                texture = TerrainTextureBuilder.LoadGroundTexture(source, project != null);
                string directory = folder + "/GroundTextures/" + textureIndex.ToString("D4");
                EnsureFolder(directory);
                var saved = Save(texture, directory + "/" + Sanitize(Path.GetFileNameWithoutExtension(name)) + ".asset");
                texture = null;
                return new GroundTextureBinding(saved);
            }
            catch (Exception e) when (e is InvalidDataException || e is IOException || e is NotSupportedException)
            {
                warnings.Add("Ground texture could not be loaded: " + name + " (" + e.Message + ")");
                return default;
            }
            finally
            {
                if (texture != null) Object.DestroyImmediate(texture);
            }
        }

        private static TerrainLightmap[] ReadLightmaps(TerrainParser parser, GameDataIndex index,
            string directory, string sector, string extension, List<string> warnings, string siblingPath = null)
        {
            string relativePath = Join(directory, sector + extension);
            // Cached indexes created before T2LM1 support can omit a valid night map.
            string path = index.TryResolve(relativePath, out var file, out _) ? file.FullPath : siblingPath;
            if (!File.Exists(path))
            {
                if (extension == ".t2lm")
                    warnings.Add(sector + ": no daytime lightmap, terrain is unlit.");
                return null;
            }

            try { return parser.ReadLightmaps(File.ReadAllBytes(path), relativePath); }
            catch (Exception e) when (e is InvalidDataException || e is IOException)
            {
                warnings.Add(sector + extension + ": lightmap was not applied (" + e.Message + ")");
                return null;
            }
        }

        private static GameObject BuildSector(
            TerrainDocument terrain, int sector, Dictionary<long, TerrainBlock> blocks, TerrainMasks masks,
            TerrainLightmap[] day, TerrainLightmap[] night, Func<int, GroundTextureBinding> groundTexture, Shader shader,
            string mapRelativePath, Result result)
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
                var dayMap = day != null && area < day.Length ? day[area] : null;
                var nightMap = night != null && area < night.Length ? night[area] : null;
                var material = BuildMaterial(terrain, masks, area, sector, groundTexture, shader, prefix, data,
                    dayMap, nightMap, result.Warnings);
                var areaObject = new GameObject("Area_" + area.ToString("D4"));
                areaObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = areaObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
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
            Func<int, GroundTextureBinding> groundTexture, Shader shader, string prefix, TerrainMeshData mesh,
            TerrainLightmap dayMap, TerrainLightmap nightMap, List<string> warnings)
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

            string materialPath = prefix + ".mat";
            var previousMaterial = GameDataAssetBatch.Load(materialPath) as Material;
            bool direct = shader.name == HdrpShaderName && shader.FindPropertyIndex(TerrainMaskImages.DirectLayersProperty) >= 0;
            if (direct && TerrainMaskImages.UsesDirectLayers(previousMaterial))
            {
                AngelicaTerrainLayerBindings.Apply(previousMaterial);
                TerrainDetailTextureCache.Queue(previousMaterial);
                return previousMaterial;
            }

            Texture2D parameters = null;
            Texture2D height = null;
            var splats = new Texture2D[4];
            Material mat = null;
            try
            {
                if (!direct)
                {
                    parameters = new Texture2D(MaxLayers, 1, TextureFormat.RGBAFloat, false, true)
                    {
                        name = "Parameters",
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Point
                    };
                    for (int i = 0; i < entries.Length; i++)
                    {
                        var layer = ResolveLayer(masks, entries[i].LayerIndex);
                        parameters.SetPixel(i, 0, new Color(i, layer.UScale, layer.VScale, layer.Axis));
                    }
                    parameters.Apply(false, false);
                    height = BuildHeightMap(mesh, t.MaskGrid);
                    PackSplats(masks, entries, splats);
                    TerrainMaskImages.Publish(prefix, masks, entries, splats);
                }
                int previousSourceCount = previousMaterial != null && previousMaterial.HasProperty("_SourceLayerCount")
                    ? Mathf.Clamp(Mathf.RoundToInt(previousMaterial.GetFloat("_SourceLayerCount")), 0, MaxLayers)
                    : 0;
                int previousLayerCount = previousMaterial != null && previousMaterial.HasProperty("_LayerCount")
                    ? Mathf.Clamp(Mathf.RoundToInt(previousMaterial.GetFloat("_LayerCount")), 1, MaxLayers)
                    : entries.Length;
                bool hasLayerCountOverride = previousSourceCount > 0 && previousLayerCount != previousSourceCount;
                int layerCount = hasLayerCountOverride ? previousLayerCount : entries.Length;
                mat = new Material(shader) { name = Path.GetFileName(prefix) };
                if (direct)
                {
                    mat.SetFloat(TerrainMaskImages.DirectLayersProperty, 1f);
                    for (int i = 1; i < entries.Length; i++)
                    {
                        string property = TerrainMaskImages.MaskProperty(i);
                        var mask = previousMaterial != null && previousMaterial.HasProperty(property)
                            ? previousMaterial.GetTexture(property) : null;
                        mat.SetTexture(property, mask != null ? mask : TerrainMaskImages.ImportDirectMask(prefix, masks, entries, i));
                    }
                }
                // Shader slots are local to this mask area. The TRN2 table can have
                // hundreds of entries, and one texture can be projected on several axes.
                int flipLayers = 0;
                int srgbLayers = 0;
                for (int i = 0; i < entries.Length; i++)
                {
                    var layer = ResolveLayer(masks, entries[i].LayerIndex);
                    int textureIndex = layer.TextureIndex;
                    var binding = groundTexture(textureIndex);
                    mat.SetVector("_LayerMapping" + i, new Vector4(layer.UScale, layer.VScale, layer.Axis, 0f));
                    mat.SetFloat("_HasAlbedo" + i, binding.Texture != null ? 1f : 0f);
                    if (binding.Texture == null) continue;
                    mat.SetTexture("_Layer" + i, binding.Texture);
                    if (binding.FlipV) flipLayers |= 1 << i;
                    if (binding.IsSrgb) srgbLayers |= 1 << i;
                }
                mat.SetFloat("_FlipLayers", flipLayers);
                mat.SetFloat("_SrgbLayers", srgbLayers);
                for (int i = 0; i < splats.Length; i++)
                {
                    if (splats[i] == null) continue;
                    mat.SetTexture("_Splat" + i, Save(splats[i], prefix + "_Splat" + i + ".asset"));
                    splats[i] = null;
                }
                if (!direct)
                {
                    mat.SetTexture("_LayerParams", Save(parameters, prefix + "_Parameters.asset")); parameters = null;
                    mat.SetTexture("_HeightMap", Save(height, prefix + "_Height.asset")); height = null;
                }
                SaveLightmap(mat, dayMap, "_Lightmap", "_UseLightmap", prefix, warnings);
                SaveLightmap(mat, nightMap, "_NightLightmap", "_UseNightLightmap", prefix, warnings);
                mat.SetFloat("_LayerCount", layerCount);
                mat.SetFloat("_SourceLayerCount", entries.Length);
                if (previousMaterial != null)
                {
                    int preserveSourceSlots = Math.Min(entries.Length,
                        previousSourceCount > 0 ? previousSourceCount : entries.Length);
                    for (int i = 0; i < preserveSourceSlots; i++)
                        CopyLayerMaterialSettings(previousMaterial, mat, i, i, false);

                    int previousExtraCount = previousSourceCount > 0
                        ? Math.Max(0, previousLayerCount - previousSourceCount)
                        : 0;
                    int extraCount = Math.Min(Math.Max(0, layerCount - entries.Length), previousExtraCount);
                    for (int i = 0; i < extraCount; i++)
                        CopyLayerMaterialSettings(previousMaterial, mat, previousSourceCount + i, entries.Length + i, true);
                }
                mat.SetFloat("_MinHeight", t.Subs[sub].MinHeight);
                mat.SetFloat("_Brightness", 1f);
                if (direct) AngelicaTerrainLayerBindings.Apply(mat);
                var saved = Save(mat, materialPath);
                if (direct) TerrainDetailTextureCache.Queue(saved);
                mat = null;
                return saved;
            }
            finally
            {
                if (mat != null) Object.DestroyImmediate(mat);
                if (parameters != null) Object.DestroyImmediate(parameters);
                if (height != null) Object.DestroyImmediate(height);
                for (int i = 0; i < splats.Length; i++)
                    if (splats[i] != null) Object.DestroyImmediate(splats[i]);
            }
        }

        private static void CopyLayerMaterialSettings(Material source, Material destination, int sourceIndex,
            int destinationIndex, bool copyAlbedo)
        {
            string sourceSuffix = sourceIndex.ToString();
            string destinationSuffix = destinationIndex.ToString();
            if (copyAlbedo)
            {
                CopyTexture(source, destination, "_Layer" + sourceSuffix, "_Layer" + destinationSuffix);
                CopyFloat(source, destination, "_HasAlbedo" + sourceSuffix, "_HasAlbedo" + destinationSuffix);
            }
            CopyTexture(source, destination, "_Normal" + sourceSuffix, "_Normal" + destinationSuffix);
            CopyTexture(source, destination, "_Roughness" + sourceSuffix, "_Roughness" + destinationSuffix);
            CopyFloat(source, destination, "_HasNormal" + sourceSuffix, "_HasNormal" + destinationSuffix);
            CopyFloat(source, destination, "_HasRoughness" + sourceSuffix, "_HasRoughness" + destinationSuffix);
            CopyFloat(source, destination, "_NormalStrength" + sourceSuffix, "_NormalStrength" + destinationSuffix);
            CopyFloat(source, destination, "_RoughnessStrength" + sourceSuffix, "_RoughnessStrength" + destinationSuffix);
            CopyTexture(source, destination, "_ParallaxHeight" + sourceSuffix, "_ParallaxHeight" + destinationSuffix);
            CopyFloat(source, destination, "_UseParallax" + sourceSuffix, "_UseParallax" + destinationSuffix);
            CopyFloat(source, destination, "_ParallaxDepth" + sourceSuffix, "_ParallaxDepth" + destinationSuffix);
            CopyFloat(source, destination, "_ParallaxSteps" + sourceSuffix, "_ParallaxSteps" + destinationSuffix);
            CopyFloat(source, destination, "_ParallaxFadeMip" + sourceSuffix, "_ParallaxFadeMip" + destinationSuffix);
            if (copyAlbedo && source.HasProperty("_LayerMapping" + sourceSuffix) &&
                destination.HasProperty("_LayerMapping" + destinationSuffix))
                destination.SetVector("_LayerMapping" + destinationSuffix,
                    source.GetVector("_LayerMapping" + sourceSuffix));
        }

        private static void CopyTexture(Material source, Material destination, string from, string to)
        {
            if (source.HasProperty(from) && destination.HasProperty(to))
                destination.SetTexture(to, source.GetTexture(from));
        }

        private static void CopyFloat(Material source, Material destination, string from, string to)
        {
            if (source.HasProperty(from) && destination.HasProperty(to))
                destination.SetFloat(to, source.GetFloat(from));
        }

        private static void SaveLightmap(Material material, TerrainLightmap source, string property,
            string enabledProperty, string prefix, List<string> warnings)
        {
            if (source == null) return;
            Texture2D texture = null;
            try
            {
                texture = TerrainTextureBuilder.Lightmap(source, prefix + property);
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                var saved = Save(texture, prefix + property + ".asset");
                texture = null;
                material.SetTexture(property, saved);
                material.SetFloat(enabledProperty, 1f);
            }
            catch (Exception e) when (e is InvalidDataException || e is IOException || e is NotSupportedException)
            {
                warnings.Add(prefix + property + ": lightmap was not applied (" + e.Message + ")");
            }
            finally
            {
                if (texture != null) Object.DestroyImmediate(texture);
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

        // Unity packs four overlays per texture. Loader B uses an opaque base
        // and creates the mask textures with only one mip level.
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
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
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
                texture.Apply(false, false);
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
