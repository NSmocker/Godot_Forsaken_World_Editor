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

            // Complete native imports before pausing AssetDatabase for generated assets.
            foreach (var surface in assembled.Surfaces)
                if (!string.IsNullOrEmpty(surface.TextureName) &&
                    index.TryResolve(surface.TextureName, out var texture, out _, true))
                    AssembledTextureImporter.ImportOrUpdate(texture.FullPath, texture.RelativePath);

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
                root = new GameObject(Sanitize(file.DisplayName));
                root.transform.localPosition = new Vector3(
                    assembled.LocalPosition[0],
                    assembled.LocalPosition[1],
                    assembled.LocalPosition[2]);
                root.transform.localRotation = new Quaternion(
                    assembled.LocalRotation[0],
                    assembled.LocalRotation[1],
                    assembled.LocalRotation[2],
                    assembled.LocalRotation[3]);
                root.transform.localScale = new Vector3(
                    assembled.LocalScale[0],
                    assembled.LocalScale[1],
                    assembled.LocalScale[2]);
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

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

        private static Material CreateMaterial(
            AssembledBuildingSurface surface,
            Shader shader,
            GameDataIndex index,
            List<string> warnings)
        {
            if (shader == null)
                shader = Shader.Find("Hidden/InternalErrorShader");

            if (shader == null)
                throw new System.InvalidOperationException("No shader for material.");

            var material = new Material(shader)
            {
                name = string.IsNullOrEmpty(surface.Name) ? "LitModel" : surface.Name
            };
            material.SetFloat("_AlphaRef", DefaultAlphaRef);
            material.SetFloat("_Brightness", DefaultLitModelBrightness);
            material.SetFloat("_HasDayNightColors", 1f);
            material.SetFloat("_Cull", surface.TwoSided ? (float)CullMode.Off : (float)CullMode.Back);

            if (string.IsNullOrEmpty(surface.TextureName))
                return material;

            if (!index.TryResolve(surface.TextureName, out var textureFile, out var error, true))
            {
                warnings.Add("Building texture " + surface.TextureName + " not found" +
                    (string.IsNullOrEmpty(error) ? string.Empty : " (" + error + ")"));
                return material;
            }

            var imported = AssembledTextureImporter.ImportOrUpdate(
                textureFile.FullPath,
                textureFile.RelativePath);
            if (imported.Texture != null)
            {
                material.SetTexture("_Albedo", imported.Texture);
                material.SetFloat("_AlbedoIsSRGB", GraphicsFormatUtility.IsSRGBFormat(imported.Texture.graphicsFormat) ? 1f : 0f);
            }
            else if (!string.IsNullOrEmpty(imported.Error))
                warnings.Add(imported.Error);
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
