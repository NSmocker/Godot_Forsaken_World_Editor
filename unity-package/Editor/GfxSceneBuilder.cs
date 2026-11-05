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
            return Build(document, name, fullPath);
        }

        public static BuildResult Build(
            GfxDocument document,
            string nodeName = "GFX",
            string sourcePath = "")
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var plan = GfxRuntimePlanner.Build(document, nodeName, sourcePath);
            return Build(plan);
        }

        public static BuildResult Build(GfxRuntimePlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            var result = new BuildResult { Plan = plan };
            result.Warnings.AddRange(plan.Warnings);
            result.StubbedIds.AddRange(plan.StubbedElementIds);

            var root = new GameObject(Sanitize(plan.Name));
            var player = root.AddComponent<GfxPlayer>();
            player.SourcePath = plan.SourcePath;
            player.SetHeader(plan.Header);
            if (Math.Abs(plan.DefaultScale - 1f) > 0.000001f)
                root.transform.localScale = new Vector3(
                    plan.DefaultScale, plan.DefaultScale, plan.DefaultScale);

            foreach (var el in plan.Elements)
            {
                var elementGo = new GameObject(Sanitize(el.Name));
                elementGo.transform.SetParent(root.transform, false);

                GfxElementDriver driver;
                if (el.IsStub || !GfxElementRegistry.IsPorted(el.SystemId))
                {
                    driver = elementGo.AddComponent<GfxElementStub>();
                    if (!result.StubbedIds.Contains(el.SystemId))
                        result.StubbedIds.Add(el.SystemId);
                    result.Warnings.Add(
                        "GFX element ID " + el.SystemId +
                        " stubbed (visual builder not ported yet).");
                }
                else
                {
                    // Future: dispatch to create_id_* builders.
                    driver = elementGo.AddComponent<GfxElementStub>();
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
