using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SilverEcho.ForsakenWorld.Editor
{
    [InitializeOnLoad]
    internal sealed class TerrainDetailTextureCache : AssetPostprocessor
    {
        internal const string Folder = "Assets/AngelicaGenerated/TerrainDetails";
        internal const string TextureProperty = "_TerrainDetails";
        internal const string Keyword = "ANGELICA_TERRAIN_DETAIL_ARRAY";
        internal const string ParallaxKeyword = "ANGELICA_TERRAIN_PARALLAX";
        private const string BakeShader = "Hidden/Forsaken World/Terrain Detail Bake";
        private const int MaxResolution = 1024;
        private const int BakeVersion = 1;
        private static readonly HashSet<Material> PendingMaterials = new HashSet<Material>();
        private static readonly HashSet<Material> EditedMaterials = new HashSet<Material>();
        private static readonly HashSet<string> ChangedSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool scheduled;
        private static bool assetsDeleted;

        static TerrainDetailTextureCache()
        {
            Undo.undoRedoPerformed += () =>
            {
                foreach (var material in EditedMaterials)
                    Queue(material);
            };
        }

        internal static void Queue(Material material, bool edited = false)
        {
            if (!IsTerrain(material) || !AssetDatabase.Contains(material)) return;
            if (edited) EditedMaterials.Add(material);
            PendingMaterials.Add(material);
            Schedule();
        }

        private static bool IsTerrain(Material material) =>
            material != null && material.shader != null && material.shader.name == AngelicaRenderPipeline.HdrpTerrain;

        private static void Schedule()
        {
            if (scheduled) return;
            scheduled = true;
            EditorApplication.delayCall += Flush;
        }

        private static void Flush()
        {
            scheduled = false;
            var materials = PendingMaterials.ToArray();
            PendingMaterials.Clear();
            var sources = new HashSet<string>(ChangedSources, StringComparer.OrdinalIgnoreCase);
            ChangedSources.Clear();
            bool checkMissingSources = assetsDeleted;
            assetsDeleted = false;
            if ((sources.Count != 0 || checkMissingSources) && AssetDatabase.IsValidFolder(Folder))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:TerrainDetailArrayAsset", new[] { Folder }))
                {
                    var cache = AssetDatabase.LoadAssetAtPath<TerrainDetailArrayAsset>(AssetDatabase.GUIDToAssetPath(guid));
                    if (cache == null || cache.Sources == null) continue;
                    if (!checkMissingSources && !cache.Sources.Any(slice =>
                        sources.Contains(AssetDatabase.GetAssetPath(slice.Normal)) ||
                        sources.Contains(AssetDatabase.GetAssetPath(slice.Roughness)) ||
                        sources.Contains(AssetDatabase.GetAssetPath(slice.Height)))) continue;
                    TryUpdate(() => Refresh(cache), cache);
                }
            }
            foreach (var material in materials)
                TryUpdate(() => Bind(material), material);
        }

        private static void TryUpdate(Action update, Object context)
        {
            try { update(); }
            catch (Exception e) { Debug.LogError("Could not update HDRP terrain detail maps. " + e.Message, context); }
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool hasCache = AssetDatabase.IsValidFolder(Folder);
            if (hasCache && deleted.Any(path => !path.StartsWith(Folder + "/", StringComparison.OrdinalIgnoreCase)))
            {
                assetsDeleted = true;
                Schedule();
            }
            foreach (string path in imported.Concat(moved))
            {
                if (path.StartsWith(Folder + "/", StringComparison.OrdinalIgnoreCase)) continue;
                if (path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                    Queue(AssetDatabase.LoadAssetAtPath<Material>(path));
                else if (hasCache && AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(Texture2D))
                {
                    ChangedSources.Add(path);
                    Schedule();
                }
            }
        }

        internal static void Bind(Material material)
        {
            if (!IsTerrain(material)) return;
            int limit = TerrainMaskImages.UsesDirectLayers(material)
                ? TerrainAssetImporter.MaxLayers : AngelicaTerrainLayerBindings.StandardLayerCount;
            int count = Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_LayerCount")), 1, limit);
            var palette = new SortedDictionary<string, TerrainDetailArrayAsset.Slice>(StringComparer.Ordinal);
            var slots = new string[count];
            bool hasOverlay = false;
            bool hasParallax = false;
            for (int i = 0; i < count; i++)
            {
                var normal = material.GetTexture("_Normal" + i) as Texture2D;
                var roughness = material.GetTexture("_Roughness" + i) as Texture2D;
                var height = material.GetFloat("_UseParallax" + i) > 0.5f
                    ? material.GetTexture("_ParallaxHeight" + i) as Texture2D : null;
                string normalKey = GetAssetKey(normal);
                string roughnessKey = GetAssetKey(roughness);
                string heightKey = GetAssetKey(height);
                bool storesHeight = heightKey != "";
                if (normalKey == "" && roughnessKey == "" && !storesHeight) continue;
                string key = normalKey + "/" + roughnessKey;
                if (storesHeight) key += "/" + heightKey;
                slots[i] = key;
                palette[key] = new TerrainDetailArrayAsset.Slice
                {
                    Normal = normalKey != "" ? normal : null,
                    Roughness = roughnessKey != "" ? roughness : null,
                    Height = storesHeight ? height : null,
                    StoresHeight = storesHeight
                };
                hasOverlay |= i > 0;
                hasParallax |= storesHeight;
            }

            Texture2DArray texture = null;
            var indices = new Vector4[4];
            var heightChannels = new Vector4[4];
            if (hasOverlay || hasParallax)
            {
                // Stable source identities let every material with the same palette use
                // one array, even if textures were assigned to different layer indices.
                string hash = Hash128.Compute(string.Join("|", palette.Keys)).ToString();
                string path = Folder + "/" + hash + ".asset";
                var cache = AssetDatabase.LoadAssetAtPath<TerrainDetailArrayAsset>(path);
                if (cache == null)
                {
                    var sources = palette.Values.ToArray();
                    EnsureFolder();
                    var baked = Bake(sources);
                    cache = ScriptableObject.CreateInstance<TerrainDetailArrayAsset>();
                    cache.Sources = sources;
                    cache.Texture = baked;
                    cache.Revision = GetRevision(sources);
                    AssetDatabase.CreateAsset(cache, path);
                    AssetDatabase.AddObjectToAsset(baked, cache);
                    EditorUtility.SetDirty(cache);
                    AssetDatabase.SaveAssetIfDirty(cache);
                }
                else
                {
                    Refresh(cache);
                }
                texture = cache.Texture;
                int index = 1;
                foreach (var entry in palette)
                {
                    var slice = entry.Value;
                    // Height replaces the alpha channel's detail flags for these slices.
                    int channels = slice.StoresHeight
                        ? 4 + (slice.Normal != null ? 1 : 0) + (slice.Roughness != null ? 2 : 0) : 0;
                    for (int i = 0; i < count; i++)
                    {
                        if (slots[i] != entry.Key) continue;
                        indices[i / 4][i % 4] = index;
                        heightChannels[i / 4][i % 4] = channels;
                    }
                    index++;
                }
            }

            bool changed = false;
            if (material.GetTexture(TextureProperty) != texture)
            {
                material.SetTexture(TextureProperty, texture);
                changed = true;
            }
            for (int i = 0; i < indices.Length; i++)
            {
                changed |= SetVector(material, "_TerrainDetailSlices" + i, indices[i]);
                changed |= SetVector(material, "_TerrainHeightChannels" + i, heightChannels[i]);
            }
            changed |= SetKeyword(material, Keyword, hasOverlay && !hasParallax);
            changed |= SetKeyword(material, ParallaxKeyword, hasParallax);
            if (changed) EditorUtility.SetDirty(material);
        }

        private static bool SetVector(Material material, string property, Vector4 value)
        {
            if (material.GetVector(property) == value) return false;
            material.SetVector(property, value);
            return true;
        }

        private static bool SetKeyword(Material material, string keyword, bool enabled)
        {
            if (material.IsKeywordEnabled(keyword) == enabled) return false;
            if (enabled) material.EnableKeyword(keyword);
            else material.DisableKeyword(keyword);
            return true;
        }

        private static string GetAssetKey(Texture2D texture)
        {
            if (texture == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(texture, out string guid, out long id))
                return "";
            return guid + ":" + id;
        }

        private static string GetRevision(TerrainDetailArrayAsset.Slice[] sources)
        {
            var revision = new StringBuilder().Append(BakeVersion);
            foreach (var slice in sources)
            {
                revision.Append('|').Append(SourceHash(slice.Normal));
                revision.Append('|').Append(SourceHash(slice.Roughness));
                if (slice.StoresHeight) revision.Append("/height:").Append(SourceHash(slice.Height));
            }
            return Hash128.Compute(revision.ToString()).ToString();
        }

        private static string SourceHash(Texture2D texture) =>
            texture == null ? "" : AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(texture)).ToString();

        private static void Refresh(TerrainDetailArrayAsset cache)
        {
            string revision = GetRevision(cache.Sources);
            if (cache.Texture != null && cache.Revision == revision) return;
            var baked = Bake(cache.Sources);
            if (cache.Texture == null)
            {
                cache.Texture = baked;
                AssetDatabase.AddObjectToAsset(baked, cache);
            }
            else
            {
                // Preserve the subasset ID so materials stay linked after a texture edit.
                EditorUtility.CopySerialized(baked, cache.Texture);
                Object.DestroyImmediate(baked);
            }
            cache.Revision = revision;
            EditorUtility.SetDirty(cache.Texture);
            EditorUtility.SetDirty(cache);
            AssetDatabase.SaveAssetIfDirty(cache);
        }

        private static void EnsureFolder()
        {
            const string parent = "Assets/AngelicaGenerated";
            if (!AssetDatabase.IsValidFolder(parent)) AssetDatabase.CreateFolder("Assets", "AngelicaGenerated");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(parent, "TerrainDetails");
        }

        private static Texture2DArray Bake(TerrainDetailArrayAsset.Slice[] sources)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !SystemInfo.supports2DArrayTextures)
                throw new InvalidOperationException("A graphics device with texture array support is required to bake the detail cache.");
            var shader = Shader.Find(BakeShader);
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("The terrain detail bake shader is unavailable.");

            int width = 1;
            int height = 1;
            foreach (var slice in sources)
            {
                width = Mathf.Max(width, Mathf.Max(slice.Normal != null ? slice.Normal.width : 1,
                    slice.Roughness != null ? slice.Roughness.width : 1));
                height = Mathf.Max(height, Mathf.Max(slice.Normal != null ? slice.Normal.height : 1,
                    slice.Roughness != null ? slice.Roughness.height : 1));
                if (slice.Height != null)
                {
                    width = Mathf.Max(width, slice.Height.width);
                    height = Mathf.Max(height, slice.Height.height);
                }
            }
            width = Mathf.Min(MaxResolution, width);
            height = Mathf.Min(MaxResolution, height);
            var array = new Texture2DArray(width, height, sources.Length, TextureFormat.RGBA32, true, true)
            {
                name = "Terrain Details",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear
            };
            var material = new Material(shader);
            var staging = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            try
            {
                for (int i = 0; i < sources.Length; i++)
                {
                    var slice = sources[i];
                    material.SetTexture("_NormalSource", slice.Normal);
                    material.SetTexture("_RoughnessSource", slice.Roughness);
                    material.SetTexture("_HeightSource", slice.Height != null ? slice.Height : Texture2D.whiteTexture);
                    material.SetVector("_Presence", new Vector4(slice.Normal != null ? 1 : 0,
                        slice.Roughness != null ? 1 : 0, slice.StoresHeight ? 1 : 0, 0));
                    Graphics.Blit(null, target, material);
                    RenderTexture.active = target;
                    staging.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                    array.SetPixelData(staging.GetRawTextureData<Color32>(), 0, i);
                }
                array.Apply(true, false);
                return array;
            }
            catch
            {
                Object.DestroyImmediate(array);
                throw;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(staging);
                Object.DestroyImmediate(material);
            }
        }
    }
}
