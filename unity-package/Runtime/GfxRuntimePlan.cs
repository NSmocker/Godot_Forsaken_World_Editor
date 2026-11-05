using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Portable description of a GFX hierarchy built from GfxDocument
    /// (gfx_parser.gd build_file_node), before Unity scene instantiation.
    /// </summary>
    public sealed class GfxRuntimePlan
    {
        public string Name = "GFX";
        public string SourcePath = string.Empty;
        public Dictionary<string, string> Header =
            new Dictionary<string, string>(StringComparer.Ordinal);
        public float DefaultScale = 1f;
        public List<GfxElementPlan> Elements = new List<GfxElementPlan>();
        public List<string> Warnings = new List<string>();
        public List<string> StubbedElementIds = new List<string>();
        public Dictionary<string, int> ElementIdHistogram =
            new Dictionary<string, int>(StringComparer.Ordinal);
    }

    public sealed class GfxElementPlan
    {
        public int Index = 1;
        public string SystemId = string.Empty;
        public bool IsStub = true;
        public string Name = string.Empty;
        public Dictionary<string, string> Properties =
            new Dictionary<string, string>(StringComparer.Ordinal);
        public List<Dictionary<string, string>> Affectors =
            new List<Dictionary<string, string>>();
        public List<Dictionary<string, string>> ControlMethods =
            new List<Dictionary<string, string>>();
        public List<GfxKeypointSpec> Keypoints = new List<GfxKeypointSpec>();
        public float GlobalLoopTime = 3000f;
        public float LocalStartTime;
        public float LocalTotalTime = 3000f;
        public int RepeatCount = 1;
        public float RepeatDelay;
        public bool IsEndless;
        public List<string> GridAnimations = new List<string>();
    }

    /// <summary>
    /// Element IDs that have a real Unity builder. Core slice leaves all visual
    /// families as stubs; later slices register IDs here.
    /// </summary>
    public static class GfxElementRegistry
    {
        // Godot has builders for these; Unity core slice stubs them all.
        public static readonly HashSet<string> GodotSupportedIds =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "100", "101", "110",
                "120", "121", "122", "123", "124", "125",
                "130", "140",
                "150", "151", "152",
                "160", "170", "180", "190", "200",
                "210", "211", "220", "230"
            };

        // IDs with a real Unity visual builder in this package.
        private static readonly string[] DefaultPortedIds = { "100", "101", "110", "120", "121", "122", "123", "124", "125", "130", "140", "150", "151", "152", "160", "170", "180", "190", "200", "210", "211", "220", "230" };

        private static readonly HashSet<string> PortedIds =
            new HashSet<string>(DefaultPortedIds, StringComparer.Ordinal);

        public static bool IsPorted(string systemId) =>
            !string.IsNullOrEmpty(systemId) && PortedIds.Contains(systemId);

        public static bool IsKnownInGodot(string systemId) =>
            !string.IsNullOrEmpty(systemId) && GodotSupportedIds.Contains(systemId);

        /// <summary>Test/helper hook for later slices.</summary>
        internal static void RegisterPortedForTests(string systemId)
        {
            if (!string.IsNullOrEmpty(systemId))
                PortedIds.Add(systemId);
        }

        /// <summary>Resets to the default ported set (currently 100/101/110/120–125/130/140/150–152/160/170/180/190/200/210/211/220/230).</summary>
        internal static void ClearPortedForTests()
        {
            PortedIds.Clear();
            foreach (var id in DefaultPortedIds)
                PortedIds.Add(id);
        }
    }

    public static class GfxRuntimePlanner
    {
        public static GfxRuntimePlan Build(
            GfxDocument document,
            string name = "GFX",
            string sourcePath = "")
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var plan = new GfxRuntimePlan
            {
                Name = string.IsNullOrEmpty(name) ? "GFX" : name,
                SourcePath = sourcePath ?? string.Empty,
                DefaultScale = GfxPropertyValues.Float(document.Header, "DedaultScale", 1f)
            };

            foreach (var pair in document.Header)
                plan.Header[pair.Key] = pair.Value;
            plan.Warnings.AddRange(document.Warnings);

            var index = 1;
            var fileLoop = 0f;
            foreach (var element in document.Elements)
            {
                var id = element.SystemId ?? string.Empty;
                if (!plan.ElementIdHistogram.ContainsKey(id))
                    plan.ElementIdHistogram[id] = 0;
                plan.ElementIdHistogram[id]++;

                var isPorted = GfxElementRegistry.IsPorted(id);
                if (!isPorted && !plan.StubbedElementIds.Contains(id))
                    plan.StubbedElementIds.Add(id);

                var el = new GfxElementPlan
                {
                    Index = index,
                    SystemId = id,
                    IsStub = !isPorted,
                    Name = "Element_" + index + "_ID_" + (string.IsNullOrEmpty(id) ? "Unknown" : id),
                    Keypoints = GfxKeypointBuilder.Build(element)
                };
                foreach (var pair in element.Properties)
                    el.Properties[pair.Key] = pair.Value;
                foreach (var a in element.Affectors)
                    el.Affectors.Add(new Dictionary<string, string>(a));
                foreach (var c in element.ControlMethods)
                    el.ControlMethods.Add(new Dictionary<string, string>(c));
                if (element.GridAnimations != null)
                {
                    foreach (var line in element.GridAnimations)
                        el.GridAnimations.Add(line);
                }

                float headerContribution;
                FillTiming(el, element, out headerContribution);
                if (headerContribution > fileLoop)
                    fileLoop = headerContribution;
                plan.Elements.Add(el);
                index++;
            }

            // Godot create_id_*._get_max_time_for_header: shared GlobalLoopTime for every
            // element = max non-endless raw totals, else 3000 when all endless/empty.
            if (fileLoop <= 0f)
                fileLoop = 3000f;
            foreach (var el in plan.Elements)
                el.GlobalLoopTime = fileLoop;

            return plan;
        }

        private static void FillTiming(
            GfxElementPlan el, GfxElement element, out float headerContribution)
        {
            // LocalStart/LocalTotal: lightning 150/152 v102+ uses StartTime_2 (create_id_150).
            var start = GfxPropertyValues.Float(element.Properties, "StartTime", 0f);
            if ((el.SystemId == "150" || el.SystemId == "152") &&
                element.Properties != null &&
                element.Properties.ContainsKey("DestNum") &&
                element.Properties.ContainsKey("StartTime_2"))
            {
                start = GfxPropertyValues.Float(element.Properties, "StartTime_2", start);
            }

            // Header max uses Godot _get_max_time_for_header: always property StartTime
            // (not StartTime_2), no per-element +3000 fallback, endless → ignore.
            var headerStart = GfxPropertyValues.Float(element.Properties, "StartTime", 0f);
            var headerTotal = headerStart;
            var localTotal = start;
            var endless = false;
            foreach (var kp in element.Keypoints)
            {
                if (!kp.Properties.ContainsKey("TimeSpan"))
                    continue;
                var ts = GfxPropertyValues.Float(kp.Properties, "TimeSpan", 0f);
                if (ts < 0f)
                    endless = true;
                else if (ts > 0f)
                {
                    localTotal += ts;
                    headerTotal += ts;
                }
            }

            if (localTotal <= start && !endless)
                localTotal = start + 3000f;

            el.LocalStartTime = start;
            el.LocalTotalTime = localTotal;
            el.IsEndless = endless;
            el.RepeatCount = GfxPropertyValues.Int(element.Properties, "RepeatCount", 1);
            el.RepeatDelay = GfxPropertyValues.Float(element.Properties, "RepeatDelay", 0f);
            // Placeholder; Build overwrites with file-wide max.
            el.GlobalLoopTime = localTotal;
            headerContribution = endless ? 0f : headerTotal;
        }
    }
}
