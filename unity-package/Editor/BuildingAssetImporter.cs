using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Imports one Angelica .bmd litmodel as Mesh + materials + Prefab under
    /// Assets/ForsakenWorld/Imported/Buildings, matching gfx_importer/angelica_buildings.gd
    /// for a single building (map batch + .ecwld filter stay with the world slice).
    /// </summary>
    public static class BuildingAssetImporter
    {
        public const string ShaderName = "Forsaken World/Angelica LitModel";
        public const string HdrpShaderName = AngelicaRenderPipeline.HdrpLitModel;
        public const string ImportRoot = "Assets/ForsakenWorld/Imported/Buildings";
        public const float DefaultAlphaRef = 84f / 255f;
        public const float DefaultBrightness = 2f;
        // Original LitModel rendering already applies MODULATE2X; no additional boost.
        public const float DefaultLitModelBrightness = 1f;

        public sealed class Result
        {
            public string PrefabPath = string.Empty;
            public string BmdPath = string.Empty;
            public string MeshPath = string.Empty;
            public int Surfaces;
            public int Vertices;
            public int Triangles;
            public bool CollideOnly;
            public bool Skipped;
            public readonly List<string> MaterialPaths = new List<string>();
            public readonly List<string> Warnings = new List<string>();
        }

        public static string GetOutputFolder(string relativePath)
        {
            string name = Path.GetFileNameWithoutExtension(relativePath);
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(
                    relativePath.Replace('\\', '/').ToLowerInvariant()));
                return ImportRoot + "/" + name + "_" +
                    BitConverter.ToString(hash, 0, 6).Replace("-", "").ToLowerInvariant();
            }
        }

        internal static string GetNativeAssetPath(string relativePath) =>
            GetOutputFolder(relativePath) + "/" + Sanitize(Path.GetFileNameWithoutExtension(relativePath)) + ".bmd";

        public static Result Import(
            GameDataFile file,
            GameDataIndex index,
            GfxTextDecoder decoder,
            bool showProgress = true, BmdDocument parsedDocument = null)
        {
            using (new GameDataImportSession())
            using (GameDataImportSession.Stage("Buildings"))
                return ImportCore(file, index, decoder, showProgress, parsedDocument);
        }

        /// <summary>Copy the BMD and let the scripted importer produce the Unity model.</summary>
        public static Result ImportNative(GameDataFile file, GameDataIndex index, GfxTextDecoder decoder)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (index == null) throw new ArgumentNullException(nameof(index));

            using (new GameDataImportSession())
            using (GameDataImportSession.Stage("Native buildings"))
            {
                var bytes = File.ReadAllBytes(file.FullPath);
                var document = new BmdParser(decoder ?? GameDataBrowserWindow.GetGameDataDecoder()).Parse(bytes, file.RelativePath);
                var result = new Result { CollideOnly = document.CollideOnly };
                var bindings = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
                var attempted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!document.CollideOnly)
                foreach (var surface in document.Meshes)
                {
                    if (surface.Indices.Length == 0 || string.IsNullOrWhiteSpace(surface.Texture) ||
                        !attempted.Add(surface.Texture))
                        continue;
                    if (index.TryResolve(surface.Texture, out var source, out var error, true))
                    {
                        var texture = AssembledTextureImporter.ImportOrUpdate(source.FullPath, source.RelativePath);
                        if (texture.Texture != null)
                            bindings.Add(surface.Texture, texture.Texture);
                        else
                            result.Warnings.Add(texture.Error);
                    }
                    else
                    {
                        result.Warnings.Add(error);
                    }
                }

                result.BmdPath = GetNativeAssetPath(file.RelativePath);
                AngelicaAssetFolders.Ensure(Path.GetDirectoryName(result.BmdPath));
                var destination = AngelicaAssetFolders.AssetPathToAbsolute(result.BmdPath);
                bool changed = !File.Exists(destination) || !bytes.AsSpan().SequenceEqual(File.ReadAllBytes(destination));
                if (changed)
                    File.WriteAllBytes(destination, bytes);
                if (changed || AssetImporter.GetAtPath(result.BmdPath) == null)
                    AssetDatabase.ImportAsset(result.BmdPath, ImportAssetOptions.ForceSynchronousImport);

                if (!(AssetImporter.GetAtPath(result.BmdPath) is BmdAssetImporter importer))
                    throw new IOException("Could not import building BMD: " + result.BmdPath);
                importer.SetSourceAndTextures(file.RelativePath, bindings);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(result.BmdPath);
                if (root == null)
                    throw new IOException("Unity did not produce a model for " + result.BmdPath);
                var mesh = root.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh != null)
                {
                    result.Surfaces = mesh.subMeshCount;
                    result.Vertices = mesh.vertexCount;
                    for (int i = 0; i < mesh.subMeshCount; i++)
                        result.Triangles += (int)mesh.GetIndexCount(i) / 3;
                }
                return result;
            }
        }

        private static Result ImportCore(GameDataFile file, GameDataIndex index,
            GfxTextDecoder decoder, bool showProgress, BmdDocument parsedDocument)
        {
            if (file == null || index == null)
                throw new ArgumentNullException();

            var shader = AngelicaRenderPipeline.Find(HdrpShaderName, ShaderName);
            if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException(
                    "Angelica LitModel shader is unavailable. Use a URP project and resolve shader compilation errors.");

            var parser = new BmdParser(decoder);
            var document = parsedDocument ?? parser.Parse(File.ReadAllBytes(file.FullPath), file.RelativePath);
            var assembled = BmdMeshAssembler.Assemble(document);
            var result = new Result { CollideOnly = assembled.CollideOnly };
            result.Warnings.AddRange(assembled.Warnings);

            if (assembled.CollideOnly)
            {
                result.Skipped = true;
                return result;
            }

            if (assembled.Surfaces.Length == 0)
                throw new InvalidDataException(file.RelativePath + ": building has no drawable surfaces.");

            // Copy every texture, import them in one AssetDatabase chunk, then load.
            // CreateMaterial runs inside the write batch and must not ImportAsset.
            var pendingTextures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var surface in assembled.Surfaces)
                if (!string.IsNullOrEmpty(surface.TextureName) &&
                    index.TryResolve(surface.TextureName, out var texture, out _, true))
                    AssembledTextureImporter.StageNativeCopy(
                        texture.FullPath, texture.RelativePath, false, pendingTextures);
            GameDataNativeImportBatch.Import(
                pendingTextures, showProgress ? "Importing building textures" : null);
            foreach (var surface in assembled.Surfaces)
                if (!string.IsNullOrEmpty(surface.TextureName) &&
                    index.TryResolve(surface.TextureName, out var loaded, out _, true))
                    AssembledTextureImporter.ImportOrUpdate(loaded.FullPath, loaded.RelativePath);

            string folder = GetOutputFolder(file.RelativePath);
            GameObject root = null;
            using (new GameDataAssetBatch())
            try
            {
                EnsureFolder(folder);
                Progress(showProgress, "Building mesh", 0.2f);
                var mesh = Save(BuildMesh(assembled, file.Name), folder + "/" + Sanitize(file.Name) + "_mesh.asset");
                result.MeshPath = folder + "/" + Sanitize(file.Name) + "_mesh.asset";
                result.Surfaces = assembled.Surfaces.Length;
                result.Vertices = mesh.vertexCount;
                result.Triangles = 0;
                foreach (var surface in assembled.Surfaces)
                    result.Triangles += surface.Indices.Length / 3;

                Progress(showProgress, "Building materials", 0.5f);
                var materials = new Material[assembled.Surfaces.Length];
                var shared = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < assembled.Surfaces.Length; i++)
                {
                    var surface = assembled.Surfaces[i];
                    var key = (surface.TextureName ?? string.Empty).ToLowerInvariant() +
                        (surface.TwoSided ? "|2" : string.Empty);
                    Material material;
                    if (!shared.TryGetValue(key, out material))
                    {
                        material = CreateMaterial(surface, shader, index, result.Warnings);
                        var matName = Sanitize(
                            string.IsNullOrEmpty(surface.TextureName)
                                ? "litmodel_" + i
                                : Path.GetFileNameWithoutExtension(surface.TextureName)) +
                            (surface.TwoSided ? "_2s" : string.Empty);
                        var matPath = folder + "/" + matName + ".mat";
                        material = Save(material, matPath);
                        result.MaterialPaths.Add(matPath);
                        shared[key] = material;
                    }

                    materials[i] = material;
                }

                Progress(showProgress, "Saving prefab", 0.8f);
                root = CreateBuildingObject(Sanitize(file.DisplayName), assembled, mesh, materials);

                result.PrefabPath = folder + "/" + Sanitize(file.Name) + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, result.PrefabPath, out bool saved);
                if (!saved)
                    throw new IOException("Could not save building prefab: " + result.PrefabPath);
                GameDataImportSession.SaveAssets();
                return result;
            }
            finally
            {
                if (root != null)
                    Object.DestroyImmediate(root);
                if (showProgress)
                    EditorUtility.ClearProgressBar();
            }
        }

        public static Mesh BuildMesh(AssembledBuilding building, string name)
        {
            if (building == null)
                throw new ArgumentNullException(nameof(building));

            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var nightColors = new List<Vector3>();
            var submeshIndices = new List<int>[building.Surfaces.Length];

            for (var s = 0; s < building.Surfaces.Length; s++)
            {
                var surface = building.Surfaces[s];
                var vertexOffset = positions.Count;
                var vertexCount = surface.Positions.Length / 3;
                bool hasNightColors = surface.NightColors != null && surface.NightColors.Length == vertexCount * 3;
                for (var v = 0; v < vertexCount; v++)
                {
                    positions.Add(new Vector3(
                        surface.Positions[v * 3],
                        surface.Positions[v * 3 + 1],
                        surface.Positions[v * 3 + 2]));
                    normals.Add(new Vector3(
                        surface.Normals[v * 3],
                        surface.Normals[v * 3 + 1],
                        surface.Normals[v * 3 + 2]));
                    uvs.Add(new Vector2(
                        surface.Uvs[v * 2],
                        AngelicaGeometry.UnityTextureV(surface.Uvs[v * 2 + 1])));
                    var cr = surface.Colors.Length > v * 3 ? surface.Colors[v * 3] : 1f;
                    var cg = surface.Colors.Length > v * 3 + 1 ? surface.Colors[v * 3 + 1] : 1f;
                    var cb = surface.Colors.Length > v * 3 + 2 ? surface.Colors[v * 3 + 2] : 1f;
                    colors.Add(new Color(cr, cg, cb, 1f));
                    nightColors.Add(hasNightColors
                        ? new Vector3(surface.NightColors[v * 3], surface.NightColors[v * 3 + 1], surface.NightColors[v * 3 + 2])
                        : new Vector3(cr, cg, cb));
                }

                var indices = new List<int>(surface.Indices.Length);
                for (var i = 0; i < surface.Indices.Length; i++)
                    indices.Add(surface.Indices[i] + vertexOffset);
                submeshIndices[s] = indices;
            }

            var mesh = new Mesh
            {
                name = name,
                indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            // TEXCOORD2 (Unity UV3) carries encoded RGB, not texture coordinates.
            // UV0 remains albedo, UV1 is reserved for lightmap coordinates.
            mesh.SetUVs(2, nightColors);
            mesh.subMeshCount = submeshIndices.Length;
            for (var i = 0; i < submeshIndices.Length; i++)
                mesh.SetTriangles(submeshIndices[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }

        internal static GameObject CreateBuildingObject(string name, AssembledBuilding building, Mesh mesh, Material[] materials)
        {
            var root = new GameObject(name);
            var p = building.LocalPosition;
            var q = building.LocalRotation;
            var s = building.LocalScale;
            root.transform.SetLocalPositionAndRotation(new Vector3(p[0], p[1], p[2]), new Quaternion(q[0], q[1], q[2], q[3]));
            root.transform.localScale = new Vector3(s[0], s[1], s[2]);
            if (mesh == null)
                return root;

            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return root;
        }

        private static Material CreateMaterial(
            AssembledBuildingSurface surface, Shader shader, GameDataIndex index, List<string> warnings)
        {
            if (string.IsNullOrEmpty(surface.TextureName))
                return CreateLitModelMaterial(surface, shader, null);

            if (!index.TryResolve(surface.TextureName, out var textureFile, out var error, true))
            {
                warnings.Add("Building texture " + surface.TextureName + " not found" +
                    (string.IsNullOrEmpty(error) ? string.Empty : " (" + error + ")"));
                return CreateLitModelMaterial(surface, shader, null);
            }

            var imported = AssembledTextureImporter.ImportOrUpdate(textureFile.FullPath, textureFile.RelativePath);
            if (imported.Texture == null && !string.IsNullOrEmpty(imported.Error))
                warnings.Add(imported.Error);
            return CreateLitModelMaterial(surface, shader, imported.Texture);
        }

        internal static Material CreateLitModelMaterial(AssembledBuildingSurface surface, Shader shader, Texture2D texture)
        {
            var material = new Material(shader)
            {
                name = string.IsNullOrEmpty(surface.Name) ? "LitModel" : surface.Name
            };
            material.SetFloat("_AlphaRef", DefaultAlphaRef);
            material.SetFloat("_Brightness", DefaultLitModelBrightness);
            material.SetFloat("_HasDayNightColors", 1f);
            material.SetFloat("_Cull", surface.TwoSided ? (float)CullMode.Off : (float)CullMode.Back);
            if (texture != null)
            {
                material.SetTexture("_Albedo", texture);
                material.SetFloat("_AlbedoIsSRGB", GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat) ? 1f : 0f);
            }
            return material;
        }

        private static void Progress(bool show, string text, float value)
        {
            if (show && EditorUtility.DisplayCancelableProgressBar(
                    "Import Angelica Building", text, value))
                throw new OperationCanceledException(
                    "Building import cancelled. Existing generated assets may have been updated; import again to finish.");
        }

        private static void EnsureFolder(string folder)
        {
            AngelicaAssetFolders.Ensure(folder);
        }

        private static T Save<T>(T value, string path) where T : Object
        {
            value.name = Path.GetFileNameWithoutExtension(path);
            var existing = GameDataAssetBatch.Load(path) as T;
            if (existing == null)
            {
                GameDataAssetBatch.Create(value, path);
                return value;
            }

            EditorUtility.CopySerialized(value, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(value);
            return existing;
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "building";
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value.Replace('/', '_').Replace('\\', '_');
        }
    }
}
