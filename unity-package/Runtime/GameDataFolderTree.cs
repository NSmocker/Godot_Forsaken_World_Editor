using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Godot ecm_gfx_panel.gd folder tree: nested dirs + files, lazy fill metadata,
    /// optional expand-all when search hit count ≤ ExpandLimit.
    /// </summary>
    public sealed class GameDataFolderTree
    {
        public const int ExpandLimit = 300;

        public sealed class FolderNode
        {
            public readonly SortedDictionary<string, FolderNode> Dirs =
                new SortedDictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase);
            public readonly List<string> Files = new List<string>();
            public int Count;
        }

        public sealed class BuildResult
        {
            public FolderNode Root = new FolderNode();
            public int EcmCount;
            public int GfxCount;
            public int MapCount;
            public int ShownCount;
            public bool ExpandAll;
        }

        /// <summary>
        /// Build a tree from relative paths (forward slashes).
        /// <paramref name="extensionFilter"/> empty = all; else match file extension (no dot).
        /// <paramref name="query"/> substring match on relative path (case-insensitive).
        /// </summary>
        public static BuildResult Build(
            IEnumerable<string> relativePaths,
            string extensionFilter = "",
            string query = "")
        {
            if (relativePaths == null)
                throw new ArgumentNullException(nameof(relativePaths));

            var result = new BuildResult();
            var extFilter = (extensionFilter ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
            var q = (query ?? string.Empty).Trim().ToLowerInvariant();

            foreach (var raw in relativePaths)
            {
                if (string.IsNullOrEmpty(raw))
                    continue;
                var path = raw.Replace('\\', '/');
                var lower = path.ToLowerInvariant();
                var ext = ExtensionOf(lower);
                if (extFilter.Length > 0 && ext != extFilter)
                    continue;
                if (q.Length > 0 && lower.IndexOf(q, StringComparison.Ordinal) < 0)
                    continue;

                switch (ext)
                {
                    case "ecm":
                        result.EcmCount++;
                        break;
                    case "gfx":
                        result.GfxCount++;
                        break;
                    case "trn2":
                        result.MapCount++;
                        break;
                }

                result.ShownCount++;
                var parts = path.Split('/');
                var node = result.Root;
                for (var k = 0; k < parts.Length - 1; k++)
                {
                    FolderNode child;
                    if (!node.Dirs.TryGetValue(parts[k], out child))
                    {
                        child = new FolderNode();
                        node.Dirs[parts[k]] = child;
                    }

                    node = child;
                    node.Count++;
                }

                node.Files.Add(parts[parts.Length - 1]);
            }

            foreach (var folder in EnumerateFolders(result.Root))
                folder.Files.Sort(StringComparer.OrdinalIgnoreCase);

            result.ExpandAll = q.Length > 0 && result.ShownCount <= ExpandLimit;
            return result;
        }

        private static IEnumerable<FolderNode> EnumerateFolders(FolderNode root)
        {
            var stack = new Stack<FolderNode>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var n = stack.Pop();
                yield return n;
                foreach (var child in n.Dirs.Values)
                    stack.Push(child);
            }
        }

        private static string ExtensionOf(string lowerPath)
        {
            var slash = lowerPath.LastIndexOf('/');
            var name = slash >= 0 ? lowerPath.Substring(slash + 1) : lowerPath;
            var dot = name.LastIndexOf('.');
            return dot >= 0 ? name.Substring(dot + 1) : string.Empty;
        }
    }
}
