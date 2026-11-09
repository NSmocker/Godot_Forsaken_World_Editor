using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>Imports a batch through one closure, then builds the requested roots.</summary>
    public static class GameDataDropImporter
    {
        public const string ImportRoot = "Assets/ForsakenWorld/Imported";
        public sealed class Result
        {
            public GameDataDependencyGraph Graph;
            public readonly List<Object> Assets = new List<Object>();
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Warnings = new List<string>();
            public int ImportedFiles;
            public bool Cancelled;
        }

        public static Result Import(GameDataIndex index, IEnumerable<string> paths,
            GfxTextDecoder decoder = null, bool showProgress = true)
        {
            decoder = decoder ?? GameDataBrowserWindow.GetGameDataDecoder();
            var result = new Result { Graph = new GameDataDependencyGraph(index, decoder) };
            var assets = new Dictionary<GameDataDependencyGraph.Node, Object>();
            using (new GameDataImportSession())
            try
            {
                foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    Progress(showProgress, "Resolving " + Path.GetFileName(path), 0);
                    try { result.Graph.AddRoot(path); }
                    catch (Exception e) when (IsImportError(e)) { result.Errors.Add(path + ": " + e.Message); }
                }
                var graph = result.Graph;
                for (var i = 0; i < graph.ImportOrder.Count; i++)
                {
                    var node = graph.ImportOrder[i];
                    Progress(showProgress, node.File.DisplayRelativePath, (float)i / Math.Max(1, graph.ImportOrder.Count));
                    if (!node.CanImport) continue;
                    try
                    {
                        // A failed required leaf prevents its parent data asset as well as its prefab.
                        if (node.Required.Any(d => !assets.ContainsKey(d)))
                            throw new InvalidDataException("A required dependency could not be imported.");
                        assets[node] = ImportData(node);
                        result.ImportedFiles++;
                    }
                    catch (Exception e) when (IsImportError(e)) { node.Errors.Add(e.Message); }
                }

                var built = new Dictionary<string, Object>(StringComparer.OrdinalIgnoreCase);
                foreach (var node in graph.Roots)
                {
                    if (!node.CanImport || node.Required.Any(d => !d.CanImport)) continue;
                    try
                    {
                        var key = node.File.RelativePath;
                        // Dropping both world and terrain must not import the map twice.
                        if (node.File.Extension == "ecwld") key = graph.Sibling(node.File, "trn2")?.RelativePath ?? key;
                        if (!built.TryGetValue(key, out var asset))
                        {
                            asset = BuildRoot(node, graph, decoder, result) ?? assets[node];
                            built.Add(key, asset);
                        }
                        if (asset != null && !result.Assets.Contains(asset)) result.Assets.Add(asset);
                    }
                    catch (Exception e) when (IsImportError(e)) { node.Errors.Add(e.Message); }
                }
                AssetDatabase.SaveAssets();
            }
            catch (OperationCanceledException) { result.Cancelled = true; }
            finally
            {
                if (showProgress) EditorUtility.ClearProgressBar();
                foreach (var node in result.Graph.ImportOrder)
                {
                    result.Warnings.AddRange(node.Warnings.Select(w => node.File.RelativePath + ": " + w));
                    var messages = node.Errors.Select(e => node.File.RelativePath + ": " + e);
                    // Failed soft branches are diagnostics, not failed roots.
                    if (result.Graph.Roots.Contains(node)) result.Errors.AddRange(messages);
                    else result.Warnings.AddRange(messages);
                }
            }
            return result;
        }

        private static Object ImportData(GameDataDependencyGraph.Node node)
        {
            var f = node.File;
            switch (f.Extension)
            {
                case "dds": case "bmp": case "tga": case "png": case "jpg": case "jpeg":
                    var texture = AssembledTextureImporter.ImportOrUpdate(f.FullPath, f.RelativePath);
                    if (texture.Texture == null) throw new IOException(texture.Error);
                    return texture.Texture;
                case "wav": case "ogg": case "mp3":
                    var audio = AssembledAudioImporter.ImportGameDataFile(f);
                    if (audio.Clip == null) throw new IOException(audio.Error);
                    return audio.Clip;
                case "smd": return Save(SmdAsset.CreateFrom(f.RelativePath, (SmdDocument)node.Document), DataPath(f, "Models"));
                case "bon": return Save(BonAsset.CreateFrom(f.RelativePath, (BonDocument)node.Document), DataPath(f, "Skeletons"));
                case "ski": return Save(SkiAsset.CreateFrom(f.RelativePath, (SkiDocument)node.Document), DataPath(f, "Skins"));
                case "stck": return Save(StckAsset.CreateFrom(f.RelativePath, (StckDocument)node.Document), DataPath(f, "Tracks"));
                case "ecm": return Save(EcmAsset.CreateFrom(f.RelativePath, (EcmDocument)node.Document), DataPath(f, "ECM"));
                case "gfx": return Save(GfxAsset.CreateFrom(f.RelativePath, (GfxDocument)node.Document), DataPath(f, "GFX"));
                default:
                    // Preserve auxiliary payloads without pretending they are standalone meshes/clips.
                    var path = ImportRoot + "/Sources/" + f.RelativePath + (f.Extension == "ttf" ? "" : ".bytes");
                    AngelicaAssetFolders.Ensure(Path.GetDirectoryName(path));
                    var absolute = AssembledTextureImporter.AssetPathToAbsolute(path);
                    File.Copy(f.FullPath, absolute, true);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (asset == null) throw new IOException("Unity did not import " + path);
                    return asset;
            }
        }

        private static Object BuildRoot(GameDataDependencyGraph.Node node, GameDataDependencyGraph graph,
            GfxTextDecoder decoder, Result result)
        {
            var file = node.File;
            switch (file.Extension)
            {
                case "smd": case "ecm":
                    return ExportModel(node, graph, decoder, result);
                case "bmd":
                    var building = BuildingAssetImporter.Import(file, graph.Index, decoder, false);
                    result.Warnings.AddRange(building.Warnings);
                    return string.IsNullOrEmpty(building.PrefabPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(building.PrefabPath);
                case "trn2": case "ecwld":
                    var terrain = file.Extension == "trn2" ? file : graph.Sibling(file, "trn2");
                    var map = MapAssetImporter.Import(terrain ?? file, graph.Index, decoder, false, terrain != null);
                    result.Warnings.AddRange(map.Warnings);
                    return AssetDatabase.LoadAssetAtPath<GameObject>(map.PrefabPath);
                case "gfx":
                    var gfxBuild = GfxSceneBuilder.BuildFromFile(file.FullPath, decoder, graph.Index);
                    result.Warnings.AddRange(gfxBuild.Warnings);
                    return SaveGfxPrefab(gfxBuild.Root, file);
                case "xml": case "dcf":
                    return BuildUi(node, graph, result);
                default: return null;
            }
        }

        private static Object ExportModel(GameDataDependencyGraph.Node node, GameDataDependencyGraph graph,
            GfxTextDecoder decoder, Result result)
        {
            var ecm = node.Document as EcmDocument;
            var smdFile = node.File;
            if (ecm != null) smdFile = new EcmModelResolver(graph.Index).Prepare(ecm, node.File.RelativePath).SkinModel.File;
            var smd = (SmdDocument)graph.Find(smdFile).Document;
            var resolved = ecm == null ? new ModelResourceResolver(graph.Index).Resolve(smd, smdFile.RelativePath) :
                new EcmModelResolver(graph.Index).ResolveSmdWithAdditionalSkins(smd, smdFile.RelativePath, ecm.AdditionalSkinPaths);
            var skeletonNode = graph.Find(resolved.Skeleton.File);
            if (skeletonNode == null || !skeletonNode.CanImport) throw new InvalidDataException("Skeleton failed to import.");
            var skins = resolved.Skins.Where(s => s.IsResolved && graph.Find(s.File)?.CanImport == true).ToArray();
            var model = new ModelAssembler().Assemble(smd, smdFile.RelativePath,
                (BonDocument)skeletonNode.Document, skeletonNode.File.RelativePath,
                skins.Select(s => (SkiDocument)graph.Find(s.File).Document).ToList(),
                skins.Select(s => s.File.RelativePath).ToList(), resolved);
            var tracks = new List<ActionAnimationSource>();
            foreach (var action in resolved.ActionTracks)
                tracks.Add(new ActionAnimationSource
                {
                    ActionName = action.ActionName, StartFrame = action.StartFrame, EndFrame = action.EndFrame,
                    TrackSourcePath = action.Track.IsResolved ? action.Track.File.RelativePath : action.Track.RequestedPath,
                    Track = action.Track.IsResolved ? graph.Find(action.Track.File)?.Document as StckDocument : null
                });
            if (tracks.Count > 0) new ModelAnimationAssembler().Build(model, tracks);
            new ModelMaterialAssembler(new SkiTextureResolver(graph.Index)).BuildForModel(model, graph.Index);
            var exported = AssembledModelAssetExporter.Export(model, graph.Index, node.File.RelativePath, ecm != null, ecmDocument: ecm);
            result.Warnings.AddRange(model.Warnings);
            result.Warnings.AddRange(exported.Warnings);
            if (!exported.Succeeded) throw new InvalidDataException(exported.Error);
            return AssetDatabase.LoadAssetAtPath<GameObject>(exported.PrefabPath);
        }

        private static Object BuildUi(GameDataDependencyGraph.Node node, GameDataDependencyGraph graph, Result result)
        {
            var ui = graph.CreateUi(node.File);
            var loader = new AuiSurfaceLoader();
            // AuiSurfaceLoader intentionally applies color-key to readable copies.
            loader.LoadOverride = path =>
            {
                try { return TerrainTextureBuilder.LoadSource(graph.FileFromPath(path)); }
                catch (Exception e) when (IsImportError(e)) { result.Warnings.Add(e.Message); return null; }
            };
            GameObject root = null;
            try
            {
                if (node.Document is AuiDialog dialog) root = AngelicaUiBuilder.BuildDialog(dialog, ui, loader);
                else
                {
                    root = new GameObject(node.File.DisplayName);
                    foreach (var entry in ui.ReadLayout(node.File.FullPath))
                    {
                        if (string.IsNullOrEmpty(entry.Path)) continue;
                        var childNode = graph.Find(graph.FileFromPath(entry.Path));
                        if (childNode == null || !childNode.CanImport || !(childNode.Document is AuiDialog child)) continue;
                        var childRoot = AngelicaUiBuilder.BuildDialog(child, ui, loader);
                        childRoot.transform.SetParent(root.transform, false);
                        childRoot.SetActive(entry.Show);
                    }
                }
                var saved = SaveHierarchy(root, node.File);
                root = null; // SaveHierarchy owns cleanup.
                result.Warnings.AddRange(loader.Warnings);
                return saved;
            }
            finally { if (root != null) Object.DestroyImmediate(root); }
        }

        private static Object SaveHierarchy(GameObject root, GameDataFile source)
        {
            if (root == null) throw new InvalidDataException("Could not build " + source.RelativePath);
            var folder = ImportRoot + "/Generated/" + source.RelativePath;
            try
            {
                AngelicaAssetFolders.Ensure(folder);
                using (new GameDataPrefabAssets(root, folder))
                {
                    var prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + source.Name + ".prefab", out var success);
                    if (!success) throw new IOException("Could not save prefab for " + source.RelativePath);
                    return prefab;
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Saves a GFX hierarchy as a reusable Prefab under Imported/GFX with GUID-preserving reimport.
        /// This is the main entry point for drag-and-drop of .gfx files.
        /// </summary>
        private static Object SaveGfxPrefab(GameObject root, GameDataFile source)
        {
            if (root == null) throw new InvalidDataException("Could not build GFX " + source.RelativePath);

            var rel = Path.ChangeExtension(source.RelativePath, null).Replace('\\', '/').TrimEnd('/');
            var name = Path.GetFileNameWithoutExtension(source.RelativePath);
            var folder = ImportRoot + "/GFX/" + Path.GetDirectoryName(rel).TrimEnd('/');

            AngelicaAssetFolders.Ensure(folder);

            var prefabPath = folder + "/" + name + "_gfx.prefab";

            using (new GameDataPrefabAssets(root, folder))
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (existing != null)
                {
                    // Reimport in place — preserves GUID and references
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Object.DestroyImmediate(root);
                    return existing;
                }
                else
                {
                    var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out var success);
                    Object.DestroyImmediate(root);
                    if (!success)
                        throw new IOException("Failed to save GFX prefab: " + prefabPath);
                    return prefab;
                }
            }
        }

        private static string DataPath(GameDataFile file, string category) =>
            ImportRoot + "/" + category + "/" + Path.ChangeExtension(file.RelativePath, ".asset").Replace('\\', '/');

        private static Object Save(Object value, string path)
        {
            try
            {
                var existing = AssetDatabase.LoadMainAssetAtPath(path);
                if (existing != null)
                {
                    if (existing.GetType() != value.GetType()) throw new IOException("Incompatible asset at " + path);
                    EditorUtility.CopySerialized(value, existing);
                    EditorUtility.SetDirty(existing);
                    return existing;
                }
                if (File.Exists(AssembledTextureImporter.AssetPathToAbsolute(path))) throw new IOException("Unrecognized asset at " + path);
                AngelicaAssetFolders.Ensure(Path.GetDirectoryName(path));
                AssetDatabase.CreateAsset(value, path);
                return value;
            }
            finally { if (value != null && !EditorUtility.IsPersistent(value)) Object.DestroyImmediate(value); }
        }

        private static void Progress(bool show, string message, float progress)
        {
            if (show && EditorUtility.DisplayCancelableProgressBar("Forsaken World import", message, progress))
                throw new OperationCanceledException();
        }

        private static bool IsImportError(Exception e) => e is IOException || e is InvalidDataException || e is UnauthorizedAccessException ||
            e is ArgumentException || e is InvalidOperationException || e is NotSupportedException || e is UnityException;
    }
}
