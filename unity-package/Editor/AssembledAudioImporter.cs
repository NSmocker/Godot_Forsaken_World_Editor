using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Host-side audio binding for GFX ID 170 and map PlaceSounds.
    /// Copies resolved .wav / .ogg / .mp3 under Assets/ForsakenWorld/Imported/Sounds so
    /// Unity's AudioImporter yields an AudioClip (same AssetDatabase pattern as
    /// AssembledTextureImporter). Reimport overwrites in place so the .meta GUID is preserved.
    /// Map ambience in Godot (angelica_world._sound) is wav-only; GFX 170 also loads ogg/mp3.
    /// </summary>
    public static class AssembledAudioImporter
    {
        public const string SoundImportRoot = "Assets/ForsakenWorld/Imported/Sounds";

        private static readonly string[] SupportedExtensions = { ".wav", ".ogg", ".mp3" };

        public sealed class ImportResult
        {
            public AudioClip Clip;
            public string AssetPath = string.Empty;
            public bool Created;
            public bool Updated;
            public bool Missing;
            public bool UnsupportedExtension;
            public string Error;
        }

        public static bool IsSupportedExtension(string pathOrExt)
        {
            var ext = pathOrExt ?? string.Empty;
            if (!ext.StartsWith(".", StringComparison.Ordinal))
                ext = Path.GetExtension(ext) ?? string.Empty;
            foreach (var supported in SupportedExtensions)
            {
                if (ext.Equals(supported, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static string GetSoundAssetPath(string sourceRelativePath)
        {
            var relative = (sourceRelativePath ?? "sound").Replace('\\', '/').TrimStart('/');
            var parts = relative.Split('/');
            for (var i = 0; i < parts.Length; i++)
                parts[i] = Sanitize(parts[i]);

            var joined = string.Join("/", parts);
            var extension = Path.GetExtension(joined);
            if (string.IsNullOrEmpty(extension))
                joined += ".wav";
            return SoundImportRoot + "/" + joined;
        }

        public static ImportResult ImportOrUpdate(string sourceFullPath, string sourceRelativePath)
        {
            var result = new ImportResult();
            if (string.IsNullOrWhiteSpace(sourceFullPath) || !File.Exists(sourceFullPath))
            {
                result.Missing = true;
                result.Error = "Sound file missing: " +
                    (string.IsNullOrEmpty(sourceRelativePath) ? sourceFullPath : sourceRelativePath);
                return result;
            }

            var extension = Path.GetExtension(sourceFullPath) ?? string.Empty;
            if (!IsSupportedExtension(extension))
            {
                result.UnsupportedExtension = true;
                result.Error = "Unsupported sound extension '" + extension +
                    "' (expected .wav / .ogg / .mp3).";
                return result;
            }

            try
            {
                return ImportNativeFile(sourceFullPath, sourceRelativePath);
            }
            catch (Exception exception)
            {
                result.Error = exception.Message;
                return result;
            }
        }

        public static ImportResult ImportGameDataFile(GameDataFile file)
        {
            if (file == null)
            {
                return new ImportResult
                {
                    Missing = true,
                    Error = "Sound GameDataFile is null."
                };
            }

            return ImportOrUpdate(file.FullPath, file.RelativePath);
        }

        private static ImportResult ImportNativeFile(string sourceFullPath, string sourceRelativePath)
        {
            var result = new ImportResult
            {
                AssetPath = GetSoundAssetPath(sourceRelativePath)
            };

            var absoluteDest = AssetPathToAbsolute(result.AssetPath);
            var destDir = Path.GetDirectoryName(absoluteDest);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            EnsureAssetFolders(Path.GetDirectoryName(result.AssetPath));

            var existed = File.Exists(absoluteDest) ||
                AssetDatabase.LoadMainAssetAtPath(result.AssetPath) != null;
            File.Copy(sourceFullPath, absoluteDest, overwrite: true);
            AssetDatabase.ImportAsset(result.AssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            result.Clip = AssetDatabase.LoadAssetAtPath<AudioClip>(result.AssetPath);
            if (result.Clip == null)
            {
                result.Error =
                    "Unity did not produce an AudioClip at " + result.AssetPath +
                    " after native import.";
                return result;
            }

            if (existed)
                result.Updated = true;
            else
                result.Created = true;
            return result;
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
