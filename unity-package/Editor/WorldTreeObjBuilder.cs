using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using SilverEcho.ForsakenWorld;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Builds trees from the unpacked OBJ tree export (B:\Git\PW146RUUnpackedAssets\trees)
    /// instead of the previous "дерефа" TextMeshPro placeholder. .ecwld tree types store
    /// .spt paths (e.g. "trees\badtree.spt"); the unpacked client ships one folder with
    /// per-tree OBJ parts (&lt;base&gt;_Branch / &lt;base&gt;_Fronds / &lt;base&gt;_Leaves /
    /// &lt;base&gt;_Leaves_Centers .obj), shared defaultbark.dds / compositemap.dds and a
    /// &lt;base&gt;_TextureMap.txt ("defaultbark", "CompositeMap"). OBJ vertices are already
    /// Y-up (trunk at y=0), so the parse is identity-transformed. When no OBJ parts are
    /// found for a tree the caller keeps the old text placeholder and a console warning.
    /// </summary>
    public static class WorldTreeObjBuilder
    {
        /// <summary>Root of the unpacked OBJ tree export (outside the package and game data).</summary>
        public static string TreesRoot = @"B:\Git\PW146RUUnpackedAssets\trees";

        public const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        public const string UrpUnlitShaderName = "Universal Render Pipeline/Unlit";
        private const float LeafCutoff = 0.35f;

        private static readonly string[] PartSuffixes =
        {
            "_Leaves_Centers", "_Leaves", "_Fronds", "_Branch"
        };

        private static readonly Dictionary<string, CachedTree> cache =
            new Dictionary<string, CachedTree>(StringComparer.OrdinalIgnoreCase);

        private static TreeLibrary library;

        /// <summary>Drops parsed OBJ data and the folder index (between Unity test runs).</summary>
        internal static void ClearCache()
        {
            cache.Clear();
            library = null;
        }

        private sealed class TreeLibrary
        {
            public string Root;
            public readonly Dictionary<string, List<string>> Folders =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        internal sealed class CachedPart
        {
            public string Name;
            public bool IsFoliage;
            public Vector3[] Vertices;
            public Vector2[] Uvs;
            public Vector3[] Normals;
            public int[] Triangles;
            public Mesh Mesh;
        }

        internal sealed class CachedTree
        {
            public string BaseName;
            public string Folder;
            public List<CachedPart> Parts; // null once probing found no tree like this
            public string BarkTextureFile;
            public string LeafTextureFile;
            public Material BarkMaterial;
            public Material LeafMaterial;
            public readonly List<string> Warnings = new List<string>();
            public bool WarningsReported;
        }

        /// <summary>
        /// Builds an object tree from the unpacked OBJ export. Returns null when no
        /// &lt;base&gt;_*.obj files exist for the .spt path; warnings then hold the reason.
        /// </summary>
        public static GameObject Build(string sptRelativePath)
        {
            List<string> warnings;
            return Build(sptRelativePath, out warnings);
        }

        public static GameObject Build(string sptRelativePath, out List<string> warnings)
        {
            warnings = new List<string>();
            var cached = FindTree(sptRelativePath);
            if (cached == null || cached.Parts == null)
                return null;
            if (!cached.WarningsReported)
            {
                cached.WarningsReported = true;
                warnings.AddRange(cached.Warnings);
            }

            var shader = FindTreeShader();
            if (shader == null)
                throw new InvalidOperationException(
                    "No shader is available for tree OBJ '" + cached.BaseName + "'.");

            var treeRoot = new GameObject("tree_" + cached.BaseName);
            foreach (var part in cached.Parts)
            {
                if (part.IsFoliage && cached.LeafMaterial == null)
                    cached.LeafMaterial = BuildMaterial(cached, shader, cached.LeafTextureFile, true, warnings);
                if (!part.IsFoliage && cached.BarkMaterial == null)
                    cached.BarkMaterial = BuildMaterial(cached, shader, cached.BarkTextureFile, false, warnings);

                var partObject = new GameObject(part.Name);
                partObject.transform.SetParent(treeRoot.transform, false);
                var filter = partObject.AddComponent<MeshFilter>();
                filter.sharedMesh = GetOrBuildMesh(part);
                var renderer = partObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = part.IsFoliage
                    ? cached.LeafMaterial ?? DefaultMaterial(shader, true)
                    : cached.BarkMaterial ?? DefaultMaterial(shader, false);
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }

            return treeRoot;
        }

        /// <summary>True when the unpacked OBJ export contains a tree for the .spt path.</summary>
        public static bool HasTree(string sptRelativePath)
        {
            var cached = FindTree(sptRelativePath);
            return cached != null && cached.Parts != null;
        }

        private static CachedTree FindTree(string sptRelativePath)
        {
            var baseName = ExtractBaseName(sptRelativePath);
            if (baseName.Length == 0)
                return null;
            var folder = FindTreeFolder(sptRelativePath, baseName);
            // Null folder caches per base name so repeated lookups do not re-probe the disk.
            var key = (folder ?? string.Empty) + "|" + baseName;
            CachedTree cached;
            if (cache.TryGetValue(key, out cached))
                return cached;
            cached = string.IsNullOrEmpty(folder)
                ? new CachedTree { BaseName = baseName } // Parts stays null: tree not unpacked.
                : ParseTree(folder, baseName);
            cache[key] = cached;
            return cached;
        }

        /// <summary>"trees\Sample_Trees\badtree.spt" → "badtree".</summary>
        internal static string ExtractBaseName(string sptRelativePath)
        {
            if (string.IsNullOrEmpty(sptRelativePath))
                return string.Empty;
            return Path.GetFileNameWithoutExtension(sptRelativePath.Replace('\\', '/')) ?? string.Empty;
        }

        /// <summary>
        /// OBJ parts are not always next to the .spt path stored in .ecwld. The unpacked
        /// export keeps most trees in nested folders (精简树木\…\name_Branch.obj), so the
        /// stem is indexed once across the whole tree root.
        /// </summary>
        private static string FindTreeFolder(string sptRelativePath, string baseName)
        {
            var index = Library;
            if (index == null)
                return null;
            List<string> folders;
            if (!index.Folders.TryGetValue(baseName, out folders) || folders.Count == 0)
                return null;
            if (folders.Count == 1)
                return folders[0];

            var wanted = (sptRelativePath ?? string.Empty).Replace('\\', '/');
            var best = folders[0];
            var bestScore = int.MinValue;
            foreach (var folder in folders)
            {
                var relative = folder.Length > index.Root.Length
                    ? folder.Substring(index.Root.Length).Replace('\\', '/').Trim('/')
                    : string.Empty;
                var score = 0;
                if (relative.Length > 0 &&
                    wanted.IndexOf(relative, StringComparison.OrdinalIgnoreCase) >= 0)
                    score = 1000 + relative.Length;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = folder;
                }
            }

            return best;
        }

        private static TreeLibrary Library
        {
            get
            {
                var root = TreesRoot;
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                    return null;
                if (library != null &&
                    string.Equals(library.Root, root, StringComparison.OrdinalIgnoreCase))
                    return library;
                library = ScanLibrary(root);
                return library;
            }
        }

        private static TreeLibrary ScanLibrary(string root)
        {
            var index = new TreeLibrary { Root = root };
            string[] objects;
            try
            {
                objects = Directory.GetFiles(root, "*.obj", SearchOption.AllDirectories);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return index;
            }

            foreach (var path in objects)
            {
                var stem = Path.GetFileNameWithoutExtension(path);
                if (stem.EndsWith("_Leaves_Centers", StringComparison.OrdinalIgnoreCase))
                    continue;
                var baseName = StripPartSuffix(stem);
                if (baseName.Length == 0)
                    continue;
                List<string> folders;
                if (!index.Folders.TryGetValue(baseName, out folders))
                {
                    folders = new List<string>();
                    index.Folders[baseName] = folders;
                }

                var folder = Path.GetDirectoryName(path) ?? root;
                var known = false;
                foreach (var existing in folders)
                {
                    if (string.Equals(existing, folder, StringComparison.OrdinalIgnoreCase))
                    {
                        known = true;
                        break;
                    }
                }

                if (!known)
                    folders.Add(folder);
            }

            return index;
        }

        private static string StripPartSuffix(string stem)
        {
            foreach (var suffix in PartSuffixes)
            {
                if (stem.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                    stem.Length > suffix.Length)
                    return stem.Substring(0, stem.Length - suffix.Length);
            }

            return stem;
        }

        private static CachedTree ParseTree(string folder, string baseName)
        {
            var cached = new CachedTree { BaseName = baseName, Folder = folder, Parts = new List<CachedPart>() };
            try
            {
                var objects = Directory.GetFiles(folder, baseName + "_*.obj");
                // Single-mesh exports (<base>.obj without a part suffix) are supported too.
                var single = Directory.GetFiles(folder, baseName + ".obj");
                string barkTextureName, leafTextureName;
                ReadTextureMap(folder, baseName, out barkTextureName, out leafTextureName);
                var entries = new List<string>(objects.Length + 1);
                entries.AddRange(objects);
                entries.AddRange(single);
                if (entries.Count == 0)
                {
                    cached.Parts = null;
                    return cached;
                }

                cached.BarkTextureFile = ResolveTextureFile(folder, barkTextureName, false);
                cached.LeafTextureFile = ResolveTextureFile(folder, leafTextureName, true);
                foreach (var path in entries)
                {
                    var stem = Path.GetFileNameWithoutExtension(path);
                    // `oak_*.obj` also matches `oak_rt_Branch.obj`; keep only this stem.
                    if (!string.Equals(StripPartSuffix(stem), baseName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    // Point lists exported beside the mesh, not renderable OBJ.
                    if (stem.EndsWith("_Leaves_Centers", StringComparison.OrdinalIgnoreCase))
                        continue;
                    try
                    {
                        var part = ParseObj(path, baseName);
                        if (part != null)
                            cached.Parts.Add(part);
                    }
                    catch (Exception e) when (
                        e is IOException || e is UnauthorizedAccessException || e is InvalidDataException)
                    {
                        cached.Warnings.Add("Tree OBJ " + Path.GetFileName(path) + " is unreadable: " + e.Message);
                    }
                }
                if (cached.Parts.Count == 0)
                    cached.Parts = null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                cached.Warnings.Add("Tree folder " + folder + " is unreadable: " + e.Message);
                cached.Parts = null;
            }

            return cached;
        }

        private static void ReadTextureMap(string folder, string baseName, out string bark, out string leaf)
        {
            bark = null;
            leaf = null;
            try
            {
                var path = Directory.GetFiles(folder, baseName + "_TextureMap.txt");
                if (path.Length == 0)
                    return;
                var lines = File.ReadAllLines(path[0]);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length == 0)
                        continue;
                    if (bark == null)
                        bark = trimmed;
                    else if (leaf == null)
                        leaf = trimmed;
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Missing TextureMap keeps the shared fallback names below.
            }
        }

        /// <summary>
        /// Resolves a TextureMap entry to a file in the tree folder. "CompositeMap" means
        /// the unpacker's packed compositemap.dds; other entries are &lt;name&gt;.dds.
        /// </summary>
        private static string ResolveTextureFile(string folder, string textureName, bool foliage)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(textureName))
            {
                var trimmed = textureName.Replace('\\', '/').Trim();
                if (IsCompositeMapName(trimmed))
                    candidates.Add("compositemap.dds");
                else
                    candidates.Add(Path.GetFileName(EnsureExtension(trimmed, ".dds")));
            }
            candidates.Add(foliage ? "compositemap.dds" : "defaultbark.dds");

            foreach (var candidate in candidates)
            {
                if (candidate.Length == 0)
                    continue;
                try
                {
                    foreach (var found in Directory.GetFiles(folder, "*.dds"))
                        if (string.Equals(Path.GetFileName(found), candidate,
                                StringComparison.OrdinalIgnoreCase))
                            return found;
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>"CompositeMap", "compositemap", "compositemap.dds" → the packed composite.</summary>
        private static bool IsCompositeMapName(string name)
        {
            var withoutExtension = Path.GetFileNameWithoutExtension(name);
            return withoutExtension.Equals("compositemap", StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureExtension(string name, string extension)
        {
            if (Path.GetExtension(name).Length == 0)
                name += extension;
            return name;
        }

        /// <summary>True for SpeedTree leaf cards. Branch and unknown parts are bark.</summary>
        internal static bool IsFoliagePart(string stem)
        {
            if (string.IsNullOrEmpty(stem))
                return false;
            return stem.EndsWith("_Fronds", StringComparison.OrdinalIgnoreCase) ||
                stem.EndsWith("_Frond", StringComparison.OrdinalIgnoreCase) ||
                stem.EndsWith("_Leaves", StringComparison.OrdinalIgnoreCase) ||
                stem.EndsWith("_Leaf", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Parses one OBJ file. Supports v/vt/vn, mixed face orders and negative indices.</summary>
        internal static CachedPart ParseObj(string path, string baseName)
        {
            var part = new CachedPart();
            var name = Path.GetFileNameWithoutExtension(path);
            part.Name = name;
            // Branch is bark. Fronds and Leaves are the leaf cards, including LOD names
            // such as name_rt_Leaves. A bare mesh stays opaque.
            part.IsFoliage = IsFoliagePart(name);

            var sourceVertices = new List<Vector3>(256);
            var sourceUvs = new List<Vector2>(256);
            var sourceNormals = new List<Vector3>(256);
            // Faces may mix v/vt/vn indices, so vertices are unwelded per (v, t, n) group.
            var weldedIndices = new Dictionary<long, int>(256);
            var vertices = new List<Vector3>(256);
            var uvs = new List<Vector2>(256);
            var normals = new List<Vector3>(256);
            var triangles = new List<int>(512);
            var invalidFaces = 0;
            using (var reader = new StreamReader(path, System.Text.Encoding.UTF8, true))
            {
                while (reader.Peek() >= 0)
                {
                    var line = reader.ReadLine();
                    if (line == null)
                        break;
                    if (line.Length == 0 || line[0] == '#')
                        continue;
                    if (line.StartsWith("v ", StringComparison.Ordinal))
                    {
                        var values = ParseNumbers(line.Substring(2));
                        sourceVertices.Add(new Vector3(
                            values.Length > 0 ? values[0] : 0f,
                            values.Length > 1 ? values[1] : 0f,
                            values.Length > 2 ? values[2] : 0f));
                    }
                    else if (line.StartsWith("vt ", StringComparison.Ordinal))
                    {
                        var values = ParseNumbers(line.Substring(3));
                        sourceUvs.Add(new Vector2(
                            values.Length > 0 ? values[0] : 0f,
                            values.Length > 1 ? values[1] : 0f));
                    }
                    else if (line.StartsWith("vn ", StringComparison.Ordinal))
                    {
                        var values = ParseNumbers(line.Substring(3));
                        sourceNormals.Add(new Vector3(
                            values.Length > 0 ? values[0] : 0f,
                            values.Length > 1 ? values[1] : 0f,
                            values.Length > 2 ? values[2] : 0f));
                    }
                    else if (line.StartsWith("f ", StringComparison.Ordinal))
                    {
                        ParseFace(line, sourceVertices.Count, sourceUvs.Count, sourceNormals.Count,
                            sourceVertices, sourceUvs, sourceNormals,
                            weldedIndices, vertices, uvs, normals, triangles, ref invalidFaces);
                    }
                }
            }

            if (triangles.Count < 3 || vertices.Count == 0)
                throw new InvalidDataException("OBJ has no triangles: " + Path.GetFileName(path));
            part.Vertices = vertices.ToArray();
            part.Uvs = sourceUvs.Count > 0 ? uvs.ToArray() : null;
            part.Normals = sourceNormals.Count > 0 ? normals.ToArray() : null;
            part.Triangles = triangles.ToArray();
            return part;
        }

        private static void ParseFace(
            string line,
            int sourceVertexCount, int sourceUvCount, int sourceNormalCount,
            List<Vector3> sourceVertices, List<Vector2> sourceUvs, List<Vector3> sourceNormals,
            Dictionary<long, int> weldedIndices,
            List<Vector3> vertices, List<Vector2> uvs, List<Vector3> normals,
            List<int> triangles, ref int invalidFaces)
        {
            var cornerTokens = line.Substring(2).Trim()
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (cornerTokens.Length < 3)
            {
                invalidFaces++;
                return;
            }
            var corners = new int[cornerTokens.Length, 3];
            var valid = true;
            for (var i = 0; i < cornerTokens.Length; i++)
            {
                var groups = cornerTokens[i].Split('/');
                var vertex = ResolveIndex(groups.Length > 0 ? groups[0] : "0", sourceVertexCount);
                var uv = groups.Length > 1 && groups[1].Length > 0
                    ? ResolveIndex(groups[1], sourceUvCount)
                    : -1;
                var normal = groups.Length > 2 && groups[2].Length > 0
                    ? ResolveIndex(groups[2], sourceNormalCount)
                    : -1;
                // `f 1/1/1` with no vt/vn lines is still a valid triangle.
                if (sourceUvCount == 0)
                    uv = -1;
                if (sourceNormalCount == 0)
                    normal = -1;
                if (vertex < 0 || vertex >= sourceVertexCount ||
                    (uv >= 0 && uv >= sourceUvCount) ||
                    (normal >= 0 && normal >= sourceNormalCount))
                {
                    valid = false;
                    break;
                }
                corners[i, 0] = vertex;
                corners[i, 1] = uv;
                corners[i, 2] = normal;
            }
            if (!valid)
            {
                invalidFaces++;
                return;
            }
            // Fan triangulation covers triangles and quads. Winding stays as authored;
            // foliage materials render two-sided, so a flipped axis cannot hide leaves.
            for (var i = 2; i < cornerTokens.Length; i++)
            {
                triangles.Add(WeldCorner(corners, 0));
                triangles.Add(WeldCorner(corners, i - 1));
                triangles.Add(WeldCorner(corners, i));
            }
            return;

            int WeldCorner(int[,] group, int index)
            {
                var vertex = group[index, 0];
                var uv = group[index, 1];
                var normal = group[index, 2];
                // Unweld: one output vertex per (v, t, n) group so face group indices
                // stay valid even when the OBJ aligns them (SpeedTree exports do).
                var key = ((long)vertex << 40) | ((long)(uv + 1) << 20) | (long)(normal + 1);
                int welded;
                if (weldedIndices.TryGetValue(key, out welded))
                    return welded;
                welded = vertices.Count;
                weldedIndices[key] = welded;
                vertices.Add(sourceVertices[vertex]);
                uvs.Add(uv >= 0 ? sourceUvs[uv] : Vector2.zero);
                normals.Add(normal >= 0 ? sourceNormals[normal] : Vector3.zero);
                return welded;
            }
        }

        private static int ResolveIndex(string token, int count)
        {
            int value;
            if (!int.TryParse(token, out value))
                return -1;
            return value > 0 ? value - 1 : count + value;
        }

        private static float[] ParseNumbers(string body)
        {
            var tokens = body.Trim()
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var numbers = new float[Mathf.Min(tokens.Length, 4)];
            for (var i = 0; i < numbers.Length; i++)
                numbers[i] = float.Parse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture);
            return numbers;
        }

        private static Shader FindTreeShader()
        {
            var shader = AngelicaRenderPipeline.Find(
                AngelicaRenderPipeline.HdrpLit,
                UrpLitShaderName,
                AngelicaRenderPipeline.HdrpUnlit,
                UrpUnlitShaderName);
            if (shader != null)
                return shader;
            return Shader.Find("Standard") ?? Shader.Find("Hidden/InternalErrorShader");
        }

        private static Mesh GetOrBuildMesh(CachedPart part)
        {
            if (part.Mesh != null)
                return part.Mesh;
            var mesh = new Mesh { name = part.Name };
            if (part.Vertices.Length > ushort.MaxValue)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = part.Vertices;
            if (part.Uvs != null)
                mesh.uv = part.Uvs;
            if (part.Normals != null)
                mesh.normals = part.Normals;
            mesh.triangles = part.Triangles;
            var colors = new Color[part.Vertices.Length];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = Color.white;
            mesh.colors = colors;
            if (part.Normals == null)
                mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            part.Mesh = mesh;
            return mesh;
        }

        /// <summary>
        /// URP Lit material with the resolved diffuse texture. Foliage parts use alpha
        /// clip (leaf quads from the composite map) and two-sided rendering because
        /// SpeedTree leaves are single quads without a back half.
        /// </summary>
        private static Material BuildMaterial(
            CachedTree cached, Shader shader, string textureFile, bool foliage, List<string> warnings)
        {
            if (textureFile != null)
            {
                var relative = "trees/" + cached.BaseName + "/" + Path.GetFileName(textureFile);
                var before = warnings.Count;
                var material = BuildMaterialCore(shader, textureFile, relative, foliage, warnings);
                if (material != null)
                    return material;
                if (warnings.Count == before)
                    warnings.Add("Tree " + cached.BaseName + " texture import failed: " + relative);
            }
            return DefaultMaterial(shader, foliage);
        }

        private static Material BuildMaterialCore(
            Shader shader, string textureFile, string relative, bool foliage, List<string> warnings)
        {
            try
            {
                var result = AssembledTextureImporter.ImportOrUpdate(textureFile, relative);
                if (result.Texture == null)
                {
                    warnings.Add("Tree texture import failed: " + relative +
                        (result.Error != null ? " (" + result.Error + ")" : string.Empty));
                    return null;
                }
                return CreateMaterial(shader, result.Texture, textureFile, foliage);
            }
            catch (Exception e) when (
                e is IOException || e is UnauthorizedAccessException ||
                e is InvalidOperationException || e is NotSupportedException)
            {
                warnings.Add("Tree texture import failed: " + relative + " (" + e.Message + ")");
                return null;
            }
        }

        /// <summary>Single-texture URP Lit clone matching the model importer conventions.</summary>
        private static Material CreateMaterial(Shader shader, Texture texture, string sourceName, bool foliage)
        {
            var material = new Material(shader);
            material.name = Path.GetFileNameWithoutExtension(sourceName) + (foliage ? "_leaf" : "_bark");
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_BaseColorMap"))
                material.SetTexture("_BaseColorMap", texture);
            if (material.HasProperty("_Albedo"))
                material.SetTexture("_Albedo", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_Brightness"))
                material.SetFloat("_Brightness", 1f);
            if (material.HasProperty("_AlphaRef"))
                material.SetFloat("_AlphaRef", foliage ? LeafCutoff : 0f);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0f);
            // Foliage quads need both sides; bark shells can be single sided but this is
            // harmless for a handful of trees.
            if (material.HasProperty("_Cull"))
                material.SetFloat("_Cull", (float)CullMode.Off);
            if (material.HasProperty("_DoubleSidedEnable"))
                material.SetFloat("_DoubleSidedEnable", 1f);
            if (foliage)
                ApplyFoliageClip(material);
            return material;
        }

        private static void ApplyFoliageClip(Material material)
        {
            if (material.HasProperty("_AlphaClip"))
                material.SetFloat("_AlphaClip", 1f);
            if (material.HasProperty("_Cutoff"))
                material.SetFloat("_Cutoff", LeafCutoff);
            if (material.HasProperty("_AlphaCutoffEnable"))
                material.SetFloat("_AlphaCutoffEnable", 1f);
            if (material.HasProperty("_AlphaCutoff"))
                material.SetFloat("_AlphaCutoff", LeafCutoff);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)RenderQueue.AlphaTest;
        }

        private static Material DefaultMaterial(Shader shader, bool foliage)
        {
            var material = new Material(shader);
            material.name = foliage ? "TreeFoliageFallback" : "TreeBarkFallback";
            var leafColor = new Color(0.30f, 0.48f, 0.22f, 1f);
            var barkColor = new Color(0.42f, 0.30f, 0.19f, 1f);
            var color = foliage ? leafColor : barkColor;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0f);
            if (foliage)
                ApplyFoliageClip(material);
            if (material.HasProperty("_Cull"))
                material.SetFloat("_Cull", (float)CullMode.Off);
            if (material.HasProperty("_DoubleSidedEnable"))
                material.SetFloat("_DoubleSidedEnable", 1f);
            return material;
        }
    }
}
