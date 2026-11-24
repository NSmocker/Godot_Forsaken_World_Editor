using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Overlay masks are independent grayscale PNGs. White is full coverage, black is none.
    /// Legacy materials use packed channels in A, R, G, B order.
    /// </summary>
    public static class TerrainMaskImages
    {
        public const string FileSuffix = "_Mask.png";
        internal const string DirectLayersProperty = "_UseDirectLayers";

        internal static bool UsesDirectLayers(Material material) => material != null &&
            material.HasProperty(DirectLayersProperty) && material.GetFloat(DirectLayersProperty) > .5f;

        public static int SplatIndex(int layer) => (layer - 1) / 4;

        public static int Channel(int layer) => (layer - 1) % 4;

        public static bool IsMaskPath(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                   assetPath.Replace('\\', '/').EndsWith(FileSuffix, StringComparison.OrdinalIgnoreCase) &&
                   LayerIndex(assetPath) > 0;
        }

        public static int LayerIndex(string assetPath)
        {
            var name = Path.GetFileNameWithoutExtension(assetPath);
            const string token = "_Layer";
            int at = name.LastIndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (at < 0 || at + token.Length + 2 > name.Length) return -1;
            return int.TryParse(name.Substring(at + token.Length, 2), out int layer) ? layer : -1;
        }

        public static string MaterialPath(string maskAssetPath)
        {
            var slash = maskAssetPath.Replace('\\', '/');
            var name = Path.GetFileNameWithoutExtension(slash);
            int at = name.LastIndexOf("_Layer", StringComparison.OrdinalIgnoreCase);
            if (at < 0) return null;
            var directory = Path.GetDirectoryName(slash)?.Replace('\\', '/');
            return string.IsNullOrEmpty(directory) ? name.Substring(0, at) + ".mat" : directory + "/" + name.Substring(0, at) + ".mat";
        }

        public static string MaskAssetPath(string materialOrPrefixPath, int layer)
        {
            var slash = materialOrPrefixPath.Replace('\\', '/');
            if (slash.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                slash = slash.Substring(0, slash.Length - 4);
            var directory = Path.GetDirectoryName(slash)?.Replace('\\', '/');
            var name = Path.GetFileName(slash);
            var file = name + "_Layer" + layer.ToString("00") + FileSuffix;
            return string.IsNullOrEmpty(directory) ? file : directory + "/" + file;
        }

        public static byte ReadChannel(Color32 pixel, int channel)
        {
            if (channel == 0) return pixel.a;
            if (channel == 1) return pixel.r;
            if (channel == 2) return pixel.g;
            return pixel.b;
        }

        public static void WriteChannel(ref Color32 pixel, int channel, byte value)
        {
            if (channel == 0) pixel.a = value;
            else if (channel == 1) pixel.r = value;
            else if (channel == 2) pixel.g = value;
            else pixel.b = value;

        }

        /// <summary>
        /// Writes a PNG for every overlay that does not already have one, then copies existing
        /// PNGs into the splat textures. An edited PNG therefore survives a later terrain import.
        /// </summary>
        internal static void Publish(string prefix, TerrainMasks masks, TerrainAreaLayer[] entries, Texture2D[] splats)
        {
            if (entries == null) return;
            int count = Math.Min(entries.Length, TerrainAssetImporter.MaxLayers);
            for (int layer = 1; layer < count; layer++)
            {
                var assetPath = MaskAssetPath(prefix, layer);
                var absolute = AssetPathToAbsolute(assetPath);
                if (!File.Exists(absolute))
                {
                    WritePng(absolute, MaskBytes(masks, entries, layer));
                    AssetDatabase.ImportAsset(assetPath);
                }
                else if (TryReadPng(absolute, out var pixels, out int width, out int height))
                    ApplyToSplats(splats, layer, pixels, width, height);
            }
        }

        internal static Texture2D ImportDirectMask(string prefix, TerrainMasks masks, TerrainAreaLayer[] entries, int layer)
        {
            var path = MaskAssetPath(prefix, layer);
            var absolute = AssetPathToAbsolute(path);
            if (!File.Exists(absolute))
                WritePng(absolute, MaskBytes(masks, entries, layer));
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            if (texture == null)
                throw new IOException("Could not import terrain layer mask: " + path);
            return texture;
        }

        internal static int ExportMaterial(Material material, bool overwrite)
        {
            if (material == null || !IsTerrainShader(material.shader)) return 0;
            if (UsesDirectLayers(material)) return 0;
            var materialPath = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrEmpty(materialPath)) return 0;
            int layers = Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_LayerCount")), 1, TerrainAssetImporter.MaxLayers);
            int written = 0;
            for (int layer = 1; layer < layers; layer++)
            {
                var splat = material.GetTexture("_Splat" + SplatIndex(layer)) as Texture2D;
                if (splat == null) continue;
                var path = MaskAssetPath(materialPath, layer);
                var absolute = AssetPathToAbsolute(path);
                if (File.Exists(absolute) && !overwrite) continue;
                if (!splat.isReadable)
                    throw new InvalidOperationException(splat.name + " is not readable, so its mask cannot be exported.");
                var source = splat.GetPixels32();
                int channel = Channel(layer);
                var gray = new byte[splat.width * splat.height];
                for (int i = 0; i < gray.Length; i++)
                    gray[i] = ReadChannel(source[i], channel);
                WritePng(absolute, gray, splat.width, splat.height);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                written++;
            }
            BindExisting(material);
            return written;
        }

        public static string MaskProperty(int layer) => "_LayerMask" + layer;

        /// <summary>Assigns already exported PNGs to the material slots the inspector shows.</summary>
        public static int BindExisting(Material material)
        {
            if (material == null || !IsTerrainShader(material.shader)) return 0;
            if (!material.HasProperty(MaskProperty(1))) return 0;
            var materialPath = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrEmpty(materialPath)) return 0;
            int layers = Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_LayerCount")), 1, TerrainAssetImporter.MaxLayers);
            int bound = 0;
            for (int layer = 1; layer < layers; layer++)
            {
                var property = MaskProperty(layer);
                if (material.GetTexture(property) != null) continue;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(MaskAssetPath(materialPath, layer));
                if (texture == null) continue;
                material.SetTexture(property, texture);
                bound++;
            }
            if (bound > 0)
                EditorUtility.SetDirty(material);
            return bound;
        }

        public static void ApplyAssignedMask(Material material, int layer)
        {
            if (material == null || layer <= 0 || !material.HasProperty(MaskProperty(layer))) return;
            if (UsesDirectLayers(material))
            {
                string mappingProperty = "_LayerMapping" + layer;
                var mapping = material.GetVector(mappingProperty);
                mapping.w = material.GetTexture(MaskProperty(layer)) != null ? 1f : 0f;
                material.SetVector(mappingProperty, mapping);
                EditorUtility.SetDirty(material);
                return;
            }
            var assigned = material.GetTexture(MaskProperty(layer)) as Texture2D;
            int splatIndex = SplatIndex(layer);
            var splat = material.GetTexture("_Splat" + splatIndex) as Texture2D;
            if (assigned == null)
            {
                if (splat == null || !splat.isReadable) return;
                ClearChannel(splat, Channel(layer));
                EditorUtility.SetDirty(splat);
                return;
            }
            var read = TerrainTextureBuilder.Readback(assigned, assigned.width, assigned.height, false);
            try
            {
                if (splat == null)
                    splat = NewSplat(read.width, read.height, splatIndex);
                var holder = new Texture2D[splatIndex + 1];
                holder[splatIndex] = splat;
                ApplyToSplats(holder, layer, read.GetPixels32(), read.width, read.height);
                var materialPath = AssetDatabase.GetAssetPath(material);
                if (!string.IsNullOrEmpty(materialPath))
                {
                    var directory = Path.GetDirectoryName(materialPath)?.Replace('\\', '/');
                    var splatAsset = (string.IsNullOrEmpty(directory) ? "" : directory + "/") +
                                     Path.GetFileNameWithoutExtension(materialPath) + "_Splat" + splatIndex + ".asset";
                    splat = SaveTexture(holder[splatIndex], splatAsset);
                    material.SetTexture("_Splat" + splatIndex, splat);
                }
                EditorUtility.SetDirty(material);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(read);
            }
        }

        internal static void ApplyImportedMask(string maskAssetPath)
        {
            var materialPath = MaterialPath(maskAssetPath);
            if (string.IsNullOrEmpty(materialPath)) return;
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null || !IsTerrainShader(material.shader) || UsesDirectLayers(material)) return;
            int layer = LayerIndex(maskAssetPath);
            if (layer <= 0) return;
            if (!TryReadPng(AssetPathToAbsolute(maskAssetPath), out var pixels, out int width, out int height))
                return;
            int splatIndex = SplatIndex(layer);
            var splat = material.GetTexture("_Splat" + splatIndex) as Texture2D;
            if (splat == null)
                splat = NewSplat(width, height, splatIndex);
            var holder = new Texture2D[splatIndex + 1];
            holder[splatIndex] = splat;
            ApplyToSplats(holder, layer, pixels, width, height);
            var directory = Path.GetDirectoryName(materialPath)?.Replace('\\', '/');
            var splatAsset = (string.IsNullOrEmpty(directory) ? "" : directory + "/") +
                             Path.GetFileNameWithoutExtension(materialPath) + "_Splat" + splatIndex + ".asset";
            var saved = SaveTexture(holder[splatIndex], splatAsset);
            material.SetTexture("_Splat" + splatIndex, saved);
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskAssetPath);
            if (mask != null && material.HasProperty(MaskProperty(layer)))
                material.SetTexture(MaskProperty(layer), mask);
            EditorUtility.SetDirty(material);
        }

        static void ClearChannel(Texture2D splat, int channel)
        {
            var pixels = splat.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                WriteChannel(ref pixel, channel, 0);
                pixels[i] = pixel;
            }
            splat.SetPixels32(pixels);
            splat.Apply(false, false);
        }

        static Texture2D NewSplat(int width, int height, int splatIndex)
        {
            var texture = new Texture2D(Math.Max(1, width), Math.Max(1, height), TextureFormat.RGBA32, false, true)
            {
                name = "Splat" + splatIndex,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var blank = new Color32[texture.width * texture.height];
            texture.SetPixels32(blank);
            texture.Apply(false, false);
            return texture;
        }

        internal static bool IsTerrainShader(Shader shader)
        {
            if (shader == null) return false;
            return shader.name == TerrainAssetImporter.ShaderName || shader.name == TerrainAssetImporter.HdrpShaderName;
        }

        static byte[] MaskBytes(TerrainMasks masks, TerrainAreaLayer[] entries, int layer)
        {
            var source = TerrainAssetImporter.ResolveLayer(masks, entries[layer].LayerIndex);
            var mask = entries[layer].Mask;
            int side = Math.Max(1, source.MaskSize);
            var gray = new byte[side * side];
            bool valid = source.MaskSize > 0 && mask != null && mask.Length == source.MaskSize * source.MaskSize;
            if (!valid) return gray;
            Buffer.BlockCopy(mask, 0, gray, 0, gray.Length);
            return gray;
        }

        static void ApplyToSplats(Texture2D[] splats, int layer, Color32[] png, int pngWidth, int pngHeight)
        {
            if (splats == null || layer <= 0) return;
            int splatIndex = SplatIndex(layer);
            if (splatIndex < 0 || splatIndex >= splats.Length || splats[splatIndex] == null) return;
            var splat = splats[splatIndex];
            if (!splat.isReadable) return;
            int channel = Channel(layer);
            var pixels = splat.GetPixels32();
            int width = splat.width;
            int height = splat.height;
            for (int y = 0; y < height; y++)
            {
                int sy = pngHeight <= 1 ? 0 : y * pngHeight / height;
                for (int x = 0; x < width; x++)
                {
                    int sx = pngWidth <= 1 ? 0 : x * pngWidth / width;
                    byte value = png[sy * pngWidth + sx].r;
                    int index = y * width + x;
                    var pixel = pixels[index];
                    WriteChannel(ref pixel, channel, value);
                    pixels[index] = pixel;
                }
            }
            splat.SetPixels32(pixels);
            splat.Apply(false, false);
        }

        public static void WriteGrayPng(string assetPath, byte[] gray, int width, int height)
        {
            WriteGrayPngFile(assetPath, gray, width, height);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        internal static void WriteGrayPngFile(string assetPath, byte[] gray, int width, int height) =>
            WritePng(AssetPathToAbsolute(assetPath), gray, width, height);

        static void WritePng(string absolutePath, byte[] gray, int width = 0, int height = 0)
        {
            if (width <= 0 || height <= 0)
            {
                int side = (int)Math.Round(Math.Sqrt(gray.Length));
                if (side * side != gray.Length) side = Math.Max(1, side);
                width = side;
                height = gray.Length / width;
            }
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte value = i < gray.Length ? gray[i] : (byte)0;
                pixels[i] = new Color32(value, value, value, 255);
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? ".");
                File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static bool TryReadPng(string absolutePath, out Color32[] pixels, out int width, out int height)
        {
            pixels = null;
            width = 0;
            height = 0;
            if (!File.Exists(absolutePath)) return false;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(absolutePath), false)) return false;
                width = texture.width;
                height = texture.height;
                pixels = texture.GetPixels32();
                return pixels != null && pixels.Length == width * height;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static Texture2D SaveTexture(Texture2D texture, string assetPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(texture, assetPath);
                return texture;
            }
            if (existing != texture)
            {
                if (existing.width == texture.width && existing.height == texture.height && existing.isReadable)
                {
                    existing.SetPixels32(texture.GetPixels32());
                    existing.Apply(false, false);
                }
                else
                {
                    var replacement = UnityEngine.Object.Instantiate(texture);
                    replacement.name = existing.name;
                    EditorUtility.CopySerialized(replacement, existing);
                    UnityEngine.Object.DestroyImmediate(replacement);
                }
                UnityEngine.Object.DestroyImmediate(texture);
            }
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static string AssetPathToAbsolute(string assetPath)
        {
            var project = Path.GetDirectoryName(Application.dataPath);
            return Path.GetFullPath(Path.Combine(project ?? "", assetPath));
        }

        [MenuItem("Tools/Angelica 2.x/Export Terrain Layer Masks")]
        static void ExportSelected()
        {
            var materials = new HashSet<Material>();
            foreach (var guid in Selection.assetGUIDs)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                Collect(path, materials);
            }
            foreach (var obj in Selection.objects)
            {
                if (obj is Material material) materials.Add(material);
                if (obj is GameObject go)
                {
                    foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                    {
                        foreach (var shared in renderer.sharedMaterials)
                            if (shared != null) materials.Add(shared);
                    }
                }
            }
            int written = 0;
            int skipped = 0;
            foreach (var material in materials)
            {
                if (!IsTerrainShader(material.shader))
                {
                    skipped++;
                    continue;
                }
                written += ExportMaterial(material, true);
            }
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "Terrain layer masks",
                written + " grayscale PNG mask" + (written == 1 ? "" : "s") + " written beside the terrain materials." +
                (skipped > 0 ? "\n" + skipped + " selected material" + (skipped == 1 ? " is" : "s are") + " not Angelica terrain." : ""),
                "OK");
        }

        static void Collect(string path, HashSet<Material> materials)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (AssetDatabase.IsValidFolder(path))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { path }))
                {
                    var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if (material != null && IsTerrainShader(material.shader)) materials.Add(material);
                }
                return;
            }
            var asset = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (asset != null) materials.Add(asset);
        }
    }

    sealed class TerrainMaskPngPostprocessor : AssetPostprocessor
    {
        static bool applying;

        void OnPreprocessTexture()
        {
            if (!TerrainMaskImages.IsMaskPath(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            importer.sRGBTexture = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.isReadable = true;
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (applying || imported == null || TerrainDirectAssetMigration.IsMigrating) return;
            applying = true;
            try
            {
                foreach (var path in imported)
                {
                    if (!TerrainMaskImages.IsMaskPath(path)) continue;
                    TerrainMaskImages.ApplyImportedMask(path);
                }
            }
            finally
            {
                applying = false;
            }
        }
    }
}
