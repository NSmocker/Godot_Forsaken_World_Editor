using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SilverEcho.ForsakenWorld.Editor
{
    [ScriptedImporter(4, "bmd")]
    public sealed class BmdAssetImporter : ScriptedImporter
    {
        [SerializeField, HideInInspector] private string originalPath;
        private const string ShaderFolder = "Packages/com.silverechogames.forsaken-world-editor/Runtime/Shaders/";

        public override void OnImportAsset(AssetImportContext context)
        {
            var modelName = Path.GetFileNameWithoutExtension(context.assetPath);
            BmdDocument document;
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(context.assetPath);
                document = new BmdParser(GameDataBrowserWindow.GetGameDataDecoder())
                    .Parse(bytes, string.IsNullOrEmpty(originalPath) ? GameRelativePath(context.assetPath) : originalPath);
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException ||
                                              exception is UnauthorizedAccessException || exception is NotSupportedException)
            {
                context.LogImportError(exception.Message);
                return;
            }

            var building = BmdMeshAssembler.Assemble(document);
            var data = BmdAsset.Create(document, bytes);
            context.AddObjectToAsset("source", data);
            Mesh mesh = null;
            Material[] materials = null;
            if (building.Surfaces.Length != 0)
            {
                var shaderPath = ShaderFolder + (AngelicaRenderPipeline.IsHdrp
                    ? "AngelicaLitModelHdrp.shader" : "AngelicaLitModel.shader");
                var shader = context.GetReferenceToAssetMainObject(shaderPath) as Shader ??
                    AngelicaRenderPipeline.Find(BuildingAssetImporter.HdrpShaderName,
                        BuildingAssetImporter.ShaderName, "Hidden/InternalErrorShader");
                mesh = BuildingAssetImporter.BuildMesh(building, modelName);
                context.AddObjectToAsset("mesh", mesh);
                materials = BuildMaterials(context, building, shader);
            }
            else if (!building.CollideOnly)
            {
                context.LogImportWarning("Building has no drawable mesh surfaces.");
            }

            var root = BuildingAssetImporter.CreateBuildingObject(modelName, building, mesh, materials);
            var instance = root.AddComponent<LitModelInstance>();
            instance.SourceData = data;
            instance.OriginalBmdPath = document.SourcePath;
            instance.RecordOriginalTransform();
            context.AddObjectToAsset("litmodel", root);
            context.SetMainObject(root);
        }

        private Material[] BuildMaterials(AssetImportContext context, AssembledBuilding building, Shader shader)
        {
            var materials = new Material[building.Surfaces.Length];
            var shared = new Dictionary<(string Texture, bool TwoSided), Material>();
            var textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
            var remaps = GetExternalObjectMap();
            for (int i = 0; i < building.Surfaces.Length; i++)
            {
                var surface = building.Surfaces[i];
                string reference = TextureKey(surface.TextureName);
                var key = (reference, surface.TwoSided);
                if (!shared.TryGetValue(key, out var material))
                {
                    if (!textures.TryGetValue(reference, out var texture))
                    {
                        texture = ResolveTexture(context, surface.TextureName, remaps);
                        textures.Add(reference, texture);
                        if (texture == null && reference.Length != 0)
                            context.LogImportWarning("Building texture not found: " + surface.TextureName);
                    }

                    material = BuildingAssetImporter.CreateLitModelMaterial(surface, shader, texture);
                    context.AddObjectToAsset((surface.TwoSided ? "material:2:" : "material:1:") + reference, material);
                    shared.Add(key, material);
                }
                materials[i] = material;
            }
            return materials;
        }

        private static Texture2D ResolveTexture(AssetImportContext context, string reference,
            Dictionary<SourceAssetIdentifier, Object> remaps)
        {
            var relative = reference?.Trim().Replace('\\', '/') ?? string.Empty;
            if (relative.Length == 0 || Path.IsPathRooted(relative) || relative.Contains(":"))
                return null;

            if (remaps.TryGetValue(TextureIdentifier(reference), out var mapped) && mapped is Texture2D imported)
            {
                var path = AssetDatabase.GetAssetPath(imported);
                if (!string.IsNullOrEmpty(path))
                {
                    context.DependsOnArtifact(path);
                    return imported;
                }
            }

            var localPaths = new List<string>(LocalTexturePaths(context.assetPath, relative));
            if (relative.StartsWith("Building/", StringComparison.OrdinalIgnoreCase))
                localPaths.AddRange(BuildingPackageTexturePaths(relative.Substring("Building/".Length)));

            // Prefer the texture next to the source or under the copied game root.
            // GetReferenceToAssetMainObject also registers missing candidates for a later import.
            foreach (var candidate in localPaths)
            {
                if (LoadTextureDependency(context, candidate) is Texture2D texture)
                    return texture;
            }

            // Map and building imports keep game-relative textures under Imported/Textures.
            foreach (var part in relative.Split('/'))
                if (part.Length == 0 || part == "." || part == "..")
                    return null;

            var importedPath = AssembledTextureImporter.GetTextureAssetPath(relative);
            if (LoadTextureDependency(context, importedPath) is Texture2D projectTexture)
                return projectTexture;

            if (!importedPath.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                return null;

            var decodedPath = AssembledTextureImporter.GetTextureAssetPath(relative, decodedAsset: true);
            if (LoadTextureDependency(context, decodedPath) is Texture2D decoded)
                return decoded;

            // Unity's native DDS importer rejects some partial mip chains used by the client.
            foreach (var candidate in localPaths)
                if (DecodeDdsDependency(context, candidate, relative) is Texture2D localDds)
                    return localDds;
            return DecodeDdsDependency(context, importedPath, relative);
        }

        private static Texture2D LoadTextureDependency(AssetImportContext context, string path)
        {
            var texture = context.GetReferenceToAssetMainObject(path) as Texture2D;
            // The material also stores the texture's sRGB flag.
            if (texture != null)
                context.DependsOnArtifact(path);
            return texture;
        }

        private static IEnumerable<string> LocalTexturePaths(string modelPath, string relative)
        {
            for (var folder = Parent(modelPath); folder == "Assets" || folder.StartsWith("Assets/", StringComparison.Ordinal);
                folder = Parent(folder))
            {
                var candidate = Path.GetRelativePath(".", Path.GetFullPath(Path.Combine(folder, relative))).Replace('\\', '/');
                if (candidate.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    yield return candidate;
            }
        }

        private static uint buildingFolderVersion;
        private static string[] buildingFolders;

        private static IEnumerable<string> BuildingPackageTexturePaths(string relative)
        {
            foreach (var part in relative.Split('/'))
                if (part.Length == 0 || part == "." || part == "..")
                    yield break;

            // A refresh can add, move or remove extraction folders. Share the
            // lookup between models within that refresh, including import workers.
            uint version = AssetDatabase.GlobalArtifactDependencyVersion;
            if (buildingFolders == null || buildingFolderVersion != version)
            {
                var folders = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string query in new[] { "building", "bulding" })
                foreach (string guid in AssetDatabase.FindAssets("t:Folder " + query, new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string name = Path.GetFileName(path);
                    if (name.Equals("building.pck", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("bulding.pck", StringComparison.OrdinalIgnoreCase))
                        folders.Add(path);
                }
                buildingFolders = new List<string>(folders).ToArray();
                buildingFolderVersion = version;
            }

            foreach (string folder in buildingFolders)
            {
                string path = folder + "/" + relative;
                if (path.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                    yield return Path.ChangeExtension(path, ".png");
                yield return path;
            }
        }

        private static Texture2D DecodeDdsDependency(AssetImportContext context, string candidate, string reference)
        {
            if (!File.Exists(candidate))
                return null;

            context.DependsOnSourceAsset(candidate);
            try
            {
                var texture = AssembledTextureImporter.DecodeDdsTexture(
                    File.ReadAllBytes(candidate), reference, null, out var error);
                if (texture != null)
                    context.AddObjectToAsset("dds:" + TextureKey(reference), texture);
                else
                    context.LogImportWarning(candidate + ": " + error);
                return texture;
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException ||
                                              exception is UnauthorizedAccessException || exception is NotSupportedException)
            {
                context.LogImportWarning(candidate + ": " + exception.Message);
                return null;
            }
        }

        internal void SetSourceAndTextures(string source, IReadOnlyDictionary<string, Texture2D> textures)
        {
            var previous = GetExternalObjectMap();
            var desired = new Dictionary<SourceAssetIdentifier, Texture2D>();
            foreach (var entry in textures)
                desired[TextureIdentifier(entry.Key)] = entry.Value;

            var changed = !string.Equals(originalPath, source, StringComparison.Ordinal);
            foreach (var remap in previous)
            {
                if (remap.Key.type == typeof(Texture2D) && !desired.ContainsKey(remap.Key))
                {
                    RemoveRemap(remap.Key);
                    changed = true;
                }
            }
            foreach (var entry in desired)
            {
                if (!previous.TryGetValue(entry.Key, out var old) || old != entry.Value)
                {
                    AddRemap(entry.Key, entry.Value);
                    changed = true;
                }
            }
            if (changed)
            {
                originalPath = source;
                SaveAndReimport();
            }
        }

        private static SourceAssetIdentifier TextureIdentifier(string path) =>
            new SourceAssetIdentifier(typeof(Texture2D), TextureKey(path));

        private static string TextureKey(string path) =>
            (path ?? string.Empty).Trim().Replace('\\', '/').ToLowerInvariant();

        private static string Parent(string path) => Path.GetDirectoryName(path)?.Replace('\\', '/') ?? string.Empty;

        private static string GameRelativePath(string path)
        {
            var index = path.LastIndexOf("/LitModels/", StringComparison.OrdinalIgnoreCase);
            return index < 0 ? path : path.Substring(index + 1);
        }
    }
}
