using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Godot ecm_gfx_panel.gd cache: <c>ecm_gfx_index3.txt</c> with
    /// <c>#root=</c>, <c>#time=</c>, then one relative path per line.
    /// </summary>
    public static class GameDataScanCache
    {
        public const string CacheFileName = "ecm_gfx_index3.txt";

        public sealed class Snapshot
        {
            public string Root = string.Empty;
            public string ScanTime = string.Empty;
            public readonly List<string> RelativePaths = new List<string>();
        }

        /// <summary>
        /// Unity Library/ForsakenWorld/ecm_gfx_index3.txt (parity with Godot
        /// <c>.godot/editor/ecm_gfx_index3.txt</c>). Requires a project Assets path.
        /// </summary>
        public static string DefaultCachePath(string projectAssetsPath)
        {
            if (string.IsNullOrWhiteSpace(projectAssetsPath))
                throw new ArgumentException("projectAssetsPath is required.", nameof(projectAssetsPath));
            var projectRoot = Path.GetFullPath(Path.Combine(projectAssetsPath, ".."));
            return Path.Combine(projectRoot, "Library", "ForsakenWorld", CacheFileName);
        }

        public static bool TryLoad(string cachePath, string expectedRoot, out Snapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrWhiteSpace(cachePath) || !File.Exists(cachePath))
                return false;

            var lines = File.ReadAllLines(cachePath, Encoding.UTF8);
            if (lines.Length == 0)
                return false;

            var rootLine = lines[0].TrimEnd('\r');
            if (!rootLine.StartsWith("#root=", StringComparison.Ordinal))
                return false;

            var cachedRoot = rootLine.Substring("#root=".Length);
            var expected = NormalizeRoot(expectedRoot);
            if (!string.Equals(NormalizeRoot(cachedRoot), expected, StringComparison.OrdinalIgnoreCase))
                return false;

            var result = new Snapshot { Root = cachedRoot };
            var start = 1;
            if (lines.Length > 1 && lines[1].StartsWith("#time=", StringComparison.Ordinal))
            {
                result.ScanTime = lines[1].Substring("#time=".Length).TrimEnd('\r');
                start = 2;
            }

            for (var i = start; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                if (line.Length == 0)
                    continue;
                result.RelativePaths.Add(line.Replace('\\', '/'));
            }

            snapshot = result;
            return true;
        }

        public static void Save(string cachePath, string root, string scanTime, IEnumerable<string> relativePaths)
        {
            if (string.IsNullOrWhiteSpace(cachePath))
                throw new ArgumentException("cachePath is required.", nameof(cachePath));
            if (relativePaths == null)
                throw new ArgumentNullException(nameof(relativePaths));

            var dir = Path.GetDirectoryName(cachePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using (var writer = new StreamWriter(cachePath, false, new UTF8Encoding(false)))
            {
                writer.WriteLine("#root=" + (root ?? string.Empty));
                writer.WriteLine("#time=" + (scanTime ?? string.Empty));
                foreach (var path in relativePaths)
                {
                    if (string.IsNullOrEmpty(path))
                        continue;
                    writer.WriteLine(path.Replace('\\', '/'));
                }
            }
        }

        public static string FormatScanTime(DateTime utcOrLocal)
        {
            // Godot Time.get_datetime_string_from_system(false, true) → "YYYY-MM-DD HH:MM:SS"
            return utcOrLocal.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private static string NormalizeRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
                return string.Empty;
            try
            {
                return Path.GetFullPath(root.Trim().TrimEnd('/', '\\'));
            }
            catch
            {
                return root.Trim().TrimEnd('/', '\\');
            }
        }
    }
}
