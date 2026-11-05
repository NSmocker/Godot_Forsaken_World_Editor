using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Builds a Unity hierarchy from a GfxDocument / GfxRuntimePlan
    /// (gfx_parser.gd build_file_node). Unported element IDs become GfxElementStub.
    /// </summary>
    public static class GfxSceneBuilder
    {
        private const int MaxContainerNesting = 4;
        private static readonly List<string> BuildingStack = new List<string>();

        public sealed class BuildResult
        {
            public GameObject Root;
            public GfxPlayer Player;
            public GfxRuntimePlan Plan;
            public readonly List<string> Warnings = new List<string>();
            public readonly List<string> StubbedIds = new List<string>();
        }

        public static BuildResult BuildFromFile(
            string fullPath,
            GfxTextDecoder decoder,
            string nodeName = null)
        {
            if (string.IsNullOrEmpty(fullPath))
                throw new ArgumentException("path required", nameof(fullPath));
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("GFX file not found", fullPath);

            var bytes = File.ReadAllBytes(fullPath);
            var document = new GfxParser(decoder ?? new GfxTextDecoder()).Parse(bytes);
            var name = string.IsNullOrEmpty(nodeName)
                ? Path.GetFileNameWithoutExtension(fullPath)
                : nodeName;
            return Build(document, name, fullPath, null);
        }

        public static BuildResult BuildFromFile(
            string fullPath,
            GfxTextDecoder decoder,
            GameDataIndex index,
            string nodeName = null)
        {
            if (string.IsNullOrEmpty(fullPath))
                throw new ArgumentException("path required", nameof(fullPath));
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("GFX file not found", fullPath);

            var bytes = File.ReadAllBytes(fullPath);
            var document = new GfxParser(decoder ?? new GfxTextDecoder()).Parse(bytes);
            var name = string.IsNullOrEmpty(nodeName)
                ? Path.GetFileNameWithoutExtension(fullPath)
                : nodeName;
            return Build(document, name, fullPath, index);
        }

        public static BuildResult Build(
            GfxDocument document,
            string nodeName = "GFX",
            string sourcePath = "",
            GameDataIndex index = null)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var plan = GfxRuntimePlanner.Build(document, nodeName, sourcePath);
            return Build(plan, index);
        }

        public static BuildResult Build(GfxRuntimePlan plan, GameDataIndex index = null)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            var result = new BuildResult { Plan = plan };
            result.Warnings.AddRange(plan.Warnings);
            // Recompute stub list after ported IDs may have changed since plan build.
            result.StubbedIds.Clear();
            foreach (var el in plan.Elements)
            {
                if (!GfxElementRegistry.IsPorted(el.SystemId) &&
                    !result.StubbedIds.Contains(el.SystemId))
                    result.StubbedIds.Add(el.SystemId);
            }

            var root = new GameObject(Sanitize(plan.Name));
            var player = root.AddComponent<GfxPlayer>();
            player.SourcePath = plan.SourcePath;
            player.SetHeader(plan.Header);
            if (Math.Abs(plan.DefaultScale - 1f) > 0.000001f)
                root.transform.localScale = new Vector3(
                    plan.DefaultScale, plan.DefaultScale, plan.DefaultScale);

            var gfx2d = GfxPropertyValues.Bool(plan.Header, "2DRender", false);
            GfxTextureResolver texResolver = index != null
                ? new GfxTextureResolver(index)
                : null;

            foreach (var el in plan.Elements)
            {
                var elementGo = new GameObject(Sanitize(el.Name));
                elementGo.transform.SetParent(root.transform, false);

                GfxElementDriver driver;
                if (!GfxElementRegistry.IsPorted(el.SystemId))
                {
                    driver = elementGo.AddComponent<GfxElementStub>();
                    result.Warnings.Add(
                        "GFX element ID " + el.SystemId +
                        " stubbed (visual builder not ported yet).");
                }
                else if (el.SystemId == "100" || el.SystemId == "101")
                {
                    driver = AttachSprite(el, elementGo, texResolver, gfx2d, result.Warnings);
                }
                else if (el.SystemId == "110")
                {
                    driver = AttachSpeedTrail(el, elementGo, texResolver, gfx2d, result.Warnings);
                }
                else if (el.SystemId == "180")
                {
                    driver = AttachLtnTrail(el, elementGo, texResolver, gfx2d, result.Warnings);
                }
                else if (el.SystemId == "120" || el.SystemId == "121" ||
                         el.SystemId == "122" || el.SystemId == "123" ||
                         el.SystemId == "124" || el.SystemId == "125")
                {
                    driver = AttachParticle(el, elementGo, texResolver, gfx2d, result.Warnings);
                }
                else if (el.SystemId == "130")
                {
                    driver = AttachLight(el, elementGo, result.Warnings);
                }
                else if (el.SystemId == "140" || el.SystemId == "190")
                {
                    driver = AttachRing(el, elementGo, texResolver, gfx2d, result.Warnings);
                }
                else if (el.SystemId == "150" || el.SystemId == "152")
                {
                    driver = AttachLightning(el, elementGo, texResolver, gfx2d, result.Warnings);
                }
                else if (el.SystemId == "151")
                {
                    driver = AttachLtnBolt(el, elementGo, texResolver, gfx2d, result.Warnings);
                }
                else if (el.SystemId == "160")
                {
                    driver = AttachModel(el, elementGo, index, gfx2d, result.Warnings);
                }
                else if (el.SystemId == "170")
                {
                    driver = AttachSound(el, elementGo, index, result.Warnings);
                }
                else if (el.SystemId == "200")
                {
                    driver = AttachContainer(el, elementGo, index, result.Warnings);
                }
                else if (el.SystemId == "210" || el.SystemId == "211")
                {
                    driver = AttachGrid(el, elementGo, texResolver, gfx2d, result.Warnings);
                }
                else
                {
                    driver = elementGo.AddComponent<GfxElementStub>();
                    if (!result.StubbedIds.Contains(el.SystemId))
                        result.StubbedIds.Add(el.SystemId);
                    result.Warnings.Add(
                        "GFX element ID " + el.SystemId +
                        " registered as ported but has no builder yet; stubbed.");
                }

                driver.SystemId = el.SystemId;
                driver.GlobalLoopTime = el.GlobalLoopTime;
                driver.LocalStartTime = el.LocalStartTime;
                driver.LocalTotalTime = el.LocalTotalTime;
                driver.RepeatCount = el.RepeatCount;
                driver.RepeatDelay = el.RepeatDelay;
                driver.IsEndless = el.IsEndless;
                driver.SetKeypoints(el.Keypoints);
                driver.TimeManual = true;

                SpawnCtrlGroup("Affector", el.Affectors, elementGo);
                SpawnCtrlGroup("CtrlMethod", el.ControlMethods, elementGo);
            }

            player.MarkHierarchyDirty();
            // Seed loop length from plan elements.
            var loop = 0f;
            foreach (var el in plan.Elements)
            {
                if (el.GlobalLoopTime > loop)
                    loop = el.GlobalLoopTime;
            }

            if (player.Driver != null)
                player.Driver.LoopMs = loop > 0f ? loop : 3000f;

            result.Root = root;
            result.Player = player;
            return result;
        }

        private static GfxElementDriver AttachSprite(
            GfxElementPlan el,
            GameObject elementGo,
            GfxTextureResolver texResolver,
            bool gfx2d,
            List<string> warnings)
        {
            var sprite = elementGo.AddComponent<GfxSpriteDriver>();
            var spriteParams = GfxSpriteParams.FromProperties(
                el.Properties, el.SystemId, el.Index);
            var texture = ResolveElementTexture(
                spriteParams.TexFile, el.SystemId, texResolver, warnings);
            sprite.Configure(spriteParams, texture, gfx2d, warnings);
            return sprite;
        }

        private static GfxElementDriver AttachSpeedTrail(
            GfxElementPlan el,
            GameObject elementGo,
            GfxTextureResolver texResolver,
            bool gfx2d,
            List<string> warnings)
        {
            var trail = elementGo.AddComponent<GfxSpeedTrailDriver>();
            var trailParams = GfxSpeedTrailParams.FromProperties(
                el.Properties, el.SystemId, el.Index);
            var texture = ResolveElementTexture(
                trailParams.TexFile, el.SystemId, texResolver, warnings);
            // Keypoints must be set before Configure seeds the ribbon.
            trail.SystemId = el.SystemId;
            trail.LocalStartTime = el.LocalStartTime;
            trail.LocalTotalTime = el.LocalTotalTime;
            trail.SetKeypoints(el.Keypoints);
            trail.Configure(trailParams, texture, gfx2d, warnings);
            return trail;
        }

        private static GfxElementDriver AttachLtnTrail(
            GfxElementPlan el,
            GameObject elementGo,
            GfxTextureResolver texResolver,
            bool gfx2d,
            List<string> warnings)
        {
            var trail = elementGo.AddComponent<GfxLtnTrailDriver>();
            var trailParams = GfxLtnTrailParams.FromProperties(
                el.Properties, el.SystemId, el.Index, el.Name);
            var texture = ResolveElementTexture(
                trailParams.TexFile, el.SystemId, texResolver, warnings);
            trail.Configure(trailParams, texture, gfx2d, warnings);
            return trail;
        }


        private static GfxElementDriver AttachParticle(
            GfxElementPlan el,
            GameObject elementGo,
            GfxTextureResolver texResolver,
            bool gfx2d,
            List<string> warnings)
        {
            var particle = elementGo.AddComponent<GfxParticleDriver>();
            var particleParams = GfxParticleParams.FromProperties(
                el.Properties, el.SystemId, el.Index);
            particleParams.RandomSeed = (el.Name != null ? el.Name.GetHashCode() : el.Index) & 0xFFFF;

            // Emitter-owned CtrlMethods (125 curve) that were not consumed into keypoints.
            var emitterExtra = new List<GfxCtrlSpec>();
            var usedInKp = 0;
            foreach (var kp in el.Keypoints)
                usedInKp += kp.Ctrls != null ? kp.Ctrls.Count : 0;
            // Prefer scanning element ControlMethods for type 110 / extras.
            if (el.ControlMethods != null)
            {
                foreach (var raw in el.ControlMethods)
                {
                    var spec = GfxKeypointBuilder.FromDictionary(raw);
                    if (spec == null)
                        continue;
                    if (spec.Type == 110)
                        emitterExtra.Add(spec);
                }
            }

            if (particleParams.SystemId == "125")
            {
                GfxCtrlSpec curve = null;
                foreach (var c in emitterExtra)
                {
                    if (c.Type == 110) { curve = c; break; }
                }
                if (curve == null)
                {
                    foreach (var kp in el.Keypoints)
                    {
                        foreach (var c in kp.Ctrls)
                        {
                            if (c != null && c.Type == 110) { curve = c; break; }
                        }
                        if (curve != null) break;
                    }
                }
                if (curve != null && curve.CurvePath != null &&
                    curve.CurvePath.Points != null && curve.CurvePath.Points.Length >= 2)
                {
                    particleParams.CurvePoints = curve.CurvePath.Points;
                    particleParams.CurveRatios = curve.CurvePath.Ratios;
                    particleParams.CurveDirs = curve.CurvePath.Dirs;
                    particleParams.AvgStepCount =
                        GfxParticleParams.AvgStepCountForSeg(particleParams.AvgGenSeg);
                    particleParams.EmitterEnabled = true;
                }
                else
                {
                    particleParams.EmitterEnabled = false;
                }
            }

            var affectors = new List<GfxCtrlSpec>();
            if (el.Affectors != null)
            {
                foreach (var raw in el.Affectors)
                {
                    var spec = GfxKeypointBuilder.FromDictionary(raw);
                    if (spec != null)
                        affectors.Add(spec);
                }
            }

            var texture = ResolveElementTexture(
                particleParams.TexFile, el.SystemId, texResolver, warnings);
            particle.SystemId = el.SystemId;
            particle.LocalStartTime = el.LocalStartTime;
            particle.LocalTotalTime = el.LocalTotalTime;
            particle.SetKeypoints(el.Keypoints);
            particle.Configure(
                particleParams, texture, gfx2d, affectors, emitterExtra, warnings);
            return particle;
        }


        private static GfxElementDriver AttachLight(
            GfxElementPlan el,
            GameObject elementGo,
            List<string> warnings)
        {
            var light = elementGo.AddComponent<GfxLightDriver>();
            var lightParams = GfxLightParams.FromProperties(
                el.Properties, el.SystemId, el.Index);
            light.Configure(lightParams, warnings);
            return light;
        }



        private static GfxElementDriver AttachLightning(
            GfxElementPlan el,
            GameObject elementGo,
            GfxTextureResolver texResolver,
            bool gfx2d,
            List<string> warnings)
        {
            var ltn = elementGo.AddComponent<GfxLightningDriver>();
            var lp = GfxLightningParams.FromProperties(el.Properties, el.SystemId, el.Index);
            lp.RandomSeed = (el.Name != null ? el.Name.GetHashCode() : el.Index) & 0xFFFF;
            // Align ElementStartTime with plan LocalStartTime (StartTime_2 already applied).
            lp.ElementStartTime = el.LocalStartTime;

            // Curve mode: first CtrlType 110 under KP0 (create_id_150 InitBuf).
            GfxCtrlSpec curve = null;
            if (el.Keypoints != null && el.Keypoints.Count > 0 && el.Keypoints[0].Ctrls != null)
            {
                foreach (var c in el.Keypoints[0].Ctrls)
                {
                    if (c != null && c.Type == 110) { curve = c; break; }
                }
            }
            if (curve == null && el.ControlMethods != null)
            {
                foreach (var raw in el.ControlMethods)
                {
                    var spec = GfxKeypointBuilder.FromDictionary(raw);
                    if (spec != null && spec.Type == 110) { curve = spec; break; }
                }
            }
            if (curve != null && curve.CurvePath != null)
            {
                lp.Curved = true;
                lp.LightNum = 1;
                float[][] intPos, intDir, intUp;
                float curveLen;
                GfxLightningEval.BuildCurveSamples(
                    curve.CurvePath, lp.Segs,
                    out intPos, out intDir, out intUp, out curveLen);
                lp.CurveIntPos = intPos;
                lp.CurveIntDir = intDir;
                lp.CurveIntUp = intUp;
                lp.CurveLen = curveLen;
            }
            if (el.Keypoints != null && el.Keypoints.Count > 0)
                lp.Kp0TimeSpan = el.Keypoints[0].TimeSpan;

            var texture = ResolveElementTexture(
                lp.TexFile, el.SystemId, texResolver, warnings);
            ltn.Configure(lp, texture, gfx2d, warnings);
            return ltn;
        }

        private static GfxElementDriver AttachLtnBolt(
            GfxElementPlan el,
            GameObject elementGo,
            GfxTextureResolver texResolver,
            bool gfx2d,
            List<string> warnings)
        {
            var bolt = elementGo.AddComponent<GfxLtnBoltDriver>();
            var bp = GfxLtnBoltParams.FromProperties(el.Properties, el.SystemId, el.Index);
            bp.RandomSeed = (el.Name != null ? el.Name.GetHashCode() : el.Index) & 0xFFFF;
            var texture = ResolveElementTexture(
                bp.TexFile, el.SystemId, texResolver, warnings);
            bolt.SystemId = el.SystemId;
            bolt.LocalStartTime = el.LocalStartTime;
            bolt.LocalTotalTime = el.LocalTotalTime;
            bolt.GlobalLoopTime = el.GlobalLoopTime;
            bolt.RepeatCount = el.RepeatCount;
            bolt.RepeatDelay = el.RepeatDelay;
            bolt.IsEndless = el.IsEndless;
            bolt.SetKeypoints(el.Keypoints);
            bolt.Configure(bp, texture, gfx2d, el.Properties, warnings);
            return bolt;
        }



        private static GfxElementDriver AttachSound(
            GfxElementPlan el,
            GameObject elementGo,
            GameDataIndex index,
            List<string> warnings)
        {
            var sound = elementGo.AddComponent<GfxSoundDriver>();
            var soundParams = GfxSoundParams.FromProperties(
                el.Properties, el.SystemId, el.Index);
            var clips = new AudioClip[
                soundParams.SoundPaths != null ? soundParams.SoundPaths.Length : 0];
            if (index != null && soundParams.SoundPaths != null)
            {
                var resolver = new GfxSoundResolver(index);
                for (var i = 0; i < soundParams.SoundPaths.Length; i++)
                {
                    var resolved = resolver.Resolve(soundParams.SoundPaths[i]);
                    if (!resolved.IsResolved)
                    {
                        if (warnings != null)
                            warnings.Add(
                                "GFX ID 170 Path not in game data: " +
                                soundParams.SoundPaths[i] +
                                (string.IsNullOrEmpty(resolved.Error)
                                    ? string.Empty
                                    : " (" + resolved.Error + ")"));
                        continue;
                    }

                    var imported = AssembledAudioImporter.ImportGameDataFile(resolved.File);
                    if (imported.Clip != null)
                    {
                        clips[i] = imported.Clip;
                    }
                    else if (warnings != null)
                    {
                        warnings.Add(
                            "GFX ID 170 AudioClip import failed: " +
                            resolved.File.RelativePath +
                            (string.IsNullOrEmpty(imported.Error)
                                ? string.Empty
                                : " (" + imported.Error + ")"));
                    }
                }
            }

            sound.Configure(soundParams, clips, warnings);
            return sound;
        }

        private static GfxElementDriver AttachModel(
            GfxElementPlan el,
            GameObject elementGo,
            GameDataIndex index,
            bool gfx2d,
            List<string> warnings)
        {
            var model = elementGo.AddComponent<GfxModelDriver>();
            var modelParams = GfxModelParams.FromProperties(
                el.Properties, el.SystemId, el.Index);
            GameObject instance = null;
            if (index != null && !string.IsNullOrWhiteSpace(modelParams.ModelPath))
            {
                instance = GfxModelSceneFactory.TryBuild(
                    modelParams.ModelPath,
                    modelParams.ModelActName,
                    index,
                    null,
                    warnings);
            }
            model.Configure(modelParams, instance, gfx2d, warnings);
            return model;
        }

        private static GfxElementDriver AttachRing(
            GfxElementPlan el,
            GameObject elementGo,
            GfxTextureResolver texResolver,
            bool gfx2d,
            List<string> warnings)
        {
            var ring = elementGo.AddComponent<GfxRingDriver>();
            var ringParams = GfxRingParams.FromProperties(
                el.Properties, el.SystemId, el.Index);
            var texture = ResolveElementTexture(
                ringParams.TexFile, el.SystemId, texResolver, warnings);
            ring.Configure(ringParams, texture, gfx2d, warnings);
            return ring;
        }

        private static Texture ResolveElementTexture(
            string texFile,
            string systemId,
            GfxTextureResolver texResolver,
            List<string> warnings)
        {
            Texture texture = null;
            if (texResolver == null || string.IsNullOrEmpty(texFile))
                return texture;

            var resolved = texResolver.Resolve(texFile);
            if (resolved != null && resolved.IsResolved)
            {
                try
                {
                    var imported = AssembledTextureImporter.ImportOrUpdate(
                        resolved.Resource.File.FullPath,
                        resolved.RequestedRelativePath,
                        resolved.DdsHeader);
                    if (imported != null && imported.Texture != null)
                        texture = imported.Texture;
                    else if (warnings != null && imported != null &&
                             !string.IsNullOrEmpty(imported.Error))
                        warnings.Add(
                            "GFX ID " + systemId + " TexFile import: " + imported.Error);
                }
                catch (Exception ex)
                {
                    if (warnings != null)
                        warnings.Add(
                            "GFX ID " + systemId + " TexFile import failed: " +
                            ex.Message);
                }
            }
            else if (warnings != null && resolved != null && resolved.Resource != null &&
                     !string.IsNullOrEmpty(resolved.Resource.Error))
            {
                warnings.Add("GFX ID " + systemId + " TexFile: " + resolved.Resource.Error);
            }

            return texture;
        }



        private static GfxElementDriver AttachGrid(
            GfxElementPlan el,
            GameObject elementGo,
            GfxTextureResolver texResolver,
            bool gfx2d,
            List<string> warnings)
        {
            var grid = elementGo.AddComponent<GfxGridDriver>();
            var gp = GfxGridParams.FromElement(
                el.Properties, el.GridAnimations, el.SystemId, el.Index);
            var texture = ResolveElementTexture(gp.TexFile, el.SystemId, texResolver, warnings);
            grid.Configure(gp, texture, gfx2d, warnings);
            return grid;
        }

        private static GfxElementDriver AttachContainer(
            GfxElementPlan el,
            GameObject elementGo,
            GameDataIndex index,
            List<string> warnings)
        {
            var container = elementGo.AddComponent<GfxContainerDriver>();
            var containerParams = GfxContainerParams.FromProperties(
                el.Properties, el.SystemId, el.Index, el.Name);
            containerParams.DummyUsers = GfxPropertyValues.Get(
                el.Properties, "DummyUsers", string.Empty);

            GameObject subRoot = null;
            var subSpeed = 1f;
            var subScale = 1f;
            if (!string.IsNullOrWhiteSpace(containerParams.GfxPath) && index != null)
            {
                var built = TryBuildNestedSubGfx(
                    containerParams.GfxPath, index, warnings);
                if (built != null && built.Root != null)
                {
                    subRoot = built.Root;
                    subScale = subRoot.transform.localScale.x;
                    if (Math.Abs(subScale) < 0.000001f)
                        subScale = 1f;
                    subRoot.transform.localScale = Vector3.one;
                    if (built.Player != null)
                    {
                        subSpeed = built.Player.PlaySpeed > 0f ? built.Player.PlaySpeed : 1f;
                        built.Player.enabled = false;
                        built.Player.Playing = false;
                        built.Player.HeaderControl = false;
                    }
                }
            }

            container.Configure(containerParams, subRoot, subSpeed, subScale, warnings);
            return container;
        }

        private static BuildResult TryBuildNestedSubGfx(
            string gfxPath,
            GameDataIndex index,
            List<string> warnings)
        {
            if (index == null || string.IsNullOrWhiteSpace(gfxPath))
                return null;

            GameDataFile file;
            string err;
            if (!TryResolveGfx(index, gfxPath, out file, out err))
            {
                if (warnings != null)
                    warnings.Add(
                        "GFX Container: " + gfxPath.Replace('\\', '/') +
                        " not found (" + (err ?? "missing") + ")");
                return null;
            }

            var key = (file.FullPath ?? file.RelativePath ?? gfxPath).ToLowerInvariant();
            if (BuildingStack.Contains(key) || BuildingStack.Count >= MaxContainerNesting)
            {
                if (warnings != null)
                    warnings.Add(
                        "GFX Container: " + file.RelativePath +
                        " self-contains or exceeds nesting " + MaxContainerNesting +
                        " — skipped");
                return null;
            }

            BuildingStack.Add(key);
            try
            {
                return BuildFromFile(
                    file.FullPath,
                    null,
                    index,
                    GfxContainerDriver.SubGfxNodeName);
            }
            catch (Exception e) when (
                e is IOException || e is InvalidDataException || e is InvalidOperationException ||
                e is NotSupportedException || e is ArgumentException)
            {
                if (warnings != null)
                    warnings.Add(
                        "GFX Container build failed: " + file.RelativePath +
                        " (" + e.Message + ")");
                return null;
            }
            finally
            {
                if (BuildingStack.Count > 0)
                    BuildingStack.RemoveAt(BuildingStack.Count - 1);
            }
        }

        /// <summary>
        /// Tries to resolve a gfx\ relative path via GameDataIndex and build a runtime root.
        /// Returns null when the file cannot be resolved (caller keeps placeholder).
        /// </summary>
        public static BuildResult TryBuildFromGameData(
            string gfxRelativePath,
            GameDataIndex index,
            GfxTextDecoder decoder,
            List<string> warnings)
        {
            if (index == null || string.IsNullOrWhiteSpace(gfxRelativePath))
                return null;

            GameDataFile file;
            string err;
            if (!TryResolveGfx(index, gfxRelativePath, out file, out err))
            {
                if (warnings != null)
                    warnings.Add("GFX not found: " + gfxRelativePath + " (" + err + ")");
                return null;
            }

            try
            {
                return BuildFromFile(
                    file.FullPath,
                    decoder,
                    index,
                    Path.GetFileNameWithoutExtension(file.Name));
            }
            catch (Exception e) when (
                e is IOException || e is InvalidDataException || e is InvalidOperationException ||
                e is NotSupportedException || e is ArgumentException)
            {
                if (warnings != null)
                    warnings.Add(file.RelativePath + ": " + e.Message);
                return null;
            }
        }

        public static bool TryResolveGfx(
            GameDataIndex index,
            string gfxRelativePath,
            out GameDataFile file,
            out string error)
        {
            file = null;
            error = "empty path";
            if (index == null || string.IsNullOrWhiteSpace(gfxRelativePath))
                return false;

            var rel = gfxRelativePath.Replace('\\', '/').Trim().TrimStart('/');
            var candidates = new List<string>();
            void Add(string p)
            {
                if (string.IsNullOrEmpty(p)) return;
                if (!candidates.Contains(p))
                    candidates.Add(p);
            }

            Add(rel);
            if (!rel.EndsWith(".gfx", StringComparison.OrdinalIgnoreCase))
                Add(rel + ".gfx");
            Add("gfx/" + rel);
            if (!rel.EndsWith(".gfx", StringComparison.OrdinalIgnoreCase))
                Add("gfx/" + rel + ".gfx");
            if (rel.StartsWith("gfx/", StringComparison.OrdinalIgnoreCase))
            {
                Add(rel.Substring(4));
            }

            foreach (var candidate in candidates)
            {
                if (index.TryResolve(candidate, out file, out error) ||
                    index.TryResolve(candidate, out file, out error, true))
                    return true;
            }

            error = error ?? "not found";
            return false;
        }

        private static void SpawnCtrlGroup(
            string groupName,
            IList<Dictionary<string, string>> list,
            GameObject parent)
        {
            if (list == null || list.Count == 0)
                return;

            var container = new GameObject(groupName + "s");
            container.transform.SetParent(parent.transform, false);
            var count = 1;
            foreach (var data in list)
            {
                var typeId = "Unknown";
                string raw;
                if (data != null && data.TryGetValue("CtrlType", out raw))
                    typeId = raw;
                var node = new GameObject(groupName + "_" + count + "_Type_" + typeId);
                node.transform.SetParent(container.transform, false);
                var host = node.AddComponent<GfxCtrlHost>();
                host.CtrlKind = groupName;
                host.SetProperties(data);
                count++;
            }
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "GFX";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace('/', '_').Replace('\\', '_');
        }
    }
}
