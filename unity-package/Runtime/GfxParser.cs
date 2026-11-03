using System;
using System.Collections.Generic;
using System.Globalization;

namespace SilverEcho.ForsakenWorld
{
    public sealed class GfxParser
    {
        private static readonly HashSet<string> NumberedKeys =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Vec", "TexSpeed", "NoScale", "OrgPt"
            };

        private readonly GfxTextDecoder decoder;

        public GfxParser(GfxTextDecoder decoder = null)
        {
            this.decoder = decoder ?? new GfxTextDecoder();
        }

        public GfxDocument Parse(byte[] bytes)
        {
            return Parse(decoder.Decode(bytes));
        }

        public GfxDocument Parse(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            var document = new GfxDocument();
            var headerLines = new List<string>();
            var elementLines = new List<string>();
            var hasElement = false;

            foreach (var rawLine in SplitLines(text))
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                    continue;

                if (line.StartsWith("GFXELEMENTID:", StringComparison.Ordinal))
                {
                    if (hasElement)
                        document.Elements.Add(ParseElement(elementLines, document.Warnings));

                    elementLines = new List<string> { line };
                    hasElement = true;
                }
                else if (hasElement)
                {
                    elementLines.Add(line);
                }
                else
                {
                    headerLines.Add(line);
                }
            }

            ParseProperties(headerLines, document.Header);
            if (hasElement)
                document.Elements.Add(ParseElement(elementLines, document.Warnings));

