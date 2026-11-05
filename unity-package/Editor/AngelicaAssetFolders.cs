using System.IO;
using UnityEditor;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Creates AssetDatabase folders without Unity's "Name 1" / "Name 2" sibling
    /// collision. That happens when the directory already exists on disk (e.g. from
    /// <see cref="Directory.CreateDirectory"/> or a prior import) but
    /// <see cref="AssetDatabase.IsValidFolder"/> is still false — typically inside
    /// <c>AssetDatabase.StartAssetEditing</c> or before the folder's .meta is registered.
    /// Calling <see cref="AssetDatabase.CreateFolder"/> in that state mints a uniquified
    /// sibling instead of adopting the existing directory.
    /// </summary>
    public static class AngelicaAssetFolders
    {
        public static void Ensure(string folderPath)
        {
            var normalizedPath = (folderPath ?? string.Empty).Replace('\\', '/');
            if (string.IsNullOrEmpty(normalizedPath) || normalizedPath == "Assets")
                return;
            if (AssetDatabase.IsValidFolder(normalizedPath))
                return;

            var absolute = AssetPathToAbsolute(normalizedPath);
            if (Directory.Exists(absolute))
            {
                var parentExisting = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(parentExisting) && parentExisting != "Assets")
                    Ensure(parentExisting);
                // Adopt the on-disk folder instead of CreateFolder (which would mint "Name 1").
                AssetDatabase.ImportAsset(normalizedPath);
                if (AssetDatabase.IsValidFolder(normalizedPath))
                    return;
                // ImportAsset may no-op while StartAssetEditing is active; disk path is
                // enough for subsequent CreateAsset/ImportAsset of children.
                return;
            }

            var parent = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/');
            var folderName = Path.GetFileName(normalizedPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
                throw new IOException("Cannot create asset folder: " + folderPath);

            Ensure(parent);
            if (AssetDatabase.IsValidFolder(normalizedPath))
                return;

            // Race: another call may have created the directory on disk.
            if (Directory.Exists(AssetPathToAbsolute(normalizedPath)))
            {
                AssetDatabase.ImportAsset(normalizedPath);
                return;
            }

            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, folderName)))
                throw new IOException("Could not create asset folder: " + normalizedPath);
        }

        public static string AssetPathToAbsolute(string assetPath)
        {
            var normalized = (assetPath ?? string.Empty).Replace('\\', '/');
            if (normalized.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("Assets/".Length);
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, normalized));
        }
    }
}
