using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Host-side DDS/texture binding for assembled URP materials.
    /// Prefer copying the original .dds under Assets/ForsakenWorld/Imported/Textures so
    /// Unity 6000.0 (Unity 6) IHVImageFormatImporter imports DXT1/3/5 (and common uncompressed DDS)
    /// natively. Reimport overwrites the file in place so the .meta GUID is preserved.
    /// Falls back to Runtime DXT→RGBA8 decode + Texture2D .asset only when the DDS header
    /// is marked not LikelyUnityNativeImport or native load fails.
    /// </summary>
    public static class AssembledTextureImporter
    {
        public const string TextureImportRoot = "Assets/ForsakenWorld/Imported/Textures";

        public sealed class ImportResult
        {
            public Texture2D Texture;
            public string AssetPath = string.Empty;
            public bool Created;
            public bool Updated;
            public bool UsedNativeDds;
            public bool UsedDecodedFallback;
            public bool Missing;
            public string Error;
        }

        public static string GetTextureAssetPath(string sourceRelativePath, bool decodedAsset = false)
        {
            var relative = (sourceRelativePath ?? "texture").Replace('\\', '/').TrimStart('/');
            var parts = relative.Split('/');
            for (var i = 0; i < parts.Length; i++)
                parts[i] = Sanitize(parts[i]);

            var joined = string.Join("/", parts);
            if (decodedAsset)
            {
                var withoutExt = Path.ChangeExtension(joined, null);
                if (string.IsNullOrEmpty(withoutExt))
                    withoutExt = "texture";
                return TextureImportRoot + "/" + withoutExt + ".asset";
            }

            var extension = Path.GetExtension(joined);
            if (string.IsNullOrEmpty(extension))
                joined += ".dds";
            return TextureImportRoot + "/" + joined;
        }

        public static ImportResult ImportOrUpdate(
            string sourceFullPath,
            string sourceRelativePath,
            DdsDocument header = null)
        {
            var result = new ImportResult();
            if (string.IsNullOrWhiteSpace(sourceFullPath) || !File.Exists(sourceFullPath))
            {
                result.Missing = true;
                result.Error = "Texture file missing: " +
                    (string.IsNullOrEmpty(sourceRelativePath) ? sourceFullPath : sourceRelativePath);
                return result;
            }

            var extension = Path.GetExtension(sourceFullPath) ?? string.Empty;
            var isDds = extension.Equals(".dds", StringComparison.OrdinalIgnoreCase);
            var preferNative = !isDds || header == null || header.LikelyUnityNativeImport;

            if (preferNative)
            {
                try
                {
                    result = ImportNativeFile(sourceFullPath, sourceRelativePath);
                    if (result.Texture != null)
                        return result;
                    if (!isDds)
                        return result;
                    // Native DDS import returned null Texture2D — try decode fallback below.
                    if (string.IsNullOrEmpty(result.Error))
                        result.Error = "Native DDS import did not yield a Texture2D.";
                }
                catch (Exception exception)
                {
                    if (!isDds)
                    {
                        result.Error = exception.Message;
                        return result;
                    }

                    result.Error = exception.Message;
                }
            }

            if (isDds)
                return ImportDecodedDds(sourceFullPath, sourceRelativePath, header, result.Error);

            return result;
        }

        public static ImportResult ImportForSlot(
            AssembledSurfaceMaterial slot,
            GameDataIndex index)
        {
            var result = new ImportResult();
            if (slot == null)
            {
                result.Error = "Material slot is null.";
                return result;
            }

            if (!slot.TextureResolved || string.IsNullOrWhiteSpace(slot.TextureSourcePath))
            {
                result.Missing = true;
                result.Error = string.IsNullOrEmpty(slot.TextureError)
                    ? ("Texture unresolved: " + (slot.TextureName ?? string.Empty))
                    : slot.TextureError;
                return result;
            }

            if (index == null)
            {
                result.Error = "Game data index is required to locate texture files.";
                return result;
            }

            GameDataFile file;
            string resolveError;
            if (!index.TryResolve(slot.TextureSourcePath, out file, out resolveError) &&
                !index.TryResolve(slot.TextureSourcePath, out file, out resolveError, true))
            {
                result.Missing = true;
                result.Error = string.IsNullOrEmpty(resolveError)
                    ? ("Texture not found: " + slot.TextureSourcePath)
                    : resolveError;
                return result;
            }

            return ImportOrUpdate(file.FullPath, file.RelativePath, slot.TextureDds);
        }

        public static Dictionary<string, Texture2D> ImportTexturesForModel(
            AssembledModel model,
            GameDataIndex index,
            out int created,
            out int updated,
            out int missing,
            out List<string> errors)
        {
            created = 0;
            updated = 0;
            missing = 0;
            errors = new List<string>();
            var cache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            if (model == null)
                return cache;

            foreach (var skin in model.Skins)
            {
                foreach (var slot in skin.SurfaceMaterials)
                {
                    if (slot == null || string.IsNullOrWhiteSpace(slot.TextureSourcePath))
                    {
                        if (slot != null && !string.IsNullOrWhiteSpace(slot.TextureName))
                            missing++;
                        continue;
                    }

                    var key = slot.TextureSourcePath.Replace('\\', '/');
                    if (cache.ContainsKey(key))
                        continue;

                    var import = ImportForSlot(slot, index);
                    if (import.Texture != null)
                    {
                        cache[key] = import.Texture;
                        if (import.Created)
                            created++;
                        if (import.Updated)
                            updated++;
                    }
                    else
                    {
                        if (import.Missing)
                            missing++;
                        if (!string.IsNullOrEmpty(import.Error))
                            errors.Add(slot.TextureName + ": " + import.Error);
                    }
                }
            }

            return cache;
        }

        private static ImportResult ImportNativeFile(string sourceFullPath, string sourceRelativePath)
        {
            var result = new ImportResult
            {
                UsedNativeDds = (Path.GetExtension(sourceFullPath) ?? string.Empty)
                    .Equals(".dds", StringComparison.OrdinalIgnoreCase),
                AssetPath = GetTextureAssetPath(sourceRelativePath, decodedAsset: false)
            };

            var absoluteDest = AssetPathToAbsolute(result.AssetPath);
            var destDir = Path.GetDirectoryName(absoluteDest);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            var existed = File.Exists(absoluteDest) ||
                AssetDatabase.LoadMainAssetAtPath(result.AssetPath) != null;
            File.Copy(sourceFullPath, absoluteDest, overwrite: true);
            AssetDatabase.ImportAsset(result.AssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            result.Texture = AssetDatabase.LoadAssetAtPath<Texture2D>(result.AssetPath);
            if (result.Texture == null)
            {
                result.Error =
                    "Unity did not produce a Texture2D at " + result.AssetPath +
                    " after native import.";
                return result;
            }

            if (existed)
                result.Updated = true;
            else
                result.Created = true;
            return result;
        }

        private static ImportResult ImportDecodedDds(
            string sourceFullPath,
            string sourceRelativePath,
            DdsDocument header,
            string priorError)
        {
            var result = new ImportResult
            {
                UsedDecodedFallback = true,
                AssetPath = GetTextureAssetPath(sourceRelativePath, decodedAsset: true)
            };

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(sourceFullPath);
            }
            catch (Exception exception)
            {
                result.Error = CombineErrors(priorError, exception.Message);
                return result;
            }

            if (header == null)
            {
                try
                {
                    header = new DdsHeaderParser().Parse(bytes, sourceRelativePath);
                }
                catch (Exception exception)
                {
                    result.Error = CombineErrors(priorError, exception.Message);
                    return result;
                }
            }

            byte[] rgba;
            string decodeError;
            if (!DdsDxtDecoder.TryDecodeTopMipRgba(bytes, header, out rgba, out decodeError))
            {
                result.Error = CombineErrors(priorError, decodeError ?? "DDS decode failed.");
                return result;
            }

            var texture = CreateRgbaTexture(header.Width, header.Height, rgba, sourceRelativePath);
            var existing = AssetDatabase.LoadMainAssetAtPath(result.AssetPath);
            if (existing != null)
            {
                if (!(existing is Texture2D))
                {
                    result.Error =
                        "An incompatible asset already exists at the output path: " +
                        result.AssetPath;
                    UnityEngine.Object.DestroyImmediate(texture);
                    return result;
                }

                EditorUtility.CopySerialized(texture, existing);
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(texture);
                result.Texture = (Texture2D)existing;
                result.Updated = true;
            }
            else
            {
                if (File.Exists(AssetPathToAbsolute(result.AssetPath)))
                {
                    result.Error =
                        "A file already exists at the output path but is not a Unity asset: " +
                        result.AssetPath;
                    UnityEngine.Object.DestroyImmediate(texture);
                    return result;
                }

                EnsureAssetFolders(Path.GetDirectoryName(result.AssetPath));
                AssetDatabase.CreateAsset(texture, result.AssetPath);
                result.Texture = texture;
                result.Created = true;
            }

            return result;
        }

        private static Texture2D CreateRgbaTexture(
            int width,
            int height,
            byte[] rgba,
            string sourceRelativePath)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = Sanitize(Path.GetFileNameWithoutExtension(
                (sourceRelativePath ?? "texture").Replace('\\', '/')));
            var colors = new Color32[width * height];
            for (var i = 0; i < colors.Length; i++)
            {
                var o = i * 4;
                colors[i] = new Color32(rgba[o], rgba[o + 1], rgba[o + 2], rgba[o + 3]);
            }

            texture.SetPixels32(colors);
            texture.Apply(false, false);
            return texture;
        }

        public static string AssetPathToAbsolute(string assetPath)
        {
            var normalized = (assetPath ?? string.Empty).Replace('\\', '/');
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("Assets/".Length);
            return Path.GetFullPath(Path.Combine(Application.dataPath, normalized));
        }

        private static void EnsureAssetFolders(string folderPath)
        {
            var normalizedPath = (folderPath ?? string.Empty).Replace('\\', '/');
            if (string.IsNullOrEmpty(normalizedPath) ||
                normalizedPath == "Assets" ||
                AssetDatabase.IsValidFolder(normalizedPath))
                return;

            var parent = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/');
            var folderName = Path.GetFileName(normalizedPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
                throw new IOException("Cannot create asset folder: " + folderPath);

            EnsureAssetFolders(parent);
            if (!AssetDatabase.IsValidFolder(normalizedPath) &&
                string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, folderName)))
                throw new IOException("Could not create asset folder: " + normalizedPath);
        }

        private static string CombineErrors(string prior, string next)
        {
            if (string.IsNullOrEmpty(prior))
                return next;
            if (string.IsNullOrEmpty(next))
                return prior;
            return prior + " | fallback: " + next;
        }

        private static string Sanitize(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = (name ?? "x").ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                // Also strip characters that break Unity asset paths on Windows hosts
                // even when the current OS allows them in filenames.
                if (c == '/' || c == '\\' || c == ':' || c == '*' || c == '?' ||
                    c == '"' || c == '<' || c == '>' || c == '|' ||
                    Array.IndexOf(invalid, c) >= 0)
                    chars[i] = '_';
            }

            return new string(chars);
        }
    }
}
