using System;
using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>Write-only phase. Import/load native dependencies before entering this scope.</summary>
    internal sealed class GameDataAssetBatch : IDisposable
    {
        private static GameDataAssetBatch current;
        private readonly GameDataAssetBatch previous;
        private readonly Dictionary<string, Object> written;
        private bool disposed;
        internal static bool IsActive => current != null;

        public GameDataAssetBatch()
        {
            previous = current;
            written = previous?.written ?? new Dictionary<string, Object>(StringComparer.OrdinalIgnoreCase);
            if (previous == null)
            {
                AssetDatabase.StartAssetEditing();
                if (GameDataImportSession.Metrics != null) GameDataImportSession.Metrics.WriteBatches++;
            }
            current = this;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            current = previous;
            if (previous == null) AssetDatabase.StopAssetEditing();
        }

        // Created objects cannot always be reloaded from the database until StopAssetEditing.
        internal static Object Load(string path)
        {
            if (current != null && current.written.TryGetValue(path, out var value) && value != null)
                return value;
            return AssetDatabase.LoadMainAssetAtPath(path);
        }

        internal static void Create(Object value, string path)
        {
            AssetDatabase.CreateAsset(value, path);
            if (current != null) current.written[path] = value;
        }
    }
}
