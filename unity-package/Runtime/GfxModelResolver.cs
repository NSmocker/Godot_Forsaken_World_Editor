using System;
using System.Collections.Generic;
using System.IO;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Resolves GFX ModelPath the way create_id_160.gd / gfx_sources.model does:
    /// gfx/Models/&lt;ModelPath&gt; (.smd), then the raw relative path.
    /// </summary>
    public sealed class GfxModelResolver
    {
        private readonly GameDataIndex index;

        public GfxModelResolver(GameDataIndex index)
        {
            this.index = index ?? throw new ArgumentNullException(nameof(index));
        }

        public ResolvedModelResource Resolve(string modelPath)
        {
            var raw = (modelPath ?? string.Empty).Trim().Replace('\\', '/');
            var result = new ResolvedModelResource { RequestedPath = raw };
            if (string.IsNullOrEmpty(raw))
            {
                result.Error = "ModelPath is empty.";
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

            Add("gfx/Models/" + raw);
            Add("gfx/models/" + raw);
            Add(raw);
            if (!raw.EndsWith(".smd", StringComparison.OrdinalIgnoreCase))
            {
                Add("gfx/Models/" + raw + ".smd");
                Add(raw + ".smd");
            }

            // Basename under Models when path has folders.
            if (!string.IsNullOrEmpty(fileName) &&
                !string.Equals(fileName, raw, StringComparison.OrdinalIgnoreCase))
            {
                Add("gfx/Models/" + fileName);
                if (!fileName.EndsWith(".smd", StringComparison.OrdinalIgnoreCase))
                    Add("gfx/Models/" + fileName + ".smd");
            }

            ResolvedModelResource last = null;
            foreach (var candidate in candidates)
            {
                GameDataFile file;
                string error;
                if (index.TryResolve(candidate, out file, out error) ||
                    index.TryResolve(candidate, out file, out error, true))
                {
                    return new ResolvedModelResource
                    {
                        RequestedPath = candidate,
                        File = file
                    };
                }

                last = new ResolvedModelResource
                {
                    RequestedPath = candidate,
                    Error = error ?? "not found"
                };
            }

            result.Error = last != null
                ? last.Error
                : "Model '" + raw + "' was not found under gfx/Models.";
            result.RequestedPath = last != null ? last.RequestedPath : candidates[0];
            return result;
        }
    }
}
