using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SilverEcho.ForsakenWorld.Editor
{
    [ScriptedImporter(1, "bmd")]
    public sealed class BmdAssetImporter : ScriptedImporter
    {
        [SerializeField, HideInInspector] private string originalPath;

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
                var shader = AngelicaRenderPipeline.Find(BuildingAssetImporter.HdrpShaderName,
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

            // Prefer the texture next to the source or under the copied game root.
            // GetReferenceToAssetMainObject also registers missing candidates for a later import.
            for (var folder = Parent(context.assetPath); folder == "Assets" || folder.StartsWith("Assets/", StringComparison.Ordinal);
                folder = Parent(folder))
            {
                var candidate = Path.GetRelativePath(".", Path.GetFullPath(Path.Combine(folder, relative))).Replace('\\', '/');
                if (!candidate.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (context.GetReferenceToAssetMainObject(candidate) is Texture2D texture)
                {
                    context.DependsOnArtifact(candidate);
                    return texture;
                }
            }

            return null;
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
