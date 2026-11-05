using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
                ".dds", ".bmp", ".tga", ".png", ".jpg", ".jpeg", ".ogg", ".mp3", ".xml"
            };

        private readonly string root;
        private readonly List<GameDataFile> files = new List<GameDataFile>();
        private readonly IReadOnlyList<GameDataFile> readOnlyFiles;
        private readonly Dictionary<string, List<GameDataFile>> filesByRelativePath =
            new Dictionary<string, List<GameDataFile>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<GameDataFile>> filesByName =
            new Dictionary<string, List<GameDataFile>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<GameDataFile>> filesByAlias =
            new Dictionary<string, List<GameDataFile>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<GameDataFile>> aliasesByName =
            new Dictionary<string, List<GameDataFile>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<GameDataFile, string> canonicalPaths = new Dictionary<GameDataFile, string>();
        private readonly GfxTextDecoder pathDecoder;

        public GameDataIndex(string dataRoot, GfxTextDecoder pathDecoder = null)
        {
            if (string.IsNullOrWhiteSpace(dataRoot))
                throw new ArgumentException("A game data root is required.", nameof(dataRoot));

            root = Path.GetFullPath(dataRoot);
            this.pathDecoder = pathDecoder ?? new GfxTextDecoder();
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
            filesByName.Clear();
            filesByAlias.Clear(); aliasesByName.Clear(); canonicalPaths.Clear();

            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(root);
            var scannedDirectories = 0;

            while (pendingDirectories.Count > 0)
            {
                var directory = pendingDirectories.Pop();
                var info = new DirectoryInfo(directory);
                scannedDirectories++;
                foreach (var subdirectory in info.EnumerateDirectories())
                {
                    if (subdirectory.Name != ".git" && (subdirectory.Attributes & FileAttributes.ReparsePoint) == 0)
                        pendingDirectories.Push(subdirectory.FullName);
                }

                // FileInfo carries the attributes returned by directory enumeration;
                // querying File.GetAttributes again costs one filesystem call per asset.
                foreach (var fileInfo in info.EnumerateFiles())
                {
                    if (!SupportedExtensions.Contains(fileInfo.Extension))
                        continue;

                    if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                        continue;

                    var fullPath = fileInfo.FullName;
                    if (!IsUnderRoot(fullPath))
                        continue;

                    var relativePath = fullPath.Substring(root.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Replace(Path.DirectorySeparatorChar, '/')
                        .Replace(Path.AltDirectorySeparatorChar, '/');
                    var entry = new GameDataFile(fullPath, relativePath);
                    files.Add(entry);
                    IndexFileName(entry);

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
            filesByName.Clear();
            filesByAlias.Clear(); aliasesByName.Clear(); canonicalPaths.Clear();
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
                IndexFileName(entry);

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

            // Some unpackers interpret valid GBK byte pairs as UTF-8 (女 -> Ů,
            // 水母 -> ˮĸ). Match a unique reversible alias without renaming source files.
            string canonical = CanonicalPath(normalizedPath);
            if (filesByAlias.TryGetValue(canonical, out var aliasMatches))
                return SelectUnique(aliasMatches, relativePath, out resolved, out error);

            if (!allowUniqueSuffixMatch)
            {
                error = "Resource not found: " + relativePath;
                return false;
            }

            var suffix = "/" + normalizedPath;
            filesByName.TryGetValue(Path.GetFileName(normalizedPath), out var candidates);
            var suffixMatches = (candidates ?? Enumerable.Empty<GameDataFile>()).Where(file =>
                    file.RelativePath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (aliasesByName.TryGetValue(Path.GetFileName(canonical), out var aliasCandidates))
                foreach (var file in aliasCandidates)
                    if (canonicalPaths[file].EndsWith("/" + canonical, StringComparison.OrdinalIgnoreCase) &&
                        !suffixMatches.Contains(file)) suffixMatches.Add(file);
            if (suffixMatches.Count == 0)
            {
                error = "Resource not found: " + relativePath;
                return false;
            }

            return SelectUnique(suffixMatches, relativePath, out resolved, out error);
        }

        private void IndexFileName(GameDataFile file)
        {
            string name = Path.GetFileName(file.RelativePath);
            if (!filesByName.TryGetValue(name, out var list))
                filesByName[name] = list = new List<GameDataFile>();
            list.Add(file);
            string canonical = CanonicalPath(file.RelativePath);
            canonicalPaths[file] = canonical;
            if (!filesByAlias.TryGetValue(canonical, out var aliases))
                filesByAlias[canonical] = aliases = new List<GameDataFile>();
            aliases.Add(file);
            string canonicalName = Path.GetFileName(canonical);
            if (!aliasesByName.TryGetValue(canonicalName, out var namedAliases))
                aliasesByName[canonicalName] = namedAliases = new List<GameDataFile>();
            namedAliases.Add(file);
        }

        private string CanonicalPath(string path)
        {
            var result = new StringBuilder(path.Length);
            for (int i = 0; i < path.Length;)
            {
                if (path[i] < 0x80 || path[i] >= 0x800) { result.Append(path[i++]); continue; }
                int start = i;
                while (i < path.Length && path[i] >= 0x80 && path[i] < 0x800) i++;
                string run = path.Substring(start, i - start), decoded = null;
                try { decoded = pathDecoder.DecodeGbk(Encoding.UTF8.GetBytes(run)); }
                catch (NotSupportedException) { }
                // Only accept complete CJK results, never a lossy replacement character.
                result.Append(!string.IsNullOrEmpty(decoded) && decoded.All(c => c >= 0x3400 && c <= 0x9fff) ? decoded : run);
            }
            return result.ToString();
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
