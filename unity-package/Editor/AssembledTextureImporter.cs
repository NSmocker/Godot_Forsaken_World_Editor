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
            public bool Unchanged;
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
            DdsDocument header = null, bool forceReimport = false) =>
            ImportOrUpdate(sourceFullPath, sourceRelativePath, header, forceReimport, false);

        internal static ImportResult ImportGfxShaderTexture(
            string sourceFullPath, string sourceRelativePath, DdsDocument header) =>
            ImportOrUpdate(sourceFullPath, sourceRelativePath, header, false, true);

        private static ImportResult ImportOrUpdate(
            string sourceFullPath, string sourceRelativePath, DdsDocument header,
            bool forceReimport, bool encodedSamples)
        {
            var key = GameDataImportSession.ResourceKey(encodedSamples ? "gfx-shader-texture" : "texture",
                sourceFullPath, sourceRelativePath);
            // Supplied headers affect fallback decoding; do not alias different policies in a session.
            if (header != null) key += "|" + header.Width + "|" + header.Height + "|" + header.Compression +
                "|" + header.HeaderByteSize + "|" + header.RgbBitCount + "|" + header.RedMask +
                "|" + header.GreenMask + "|" + header.BlueMask + "|" + header.AlphaMask +
                "|" + header.PitchOrLinearSize + "|" + header.HeaderFlags + "|" + header.LikelyUnityNativeImport;
            // Explicit requests must not be satisfied by an earlier session cache entry.
            if (forceReimport)
                return ImportOrUpdateCore(sourceFullPath, sourceRelativePath, header, true, encodedSamples);
            return GameDataImportSession.Once(key + "|" + forceReimport,
                () => ImportOrUpdateCore(sourceFullPath, sourceRelativePath, header,
                    forceReimport || GameDataImportSession.ForceReimport, encodedSamples));
        }

        private static ImportResult ImportOrUpdateCore(string sourceFullPath, string sourceRelativePath,
            DdsDocument header, bool forceReimport, bool encodedSamples)
        {
            if (GameDataAssetBatch.IsActive)
                throw new InvalidOperationException("Prepare uncached texture dependencies before the asset write batch.");
            var result = new ImportResult();
            if (string.IsNullOrWhiteSpace(sourceFullPath) || !File.Exists(sourceFullPath))
            {
                result.Missing = true;
                result.Error = "Texture file missing: " +
                    (string.IsNullOrEmpty(sourceRelativePath) ? sourceFullPath : sourceRelativePath);
                return result;
            }

            var extension = Path.GetExtension(sourceFullPath) ?? string.Empty;
            var rewritten = ApplyDetectedExtension(sourceFullPath, sourceRelativePath);
            if (!string.Equals(rewritten, sourceRelativePath, StringComparison.Ordinal))
            {
                // Keep the original filename as part of the destination so foo.dds (TGA)
                // cannot overwrite an unrelated foo.tga. Never rename the source data.
                sourceRelativePath = rewritten;
                extension = TextureFileFormat.DetectFile(sourceFullPath) ?? extension;
                header = null;
            }
            var isDds = extension.Equals(".dds", StringComparison.OrdinalIgnoreCase);
            var gfxTga = extension.Equals(".tga", StringComparison.OrdinalIgnoreCase) &&
                (sourceRelativePath ?? string.Empty).Replace('\\', '/').TrimStart('/')
                    .StartsWith("gfx/textures/", StringComparison.OrdinalIgnoreCase);
            if (encodedSamples)
                sourceRelativePath = "GfxEncoded/" + sourceRelativePath;
            var preferNative = !isDds || PreferNativeDds(header);

            if (preferNative)
            {
                try
                {
                    result = ImportNativeFile(sourceFullPath, sourceRelativePath, forceReimport, encodedSamples, gfxTga);
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
                return ImportDecodedDds(sourceFullPath, sourceRelativePath, header, result.Error, encodedSamples);

            return result;
        }

        private static bool PreferNativeDds(DdsDocument header) => header == null ||
            (header.LikelyUnityNativeImport && (header.HasCompleteMipChain ||
                header.Compression == DdsCompression.UncompressedRgb));

        /// <summary>
        /// Copies changed sources and queues their asset paths. Does not import or load.
        /// Call <see cref="GameDataNativeImportBatch.Import"/> before any code that needs the texture.
        /// </summary>
        public static void StageNativeCopy(
            string sourceFullPath,
            string sourceRelativePath,
            bool forceReimport,
            ISet<string> pending,
            DdsDocument header = null)
        {
            if (pending == null || string.IsNullOrWhiteSpace(sourceFullPath) || !File.Exists(sourceFullPath))
                return;
            if (GameDataAssetBatch.IsActive)
                throw new InvalidOperationException(
                    "Import texture dependencies before starting the asset write batch.");

            sourceRelativePath = ApplyDetectedExtension(sourceFullPath, sourceRelativePath);
            if (string.Equals(Path.GetExtension(sourceRelativePath), ".dds", StringComparison.OrdinalIgnoreCase) &&
                !PreferNativeDds(header))
                return;
            var assetPath = GetTextureAssetPath(sourceRelativePath, decodedAsset: false);
            if (pending.Contains(assetPath))
                return;

            var absoluteDest = AssetPathToAbsolute(assetPath);
            var destDir = Path.GetDirectoryName(absoluteDest);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);
            EnsureAssetFolders(Path.GetDirectoryName(assetPath));

            var force = forceReimport || GameDataImportSession.ForceReimport;
            var same = !force && GameDataSourceFiles.SameContent(sourceFullPath, absoluteDest);
            if (same && AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath) != null)
                return;
            if (!same)
                File.Copy(sourceFullPath, absoluteDest, overwrite: true);
            pending.Add(assetPath);
        }

        public static int ImportFiles(IEnumerable<GameDataFile> files, string progressLabel = null)
        {
            var pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (files != null)
            {
                foreach (var file in files)
                {
                    if (file == null)
                        continue;
                    StageNativeCopy(file.FullPath, file.RelativePath, GameDataImportSession.ForceReimport, pending);
                }
            }

            return GameDataNativeImportBatch.Import(pending, progressLabel);
        }

        private static bool TryResolveSlotFile(
            AssembledSurfaceMaterial slot, GameDataIndex index, out GameDataFile file)
        {
            string error;
            return TryResolveSlotFile(slot, index, out file, out error);
        }

        private static bool TryResolveSlotFile(
            AssembledSurfaceMaterial slot,
            GameDataIndex index,
            out GameDataFile file,
            out string error)
        {
            file = null;
            error = null;
            if (slot == null || index == null || !slot.TextureResolved ||
                string.IsNullOrWhiteSpace(slot.TextureSourcePath))
                return false;
            return index.TryResolve(slot.TextureSourcePath, out file, out error) ||
                index.TryResolve(slot.TextureSourcePath, out file, out error, true);
        }

        private static bool TryResolveReflectMap(AssembledModel model, GameDataIndex index, out GameDataFile file)
        {
            file = null;
            if (model == null || index == null || model.Skins == null)
                return false;
            var needsReflect = false;
            foreach (var skin in model.Skins)
            {
                if (skin == null || skin.SurfaceMaterials == null)
                    continue;
                foreach (var slot in skin.SurfaceMaterials)
                {
                    if (slot != null && slot.UsesReflectShader)
                    {
                        needsReflect = true;
                        break;
                    }
                }

                if (needsReflect)
                    break;
            }

            if (!needsReflect)
                return false;
            string error;
            return index.TryResolve(
                AssembledUrpMaterialBuilder.ReflectMapCacheKey, out file, out error, true);
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
            if (!TryResolveSlotFile(slot, index, out file, out resolveError))
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

            var pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (model.Skins != null)
            foreach (var skin in model.Skins)
            {
                if (skin == null || skin.SurfaceMaterials == null)
                    continue;
                foreach (var slot in skin.SurfaceMaterials)
                {
                    GameDataFile staged;
                    if (TryResolveSlotFile(slot, index, out staged))
                        StageNativeCopy(staged.FullPath, staged.RelativePath, false, pending);
                }
            }

            GameDataFile reflect;
            if (TryResolveReflectMap(model, index, out reflect))
                StageNativeCopy(reflect.FullPath, reflect.RelativePath, false, pending);
            GameDataNativeImportBatch.Import(pending);

            if (model.Skins == null)
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

            EnsureReflectMapImported(model, index, cache, ref created, ref updated, ref missing, errors);
            return cache;
        }

        /// <summary>
        /// Godot REFLECT_MAP: Textures/reflect/armorreflect.bmp for r_ weapon materials.
        /// </summary>
        public static void EnsureReflectMapImported(
            AssembledModel model,
            GameDataIndex index,
            Dictionary<string, Texture2D> cache,
            ref int created,
            ref int updated,
            ref int missing,
            List<string> errors)
        {
            if (model == null || cache == null)
                return;
            var needsReflect = false;
            foreach (var skin in model.Skins)
            {
                foreach (var slot in skin.SurfaceMaterials)
                {
                    if (slot != null && slot.UsesReflectShader)
                    {
                        needsReflect = true;
                        break;
                    }
                }

                if (needsReflect)
                    break;
            }

            if (!needsReflect)
                return;

            var key = AssembledUrpMaterialBuilder.ReflectMapCacheKey;
            if (cache.ContainsKey(key))
                return;

            if (index == null)
            {
                missing++;
                if (errors != null)
                    errors.Add(key + ": game data index unavailable.");
                return;
            }

            GameDataFile file;
            string resolveError;
            if (!index.TryResolve(key, out file, out resolveError, allowUniqueSuffixMatch: true))
            {
                missing++;
                if (errors != null)
                    errors.Add(key + ": " + (resolveError ?? "not found"));
                return;
            }

            var import = ImportOrUpdate(file.FullPath, file.RelativePath, header: null);
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
                missing++;
                if (errors != null && !string.IsNullOrEmpty(import.Error))
                    errors.Add(key + ": " + import.Error);
            }
        }

        private static ImportResult ImportNativeFile(string sourceFullPath, string sourceRelativePath,
            bool forceReimport, bool encodedSamples, bool gfxTga)
        {
            var result = new ImportResult
            {
                UsedNativeDds = (Path.GetExtension(sourceRelativePath) ?? string.Empty)
                    .Equals(".dds", StringComparison.OrdinalIgnoreCase),
                AssetPath = GetTextureAssetPath(sourceRelativePath, decodedAsset: false)
            };

            var absoluteDest = AssetPathToAbsolute(result.AssetPath);
            var destDir = Path.GetDirectoryName(absoluteDest);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            var existed = File.Exists(absoluteDest) ||
                AssetDatabase.LoadMainAssetAtPath(result.AssetPath) != null;
            if (!forceReimport && GameDataSourceFiles.SameContent(sourceFullPath, absoluteDest))
            {
                result.Texture = LoadImportedTexture(result.AssetPath, encodedSamples, gfxTga);
                if (result.Texture != null)
                {
                    result.Unchanged = true;
                    if (GameDataImportSession.Metrics != null) GameDataImportSession.Metrics.UnchangedFiles++;
                    return result;
                }
            }
            if (GameDataAssetBatch.IsActive)
                throw new InvalidOperationException("Import texture dependencies before starting the asset write batch.");
            File.Copy(sourceFullPath, absoluteDest, overwrite: true);
            // ImportAsset is enough for LoadAssetAtPath; Refresh() here is O(project)
            // per texture and must not run inside batch StartAssetEditing loops.
            AssetDatabase.ImportAsset(result.AssetPath, ImportAssetOptions.ForceUpdate);
            if (GameDataImportSession.Metrics != null) GameDataImportSession.Metrics.NativeImports++;

            result.Texture = LoadImportedTexture(result.AssetPath, encodedSamples, gfxTga);
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

        private static Texture2D LoadImportedTexture(string assetPath, bool encodedSamples, bool gfxTga)
        {
            if (encodedSamples || gfxTga)
            {
                var importer = AssetImporter.GetAtPath(assetPath);
                if (importer is TextureImporter texture)
                {
                    bool changed = false;
                    if (encodedSamples && texture.sRGBTexture)
                    {
                        texture.sRGBTexture = false;
                        changed = true;
                    }
                    if (gfxTga && (texture.textureCompression != TextureImporterCompression.Uncompressed ||
                        texture.filterMode != FilterMode.Trilinear))
                    {
                        texture.textureCompression = TextureImporterCompression.Uncompressed;
                        texture.filterMode = FilterMode.Trilinear;
                        changed = true;
                    }
                    if (changed)
                        texture.SaveAndReimport();
                }
                else if (encodedSamples && importer != null)
                {
                    // Unity's native IHV DDS importer exposes its sRGB flag through serialization.
                    using var settings = new SerializedObject(importer);
                    var srgb = settings.FindProperty("m_sRGBTexture");
                    if (srgb != null && srgb.propertyType == SerializedPropertyType.Boolean && srgb.boolValue)
                    {
                        srgb.boolValue = false;
                        settings.ApplyModifiedPropertiesWithoutUndo();
                        importer.SaveAndReimport();
                    }
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static ImportResult ImportDecodedDds(
            string sourceFullPath,
            string sourceRelativePath,
            DdsDocument header,
            string priorError,
            bool encodedSamples)
        {
            var result = new ImportResult
            {
                UsedDecodedFallback = true,
                AssetPath = GetTextureAssetPath(sourceRelativePath, decodedAsset: true)
            };

            Texture2D texture;
            try
            {
                texture = DecodeDdsTexture(File.ReadAllBytes(sourceFullPath), sourceRelativePath, header,
                    out var decodeError, encodedSamples);
                if (texture == null)
                {
                    result.Error = CombineErrors(priorError, decodeError);
                    return result;
                }
            }
            catch (Exception exception)
            {
                result.Error = CombineErrors(priorError, exception.Message);
                return result;
            }

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

        internal static Texture2D DecodeDdsTexture(byte[] bytes, string sourceRelativePath, DdsDocument header,
            out string error, bool encodedSamples = false)
        {
            header ??= new DdsHeaderParser().Parse(bytes, sourceRelativePath);
            if (!DdsDxtDecoder.TryDecodeTopMipRgba(bytes, header, out var rgba, out error))
            {
                error ??= "DDS decode failed.";
                return null;
            }

            FlipDdsRows(rgba, header.Width, header.Height);
            int mipCount = header.Compression == DdsCompression.Dxt1 || header.Compression == DdsCompression.Dxt3 ||
                header.Compression == DdsCompression.Dxt5 ? header.MipMapCount : 1;
            if (mipCount > header.ExpectedFullMipChainCount)
            {
                error = "DDS mip count exceeds its dimensions.";
                return null;
            }

            // D3DX keeps the DDS mip levels, including partial chains.
            var mips = new byte[mipCount][];
            mips[0] = rgba;
            for (int mip = 1; mip < mipCount; mip++)
            {
                if (!DdsDxtDecoder.TryDecodeMipRgba(bytes, header, mip, out mips[mip], out error))
                    return null;
                FlipDdsRows(mips[mip], Math.Max(1, header.Width >> mip), Math.Max(1, header.Height >> mip));
            }

            var texture = new Texture2D(header.Width, header.Height, TextureFormat.RGBA32, mipCount, encodedSamples)
            {
                name = Sanitize(Path.GetFileNameWithoutExtension((sourceRelativePath ?? "texture").Replace('\\', '/'))),
                filterMode = mipCount > 1 ? FilterMode.Trilinear : FilterMode.Bilinear
            };
            for (int mip = 0; mip < mipCount; mip++)
                texture.SetPixelData(mips[mip], mip);
            texture.Apply(false, false);
            return texture;
        }

        private static void FlipDdsRows(byte[] rgba, int width, int height)
        {
            // DDS file row zero is the top, while Unity uploads from the bottom row.
            int stride = width * 4;
            var row = new byte[stride];
            for (int y = 0; y < height / 2; y++)
            {
                int top = y * stride;
                int bottom = (height - 1 - y) * stride;
                Buffer.BlockCopy(rgba, top, row, 0, stride);
                Buffer.BlockCopy(rgba, bottom, rgba, top, stride);
                Buffer.BlockCopy(row, 0, rgba, bottom, stride);
            }
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
            AngelicaAssetFolders.Ensure(folderPath);
        }

        // A file named .dds that is actually TGA must not share a destination with a real .dds.
        private static string ApplyDetectedExtension(string sourceFullPath, string sourceRelativePath)
        {
            var extension = Path.GetExtension(sourceFullPath) ?? string.Empty;
            var detected = TextureFileFormat.DetectFile(sourceFullPath);
            if (detected != null && !extension.Equals(detected, StringComparison.OrdinalIgnoreCase) &&
                !(extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) && detected == ".jpg"))
                return (sourceRelativePath ?? string.Empty) + detected;
            return sourceRelativePath;
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
