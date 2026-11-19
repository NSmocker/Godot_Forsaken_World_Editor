using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Persists the transient render assets produced by GFX and AUI builders before saving a prefab.
    /// Files keep the original object names plus a type-specific extension (.mat, .anim, .asset)
    /// inside a per-source subfolder, mirroring the original game layout where every file is
    /// uniquely named and same-named files live in different folders. Repeated imports therefore
    /// reuse the same files regardless of transient-asset ordering, and a leftover file of another
    /// asset kind can never abort an import again.
    /// </summary>
    internal sealed class GameDataPrefabAssets : IDisposable
    {
        // Fixed map hierarchy groups that carry no source identity of their own; transient
        // assets skip over them when picking the per-source subfolder, so
        // Objects/Effects/0007_1/GFX/... persists under "0007_1" and Grass/Sector_0038 under "Sector_0038".
        private static readonly HashSet<string> GenericGroups = new(StringComparer.Ordinal)
        {
            "Terrain", "Buildings", "Main", "Objects", "Effects", "Models", "Trees", "Sounds",
            "Grass", "Water", "Sun", "Environment"
        };

        private const int MaxSiblingSlots = 64;
        private const int MaxFileNameLength = 80;

        private readonly Dictionary<Object, Object> replacements = new Dictionary<Object, Object>();
        private readonly Dictionary<string, Object> assigned = new Dictionary<string, Object>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Object> transient = new List<Object>();
        private readonly string folder;

        public GameDataPrefabAssets(GameObject root, string folder)
        {
            this.folder = folder;
            try
            {
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                    if (component != null) Rewrite(component, SourceKey(component.transform));
            }
            catch { Dispose(); throw; }
        }

        private Object Persist(Object value, string sourceKey)
        {
            if (value == null || EditorUtility.IsPersistent(value) ||
                !(value is Mesh || value is Material || value is Texture2D || value is Sprite || value is AnimationClip)) return value;
            if (replacements.TryGetValue(value, out var saved)) return saved;
            var path = ClaimPath(sourceKey, value);
            replacements[value] = value;
            Rewrite(value, sourceKey);
            saved = GameDataAssetBatch.Load(path);
            if (saved != null && saved.GetType() != value.GetType())
            {
                // A previous import left a different asset kind at this name; that is only
                // possible for extensions shared by several kinds. Move to a numbered sibling
                // instead of failing the whole import.
                path = ClaimSibling(path, value);
                saved = GameDataAssetBatch.Load(path);
            }
            if (saved != null)
            {
                EditorUtility.CopySerialized(value, saved);
                EditorUtility.SetDirty(saved);
                transient.Add(value);
            }
            else
            {
                AngelicaAssetFolders.Ensure(Path.GetDirectoryName(path));
                GameDataAssetBatch.Create(value, path);
                saved = value;
            }
            replacements[value] = saved;
            return saved;
        }

        private void Rewrite(Object owner, string sourceKey)
        {
            using (var serialized = new SerializedObject(owner))
            {
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        var original = property.objectReferenceValue;
                        var saved = Persist(original, sourceKey);
                        if (saved != original) property.objectReferenceValue = saved;
                    }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>Reserves a stable file path for <paramref name="value"/> under its source subfolder.</summary>
        private string ClaimPath(string sourceKey, Object value)
        {
            var dir = string.IsNullOrEmpty(sourceKey) ? folder : folder + "/" + sourceKey;
            var stem = dir + "/" + FileName(value == null ? null : value.name);
            var path = stem + Extension(value);
            for (var n = 1; assigned.ContainsKey(path); n++)
                path = stem + "_" + n + Extension(value);
            assigned[path] = value;
            return path;
        }

        /// <summary>Finds the first numbered sibling that is free or already holds the same asset kind.</summary>
        private string ClaimSibling(string path, Object value)
        {
            var stem = Path.ChangeExtension(path, null);
            var ext = Path.GetExtension(path);
            for (var n = 1; n < MaxSiblingSlots; n++)
            {
                var candidate = stem + "_" + n + ext;
                if (assigned.ContainsKey(candidate)) continue;
                var existing = GameDataAssetBatch.Load(candidate);
                if (existing == null || existing.GetType() == value.GetType())
                {
                    assigned[candidate] = value;
                    return candidate;
                }
            }
            throw new IOException("Too many conflicting generated assets at " + path);
        }

        private static string Extension(Object value)
        {
            if (value is Material) return ".mat";
            if (value is AnimationClip) return ".anim";
            return ".asset";
        }

        private static string FileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "Unnamed";
            var builder = new System.Text.StringBuilder(name.Length);
            foreach (var c in name.Trim())
                builder.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            var result = builder.ToString();
            if (result.Length > MaxFileNameLength) result = result.Substring(0, MaxFileNameLength);
            return result.Length == 0 ? "Unnamed" : result;
        }

        /// <summary>
        /// Picks the per-source subfolder from the owner's ancestor chain: the first non-generic
        /// name between the map root and the component, so identical names from different sources
        /// (the normal case in the original assets) never share a file.
        /// </summary>
        private static string SourceKey(Transform transform)
        {
            var chain = new List<string>();
            for (var current = transform; current != null; current = current.parent)
                chain.Add(current.gameObject.name);
            for (var i = chain.Count - 2; i >= 0; i--)
                if (!GenericGroups.Contains(chain[i]))
                    return FileName(chain[i]);
            return string.Empty;
        }

        public void Dispose()
        {
            foreach (var item in transient)
                if (item != null && !EditorUtility.IsPersistent(item)) Object.DestroyImmediate(item);
            transient.Clear();
        }
    }
}
