using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>Opt-in conversion of imported HDRP area materials to independently editable assets.</summary>
    public static class TerrainDirectAssetMigration
    {
        private const string DirectProperty = "_UseDirectLayers";
        private const string RootPreference = "SilverEcho.ForsakenWorld.GameDataRoot";
        private static readonly HashSet<string> LegacyTextures = new HashSet<string>(StringComparer.Ordinal)
        {
            "_LayerArray", "_NormalArray", "_RoughnessArray", "_LayerParams",
            "_Splat0", "_Splat1", "_Splat2", "_Splat3", "_HeightMap"
        };

        public sealed class Result
        {
            public int Materials;
            public int Sectors;
            public int AlreadyDirect;
            public int Ready;
            public int Migrated;
            public int MasksToExtract;
            public int MasksWritten;
            public readonly List<string> Problems = new List<string>();
        }

        private sealed class Area
        {
            public string Path;
            public Material Material;
            public Vector4[] Mapping;
            public Texture[] Albedos;
            public Texture[] Masks;
            public string[] MaskPaths;
            public int FlipLayers;
            public int SrgbLayers;
        }

        private sealed class SourceSector
        {
            public TerrainDocument Terrain;
            public TerrainMasks Masks;
        }

        internal static bool IsMigrating { get; private set; }

        public static Result Preview(string sourceRoot = null, string terrainFolder = null) =>
            Run(sourceRoot, terrainFolder, false, false);

        public static Result Migrate(string sourceRoot = null, string terrainFolder = null, bool showProgress = false) =>
            Run(sourceRoot, terrainFolder, true, showProgress);

        [MenuItem("Tools/Angelica 2.x/Extract Editable HDRP Terrain")]
        private static void MigrateProject()
        {
            var result = Migrate(showProgress: true);
            foreach (var problem in result.Problems)
                Debug.LogWarning(problem);
            EditorUtility.DisplayDialog("Editable HDRP terrain",
                $"Updated {result.Migrated} area materials and extracted {result.MasksWritten} masks.\n" +
                $"Already editable: {result.AlreadyDirect}. Skipped with issues: {result.Problems.Count}.", "OK");
        }

        private static Result Run(string sourceRoot, string terrainFolder, bool write, bool showProgress)
        {
            if (IsMigrating) throw new InvalidOperationException("HDRP terrain extraction is already running.");
            if (terrainFolder != null && !AssetDatabase.IsValidFolder(terrainFolder))
                throw new DirectoryNotFoundException(terrainFolder);

            var result = new Result();
            var source = new SourceMaps(sourceRoot ?? EditorPrefs.GetString(RootPreference, string.Empty));
            var texturePackages = TerrainTextureBuilder.FindTexturePackageFolders();
            var paths = terrainFolder == null ? AssetDatabase.FindAssets("t:Material", new[] { "Assets" }) :
                AssetDatabase.FindAssets("t:Material", new[] { terrainFolder });
            var sectors = paths.Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                .GroupBy(path => path.Substring(0, path.LastIndexOf('/')), StringComparer.OrdinalIgnoreCase).ToArray();
            var pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var areas = new List<Area>();
            IsMigrating = write;
            try
            {
                for (int sectorIndex = 0; sectorIndex < sectors.Length; sectorIndex++)
                {
                    var sector = sectors[sectorIndex];
                    var materials = sector.Select(path => (Path: path, Material: AssetDatabase.LoadAssetAtPath<Material>(path)))
                        .Where(item => item.Material != null && item.Material.shader != null &&
                            item.Material.shader.name == TerrainAssetImporter.HdrpShaderName).ToArray();
                    if (materials.Length == 0) continue;
                    result.Sectors++;
                    if (showProgress)
                        EditorUtility.DisplayProgressBar("Extract editable HDRP terrain", sector.Key,
                            (float)sectorIndex / sectors.Length);
                    result.Materials += materials.Length;
                    SourceSector sourceSector = null;
                    bool sourceChecked = false;
                    foreach (var item in materials)
                    {
                        if (!item.Material.HasProperty(DirectProperty))
                            throw new InvalidOperationException("The direct HDRP terrain shader has not finished importing.");
                        if (item.Material.GetFloat(DirectProperty) > 0.5f)
                        {
                            result.AlreadyDirect++;
                            continue;
                        }
                        try
                        {
                            if (!sourceChecked)
                            {
                                sourceSector = source.FindSector(sector.Key, result.Problems);
                                sourceChecked = true;
                            }
                            areas.Add(Prepare(item.Path, item.Material, sourceSector, texturePackages, write, pending, result));
                            result.Ready++;
                        }
                        catch (Exception error) when (error is InvalidDataException || error is IOException ||
                            error is InvalidOperationException || error is UnityException)
                        {
                            result.Problems.Add(item.Path + ": " + error.Message);
                        }
                    }
                }
                if (!write || areas.Count == 0) return result;

                if (pending.Count > 0)
                {
                    if (showProgress)
                        EditorUtility.DisplayProgressBar("Extract editable HDRP terrain", "Importing layer masks", 1f);
                    AssetDatabase.StartAssetEditing();
                    try
                    {
                        GameDataNativeImportBatch.Import(pending);
                    }
                    finally
                    {
                        AssetDatabase.StopAssetEditing();
                    }
                }

                if (showProgress)
                    EditorUtility.DisplayProgressBar("Extract editable HDRP terrain", "Saving area materials", 1f);
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var area in areas)
                    {
                        if (BindArea(area, result))
                            result.Migrated++;
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
                return result;
            }
            finally
            {
                IsMigrating = false;
                if (showProgress) EditorUtility.ClearProgressBar();
            }
        }

        private static Area Prepare(string path, Material material, SourceSector source, string[] texturePackages,
            bool write, HashSet<string> pending, Result result)
        {
            TerrainAreaLayer[] entries = null;
            const string areaPrefix = "Area_";
            var name = Path.GetFileNameWithoutExtension(path);
            if (source != null && name.StartsWith(areaPrefix, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(name.Substring(areaPrefix.Length), out int index) &&
                index >= 0 && index < source.Masks.Areas.Length)
                entries = source.Masks.Areas[index];

            int currentCount = Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_LayerCount")), 1, TerrainAssetImporter.MaxLayers);
            int sourceCount = Mathf.RoundToInt(material.GetFloat("_SourceLayerCount"));
            sourceCount = Mathf.Clamp(sourceCount > 0 ? sourceCount : entries?.Length ?? currentCount,
                1, TerrainAssetImporter.MaxLayers);
            var area = new Area
            {
                Path = path,
                Material = material,
                Mapping = new Vector4[sourceCount],
                Albedos = new Texture[sourceCount],
                Masks = new Texture[sourceCount],
                MaskPaths = new string[sourceCount],
                FlipLayers = Mathf.RoundToInt(material.GetFloat("_FlipLayers")),
                SrgbLayers = Mathf.RoundToInt(material.GetFloat("_SrgbLayers"))
            };
            var parameters = material.GetTexture("_LayerParams") as Texture2D;
            var packedPixels = new Dictionary<Texture2D, Color32[]>();
            for (int layer = 0; layer < sourceCount; layer++)
            {
                var entry = entries != null && layer < entries.Length ? entries[layer] : null;
                var native = entry == null ? null : TerrainAssetImporter.ResolveLayer(source.Masks, entry.LayerIndex);
                if (parameters != null && parameters.isReadable && layer < parameters.width)
                {
                    var pixel = parameters.GetPixel(layer, 0);
                    area.Mapping[layer] = new Vector4(pixel.g, pixel.b, pixel.a, 0f);
                }
                else if (native != null)
                    area.Mapping[layer] = new Vector4(native.UScale, native.VScale, native.Axis, 0f);
                else
                    area.Mapping[layer] = material.GetVector("_LayerMapping" + layer);

                var albedo = material.GetTexture("_Layer" + layer);
                var albedoPath = AssetDatabase.GetAssetPath(albedo);
                if (albedo == null || albedoPath.IndexOf("/GroundTextures/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string gamePath = native == null || native.TextureIndex < 0 || native.TextureIndex >= source.Terrain.Textures.Length
                        ? null : source.Terrain.Textures[native.TextureIndex];
                    var file = TerrainTextureBuilder.FindPackedTexture(gamePath, texturePackages) ??
                        TerrainTextureBuilder.FindProjectTexture(gamePath);
                    var linked = file == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainTextureBuilder.GetProjectAssetPath(file));
                    if (linked == null)
                        throw new InvalidDataException($"No directly importable source texture for layer {layer} ({gamePath ?? albedoPath}).");
                    albedo = linked;
                    int bit = 1 << layer;
                    area.FlipLayers = !Path.GetExtension(file.FullPath).Equals(".dds", StringComparison.OrdinalIgnoreCase)
                        ? area.FlipLayers | bit : area.FlipLayers & ~bit;
                    area.SrgbLayers = GraphicsFormatUtility.IsSRGBFormat(linked.graphicsFormat)
                        ? area.SrgbLayers | bit : area.SrgbLayers & ~bit;
                }
                area.Albedos[layer] = albedo;

                if (layer == 0) continue;
                var mask = material.GetTexture(TerrainMaskImages.MaskProperty(layer));
                if (mask != null)
                {
                    area.Masks[layer] = mask;
                    continue;
                }
                var maskPath = TerrainMaskImages.MaskAssetPath(path, layer);
                area.MaskPaths[layer] = maskPath;
                if (File.Exists(AngelicaAssetFolders.AssetPathToAbsolute(maskPath)))
                {
                    if (write && AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath) == null)
                        pending.Add(maskPath);
                    continue;
                }

                var splat = material.GetTexture("_Splat" + TerrainMaskImages.SplatIndex(layer)) as Texture2D;
                var pixels = ExtractMask(entry, native, splat, TerrainMaskImages.Channel(layer), packedPixels,
                    out int width, out int height);
                if (write)
                {
                    TerrainMaskImages.WriteGrayPngFile(maskPath, pixels, width, height);
                    pending.Add(maskPath);
                    result.MasksWritten++;
                }
                result.MasksToExtract++;
            }
            return area;
        }

        private static byte[] ExtractMask(TerrainAreaLayer entry, TerrainLayer native, Texture2D splat, int channel,
            Dictionary<Texture2D, Color32[]> packedPixels, out int width, out int height)
        {
            int side = native?.MaskSize ?? 0;
            var original = side > 0 && entry.Mask != null && (long)side * side == entry.Mask.Length ? entry.Mask : null;
            if (splat != null && splat.isReadable)
            {
                if (!packedPixels.TryGetValue(splat, out var pixels))
                    packedPixels.Add(splat, pixels = splat.GetPixels32());
                bool changed = original == null;
                if (!changed)
                {
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        int x = i % splat.width * side / splat.width;
                        int y = i / splat.width * side / splat.height;
                        if (TerrainMaskImages.ReadChannel(pixels[i], channel) == original[y * side + x]) continue;
                        changed = true;
                        break;
                    }
                }
                if (changed)
                {
                    width = splat.width;
                    height = splat.height;
                    var gray = new byte[pixels.Length];
                    for (int i = 0; i < pixels.Length; i++)
                        gray[i] = TerrainMaskImages.ReadChannel(pixels[i], channel);
                    return gray;
                }
            }
            if (original == null)
                throw new InvalidDataException("Neither the source mask nor the existing packed mask was readable.");
            width = height = side;
            return original;
        }

        private static bool BindArea(Area area, Result result)
        {
            for (int layer = 1; layer < area.Masks.Length; layer++)
            {
                if (area.Masks[layer] != null) continue;
                area.Masks[layer] = AssetDatabase.LoadAssetAtPath<Texture2D>(area.MaskPaths[layer]);
                if (area.Masks[layer] != null) continue;
                result.Problems.Add(area.Path + ": could not load " + area.MaskPaths[layer]);
                return false;
            }

            var material = area.Material;
            for (int layer = 0; layer < area.Mapping.Length; layer++)
            {
                material.SetVector("_LayerMapping" + layer, area.Mapping[layer]);
                material.SetTexture("_Layer" + layer, area.Albedos[layer]);
                material.SetFloat("_HasAlbedo" + layer, 1f);
                if (layer > 0) material.SetTexture(TerrainMaskImages.MaskProperty(layer), area.Masks[layer]);
            }
            int activeLayers = Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_LayerCount")), 1,
                TerrainAssetImporter.MaxLayers);
            for (int layer = area.Mapping.Length; layer < activeLayers; layer++)
            {
                material.SetFloat("_HasAlbedo" + layer, material.GetTexture("_Layer" + layer) != null ? 1f : 0f);
                if (material.GetTexture(TerrainMaskImages.MaskProperty(layer)) == null) continue;
                string property = "_LayerMapping" + layer;
                var mapping = material.GetVector(property);
                mapping.w = 1f;
                material.SetVector(property, mapping);
            }
            material.SetFloat("_SourceLayerCount", area.Mapping.Length);
            material.SetFloat("_FlipLayers", area.FlipLayers);
            material.SetFloat("_SrgbLayers", area.SrgbLayers);
            material.SetFloat(DirectProperty, 1f);
            AngelicaTerrainLayerBindings.Apply(material);

            // Remove persistent object references, including legacy properties removed from the shader.
            using (var serialized = new SerializedObject(material))
            {
                var textures = serialized.FindProperty("m_SavedProperties.m_TexEnvs");
                if (textures != null)
                {
                    for (int i = textures.arraySize - 1; i >= 0; i--)
                        if (LegacyTextures.Contains(textures.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue))
                            textures.DeleteArrayElementAtIndex(i);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            TerrainDetailTextureCache.Queue(material);
            return true;
        }

        private sealed class SourceMaps
        {
            private readonly string root;
            private readonly Dictionary<string, string> maps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, TerrainDocument> terrains = new Dictionary<string, TerrainDocument>(StringComparer.OrdinalIgnoreCase);
            private TerrainParser parser;

            internal SourceMaps(string root)
            {
                this.root = root;
                if (string.IsNullOrWhiteSpace(root)) return;
                var mapRoot = Path.Combine(root, "maps");
                if (!Directory.Exists(mapRoot)) return;
                foreach (var path in Directory.EnumerateFiles(mapRoot, "*.trn2", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(this.root, path);
                    var outputName = Path.GetFileName(MapAssetImporter.GetOutputFolder(relative));
                    maps[outputName] = path;
                }
            }

            internal SourceSector FindSector(string folder, List<string> problems)
            {
                var parent = Path.GetDirectoryName(folder);
                if (!string.Equals(Path.GetFileName(parent), "terrains", StringComparison.OrdinalIgnoreCase) ||
                    !maps.TryGetValue(Path.GetFileName(Path.GetDirectoryName(parent)), out var path)) return null;
                var sector = Path.GetFileName(folder);
                var maskPath = Path.Combine(Path.GetDirectoryName(path), sector + ".t2mk");
                if (!File.Exists(maskPath)) return null;
                try
                {
                    parser ??= new TerrainParser(GameDataBrowserWindow.GetGameDataDecoder());
                    if (!terrains.TryGetValue(path, out var terrain))
                        terrains.Add(path, terrain = parser.Parse(File.ReadAllBytes(path), path));
                    return new SourceSector { Terrain = terrain, Masks = parser.ReadMasks(File.ReadAllBytes(maskPath), maskPath) };
                }
                catch (Exception error) when (error is InvalidDataException || error is IOException ||
                    error is InvalidOperationException || error is NotSupportedException)
                {
                    problems.Add(maskPath + ": " + error.Message);
                    return null;
                }
            }
        }
    }
}
