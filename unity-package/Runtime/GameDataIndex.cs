using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SilverEcho.ForsakenWorld
{
    public sealed class GameDataFile
    {
        internal GameDataFile(string fullPath, string relativePath)
        {
            FullPath = fullPath;
            RelativePath = relativePath;
            Extension = Path.GetExtension(relativePath).TrimStart('.').ToLowerInvariant();
        }

        public string FullPath { get; }
        public string RelativePath { get; }
        public string Extension { get; }
        public string Name => Path.GetFileNameWithoutExtension(RelativePath);
    }

    public sealed class GameDataIndex
    {
        private static readonly HashSet<string> SupportedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".bmd", ".bon", ".dat", ".ecbsd", ".ecm", ".ecwld", ".gfx", ".ski", ".smd", ".stck", ".trn2", ".t2bk", ".t2mk", ".t2lm", ".wav",
                // Textures referenced by SKI / materials (DDS policy: Unity-native import).
                ".dds", ".bmp", ".tga", ".png", ".jpg", ".jpeg"
            };

        private readonly string root;
        private readonly List<GameDataFile> files = new List<GameDataFile>();
        private readonly IReadOnlyList<GameDataFile> readOnlyFiles;
        private readonly Dictionary<string, List<GameDataFile>> filesByRelativePath =
            new Dictionary<string, List<GameDataFile>>(StringComparer.OrdinalIgnoreCase);

        public GameDataIndex(string dataRoot)
        {
            if (string.IsNullOrWhiteSpace(dataRoot))
                throw new ArgumentException("A game data root is required.", nameof(dataRoot));

            root = Path.GetFullPath(dataRoot);
            readOnlyFiles = files.AsReadOnly();
        }

        public string Root => root;
        public IReadOnlyList<GameDataFile> Files => readOnlyFiles;
        public string ScanTime { get; set; } = string.Empty;

        public void Scan(Action<int, int> progress = null)
        {
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException("Game data directory not found: " + root);

            files.Clear();
            filesByRelativePath.Clear();

            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(root);
            var scannedDirectories = 0;

            while (pendingDirectories.Count > 0)
            {
                var directory = pendingDirectories.Pop();
                scannedDirectories++;
                foreach (var subdirectory in Directory.GetDirectories(directory))
                {
                    var attributes = File.GetAttributes(subdirectory);
                    if ((attributes & FileAttributes.ReparsePoint) == 0)
                        pendingDirectories.Push(subdirectory);
                }

                foreach (var filePath in Directory.GetFiles(directory))
                {
                    if (!SupportedExtensions.Contains(Path.GetExtension(filePath)))
                        continue;

                    if ((File.GetAttributes(filePath) & FileAttributes.ReparsePoint) != 0)
                        continue;

                    var fullPath = Path.GetFullPath(filePath);
                    if (!IsUnderRoot(fullPath))
                        continue;

                    var relativePath = fullPath.Substring(root.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Replace(Path.DirectorySeparatorChar, '/')
                        .Replace(Path.AltDirectorySeparatorChar, '/');
                    var entry = new GameDataFile(fullPath, relativePath);
                    files.Add(entry);

                    List<GameDataFile> matches;
                    if (!filesByRelativePath.TryGetValue(relativePath, out matches))
                    {
                        matches = new List<GameDataFile>();
                        filesByRelativePath.Add(relativePath, matches);
                    }

                    matches.Add(entry);
                }

                if (scannedDirectories % 250 == 0)
                    progress?.Invoke(scannedDirectories, files.Count);
            }

            files.Sort(CompareFiles);
            progress?.Invoke(scannedDirectories, files.Count);
        }

        /// <summary>
        /// Rebuild index entries from cached relative paths (Godot ecm_gfx_index3 load).
        /// Does not touch the filesystem beyond composing FullPath under Root.
        /// </summary>
        public void ReplaceFromRelativePaths(IEnumerable<string> relativePaths)
        {
            if (relativePaths == null)
                throw new ArgumentNullException(nameof(relativePaths));

            files.Clear();
            filesByRelativePath.Clear();
            foreach (var raw in relativePaths)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;
                var relativePath = raw.Replace('\\', '/').Trim().TrimStart('/');
                if (relativePath.Length == 0)
                    continue;
                var fullPath = Path.GetFullPath(
                    Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                var entry = new GameDataFile(fullPath, relativePath);
                files.Add(entry);

                List<GameDataFile> matches;
                if (!filesByRelativePath.TryGetValue(relativePath, out matches))
                {
                    matches = new List<GameDataFile>();
                    filesByRelativePath.Add(relativePath, matches);
                }

                matches.Add(entry);
            }

            files.Sort(CompareFiles);
        }

        public IReadOnlyList<GameDataFile> Search(string query = "", string extension = "")
        {
            var normalizedQuery = (query ?? string.Empty).Trim();
            var normalizedExtension = NormalizeExtension(extension);
            return files.Where(file =>
                    (normalizedExtension.Length == 0 ||
                        string.Equals(file.Extension, normalizedExtension, StringComparison.OrdinalIgnoreCase)) &&
                    (normalizedQuery.Length == 0 ||
                        file.RelativePath.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToArray();
        }

        public bool TryResolve(
            string relativePath,
            out GameDataFile resolved,
            out string error,
            bool allowUniqueSuffixMatch = false)
        {
            resolved = null;
            error = null;

            string normalizedPath;
            if (!TryNormalizeRelativePath(relativePath, out normalizedPath, out error))
                return false;

            List<GameDataFile> exactMatches;
            if (filesByRelativePath.TryGetValue(normalizedPath, out exactMatches))
                return SelectUnique(exactMatches, relativePath, out resolved, out error);

            if (!allowUniqueSuffixMatch)
            {
                error = "Resource not found: " + relativePath;
                return false;
            }

            var suffix = "/" + normalizedPath;
            var suffixMatches = files.Where(file =>
                    file.RelativePath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (suffixMatches.Count == 0)
            {
                error = "Resource not found: " + relativePath;
                return false;
            }

            return SelectUnique(suffixMatches, relativePath, out resolved, out error);
        }

        private bool IsUnderRoot(string fullPath)
        {
            var prefix = root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? root
                : root + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SelectUnique(
            List<GameDataFile> matches,
            string requestedPath,
            out GameDataFile resolved,
            out string error)
        {
            if (matches.Count == 1)
            {
                resolved = matches[0];
                error = null;
                return true;
            }

            resolved = null;
            error = "Resource path is ambiguous: " + requestedPath;
            return false;
        }

        private static bool TryNormalizeRelativePath(
            string path,
            out string normalized,
            out string error)
        {
            normalized = null;
            error = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Resource path is empty.";
                return false;
            }

            var candidate = path.Trim().Replace('\\', '/');
            if (candidate.StartsWith("/", StringComparison.Ordinal) ||
                candidate.IndexOf(':') >= 0)
            {
                error = "Resource path must be relative: " + path;
                return false;
            }

            var parts = candidate.Split('/');
            var cleanParts = new List<string>(parts.Length);
            foreach (var part in parts)
            {
                if (part.Length == 0 || part == ".")
                    continue;

                if (part == "..")
                {
                    error = "Resource path cannot traverse parent directories: " + path;
                    return false;
                }

                cleanParts.Add(part);
            }

            if (cleanParts.Count == 0)
            {
                error = "Resource path is empty.";
                return false;
            }

            normalized = string.Join("/", cleanParts);
            return true;
        }

        private static string NormalizeExtension(string extension)
        {
            var value = (extension ?? string.Empty).Trim();
            if (value.StartsWith(".", StringComparison.Ordinal))
                value = value.Substring(1);
            return value.ToLowerInvariant();
        }

        private static int CompareFiles(GameDataFile left, GameDataFile right)
        {
            var result = StringComparer.OrdinalIgnoreCase.Compare(
                left.RelativePath,
                right.RelativePath);
            return result != 0
                ? result
                : StringComparer.Ordinal.Compare(left.RelativePath, right.RelativePath);
        }
    }
}
