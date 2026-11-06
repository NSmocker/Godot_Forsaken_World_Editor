using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Imports a full Angelica map from .trn2 + sibling .ecwld/.ecbsd the same way as
    /// gfx_importer/angelica_terrain.gd build + angelica_world.gd / angelica_buildings.gd:
    /// terrain Prefab, buildings filtered by .ecbsd ornaments, water, lights, ECModels,
    /// sound placeholders, and GFX effects (GfxPlayer hierarchy when .gfx resolves; else WorldGfxPlaceholder).
    /// </summary>
    public static class MapAssetImporter
    {
        public const string ImportRoot = "Assets/ForsakenWorld/Imported/Maps";
        public const string WaterShaderName = "Forsaken World/Angelica Water";
        public const string HdrpWaterShaderName = AngelicaRenderPipeline.HdrpWater;
        public const string WaterNormalMap = "textures/water/waternormal/water_normal00.dds";

        public sealed class Result
        {
            public string PrefabPath = string.Empty;
            public string TerrainPrefabPath = string.Empty;
            public int TerrainAreas;
            public int Buildings;
            public int UnusedBuildings;
            public int Effects;
            public int Models;
            public int Trees;
            public int Sounds;
            public int GrassBlades;
            public int Water;
            public bool HasLight;
            public bool MissingWorldFile;
            public bool MissingBsdFile;
            public readonly List<string> BuildingPaths = new List<string>();
            public readonly List<string> Warnings = new List<string>();
        }

        /// <summary>
        /// Replaces the Grass child of an already imported map prefab. Used when only
        /// grass materials need to be rebuilt, without reimporting buildings.
        /// </summary>
        public static int RebuildGrass(
            GameDataFile trn2File,
            GameDataIndex index,
            GfxTextDecoder decoder,
            string prefabPath,
            string folder)
        {
            if (trn2File == null || index == null)
                throw new ArgumentNullException();
            if (string.IsNullOrEmpty(prefabPath) ||
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                throw new FileNotFoundException("Map prefab is missing: " + prefabPath);

            var result = new Result();
            var world = LoadWorld(trn2File, index, decoder, result);
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var stripped = StripMissingScripts(root);
                if (stripped > 0)
                    Debug.Log("A37_GRASS_STRIPPED_MISSING_SCRIPTS " + stripped);
                RestoreLightColors(world, root);

                var existing = root.transform.Find("Grass");
                if (existing != null)
                    Object.DestroyImmediate(existing.gameObject);
                var blades = PlaceGrass(
                    world, index, decoder, trn2File, root, folder, result, false);
                foreach (var warning in result.Warnings)
                    Debug.LogWarning("A37_GRASS_WARN " + warning);

                var grass = root.transform.Find("Grass");
                var sample = grass != null ? grass.GetComponentInChildren<MeshRenderer>() : null;
                var sampleMat = sample != null ? sample.sharedMaterial : null;
                var sampleTex = sampleMat != null ? sampleMat.GetTexture("_Albedo") : null;
                Debug.Log("A37_GRASS_MAT " + (sampleMat != null ? sampleMat.name : "none") +
                    " albedo=" + (sampleTex != null ? sampleTex.name : "missing"));

                bool saved;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out saved);
                if (!saved)
                    throw new IOException("Could not save map prefab: " + prefabPath);
                AssetDatabase.SaveAssets();
                return blades;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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
            GameDataFile trn2File,
            GameDataIndex index,
            GfxTextDecoder decoder,
            bool showProgress = true,
            bool includeTerrain = true)
        {
            if (trn2File == null || index == null)
                throw new ArgumentNullException();

            var result = new Result();
            var mapName = trn2File.Name;
            var folder = GetOutputFolder(trn2File.RelativePath);
            EnsureFolder(folder);
            GameObject root = null;
            try
            {
                Progress(showProgress, "Terrain", 0.05f);
                try
                {
                    if (includeTerrain)
                    {
                        var terrain = TerrainAssetImporter.Import(trn2File, index, decoder, showProgress: false);
                        result.TerrainPrefabPath = terrain.PrefabPath;
                        result.TerrainAreas = terrain.Areas;
                        result.Warnings.AddRange(terrain.Warnings);
                    }
                }
                catch (Exception e) when (
                    e is IOException || e is InvalidDataException || e is InvalidOperationException ||
                    e is NotSupportedException || e is FileNotFoundException || e is OperationCanceledException)
                {
                    throw new InvalidDataException(
                        trn2File.RelativePath + ": terrain import failed (" + e.Message + ")", e);
                }

                root = new GameObject(Sanitize(mapName));
                var terrainPrefab = string.IsNullOrEmpty(result.TerrainPrefabPath) ? null :
                    AssetDatabase.LoadAssetAtPath<GameObject>(result.TerrainPrefabPath);
                if (terrainPrefab != null)
                {
                    var terrainInstance = PrefabUtility.InstantiatePrefab(terrainPrefab) as GameObject;
                    if (terrainInstance == null)
                        terrainInstance = new GameObject("Terrain");
                    terrainInstance.name = "Terrain";
                    terrainInstance.transform.SetParent(root.transform, false);
                }

                Progress(showProgress, "World (.ecwld/.ecbsd)", 0.35f);
                var world = LoadWorld(trn2File, index, decoder, result);

                Progress(showProgress, "Buildings", 0.5f);
                if (includeTerrain)
                    PlaceBuildings(trn2File, index, decoder, root, world, result, showProgress);

                if (world.Document != null)
                {
                    Progress(showProgress, "Water / light / objects", 0.7f);
                    result.Water = PlaceWater(world, index, root, folder, result);
                    result.HasLight = PlaceLight(world, root);
                    var objects = new GameObject("Objects");
                    objects.transform.SetParent(root.transform, false);
                    objects.AddComponent<WorldObjectsController>();
                    var effects = Group(objects, "Effects");
                    var models = Group(objects, "Models");
                    var trees = Group(objects, "Trees");
                    var sounds = Group(objects, "Sounds");
                    result.Effects = PlaceEffects(world.Document.Effects, effects, index, decoder, result);
                    result.Models = PlaceModels(world.Document.Models, models, index, decoder, result, showProgress);
                    result.Trees = PlaceTrees(world.Document.Trees, world.Document.TreeTypes, trees, result);
                    result.Sounds = PlaceSounds(world.Document.Sounds, sounds, index, folder, result);
                    Progress(showProgress, "Grass", 0.9f);
                    result.GrassBlades = PlaceGrass(
                        world, index, decoder, trn2File, root, folder, result, showProgress);
                }

                Progress(showProgress, "Saving map Prefab", 0.95f);
                result.PrefabPath = folder + "/" + Sanitize(mapName) + ".prefab";
                bool saved;
                using (new GameDataPrefabAssets(root, folder + "/Effects"))
                    PrefabUtility.SaveAsPrefabAsset(root, result.PrefabPath, out saved);
                if (!saved)
                    throw new IOException("Could not save map prefab: " + result.PrefabPath);
                AssetDatabase.SaveAssets();
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

        private sealed class WorldBundle
        {
            public EcwldDocument Document;
            public EcbsdDocument Bsd;
            public string EcwldRelativePath = string.Empty;
            public string EcbsdRelativePath = string.Empty;
            public string EcbsdDirectory = string.Empty;
        }

        private static WorldBundle LoadWorld(
            GameDataFile trn2File,
            GameDataIndex index,
            GfxTextDecoder decoder,
            Result result)
        {
            var bundle = new WorldBundle();
            var relativeDir = (Path.GetDirectoryName(trn2File.RelativePath) ?? "").Replace('\\', '/');
            var mapName = trn2File.Name;
            var wldRel = string.IsNullOrEmpty(relativeDir) ? mapName + ".ecwld" : relativeDir + "/" + mapName + ".ecwld";
            if (!index.TryResolve(wldRel, out var wldFile, out _) &&
                !TryResolveBeside(trn2File, mapName + ".ecwld", out wldFile))
            {
                result.MissingWorldFile = true;
                result.Warnings.Add(mapName + ".ecwld not found next to terrain; importing terrain + all litmodel buildings.");
                return bundle;
            }

            bundle.EcwldRelativePath = wldFile.RelativePath;
            try
            {
                bundle.Document = new EcwldParser(decoder).Parse(
                    File.ReadAllBytes(wldFile.FullPath), wldFile.RelativePath);
            }
            catch (InvalidDataException e)
            {
                result.Warnings.Add(e.Message);
                result.MissingWorldFile = true;
                return bundle;
            }

            var bsdRel = string.IsNullOrEmpty(relativeDir) ? mapName + ".ecbsd" : relativeDir + "/" + mapName + ".ecbsd";
            GameDataFile bsdFile;
            if (!index.TryResolve(bsdRel, out bsdFile, out _) &&
                !TryResolveBeside(trn2File, mapName + ".ecbsd", out bsdFile))
            {
                result.MissingBsdFile = true;
                result.Warnings.Add(mapName + ".ecbsd fehlt oder ist keine Block-Datei: Wasser fehlt, alle Gebäude im Ordner");
                return bundle;
            }

            bundle.EcbsdRelativePath = bsdFile.RelativePath;
            bundle.EcbsdDirectory = Path.GetDirectoryName(bsdFile.FullPath) ?? string.Empty;
            byte[] bsdBytes;
            try { bsdBytes = File.ReadAllBytes(bsdFile.FullPath); }
            catch (IOException e)
            {
                result.Warnings.Add(e.Message);
                result.MissingBsdFile = true;
                return bundle;
            }

            Func<string, byte[]> loadBs = name =>
            {
                var resolved = ResolveBesidePath(bundle.EcbsdDirectory, name);
                if (resolved == null)
                {
                    // Also try index by relative path next to ecbsd.
                    var rel = (Path.GetDirectoryName(bsdFile.RelativePath) ?? "").Replace('\\', '/');
                    var candidate = string.IsNullOrEmpty(rel) ? name.Replace('\\', '/') : rel + "/" + name.Replace('\\', '/');
                    if (index.TryResolve(candidate, out var f, out _) ||
                        index.TryResolve(name.Replace('\\', '/'), out f, out _, true))
                        return File.ReadAllBytes(f.FullPath);
                    throw new FileNotFoundException(name + " nicht gefunden, Wasser fehlt");
                }

                return File.ReadAllBytes(resolved);
            };

            bundle.Bsd = new EcbsdParser(decoder).Parse(
                bsdBytes,
                bsdFile.RelativePath,
                bundle.Document.OrnamentRefs,
                bundle.Document.BoxRefs,
                bundle.Document.WaterRefs,
                loadBs,
                bundle.Document.GrassRefs);
            result.Warnings.AddRange(bundle.Bsd.Warnings);
            if (bundle.Bsd.Ornaments.Count == 0 && bundle.Document.OrnamentRefs.Count > 0 &&
                (bsdBytes.Length < EcbsdParser.BsdHeaderEnd))
                result.MissingBsdFile = true;
            return bundle;
        }

        internal static List<GameDataFile> FindBuildingFiles(string mapName, GameDataIndex index, List<string> only)
        {
            var prefix = "litmodels/" + mapName + "/";
            var files = index.Files.Where(f => f.Extension == "bmd" &&
                f.RelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
            if (files.Count == 0)
                files = index.Files.Where(f => f.Extension == "bmd" &&
                    f.RelativePath.IndexOf("/" + prefix, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (only == null) return files;
            var filter = new HashSet<string>(only.Select(p => p.Replace('\\', '/').TrimStart('/')), StringComparer.OrdinalIgnoreCase);
            return files.Where(f => filter.Contains(f.RelativePath.Substring(
                f.RelativePath.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) + prefix.Length)) ||
                filter.Contains(f.Name + ".bmd")).ToList();
        }

        private static void PlaceBuildings(
            GameDataFile trn2File,
            GameDataIndex index,
            GfxTextDecoder decoder,
            GameObject root,
            WorldBundle world,
            Result result,
            bool showProgress)
        {
            var mapName = trn2File.Name;
            List<string> only = null;
            if (world.Bsd != null && !result.MissingBsdFile && world.Document != null)
                only = EcbsdParser.BuildingFiles(world.Bsd);

            // Collect .bmd under litmodels/<map>/ (case-insensitive path match).
            var prefix = "litmodels/" + mapName + "/";
            var all = FindBuildingFiles(mapName, index, null);

            HashSet<string> filter = null;
            if (only != null)
            {
                filter = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var path in only)
                    filter.Add(path.Replace('\\', '/').TrimStart('/'));
            }

            var buildingsRoot = new GameObject("Buildings");
            buildingsRoot.transform.SetParent(root.transform, false);
            var groups = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            var used = 0;
            var unused = 0;

            foreach (var file in all)
            {
                var relUnderMap = file.RelativePath.Replace('\\', '/');
                var idx = relUnderMap.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                {
                    var needle = "/litmodels/" + mapName + "/";
                    idx = relUnderMap.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                        relUnderMap = relUnderMap.Substring(idx + needle.Length);
                }
                else
                    relUnderMap = relUnderMap.Substring(idx + prefix.Length);

                if (filter != null)
                {
                    if (!filter.Contains(relUnderMap) && !filter.Contains(file.Name + ".bmd"))
                    {
                        unused++;
                        continue;
                    }
                }

                Progress(showProgress, "Building " + file.Name, 0.5f);
                BuildingAssetImporter.Result imported;
                try
                {
                    imported = BuildingAssetImporter.Import(file, index, decoder, showProgress: false);
                }
                catch (Exception e) when (
                    e is IOException || e is InvalidDataException || e is InvalidOperationException ||
                    e is NotSupportedException || e is FileNotFoundException)
                {
                    result.Warnings.Add(file.RelativePath + ": " + e.Message);
                    continue;
                }

                result.Warnings.AddRange(imported.Warnings);
                if (imported.Skipped || string.IsNullOrEmpty(imported.PrefabPath))
                    continue;

                var groupName = Path.GetDirectoryName(relUnderMap)?.Replace('\\', '/') ?? "";
                if (string.IsNullOrEmpty(groupName))
                    groupName = "Main";
                Transform group;
                if (!groups.TryGetValue(groupName, out group))
                {
                    var go = new GameObject(Sanitize(groupName.Replace('/', '_')));
                    go.transform.SetParent(buildingsRoot.transform, false);
                    group = go.transform;
                    groups[groupName] = group;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(imported.PrefabPath);
                var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null)
                    instance = new GameObject(Sanitize(file.DisplayName));
                instance.name = Sanitize(file.DisplayName);
                instance.transform.SetParent(group, false);
                result.Buildings++;
                result.BuildingPaths.Add(imported.PrefabPath);
                used++;
            }

            result.UnusedBuildings = unused;
            if (used == 0 && buildingsRoot.transform.childCount == 0)
                Object.DestroyImmediate(buildingsRoot);
        }

        private static int PlaceWater(
            WorldBundle world, GameDataIndex index, GameObject root, string folder, Result result)
        {
            if (world.Bsd == null || world.Bsd.WaterAreas.Count == 0)
                return 0;

            var shader = AngelicaRenderPipeline.Find(HdrpWaterShaderName, WaterShaderName);
            if (shader == null || !shader.isSupported)
            {
                result.Warnings.Add("Angelica Water shader unavailable; water meshes still imported unshaded.");
            }

            EnsureFolder(folder + "/water");
            var waterRoot = new GameObject("Water");
            waterRoot.transform.SetParent(root.transform, false);
            Material sharedMat = null;
            if (shader != null)
            {
                sharedMat = new Material(shader) { name = "Water" };
                sharedMat.SetFloat("_Brightness", BuildingAssetImporter.DefaultBrightness);
                // Normal map optional.
                if (index.TryResolve(WaterNormalMap, out var nm, out _, true))
                {
                    try
                    {
                        var texResult = AssembledTextureImporter.ImportOrUpdate(nm.FullPath, nm.RelativePath);
                        if (texResult.Texture != null)
                            sharedMat.SetTexture("_NormalMap", texResult.Texture);
                        else if (!string.IsNullOrEmpty(texResult.Error))
                            result.Warnings.Add("Wasser-Normalmap: " + texResult.Error);
                    }
                    catch (Exception e) when (
                        e is IOException || e is InvalidOperationException || e is NotSupportedException)
                    {
                        result.Warnings.Add("Wasser-Normalmap: " + e.Message);
                    }
                }
                else
                    result.Warnings.Add("Wasser-Normalmap " + WaterNormalMap + " nicht in den Spieldaten");

                sharedMat = Save(sharedMat, folder + "/water/Water.mat");
            }

            var count = 0;
            foreach (var area in world.Bsd.WaterAreas)
            {
                var meshData = WorldWaterAssembler.Build(area);
                if (meshData == null)
                    continue;
                var mesh = new Mesh
                {
                    name = "water_" + count + "_" + area.Id,
                    indexFormat = meshData.Positions.Length / 3 > 65535
                        ? IndexFormat.UInt32
                        : IndexFormat.UInt16
                };
                var verts = new Vector3[meshData.Positions.Length / 3];
                var norms = new Vector3[verts.Length];
                for (var i = 0; i < verts.Length; i++)
                {
                    verts[i] = new Vector3(
                        meshData.Positions[i * 3],
                        meshData.Positions[i * 3 + 1],
                        meshData.Positions[i * 3 + 2]);
                    norms[i] = new Vector3(
                        meshData.Normals[i * 3],
                        meshData.Normals[i * 3 + 1],
                        meshData.Normals[i * 3 + 2]);
                }

                mesh.vertices = verts;
                mesh.normals = norms;
                mesh.triangles = meshData.Indices;
                mesh.RecalculateBounds();
                mesh = Save(mesh, folder + "/water/" + mesh.name + ".asset");

                var go = new GameObject(mesh.name);
                go.transform.SetParent(waterRoot.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                if (sharedMat != null)
                    renderer.sharedMaterial = sharedMat;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                count++;
            }

            return count;
        }

        private static bool PlaceLight(WorldBundle world, GameObject root)
        {
            if (world.Bsd == null || world.Bsd.DefaultLight == null)
                return false;
            var lightData = world.Bsd.DefaultLight;
            var sun = new GameObject("Sun");
            sun.transform.SetParent(root.transform, false);
            ApplyTransform(sun.transform, lightData.LightDirection, new[] { 0f, 1f, 0f }, new float[3], new[] { 1f, 1f, 1f });
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Rgb(lightData.DayDirectionalRgb);

            var env = new GameObject("Environment");
            env.transform.SetParent(root.transform, false);
            ApplyLightColors(lightData, sun, env);
            var envColors = env.GetComponent<WorldLightColors>();
            RenderSettings.ambientLight = envColors.DayAmbient;
            RenderSettings.ambientSkyColor = envColors.DayBackground;
            return true;
        }

        private static void RestoreLightColors(WorldBundle world, GameObject root)
        {
            if (world == null || world.Bsd == null || world.Bsd.DefaultLight == null || root == null)
                return;
            var sun = root.transform.Find("Sun");
            var env = root.transform.Find("Environment");
            ApplyLightColors(
                world.Bsd.DefaultLight,
                sun != null ? sun.gameObject : null,
                env != null ? env.gameObject : null);
        }

        private static void ApplyLightColors(EcbsdBoxLight lightData, GameObject sun, GameObject env)
        {
            if (lightData == null)
                return;
            if (sun != null)
            {
                var sunColors = sun.GetComponent<WorldLightColors>() ?? sun.AddComponent<WorldLightColors>();
                sunColors.DayColor = Rgb(lightData.DayDirectionalRgb);
                sunColors.NightColor = Rgb(lightData.NightDirectionalRgb);
            }

            if (env != null)
            {
                var envColors = env.GetComponent<WorldLightColors>() ?? env.AddComponent<WorldLightColors>();
                envColors.DayAmbient = Rgb(lightData.DayAmbientRgb);
                envColors.NightAmbient = Rgb(lightData.NightAmbientRgb);
                envColors.DayBackground = Rgb(lightData.DayBackgroundRgb);
                envColors.NightBackground = Rgb(lightData.NightBackgroundRgb);
            }
        }

        private static int StripMissingScripts(GameObject root)
        {
            if (root == null)
                return 0;
            var removed = 0;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform == null)
                    continue;
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
            }

            return removed;
        }

        private static int PlaceEffects(
            IList<EcwldEffect> effects,
            GameObject parent,
            GameDataIndex index,
            GfxTextDecoder decoder,
            Result result)
        {
            var count = 0;
            foreach (var effect in effects)
            {
                var name = Sanitize(Path.GetFileNameWithoutExtension(effect.Path.Replace('\\', '/'))) +
                    "_" + effect.Id;
                var go = new GameObject(name);
                go.transform.SetParent(parent.transform, false);
                ApplyTransform(
                    go.transform,
                    effect.Dir,
                    effect.Up,
                    effect.Pos,
                    new[] { effect.Scale, effect.Scale, effect.Scale });
                var meta = go.AddComponent<WorldObjectMeta>();
                meta.ValidTime = effect.ValidTime;
                meta.SourcePath = effect.Path;
                meta.ExportId = effect.Id;

                var built = GfxSceneBuilder.TryBuildFromGameData(
                    effect.Path, index, decoder, result.Warnings);
                if (built != null && built.Root != null)
                {
                    built.Root.name = "GFX";
                    built.Root.transform.SetParent(go.transform, false);
                    if (built.Player != null)
                    {
                        built.Player.PlaySpeed = effect.Speed;
                        built.Player.Alpha = effect.Alpha;
                    }
                    // Keep path metadata for later reimport / CoGfx.
                    var placeholder = go.AddComponent<WorldGfxPlaceholder>();
                    placeholder.GfxRelativePath = effect.Path.Replace('\\', '/');
                    placeholder.PlaySpeed = effect.Speed;
                    placeholder.Alpha = effect.Alpha;
                    placeholder.ScaleFactor = effect.Scale;
                    result.Warnings.AddRange(built.Warnings);
                }
                else
                {
                    var placeholder = go.AddComponent<WorldGfxPlaceholder>();
                    placeholder.GfxRelativePath = effect.Path.Replace('\\', '/');
                    placeholder.PlaySpeed = effect.Speed;
                    placeholder.Alpha = effect.Alpha;
                    placeholder.ScaleFactor = effect.Scale;
                    result.Warnings.Add(
                        "GFX placeholder for gfx\\" + effect.Path +
                        " (id " + effect.Id + "); .gfx unresolved or failed to build.");
                }

                count++;
            }

            return count;
        }

        private static int PlaceModels(
            IList<EcwldModel> models,
            GameObject parent,
            GameDataIndex index,
            GfxTextDecoder decoder,
            Result result,
            bool showProgress)
        {
            var count = 0;
            var resolver = new EcmModelResolver(index);
            var ecmParser = new EcmParser(decoder);
            foreach (var model in models)
            {
                Progress(showProgress, "ECModel " + model.Path, 0.8f);
                var rel = model.Path.Replace('\\', '/').TrimStart('/');
                if (!index.TryResolve(rel, out var ecmFile, out var err) &&
                    !index.TryResolve(rel, out ecmFile, out err, true))
                {
                    result.Warnings.Add("ECModel not found: " + model.Path + " (" + err + ")");
                    continue;
                }

                EcmDocument ecmDoc;
                try { ecmDoc = ecmParser.Parse(File.ReadAllBytes(ecmFile.FullPath)); }
                catch (Exception e) when (
                    e is IOException || e is InvalidDataException || e is InvalidOperationException ||
                    e is NotSupportedException)
                {
                    result.Warnings.Add(ecmFile.RelativePath + ": " + e.Message);
                    continue;
                }

                var prep = resolver.Prepare(ecmDoc, ecmFile.RelativePath);
                result.Warnings.AddRange(prep.Warnings);
                if (prep.HasBlockingErrors)
                {
                    foreach (var e in prep.Errors)
                        result.Warnings.Add(e);
                    continue;
                }

                AssembledModel assembled;
                try
                {
                    var smdFile = prep.SkinModel.File;
                    var smd = new SmdParser(decoder).Parse(
                        File.ReadAllBytes(smdFile.FullPath), smdFile.RelativePath);
                    var addi = ecmDoc.AdditionalSkinPaths.Count > 0
                        ? new List<string>(ecmDoc.AdditionalSkinPaths)
                        : prep.AdditionalSkins.ConvertAll(s => s.RequestedPath);
                    var resolution = resolver.ResolveSmdWithAdditionalSkins(
                        smd, smdFile.RelativePath, addi, prep.Warnings);
                    if (resolution.Skeleton == null || !resolution.Skeleton.IsResolved)
                    {
                        result.Warnings.Add(ecmFile.RelativePath + ": skeleton unresolved");
                        continue;
                    }

                    var skeleton = new BonParser(decoder).Parse(
                        File.ReadAllBytes(resolution.Skeleton.File.FullPath),
                        resolution.Skeleton.File.RelativePath);
                    var skins = new List<SkiDocument>();
                    var skinPaths = new List<string>();
                    foreach (var skinRef in resolution.Skins)
                    {
                        if (!skinRef.IsResolved)
                            continue;
                        skins.Add(new SkiParser(decoder).Parse(
                            File.ReadAllBytes(skinRef.File.FullPath), skinRef.File.RelativePath));
                        skinPaths.Add(skinRef.File.RelativePath);
                    }

                    assembled = new ModelAssembler().Assemble(
                        smd, smdFile.RelativePath, skeleton, resolution.Skeleton.File.RelativePath,
                        skins, skinPaths, resolution);

                    if (resolution.ActionTracks.Count > 0)
                    {
                        var sources = new List<ActionAnimationSource>();
                        foreach (var action in resolution.ActionTracks)
                        {
                            if (action == null)
                                continue;
                            var source = new ActionAnimationSource
                            {
                                ActionName = action.ActionName,
                                StartFrame = action.StartFrame,
                                EndFrame = action.EndFrame,
                                TrackSourcePath = action.Track != null && action.Track.IsResolved
                                    ? action.Track.File.RelativePath
                                    : (action.Track != null ? action.Track.RequestedPath : string.Empty)
                            };
                            if (action.Track != null && action.Track.IsResolved)
                            {
                                try
                                {
                                    source.Track = new StckParser().Parse(
                                        File.ReadAllBytes(action.Track.File.FullPath),
                                        action.Track.File.RelativePath);
                                }
                                catch (Exception stckEx) when (
                                    stckEx is IOException || stckEx is InvalidDataException ||
                                    stckEx is NotSupportedException)
                                {
                                    assembled.Warnings.Add(
                                        "Action " + action.ActionName + ": " + stckEx.Message);
                                }
                            }

                            sources.Add(source);
                        }

                        new ModelAnimationAssembler().Build(assembled, sources);
                    }

                    new ModelMaterialAssembler(new SkiTextureResolver(index)).BuildForModel(assembled, index);
                }
                catch (Exception e) when (
                    e is InvalidDataException || e is ArgumentException || e is InvalidOperationException ||
                    e is IOException || e is NotSupportedException)
                {
                    result.Warnings.Add(ecmFile.RelativePath + ": assemble failed (" + e.Message + ")");
                    continue;
                }

                if (assembled == null || !assembled.IsValid)
                {
                    result.Warnings.Add(ecmFile.RelativePath + ": assembled model invalid");
                    continue;
                }

                var export = AssembledModelAssetExporter.Export(
                    assembled, index, ecmFile.RelativePath, fromEcm: true,
                    ecmDocument: ecmDoc);
                result.Warnings.AddRange(export.Warnings);
                if (!export.Succeeded)
                {
                    result.Warnings.Add(export.Error ?? "ECM prefab export failed");
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(export.PrefabPath);
                var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null)
                    instance = new GameObject(Sanitize(ecmFile.Name) + "_" + model.Id);
                instance.name = Sanitize(ecmFile.Name) + "_" + model.Id;
                instance.transform.SetParent(parent.transform, false);
                ApplyTransform(instance.transform, model.Dir, model.Up, model.Pos, new[] { 1f, 1f, 1f });

                var meta = instance.AddComponent<WorldObjectMeta>();
                meta.ValidTime = WorldObjectMeta.ValidTimeAlways;
                meta.ShowType = model.ShowType;
                meta.SourcePath = model.Path;
                meta.ExportId = model.Id;

                var host = instance.AddComponent<WorldModelHost>();
                host.EcmRelativePath = ecmFile.RelativePath;
                host.ActionName = model.Action ?? string.Empty;
                var bases = new List<string>();
                foreach (var action in ecmDoc.CombinedActions)
                {
                    if (action != null &&
                        string.Equals(action.Name, model.Action, StringComparison.OrdinalIgnoreCase))
                        bases.AddRange(action.BaseActionNames);
                }

                host.BaseActions = bases.ToArray();
                count++;
            }

            return count;
        }

        /// <summary>
        /// Scene hierarchy for a parsed .ecwld: tree labels, and grass when the BSD payload is known.
        /// </summary>
        public static GameObject CreateWorldPreview(EcwldDocument document)
        {
            return CreateWorldPreview(document, null, null, null, null, null);
        }

        public static GameObject CreateWorldPreview(
            EcwldDocument document,
            EcbsdDocument bsd,
            GameDataIndex index,
            GameDataFile terrainFile,
            GfxTextDecoder decoder,
            List<string> warnings)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            var root = new GameObject(Sanitize(Path.GetFileNameWithoutExtension(document.SourcePath)) + "_world");
            var trees = new GameObject("Trees");
            trees.transform.SetParent(root.transform, false);
            PlaceTrees(document.Trees, document.TreeTypes, trees, new Result());
            if (bsd != null && index != null)
            {
                var result = new Result();
                var folder = string.IsNullOrEmpty(document.SourcePath)
                    ? null
                    : GetOutputFolder(document.SourcePath);
                PlaceGrass(
                    new WorldBundle { Document = document, Bsd = bsd },
                    index, decoder, terrainFile, root, folder, result, false);
                if (warnings != null)
                    warnings.AddRange(result.Warnings);
            }

            return root;
        }

        private static int PlaceTrees(
            IList<EcwldTree> trees,
            Dictionary<uint, string> treeTypes,
            GameObject parent,
            Result result)
        {
            var count = 0;
            foreach (var tree in trees)
            {
                var go = new GameObject("дерефа");
                go.transform.SetParent(parent.transform, false);

                // TextMeshPro text showing the tree name.
                var tmp = go.AddComponent<TextMeshPro>();
                tmp.text = "дерефа";
                tmp.fontSize = 6f;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableWordWrapping = false;
                if (TMP_Settings.defaultFontAsset != null)
                    tmp.font = TMP_Settings.defaultFontAsset;
                tmp.ForceMeshUpdate();
                // Billboard: no shadows, text faces upright.
                var meshRenderer = go.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    meshRenderer.receiveShadows = false;
                }

                // Position from the tree record (X is always stored, Y/Z available in v12).
                go.transform.localPosition = new Vector3(tree.Pos[0], tree.Pos[1], tree.Pos[2]);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                // Script with path to the tree (.spt file resolved from tree type ID).
                var placeholder = go.AddComponent<WorldTreePlaceholder>();
                placeholder.TreeTypeId = tree.Type;
                string sptPath;
                if (treeTypes != null && treeTypes.TryGetValue(tree.Type, out sptPath))
                    placeholder.TreeRelativePath = sptPath;
                else
                {
                    placeholder.TreeRelativePath = "trees/unknown_type_" + tree.Type + ".spt";
                    result.Warnings.Add("Tree type " + tree.Type + " has no resolved .spt path; using fallback.");
                }

                count++;
            }

            return count;
        }

        private static int PlaceSounds(
            IList<EcwldSound> sounds,
            GameObject parent,
            GameDataIndex index,
            string folder,
            Result result)
        {
            EnsureFolder(folder + "/sounds");
            var count = 0;
            foreach (var sound in sounds)
            {
                var rel = ("sfx/" + sound.Path).Replace('\\', '/');
                GameDataFile file = null;
                string err = null;
                if (!index.TryResolve(rel, out file, out err) &&
                    !index.TryResolve(rel, out file, out err, true))
                {
                    // Filesystem fallback beside game root.
                    var full = Path.Combine(index.Root, rel.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(full))
                    {
                        result.Warnings.Add("Sound sfx\\" + sound.Path + " nicht in den Spieldaten");
                        continue;
                    }
                }

                var pathForCheck = file != null ? file.FullPath : Path.Combine(index.Root, rel);
                // Godot angelica_world._sound: map ambience is wav-only.
                if (!string.Equals(Path.GetExtension(pathForCheck), ".wav", StringComparison.OrdinalIgnoreCase))
                {
                    result.Warnings.Add("Sound " + sound.Path + ": nur .wav wird unterstützt");
                    continue;
                }

                var go = new GameObject(
                    Sanitize(Path.GetFileNameWithoutExtension(sound.Path.Replace('\\', '/'))) +
                    "_" + sound.Id);
                go.transform.SetParent(parent.transform, false);
                go.transform.localPosition = new Vector3(sound.Pos[0], sound.Pos[1], sound.Pos[2]);
                var meta = go.AddComponent<WorldObjectMeta>();
                meta.ValidTime = sound.ValidTime;
                meta.SourcePath = sound.Path;
                meta.ExportId = sound.Id;
                var audio = go.AddComponent<AudioSource>();
                audio.playOnAwake = true;
                audio.loop = true;
                audio.rolloffMode = AudioRolloffMode.Logarithmic;
                audio.minDistance = Math.Max(sound.MinDistance, 0.1f);
                audio.maxDistance = Math.Max(sound.MaxDistance, 0f);
                // Godot angelica_world._sound: wav-only map ambience → Assets Import + AudioClip.
                if (file != null)
                {
                    var imported = AssembledAudioImporter.ImportGameDataFile(file);
                    if (imported.Clip != null)
                        audio.clip = imported.Clip;
                    else if (!string.IsNullOrEmpty(imported.Error))
                        result.Warnings.Add("Sound " + sound.Path + ": " + imported.Error);
                }
                else
                {
                    var imported = AssembledAudioImporter.ImportOrUpdate(pathForCheck, rel);
                    if (imported.Clip != null)
                        audio.clip = imported.Clip;
                    else if (!string.IsNullOrEmpty(imported.Error))
                        result.Warnings.Add("Sound " + sound.Path + ": " + imported.Error);
                }
                count++;
            }

            return count;
        }

        private static int PlaceGrass(
            WorldBundle world,
            GameDataIndex index,
            GfxTextDecoder decoder,
            GameDataFile terrainFile,
            GameObject root,
            string folder,
            Result result,
            bool showProgress)
        {
            if (world == null || world.Document == null || world.Bsd == null ||
                world.Bsd.GrassAreas.Count == 0 || index == null)
                return 0;

            var shader = AngelicaRenderPipeline.Find(
                BuildingAssetImporter.HdrpShaderName, BuildingAssetImporter.ShaderName);
            if (shader == null)
                result.Warnings.Add("Angelica LitModel shader unavailable; grass is imported unshaded.");
            if (!string.IsNullOrEmpty(folder))
                EnsureFolder(folder + "/grass");

            TerrainHeightField heights = null;
            var heightSource = terrainFile != null &&
                terrainFile.Extension.Equals("trn2", StringComparison.OrdinalIgnoreCase)
                ? terrainFile
                : null;
            if (heightSource == null)
            {
                var dir = (Path.GetDirectoryName(world.Document.SourcePath) ?? "").Replace('\\', '/');
                var name = Path.GetFileNameWithoutExtension(world.Document.SourcePath);
                var rel = string.IsNullOrEmpty(dir) ? name + ".trn2" : dir + "/" + name + ".trn2";
                GameDataFile sibling;
                if (index.TryResolve(rel, out sibling, out _))
                    heightSource = sibling;
            }

            if (heightSource != null)
                heights = TerrainHeightField.Load(heightSource, index, decoder, result.Warnings);
            else
                result.Warnings.Add("Gras: kein .trn2, Höhe aus dem Gras-Zentrum.");

            var grassRoot = new GameObject("Grass");
            grassRoot.transform.SetParent(root.transform, false);
            var materials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            var blades = 0;
            var chunkIndex = 0;
            foreach (var area in world.Bsd.GrassAreas)
            {
                Progress(showProgress, "Grass " + area.ExportId, 0.9f);
                if (area.TypeId >= (uint)world.Document.GrassTypes.Count)
                {
                    result.Warnings.Add("Gras " + area.ExportId + ": Typ " + area.TypeId + " fehlt.");
                    continue;
                }

                var type = world.Document.GrassTypes[(int)area.TypeId];
                GrassTemplate template = null;
                string moxRelative = type.ModelPath ?? string.Empty;
                GameDataFile mox;
                string error;
                if (GrassFileResolver.TryResolveModel(index, type.ModelPath, out mox, out error))
                {
                    moxRelative = mox.RelativePath;
                    try { template = new MoxGrassParser(decoder).ParseFile(mox.FullPath); }
                    catch (Exception e) when (
                        e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
                    {
                        result.Warnings.Add(mox.RelativePath + ": " + e.Message);
                    }
                }
                else
                    result.Warnings.Add(error);

                if (template == null)
                {
                    template = GrassMeshBuilder.FallbackCard();
                    result.Warnings.Add("Gras " + area.ExportId + ": Ersatzkarte statt .mox.");
                }

                float fallbackY = area.Center != null && area.Center.Length > 1 ? area.Center[1] : 0f;
                var chunks = GrassMeshBuilder.Build(
                    area, type, template,
                    (x, z) =>
                    {
                        float h;
                        return heights != null && heights.TrySample(x, z, out h) ? h : fallbackY;
                    },
                    result.Warnings);
                foreach (var chunk in chunks)
                {
                    var mesh = BuildGrassMesh(chunk, chunkIndex);
                    Material material = null;
                    if (shader != null)
                    {
                        var key = (moxRelative + "|" + chunk.TextureName).ToLowerInvariant();
                        if (!materials.TryGetValue(key, out material))
                        {
                            material = new Material(shader)
                            {
                                name = "Grass_" + Sanitize(Path.GetFileNameWithoutExtension(
                                    string.IsNullOrEmpty(chunk.TextureName) ? moxRelative : chunk.TextureName))
                            };
                            material.SetFloat("_AlphaRef", BuildingAssetImporter.DefaultAlphaRef);
                            material.SetFloat("_Brightness", BuildingAssetImporter.DefaultBrightness);
                            if (material.HasProperty("_Cull"))
                                material.SetFloat("_Cull", (float)CullMode.Off);
                            GameDataFile textureFile;
                            string textureError;
                            if (GrassFileResolver.TryResolveTexture(
                                    index, moxRelative, chunk.TextureName, out textureFile, out textureError))
                            {
                                var imported = AssembledTextureImporter.ImportOrUpdate(
                                    textureFile.FullPath, textureFile.RelativePath);
                                if (imported.Texture != null)
                                    material.SetTexture("_Albedo", imported.Texture);
                                else if (!string.IsNullOrEmpty(imported.Error))
                                    result.Warnings.Add(imported.Error);
                            }
                            else if (!string.IsNullOrEmpty(chunk.TextureName))
                                result.Warnings.Add(textureError);

                            if (!string.IsNullOrEmpty(folder))
                                material = Save(material, folder + "/grass/" + material.name + "_" + materials.Count + ".mat");
                            materials[key] = material;
                        }
                    }

                    if (!string.IsNullOrEmpty(folder))
                        mesh = Save(mesh, folder + "/grass/" + mesh.name + ".asset");
                    var go = new GameObject(mesh.name);
                    go.transform.SetParent(grassRoot.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = go.AddComponent<MeshRenderer>();
                    if (material != null)
                        renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    blades += chunk.Blades;
                    chunkIndex++;
                }
            }

            if (grassRoot.transform.childCount == 0)
                Object.DestroyImmediate(grassRoot);
            return blades;
        }

        private static Mesh BuildGrassMesh(GrassMeshChunk chunk, int index)
        {
            var count = chunk.Positions.Length / 3;
            var vertices = new Vector3[count];
            var uvs = new Vector2[count];
            var colors = new Color[count];
            for (var i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(
                    chunk.Positions[i * 3], chunk.Positions[i * 3 + 1], chunk.Positions[i * 3 + 2]);
                // .mox stores DirectX V (0 at the top). Flip once here, same as SKI/BMD.
                // The parser keeps the raw value, so this must not be applied again.
                uvs[i] = new Vector2(
                    chunk.Uvs[i * 2],
                    AngelicaGeometry.UnityTextureV(chunk.Uvs[i * 2 + 1]));
                // LitModel multiplies texture by vertex color * 2. 0.5 keeps the texture as stored.
                colors[i] = new Color(0.5f, 0.5f, 0.5f, 1f);
            }

            var mesh = new Mesh
            {
                name = "grass_" + index,
                indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = chunk.Indices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject Group(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static void ApplyTransform(
            Transform transform, float[] dir, float[] up, float[] pos, float[] scale)
        {
            var matrix = BmdMeshAssembler.BuildLocalMatrix(dir, up, pos, scale);
            AngelicaGeometry.DecomposeUnityColumnMajor(
                matrix, out var position, out var rotation, out var localScale);
            transform.localPosition = new Vector3(position[0], position[1], position[2]);
            transform.localRotation = new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]);
            transform.localScale = new Vector3(localScale[0], localScale[1], localScale[2]);
        }

        private static Color Rgb(float[] rgb)
        {
            if (rgb == null || rgb.Length < 3)
                return Color.white;
            return new Color(rgb[0], rgb[1], rgb[2], 1f);
        }

        private static bool TryResolveBeside(GameDataFile trn2, string fileName, out GameDataFile file)
        {
            file = null;
            var dir = Path.GetDirectoryName(trn2.FullPath);
            if (string.IsNullOrEmpty(dir))
                return false;
            var full = Path.Combine(dir, fileName);
            if (!File.Exists(full))
            {
                // case-insensitive search
                full = ResolveBesidePath(dir, fileName);
                if (full == null)
                    return false;
            }

            var relDir = Path.GetDirectoryName(trn2.RelativePath) ?? "";
            var rel = string.IsNullOrEmpty(relDir) ? Path.GetFileName(full) : relDir + "/" + Path.GetFileName(full);
            file = new GameDataFile(full, rel.Replace('\\', '/'));
            return true;
        }

        private static string ResolveBesidePath(string directory, string name)
        {
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(name))
                return null;
            var direct = Path.Combine(directory, name.Replace('\\', Path.DirectorySeparatorChar));
            if (File.Exists(direct))
                return direct;
            var target = name.Replace('\\', '/');
            var leaf = Path.GetFileName(target);
            try
            {
                foreach (var candidate in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                {
                    var rel = candidate.Substring(directory.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Replace('\\', '/');
                    if (string.Equals(rel, target, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Path.GetFileName(candidate), leaf, StringComparison.OrdinalIgnoreCase))
                        return candidate;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            return null;
        }

        private static void Progress(bool show, string text, float value)
        {
            if (show && EditorUtility.DisplayCancelableProgressBar("Import Angelica Map", text, value))
                throw new OperationCanceledException("Map import cancelled.");
        }

        private static void EnsureFolder(string folder)
        {
            AngelicaAssetFolders.Ensure(folder);
        }

        private static T Save<T>(T value, string path) where T : Object
        {
            value.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(value, path);
                return value;
            }

            EditorUtility.CopySerialized(value, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(value);
            return existing;
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "map";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
