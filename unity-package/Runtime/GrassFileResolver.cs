using System;
using System.Collections.Generic;
using System.IO;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Finds a grass .mox and the texture A3DMesh joins as &lt;moxDir&gt;/Textures/&lt;name&gt;.
    /// Exact index hit, then unique suffix, then a file that exists under the data root
    /// even when the scan cache predates .mox indexing.
    /// </summary>
    public static class GrassFileResolver
    {
        public static bool TryResolveModel(
            GameDataIndex index,
            string grassFile,
            out GameDataFile file,
            out string error)
        {
            file = null;
            error = "Grass model path is empty.";
            if (index == null || string.IsNullOrWhiteSpace(grassFile))
                return false;
            var raw = grassFile.Replace('\\', '/').Trim().TrimStart('/');
            var name = Path.GetFileName(raw);
            var candidates = new List<string>();
            Add(candidates, raw);
            Add(candidates, "grasses/" + name);
            Add(candidates, "Grasses/" + name);
            return TryAny(index, candidates, "grass model '" + raw + "'", out file, out error);
        }

        public static bool TryResolveTexture(
            GameDataIndex index,
            string moxRelative,
            string textureName,
            out GameDataFile file,
            out string error)
        {
            file = null;
            error = "Grass texture name is empty.";
            if (index == null || string.IsNullOrWhiteSpace(textureName))
                return false;
            var texture = textureName.Replace('\\', '/').Trim().TrimStart('/');
            var fileName = Path.GetFileName(texture);
            var mox = (moxRelative ?? string.Empty).Replace('\\', '/');
            var slash = mox.LastIndexOf('/');
            var moxDir = slash >= 0 ? mox.Substring(0, slash) : string.Empty;
            var candidates = new List<string>();
            Add(candidates, string.IsNullOrEmpty(moxDir) ? "Textures/" + fileName : moxDir + "/Textures/" + fileName);
            Add(candidates, string.IsNullOrEmpty(moxDir) ? fileName : moxDir + "/" + fileName);
            Add(candidates, "grasses/Textures/" + fileName);
            Add(candidates, "Textures/" + fileName);
            if (!string.Equals(texture, fileName, StringComparison.OrdinalIgnoreCase))
                Add(candidates, texture);
            Add(candidates, fileName);
            return TryAny(index, candidates, "grass texture '" + texture + "'", out file, out error);
        }

        private static bool TryAny(
            GameDataIndex index,
            List<string> candidates,
            string label,
            out GameDataFile file,
            out string error)
        {
            file = null;
            error = label + " was not found.";
            foreach (var candidate in candidates)
            {
                if (index.TryResolve(candidate, out file, out error) ||
                    index.TryResolve(candidate, out file, out error, true))
                    return true;
                if (!string.IsNullOrEmpty(error) &&
                    error.IndexOf("ambiguous", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    error = error + " (" + label + ")";
                    return false;
                }

                var onDisk = FileUnderRoot(index, candidate);
                if (onDisk != null)
                {
                    file = onDisk;
                    error = null;
                    return true;
                }
            }

            return false;
        }

        private static void Add(List<string> list, string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            var normalized = path.Replace('\\', '/').TrimStart('/');
            if (!list.Contains(normalized))
                list.Add(normalized);
        }

        private static GameDataFile FileUnderRoot(GameDataIndex index, string relative)
        {
            if (string.IsNullOrEmpty(index.Root) || string.IsNullOrEmpty(relative))
                return null;
            var full = Path.Combine(index.Root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
                return null;
            if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                return null;
            return new GameDataFile(full, relative);
        }
    }
}
