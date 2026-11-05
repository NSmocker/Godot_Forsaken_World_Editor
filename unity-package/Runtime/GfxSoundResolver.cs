using System;
using System.Collections.Generic;
using System.IO;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Resolves GFX sound Path entries the way create_id_170 / MapAssetImporter do:
    /// sfx/&lt;Path&gt; (.wav / .ogg / .mp3), then the raw relative path.
    /// </summary>
    public sealed class GfxSoundResolver
    {
        private static readonly string[] Extensions = { ".wav", ".ogg", ".mp3" };

        private readonly GameDataIndex index;

        public GfxSoundResolver(GameDataIndex index)
        {
            this.index = index ?? throw new ArgumentNullException(nameof(index));
        }

        public ResolvedModelResource Resolve(string soundPath)
        {
            var raw = (soundPath ?? string.Empty).Trim().Replace('\\', '/');
            var result = new ResolvedModelResource { RequestedPath = raw };
            if (string.IsNullOrEmpty(raw))
            {
                result.Error = "Sound Path is empty.";
                return result;
            }

            var fileName = Path.GetFileName(raw);
            var baseName = Path.GetFileNameWithoutExtension(raw);
            var candidates = new List<string>();
            void Add(string p)
            {
                if (string.IsNullOrEmpty(p)) return;
                var n = p.Replace('\\', '/').TrimStart('/');
                if (!candidates.Contains(n))
                    candidates.Add(n);
            }

            Add("sfx/" + raw);
            Add(raw);
            var hasExt = false;
            foreach (var ext in Extensions)
            {
                if (raw.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    hasExt = true;
                    break;
                }
            }

            if (!hasExt)
            {
                foreach (var ext in Extensions)
                {
                    Add("sfx/" + raw + ext);
                    Add(raw + ext);
                    if (!string.IsNullOrEmpty(baseName))
                        Add("sfx/" + baseName + ext);
                }
            }

            if (!string.IsNullOrEmpty(fileName) &&
                !string.Equals(fileName, raw, StringComparison.OrdinalIgnoreCase))
            {
                Add("sfx/" + fileName);
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
                : "Sound '" + raw + "' was not found under sfx/.";
            result.RequestedPath = last != null ? last.RequestedPath : candidates[0];
            return result;
        }
    }
}
