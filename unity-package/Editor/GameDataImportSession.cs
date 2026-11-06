using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>Reuses native imports across graph traversal and nested prefab builders in one drop.</summary>
    internal sealed class GameDataImportSession : IDisposable
    {
        private static GameDataImportSession current;
        private readonly GameDataImportSession previous;
        private readonly Dictionary<string, object> results = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public GameDataImportSession() { previous = current; current = this; }
        public void Dispose() { current = previous; }
        internal static T Once<T>(string key, Func<T> import)
        {
            if (current == null) return import();
            if (current.results.TryGetValue(key, out var value)) return (T)value;
            var result = import();
            current.results[key] = result;
            return result;
        }
    }
}
