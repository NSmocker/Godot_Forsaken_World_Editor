using System;
using System.Collections.Generic;
using System.IO;

namespace SilverEcho.ForsakenWorld
{
    public sealed class ResolvedSkiTexture
    {
        public string TextureName { get; internal set; } = string.Empty;
        public string RequestedRelativePath { get; internal set; } = string.Empty;
        public ResolvedModelResource Resource { get; internal set; }
        public DdsDocument DdsHeader { get; internal set; }
        public string DdsError { get; internal set; }
        public bool IsResolved => Resource != null && Resource.IsResolved;
    }

    /// <summary>
    /// Resolves SKI texture names the way angelica_model_builder.gd _texture does:
    /// &lt;skiDir&gt;/Textures/&lt;name&gt;, then &lt;skiDir&gt;/Tex_&lt;skiBasename&gt;/&lt;name&gt;.
    /// Missing/ambiguous results are reported through ResolvedModelResource.Error.
    /// </summary>
    public sealed class SkiTextureResolver
    {
        private readonly GameDataIndex index;

        public SkiTextureResolver(GameDataIndex index)
        {
            this.index = index ?? throw new ArgumentNullException(nameof(index));
        }

        public ResolvedSkiTexture Resolve(string textureName, string skiRelativePath)
        {
            if (string.IsNullOrWhiteSpace(textureName))
                throw new ArgumentException("A texture name is required.", nameof(textureName));
            if (string.IsNullOrWhiteSpace(skiRelativePath))
                throw new ArgumentException("A SKI relative path is required.", nameof(skiRelativePath));

            var normalizedSki = Normalize(skiRelativePath);
            var skiDirectory = GetDirectory(normalizedSki);
            var skiBase = Path.GetFileNameWithoutExtension(normalizedSki);
            var name = textureName.Replace('\\', '/');
            var fileName = Path.GetFileName(name);

            var candidates = new[]
            {
                Combine(skiDirectory, "Textures/" + fileName),
                Combine(skiDirectory, "Tex_" + skiBase + "/" + fileName),
                string.IsNullOrEmpty(skiDirectory) ? fileName : Combine(skiDirectory, fileName)
            };

            ResolvedModelResource last = null;
            foreach (var candidate in candidates)
            {
                var resolved = ResolvePath(candidate);
                last = resolved;
                if (resolved.IsResolved)
                {
                    var result = new ResolvedSkiTexture
                    {
                        TextureName = textureName,
                        RequestedRelativePath = candidate,
                        Resource = resolved
                    };
                    AttachDds(result);
                    return result;
                }
            }

            return new ResolvedSkiTexture
            {
                TextureName = textureName,
                RequestedRelativePath = candidates[0],
                Resource = last ?? new ResolvedModelResource
                {
                    RequestedPath = candidates[0],
                    Error = "Texture '" + textureName + "' was not found next to " + normalizedSki + "."
                }
            };
        }

        public List<ResolvedSkiTexture> ResolveAll(SkiDocument ski, string skiRelativePath)
        {
            if (ski == null)
                throw new ArgumentNullException(nameof(ski));
            var list = new List<ResolvedSkiTexture>(ski.TexturePaths.Count);
            foreach (var texture in ski.TexturePaths)
            {
                if (string.IsNullOrWhiteSpace(texture))
                    continue;
                list.Add(Resolve(texture, skiRelativePath));
            }

            return list;
        }

        private ResolvedModelResource ResolvePath(string relativePath)
        {
            var result = new ResolvedModelResource
            {
                RequestedPath = relativePath ?? string.Empty
            };
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                result.Error = "Texture path is empty.";
                return result;
            }

            GameDataFile file;
            string error;
            if (index.TryResolve(relativePath, out file, out error))
            {
                result.File = file;
                return result;
            }

            if (index.TryResolve(relativePath, out file, out error, allowUniqueSuffixMatch: true))
            {
                result.File = file;
                return result;
            }

            result.Error = string.IsNullOrEmpty(error)
                ? "Texture not found: " + relativePath
                : error;
            return result;
        }

        private static void AttachDds(ResolvedSkiTexture result)
        {
            if (result.Resource == null || !result.Resource.IsResolved)
                return;
            if (!string.Equals(result.Resource.File.Extension, "dds", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                result.DdsHeader = new DdsHeaderParser().ParseFile(result.Resource.File.FullPath);
            }
            catch (Exception exception)
            {
                result.DdsError = exception.Message;
            }
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimStart('/');
        }

        private static string GetDirectory(string relativePath)
        {
            var idx = relativePath.LastIndexOf('/');
            return idx <= 0 ? string.Empty : relativePath.Substring(0, idx);
        }

        private static string Combine(string left, string right)
        {
            right = Normalize(right);
            if (string.IsNullOrEmpty(left))
                return right;
            if (string.IsNullOrEmpty(right))
                return left;
            return left.TrimEnd('/') + "/" + right.TrimStart('/');
        }
    }
}
