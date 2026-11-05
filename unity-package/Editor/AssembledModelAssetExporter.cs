using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Persists an assembled SMD/ECM model as Mesh assets + AnimationClips + materials/textures
    /// and a Prefab, matching Godot ecm_importer._save_model (mesh.res + actions.res + .tscn).
    /// Reimport updates assets in place so .meta GUIDs are preserved.
    /// </summary>
    public static class AssembledModelAssetExporter
    {
        public const string MeshImportRoot = "Assets/ForsakenWorld/Imported/Meshes";
        public const string ModelPrefabRoot = "Assets/ForsakenWorld/Imported/Models";
        public const string EcmPrefabRoot = "Assets/ForsakenWorld/Imported/ECM";
        public const string AnimationImportRoot = "Assets/ForsakenWorld/Imported/Animations";

        public sealed class ExportResult
        {
            public string PrefabPath = string.Empty;
            public readonly List<string> MeshPaths = new List<string>();
            public readonly List<string> ClipPaths = new List<string>();
            public readonly List<string> MaterialPaths = new List<string>();
            public readonly List<string> Warnings = new List<string>();
            public int MeshesCreated;
            public int MeshesUpdated;
            public int ClipsCreated;
            public int ClipsUpdated;
            public int MaterialsCreated;
            public int MaterialsUpdated;
            public int TexturesCreated;
            public int TexturesUpdated;
            public int TexturesMissing;
            public bool PrefabCreated;
            public bool PrefabUpdated;
            public string Error;
            public bool Succeeded => string.IsNullOrEmpty(Error) && !string.IsNullOrEmpty(PrefabPath);
        }

        public static string GetMeshAssetPath(string skiRelativePath)
        {
            var relative = (skiRelativePath ?? "skin").Replace('\\', '/').TrimStart('/');
            var parts = relative.Split('/');
            for (var i = 0; i < parts.Length; i++)
                parts[i] = Sanitize(parts[i]);
            var withoutExt = Path.ChangeExtension(string.Join("/", parts), null);
            if (string.IsNullOrEmpty(withoutExt))
                withoutExt = "skin";
            return MeshImportRoot + "/" + withoutExt + "_mesh.asset";
        }

        public static string GetPrefabAssetPath(string sourceRelativePath, bool fromEcm)
        {
            var relative = (sourceRelativePath ?? "model").Replace('\\', '/').TrimStart('/');
            var parts = relative.Split('/');
            for (var i = 0; i < parts.Length; i++)
                parts[i] = Sanitize(parts[i]);
            var joined = string.Join("/", parts);
            var withoutExt = Path.ChangeExtension(joined, null);
            if (string.IsNullOrEmpty(withoutExt))
                withoutExt = "model";
            if (fromEcm)
                return EcmPrefabRoot + "/" + withoutExt + ".prefab";
            // Godot SMD scene suffix: <SMD>_model.tscn
            return ModelPrefabRoot + "/" + withoutExt + "_model.prefab";
        }

        public static string GetAnimationClipAssetPath(string smdRelativePath, string clipName)
        {
            var relative = (smdRelativePath ?? "model").Replace('\\', '/').TrimStart('/');
            var parts = relative.Split('/');
            for (var i = 0; i < parts.Length; i++)
                parts[i] = Sanitize(parts[i]);
            var folder = AnimationImportRoot + "/" +
                Path.ChangeExtension(string.Join("/", parts), null).Replace('\\', '/');
            return folder + "/" + Sanitize(clipName) + ".anim";
        }

        public static ExportResult Export(
            AssembledModel model,
            GameDataIndex index,
            string sourceRelativePath,
            bool fromEcm,
            string rootName = null,
            EcmDocument ecmDocument = null)
        {
            var result = new ExportResult();
            if (model == null)
            {
                result.Error = "Assembled model is null.";
                return result;
            }

            if (!model.IsValid)
            {
                result.Error = "Cannot export an invalid assembled model.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(sourceRelativePath))
            {
                result.Error = "A source relative path is required for the Prefab output.";
                return result;
            }

            GameObject root = null;
            try
            {
                var meshAssets = new List<Mesh>();
                foreach (var skin in model.Skins)
                {
                    var built = AssembledModelUnityBuilder.BuildCombinedMesh(skin);
                    if (built == null)
                    {
                        meshAssets.Add(null);
                        continue;
                    }

                    var meshPath = GetMeshAssetPath(skin.SourcePath);
                    Mesh saved;
                    bool created;
                    bool updated;
                    SaveOrUpdateMesh(built, meshPath, out saved, out created, out updated);
                    meshAssets.Add(saved);
                    result.MeshPaths.Add(meshPath);
                    if (created)
                        result.MeshesCreated++;
                    if (updated)
                        result.MeshesUpdated++;
                }

                List<string> textureErrors;
                var textures = AssembledTextureImporter.ImportTexturesForModel(
                    model,
                    index,
                    out result.TexturesCreated,
                    out result.TexturesUpdated,
                    out result.TexturesMissing,
                    out textureErrors);
                if (textureErrors != null)
                    result.Warnings.AddRange(textureErrors);

                var materialLookup = new Dictionary<string, Material>(StringComparer.Ordinal);
                foreach (var skin in model.Skins)
                {
                    foreach (var slot in skin.SurfaceMaterials)
                    {
                        Texture2D albedo = null;
                        if (slot.TextureResolved &&
                            !string.IsNullOrWhiteSpace(slot.TextureSourcePath))
                        {
                            textures.TryGetValue(
                                slot.TextureSourcePath.Replace('\\', '/'),
                                out albedo);
                        }

                        var reflect = slot.UsesReflectShader
                            ? AssembledUrpMaterialBuilder.FindReflectMap(textures)
                            : null;
                        var material = AssembledUrpMaterialBuilder.CreateMaterial(slot, albedo, reflect);
                        var materialPath = AssembledUrpMaterialBuilder.GetMaterialAssetPath(
                            skin.SourcePath,
                            slot);
                        Material savedMaterial;
                        bool created;
                        bool updated;
                        SaveOrUpdateMaterial(
                            material,
                            materialPath,
                            out savedMaterial,
                            out created,
                            out updated);
                        materialLookup[SlotKey(skin.SourcePath, slot.TextureIndex, slot.MaterialIndex)] =
                            savedMaterial;
                        result.MaterialPaths.Add(materialPath);
                        if (created)
                            result.MaterialsCreated++;
                        if (updated)
                            result.MaterialsUpdated++;
                    }
                }

                var clipAssets = new List<AnimationClip>();
                if (model.Animations.Count > 0)
                {
                    var clips = AssembledAnimationClipBuilder.CreateClips(model.Animations);
                    var smdPath = string.IsNullOrEmpty(model.SourceSmdPath)
                        ? sourceRelativePath
                        : model.SourceSmdPath;
                    foreach (var clip in clips)
                    {
                        if (clip == null)
                            continue;
                        var clipPath = GetAnimationClipAssetPath(smdPath, clip.name);
                        AnimationClip savedClip;
                        bool created;
                        bool updated;
                        SaveOrUpdateClip(clip, clipPath, out savedClip, out created, out updated);
                        clipAssets.Add(savedClip);
                        result.ClipPaths.Add(clipPath);
                        if (created)
                            result.ClipsCreated++;
                        if (updated)
                            result.ClipsUpdated++;
                    }
                }

                Func<AssembledSurfaceMaterial, Texture> albedoLookup = slot =>
                {
                    if (slot == null || string.IsNullOrWhiteSpace(slot.TextureSourcePath))
                        return null;
                    Texture2D texture;
                    return textures.TryGetValue(
                        slot.TextureSourcePath.Replace('\\', '/'),
                        out texture)
                        ? texture
                        : null;
                };

                root = AssembledModelUnityBuilder.BuildHierarchy(model, rootName, albedoLookup);
                AssignPersistedMeshesAndMaterials(root, model, meshAssets, materialLookup);
                if (clipAssets.Count > 0)
                    AssembledAnimationClipBuilder.BindClipsToHierarchy(root, clipAssets);

                if (fromEcm && ecmDocument != null)
                {
                    var cogfxCount = EcmCoGfxAttacher.Attach(
                        root, ecmDocument, index, result.Warnings);
                    if (cogfxCount > 0)
                        result.Warnings.Add("CoGfx attached: " + cogfxCount);
                }

                result.PrefabPath = GetPrefabAssetPath(sourceRelativePath, fromEcm);
                EnsureAssetFolders(Path.GetDirectoryName(result.PrefabPath));
                var existed = AssetDatabase.LoadMainAssetAtPath(result.PrefabPath) != null ||
                    File.Exists(AssetPathToAbsolute(result.PrefabPath));
                // Use the success flag (like Building/Terrain/Map importers): inside
                // AssetDatabase.StartAssetEditing the prefab file is written but the
                // returned object is null until import resumes.
                PrefabUtility.SaveAsPrefabAsset(root, result.PrefabPath, out bool prefabSaved);
                if (!prefabSaved)
                    throw new InvalidOperationException(
                        "PrefabUtility.SaveAsPrefabAsset failed for " + result.PrefabPath);
                if (existed)
                    result.PrefabUpdated = true;
                else
                    result.PrefabCreated = true;

                AssetDatabase.SaveAssets();
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is ArgumentNullException ||
                exception is InvalidOperationException ||
                exception is UnityException)
            {
                result.Error = exception.GetType().Name + ": " + exception.Message;
            }
            finally
            {
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }

            return result;
        }

        private static void AssignPersistedMeshesAndMaterials(
            GameObject root,
            AssembledModel model,
            IReadOnlyList<Mesh> meshAssets,
            Dictionary<string, Material> materialLookup)
        {
            if (root == null || model == null)
                return;

            var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var skinIndex = 0;
            foreach (var renderer in renderers)
            {
                while (skinIndex < model.Skins.Count &&
                    (meshAssets.Count <= skinIndex || meshAssets[skinIndex] == null))
                    skinIndex++;
                if (skinIndex >= model.Skins.Count || skinIndex >= meshAssets.Count)
                    break;

                var skin = model.Skins[skinIndex];
                renderer.sharedMesh = meshAssets[skinIndex];
                var materials = new Material[skin.Surfaces.Count];
                for (var i = 0; i < skin.Surfaces.Count; i++)
                {
                    var surface = skin.Surfaces[i];
                    Material material;
                    if (!materialLookup.TryGetValue(
                        SlotKey(skin.SourcePath, surface.TextureIndex, surface.MaterialIndex),
                        out material))
                    {
                        material = AssembledUrpMaterialBuilder.CreateMaterial(
                            FindSlot(skin, surface.TextureIndex, surface.MaterialIndex),
                            null);
                    }

                    materials[i] = material;
                }

                if (materials.Length > 0)
                    renderer.sharedMaterials = materials;
                skinIndex++;
            }
        }

        private static AssembledSurfaceMaterial FindSlot(
            AssembledSkin skin,
            int textureIndex,
            int materialIndex)
        {
            foreach (var slot in skin.SurfaceMaterials)
            {
                if (slot.TextureIndex == textureIndex && slot.MaterialIndex == materialIndex)
                    return slot;
            }

            return new AssembledSurfaceMaterial
            {
                Name = "Material",
                TextureIndex = textureIndex,
                MaterialIndex = materialIndex
            };
        }

        private static string SlotKey(string skinPath, int textureIndex, int materialIndex)
        {
            return (skinPath ?? string.Empty).Replace('\\', '/') + "|" +
                textureIndex + "|" + materialIndex;
        }

        private static void SaveOrUpdateMesh(
            Mesh mesh,
            string assetPath,
            out Mesh saved,
            out bool created,
            out bool updated)
        {
            created = false;
            updated = false;
            var existing = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (existing != null)
            {
                if (!(existing is Mesh))
                    throw new IOException(
                        "An incompatible asset already exists at the output path: " + assetPath);
                EditorUtility.CopySerialized(mesh, existing);
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                saved = (Mesh)existing;
                updated = true;
                return;
            }

            if (File.Exists(AssetPathToAbsolute(assetPath)))
                throw new IOException(
                    "A file already exists at the output path but is not a Unity asset: " +
                    assetPath);
            EnsureAssetFolders(Path.GetDirectoryName(assetPath));
            AssetDatabase.CreateAsset(mesh, assetPath);
            saved = mesh;
            created = true;
        }

        private static void SaveOrUpdateMaterial(
            Material material,
            string assetPath,
            out Material saved,
            out bool created,
            out bool updated)
        {
            created = false;
            updated = false;
            var existing = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (existing != null)
            {
                if (!(existing is Material))
                    throw new IOException(
                        "An incompatible asset already exists at the output path: " + assetPath);
                EditorUtility.CopySerialized(material, existing);
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(material);
                saved = (Material)existing;
                updated = true;
                return;
            }

            if (File.Exists(AssetPathToAbsolute(assetPath)))
                throw new IOException(
                    "A file already exists at the output path but is not a Unity asset: " +
                    assetPath);
            EnsureAssetFolders(Path.GetDirectoryName(assetPath));
            AssetDatabase.CreateAsset(material, assetPath);
            saved = material;
            created = true;
        }

        private static void SaveOrUpdateClip(
            AnimationClip clip,
            string assetPath,
            out AnimationClip saved,
            out bool created,
            out bool updated)
        {
            created = false;
            updated = false;
            var existing = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (existing != null)
            {
                if (!(existing is AnimationClip))
                    throw new IOException(
                        "An incompatible asset already exists at the output path: " + assetPath);
                EditorUtility.CopySerialized(clip, existing);
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(clip);
                saved = (AnimationClip)existing;
                updated = true;
                return;
            }

            if (File.Exists(AssetPathToAbsolute(assetPath)))
                throw new IOException(
                    "A file already exists at the output path but is not a Unity asset: " +
                    assetPath);
            EnsureAssetFolders(Path.GetDirectoryName(assetPath));
            AssetDatabase.CreateAsset(clip, assetPath);
            saved = clip;
            created = true;
        }

        private static string AssetPathToAbsolute(string assetPath)
        {
            var normalized = (assetPath ?? string.Empty).Replace('\\', '/');
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("Assets/".Length);
            return Path.GetFullPath(Path.Combine(Application.dataPath, normalized));
        }

        private static void EnsureAssetFolders(string folderPath)
        {
            AngelicaAssetFolders.Ensure(folderPath);
        }

        private static string Sanitize(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = (name ?? "x").ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (c == '/' || c == '\\' || c == ':' || c == '*' || c == '?' ||
                    c == '"' || c == '<' || c == '>' || c == '|' ||
                    Array.IndexOf(invalid, c) >= 0)
                    chars[i] = '_';
            }

            return new string(chars);
        }
    }
}
