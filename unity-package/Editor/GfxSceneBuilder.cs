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
