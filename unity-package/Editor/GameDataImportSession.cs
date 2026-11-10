using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>One cache and save boundary shared by all nested importers in an operation.</summary>
    public sealed class GameDataImportSession : IDisposable
    {
        private static GameDataImportSession current;
        private readonly GameDataImportSession previous;
        private readonly GameDataImportSession owner;
        private readonly Dictionary<string, object> results;
        private readonly Stopwatch timer = Stopwatch.StartNew();
        private readonly string label;
        private bool pendingSave, disposed;
        public readonly ImportStatistics Statistics;
        public static bool ForceReimport => current != null && current.forceReimport;
        private readonly bool forceReimport;

        public GameDataImportSession(string label = null, bool forceReimport = false)
        {
            previous = current;
            owner = previous?.owner ?? this;
            results = previous?.results ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            Statistics = previous?.Statistics ?? new ImportStatistics();
            this.forceReimport = forceReimport || (previous?.forceReimport ?? false);
            this.label = label;
            current = this;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                if (owner == this) FlushSave();
            }
            finally
            {
                current = previous;
                if (owner == this && label != null)
                    UnityEngine.Debug.Log("[Angelica Import] " + label + " (completed or interrupted): " +
                        timer.Elapsed.TotalSeconds.ToString("F2") + "s; " + Statistics + "; stages: " +
                        string.Join(", ", Statistics.StageSeconds.Select(p => p.Key + "=" + p.Value.ToString("F2") + "s")));
            }
        }

        public static void SaveAssets()
        {
            if (current != null) current.owner.pendingSave = true;
            else AssetDatabase.SaveAssets();
        }

        public static void FlushSave()
        {
            if (current == null || !current.owner.pendingSave) return;
            if (GameDataAssetBatch.IsActive)
                throw new InvalidOperationException("Finish the asset batch before saving the import session.");
            AssetDatabase.SaveAssets();
            current.Statistics.SaveCalls++;
            current.owner.pendingSave = false;
        }

        internal static ImportStatistics Metrics => current?.Statistics;

        internal static string ResourceKey(string kind, string source, string destination)
        {
            if (!string.IsNullOrWhiteSpace(source)) source = Path.GetFullPath(source);
            return kind + "|" + (source ?? "").Replace('\\', '/') + "|" +
                (destination ?? "").Replace('\\', '/');
        }

        internal static T Once<T>(string key, Func<T> import)
        {
            if (current == null) return import();
            current.Statistics.Requests++;
            key = (ForceReimport ? "force|" : "normal|") + key;
            if (current.results.TryGetValue(key, out var value))
            {
                current.Statistics.CacheHits++;
                return (T)value;
            }
            var result = import();
            current.results[key] = result;
            return result;
        }

        public sealed class ImportStatistics
        {
            public int Requests, CacheHits, NativeImports, NativeImportBatches, UnchangedFiles, WriteBatches, SaveCalls;
            public readonly Dictionary<string, double> StageSeconds = new Dictionary<string, double>();
            public override string ToString() => "requests=" + Requests + ", cache hits=" + CacheHits +
                ", native imports=" + NativeImports + ", native batches=" + NativeImportBatches +
                ", unchanged=" + UnchangedFiles +
                ", write batches=" + WriteBatches + ", saves=" + SaveCalls;
        }

        public static IDisposable Stage(string name) => new StageTimer(name);
        private sealed class StageTimer : IDisposable
        {
            private readonly string name;
            private readonly ImportStatistics stats = Metrics;
            private readonly Stopwatch watch = Stopwatch.StartNew();
            public StageTimer(string name) { this.name = name; }
            public void Dispose()
            {
                if (stats == null) return;
                stats.StageSeconds.TryGetValue(name, out var prior);
                stats.StageSeconds[name] = prior + watch.Elapsed.TotalSeconds;
            }
        }
    }
}
