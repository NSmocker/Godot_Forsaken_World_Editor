using System;
using System.Collections.Generic;
using System.Globalization;

namespace SilverEcho.ForsakenWorld
{
    public sealed class EcmParser
    {
        private static readonly HashSet<string> EndOfCoGfxSection =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "CombineActName",
                "ECMHookCount",
                "AddiSkinCount",
                "ChildCount",
                "ScriptCount",
                "PhysFileName"
            };

        private readonly GfxTextDecoder decoder;

        public EcmParser(GfxTextDecoder decoder = null)
        {
            this.decoder = decoder ?? new GfxTextDecoder();
        }

        public EcmDocument Parse(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (bytes.Length >= 4 &&
                bytes[0] == (byte)'M' &&
                bytes[1] == (byte)'O' &&
                bytes[2] == (byte)'X' &&
                bytes[3] == (byte)'B')
            {
                return new EcmDocument
                {
                    Error = "Binary MOXB ECM files are not supported yet."
                };
            }

            return Parse(decoder.Decode(bytes));
        }

        public EcmDocument Parse(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            var document = new EcmDocument();
            var declaredCoGfxCount = 0;
            EcmCoGfxEvent currentEvent = null;
            EcmCombinedAction currentAction = null;
            var inBaseAction = false;
            var coGfxSectionOpen = true;

            foreach (var rawLine in text.Replace("\r", string.Empty).Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                    continue;

                var colon = line.IndexOf(':');
                if (colon < 0)
                    continue;

                var key = line.Substring(0, colon).Trim();
                var value = line.Substring(colon + 1).Trim();
                if (EndOfCoGfxSection.Contains(key))
                {
                    coGfxSectionOpen = false;
                    currentEvent = null;
                }

                switch (key)
                {
                    case "SkinModelPath":
                        document.SkinModelPath = value;
                        break;
                    case "AddiSkinPath":
                        document.AdditionalSkinPaths.Add(value);
                        break;
                    case "CoGfxNum":
                        declaredCoGfxCount = ParseCount(value, key, document.Warnings);
                        break;
                    case "EventType":
                        currentEvent = null;
                        if (coGfxSectionOpen &&
                            document.CoGfxEvents.Count < declaredCoGfxCount)
                        {
                            currentEvent = new EcmCoGfxEvent
                            {
                                EventType = ParseInteger(value, key, document.Warnings)
                            };
                            document.CoGfxEvents.Add(currentEvent);
                        }

                        break;
                    case "FxFilePath":
                        if (currentEvent != null)
                            currentEvent.Files.Add(value);
                        break;
                    case "ParamEleName":
                        if (currentEvent != null)
                        {
                            var parameter = new Dictionary<string, string>(
                                StringComparer.Ordinal)
                            {
                                ["ele"] = value
                            };
                            currentEvent.Parameters.Add(parameter);
                        }

                        break;
                    case "ParamId":
                    default:
                        if (currentEvent == null)
                        {
                            if (key == "CombineActName")
                            {
                                currentAction = new EcmCombinedAction { Name = value };
                                document.CombinedActions.Add(currentAction);
                                inBaseAction = false;
                            }
                            else if (key == "BaseActName" && currentAction != null)
                            {
                                currentAction.BaseActionNames.Add(value);
                                currentAction.BaseLoopCounts.Add(1);
                                inBaseAction = true;
                            }
                            else if ((key == "LoopCount" || key == "LoopMinNum") &&
                                     currentAction != null)
                            {
                                int loopN;
                                if (int.TryParse(
                                        value,
                                        NumberStyles.Integer,
                                        CultureInfo.InvariantCulture,
                                        out loopN))
                                {
                                    if (inBaseAction && currentAction.BaseLoopCounts.Count > 0)
                                        currentAction.BaseLoopCounts[
                                            currentAction.BaseLoopCounts.Count - 1] = loopN;
                                    else if (key == "LoopCount")
                                        currentAction.LoopCount = loopN;
                                }
                            }
                            else if (key == "EventCount" || key == "ScriptCount" ||
                                     key == "ChildCount" || key == "AddiSkinCount" ||
                                     key == "CoGfxNum")
                            {
                                currentAction = null;
                                inBaseAction = false;
                            }

                            break;
                        }

                        if (key == "ParamId" || key.StartsWith("ParamData", StringComparison.Ordinal))
                        {
                            if (currentEvent.Parameters.Count > 0)
                            {
                                currentEvent.Parameters[currentEvent.Parameters.Count - 1][key] = value;
                            }
                        }
                        else if (!currentEvent.Properties.ContainsKey(key))
                        {
                            currentEvent.Properties.Add(key, value);
                        }

                        break;
                }
            }

            if (document.SkinModelPath.Length == 0)
                document.Warnings.Add("ECM does not specify a SkinModelPath.");
            if (document.CoGfxEvents.Count < declaredCoGfxCount)
            {
                document.Warnings.Add(
                    "ECM declares " + declaredCoGfxCount +
                    " CoGfx events but only " + document.CoGfxEvents.Count + " were found.");
            }

            return document;
        }

        private static int ParseCount(string value, string key, List<string> warnings)
        {
            var parsed = ParseInteger(value, key, warnings);
            if (parsed < 0)
            {
                warnings.Add(key + " cannot be negative; treating it as zero.");
                return 0;
            }

            return parsed;
        }

        private static int ParseInteger(string value, string key, List<string> warnings)
        {
            int parsed;
            if (int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsed))
                return parsed;

            warnings.Add("Invalid " + key + " value '" + value + "'.");
            return 0;
        }
    }
}