            return document;
        }

        private static GfxElement ParseElement(
            List<string> lines,
            List<string> warnings)
        {
            var element = new GfxElement();
            var tracker = new Dictionary<string, int>(StringComparer.Ordinal);

            for (var i = 0; i < lines.Count;)
            {
                var line = lines[i];
                if (line.StartsWith("GFXELEMENTID:", StringComparison.Ordinal))
                {
                    var value = ValueOf(line);
                    var idLength = 0;
                    while (idLength < value.Length &&
                        value[idLength] >= '0' && value[idLength] <= '9')
                    {
                        idLength++;
                    }

                    element.SystemId = value.Substring(0, idLength);
                    AddProperty(line, element.Properties, tracker);
                    i++;
                    continue;
                }

                if (line.StartsWith("PSConstCount:", StringComparison.Ordinal))
                {
                    AddProperty(line, element.Properties, tracker);
                    var count = ReadBoundedCount(line, lines.Count - i - 1, warnings);
                    i++;
                    for (var p = 0; p < count; p++)
                    {
                        var constant = new GfxPixelShaderConstant();
                        ReadOptionalProperty(lines, ref i, constant.Properties, "PSConstIndex");
                        ReadOptionalProperty(lines, ref i, constant.Properties, "PSConstValue");
                        ReadOptionalProperty(lines, ref i, constant.Properties, "PSLoopCount");

                        if (TryReadOptionalProperty(
                            lines, ref i, constant.Properties, "PSTargetCount"))
                        {
                            var targetCount = ReadBoundedCount(
                                constant.Properties["PSTargetCount"],
                                "PSTargetCount",
                                lines.Count - i,
                                warnings);
                            for (var target = 0; target < targetCount; target++)
                            {
                                var targetProperties =
                                    new Dictionary<string, string>(StringComparer.Ordinal);
                                ReadOptionalProperty(lines, ref i, targetProperties, "PSInterval");
                                ReadOptionalProperty(lines, ref i, targetProperties, "PSConstValue");
                                constant.Targets.Add(targetProperties);
                            }
                        }

                        element.PixelShaderConstants.Add(constant);
                    }

                    continue;
                }

                if (line.StartsWith("AffectorCount:", StringComparison.Ordinal))
                {
                    AddProperty(line, element.Properties, tracker);
                    var count = ReadBoundedCount(line, lines.Count - i - 1, warnings);
                    i++;
                    if (count > 0)
                        ReadControllers(lines, ref i, element.Affectors, "CtrlMethodCount:");
                    continue;
                }

                if (line.StartsWith("KEYPOINTCOUNT:", StringComparison.Ordinal))
                {
                    AddProperty(line, element.Properties, tracker);
                    var count = ReadBoundedCount(line, lines.Count - i - 1, warnings);
                    i++;
                    for (var keypointIndex = 0; keypointIndex < count && i < lines.Count; keypointIndex++)
                    {
                        var keypoint = new GfxKeypoint();
                        var keypointTracker =
                            new Dictionary<string, int>(StringComparer.Ordinal);

                        while (i < lines.Count &&
                            !lines[i].StartsWith("CtrlMethodCount:", StringComparison.Ordinal))
                        {
                            AddProperty(lines[i], keypoint.Properties, keypointTracker);
                            i++;
                        }

                        if (i < lines.Count &&
                            lines[i].StartsWith("CtrlMethodCount:", StringComparison.Ordinal))
                        {
                            keypoint.Properties["CtrlMethodCount"] = ValueOf(lines[i]);
                            var controlCount = ReadBoundedCount(
                                lines[i],
                                lines.Count - i - 1,
                                warnings);
                            i++;
                            if (controlCount > 0)
                                ReadKeypointControllers(lines, ref i, keypoint, element);
                        }

                        element.Keypoints.Add(keypoint);
                    }

                    continue;
                }

                if (line.StartsWith("CtrlMethodCount:", StringComparison.Ordinal))
                {
                    AddProperty(line, element.Properties, tracker);
                    var count = ReadBoundedCount(line, lines.Count - i - 1, warnings);
                    i++;
                    if (count > 0)
                        ReadControllers(lines, ref i, element.ControlMethods, "AffectorCount:");
                    continue;
                }

                if (line.StartsWith("GridAnimationLines:", StringComparison.Ordinal))
                {
                    var count = ReadBoundedCount(line, lines.Count - i - 1, warnings);
                    i++;
                    for (var animationIndex = 0;
                        animationIndex < count && i < lines.Count;
                        animationIndex++, i++)
                    {
                        element.GridAnimations.Add(lines[i]);
                    }

                    continue;
                }

                AddProperty(line, element.Properties, tracker);
                i++;
            }

            return element;
        }

        private static void ReadControllers(
            List<string> lines,
            ref int index,
            List<Dictionary<string, string>> destination,
            string stopAtKey)
        {
            Dictionary<string, string> current = null;
            var tracker = new Dictionary<string, int>(StringComparer.Ordinal);

            while (index < lines.Count)
            {
                var line = lines[index];
                if (line.StartsWith(stopAtKey, StringComparison.Ordinal))
                    break;

                if (IsKeypointStart(lines, index))
                    break;

                if (line.StartsWith("CtrlType:", StringComparison.Ordinal))
                {
                    if (current != null)
                        destination.Add(current);

                    current = new Dictionary<string, string>(StringComparer.Ordinal);
                    tracker = new Dictionary<string, int>(StringComparer.Ordinal);
                    AddProperty(line, current, tracker);
                }
                else if (current != null)
                {
                    AddProperty(line, current, tracker);
                }

                index++;
            }

            if (current != null)
                destination.Add(current);
        }

        private static void ReadKeypointControllers(
            List<string> lines,
            ref int index,
            GfxKeypoint keypoint,
            GfxElement element)
        {
            Dictionary<string, string> current = null;
            var tracker = new Dictionary<string, int>(StringComparer.Ordinal);

            while (index < lines.Count &&
                !lines[index].StartsWith("InterpolateMode:", StringComparison.Ordinal))
            {
                var line = lines[index];
                if (line.StartsWith("CtrlType:", StringComparison.Ordinal))
                {
                    if (current != null)
                    {
                        keypoint.ControlMethods.Add(current);
                        element.ControlMethods.Add(current);
                    }

                    current = new Dictionary<string, string>(StringComparer.Ordinal);
                    tracker = new Dictionary<string, int>(StringComparer.Ordinal);
                    AddProperty(line, current, tracker);
                }
                else if (current != null)
                {
                    AddProperty(line, current, tracker);
                }

                index++;
            }

            if (current != null)
            {
                keypoint.ControlMethods.Add(current);
                element.ControlMethods.Add(current);
            }
        }

        private static bool IsKeypointStart(List<string> lines, int index)
        {
            return lines[index].StartsWith("StartTime:", StringComparison.Ordinal) &&
                index + 1 < lines.Count &&
                lines[index + 1].StartsWith("KEYPOINTCOUNT:", StringComparison.Ordinal);
        }

        private static void ParseProperties(
            List<string> lines,
            Dictionary<string, string> destination)
        {
            var tracker = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var line in lines)
                AddProperty(line, destination, tracker);
        }

        private static void AddProperty(
            string line,
            Dictionary<string, string> destination,
            Dictionary<string, int> tracker)
        {
            var colon = line.IndexOf(':');
            var key = colon >= 0 ? line.Substring(0, colon).Trim() : "GridVector";
            var value = colon >= 0 ? line.Substring(colon + 1).Trim() : line;

            int seen;
            if (tracker.TryGetValue(key, out seen))
            {
                seen++;
                tracker[key] = seen;
                destination[key + "_" + seen.ToString(CultureInfo.InvariantCulture)] = value;
            }
            else
            {
                tracker[key] = 1;
                destination[NumberedKeys.Contains(key) ? key + "_1" : key] = value;
            }
        }

        private static void ReadOptionalProperty(
            List<string> lines,
            ref int index,
            Dictionary<string, string> destination,
            string key)
        {
            TryReadOptionalProperty(lines, ref index, destination, key);
        }

        private static bool TryReadOptionalProperty(
            List<string> lines,
            ref int index,
            Dictionary<string, string> destination,
            string key)
        {
            if (index >= lines.Count ||
                !lines[index].StartsWith(key + ":", StringComparison.Ordinal))
                return false;

            destination[key] = ValueOf(lines[index]);
            index++;
            return true;
        }

        private static int ReadBoundedCount(
            string line,
            int availableLines,
            List<string> warnings)
        {
            var colon = line.IndexOf(':');
            return ReadBoundedCount(
                colon >= 0 ? line.Substring(colon + 1).Trim() : string.Empty,
                colon >= 0 ? line.Substring(0, colon).Trim() : "count",
                availableLines,
                warnings);
        }

        private static int ReadBoundedCount(
            string value,
            string label,
            int availableLines,
            List<string> warnings)
        {
            int count;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out count) &&
                count >= 0)
            {
                if (count > availableLines)
                {
                    warnings.Add(
                        label + " value " + count.ToString(CultureInfo.InvariantCulture) +
                        " exceeds remaining lines; clamped to " +
                        availableLines.ToString(CultureInfo.InvariantCulture) + ".");
                    return availableLines;
                }

                return count;
            }

            warnings.Add("Invalid " + label + " value '" + value + "'; treating it as zero.");
            return 0;
        }

        private static string ValueOf(string line)
        {
            var colon = line.IndexOf(':');
            return colon >= 0 ? line.Substring(colon + 1).Trim() : string.Empty;
        }

        private static IEnumerable<string> SplitLines(string text)
        {
            return text.Replace("\r", string.Empty).Split('\n');
        }
    }
}
