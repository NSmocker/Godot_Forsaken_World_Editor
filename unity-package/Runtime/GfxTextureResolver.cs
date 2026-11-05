using System;
using System.Collections.Generic;
using System.IO;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Resolves GFX TexFile the way ecm_importer.gd source_texture does:
    /// gfx/textures/&lt;TexFile&gt;, then the raw relative path.
    /// </summary>
    public sealed class GfxTextureResolver
    {
        private readonly GameDataIndex index;

        public GfxTextureResolver(GameDataIndex index)
        {
            this.index = index ?? throw new ArgumentNullException(nameof(index));
        }

        public ResolvedSkiTexture Resolve(string texFile)
        {
            var raw = (texFile ?? string.Empty).Trim().Replace('\\', '/');
            var result = new ResolvedSkiTexture
            {
                TextureName = raw,
                RequestedRelativePath = raw
            };
            if (string.IsNullOrEmpty(raw))
            {
                result.Resource = new ResolvedModelResource
                {
                    RequestedPath = string.Empty,
                    Error = "TexFile is empty."
                };
                return result;
            }

            if (raw.EndsWith(".sdr", StringComparison.OrdinalIgnoreCase))
            {
                result.Resource = new ResolvedModelResource
                {
                    RequestedPath = raw,
                    Error = "TexFile '" + raw + "' is a shader (.sdr), unsupported."
                };
                return result;
            }

            var fileName = Path.GetFileName(raw);
            var candidates = new List<string>();
            void Add(string p)
            {
                if (string.IsNullOrEmpty(p)) return;
                var n = p.Replace('\\', '/').TrimStart('/');
                if (!candidates.Contains(n))
                    candidates.Add(n);
            }

            Add("gfx/textures/" + raw);
            Add("gfx/textures/" + fileName);
            Add(raw);
            if (!raw.EndsWith(".dds", StringComparison.OrdinalIgnoreCase) &&
                !raw.EndsWith(".tga", StringComparison.OrdinalIgnoreCase) &&
                !raw.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                !raw.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
                !raw.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
            {
                Add("gfx/textures/" + raw + ".dds");
                Add(raw + ".dds");
            }

            ResolvedModelResource last = null;
            foreach (var candidate in candidates)
            {
                GameDataFile file;
                string error;
                if (index.TryResolve(candidate, out file, out error) ||
                    index.TryResolve(candidate, out file, out error, true))
                {
                    result.RequestedRelativePath = candidate;
                    result.Resource = new ResolvedModelResource
                    {
                        RequestedPath = candidate,
                        File = file
                    };
                    AttachDds(result);
                    return result;
                }

                last = new ResolvedModelResource
                {
                    RequestedPath = candidate,
                    Error = error ?? "not found"
                };
            }

            result.Resource = last ?? new ResolvedModelResource
            {
                RequestedPath = candidates[0],
                Error = "Texture '" + raw + "' was not found under gfx/textures."
            };
            return result;
        }

        private static void AttachDds(ResolvedSkiTexture result)
        {
            if (result == null || result.Resource == null || !result.Resource.IsResolved)
                return;
            if (!string.Equals(
                    result.Resource.File.Extension, "dds", StringComparison.OrdinalIgnoreCase))
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
    }
}
