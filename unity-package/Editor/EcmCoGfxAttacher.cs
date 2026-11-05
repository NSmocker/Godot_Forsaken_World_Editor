using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Godot ecm_cogfx.gd: attach ECM CoGfx (EventType 100) GFX under model root or
    /// hook bones, with HookPitch/Yaw/Rot + HookOffset + GfxScale/Speed/Alpha.
    /// GfxParam lightning endpoints (ParamId 200/201) via EcmCoGfxParamDriver.
    /// Not yet: GfxDelayTime, GfxRotWithModel=0 without hook, UseModelAlpha (same as Godot).
    /// </summary>
    public static class EcmCoGfxAttacher
    {
        public const int EventTypeGfx = 100;
        public const string AttachPrefix = "CoGfx_";
        public const int ParamLtnPos1 = 200;
        public const int ParamLtnPos2 = 201;

        /// <summary>
        /// Optional factory for tests: relative FxFilePath → built GFX root (or null).
        /// Production leaves this null and uses <see cref="GfxSceneBuilder.TryBuildFromGameData"/>.
        /// </summary>
        public static Func<string, GameDataIndex, IList<string>, GameObject> BuildGfxOverride;

        public static int Attach(
            GameObject modelRoot,
            EcmDocument document,
            GameDataIndex index,
            IList<string> warnings)
        {
            if (modelRoot == null || document == null)
                return 0;

            var count = 0;
            foreach (var info in document.CoGfxEvents)
            {
                if (info == null || info.EventType != EventTypeGfx || info.Files.Count == 0)
                    continue;

                var rel = info.Files[0];
                var gfxRoot = BuildGfx(rel, index, warnings);
                if (gfxRoot == null)
                {
                    Warn(warnings, "CoGfx " + rel + " not found in game data");
                    continue;
                }

                // Godot: tran_matrix(info, node.scale.x * GfxScale) keeps DedaultScale.
                var scale = gfxRoot.transform.localScale.x * ReadFloat(info, "GfxScale", 1f);
                ApplyTranMatrix(gfxRoot.transform, info, scale);

                var player = gfxRoot.GetComponent<GfxPlayer>();
                if (player != null)
                {
                    player.PlaySpeed = ReadFloat(info, "GfxSpeed", 1f);
                    player.Alpha = ReadFloat(info, "GfxAlpha", 1f);
                }

                var parent = ResolveParent(info, modelRoot, warnings);
                gfxRoot.name = SanitizeNodeName(
                    Path.GetFileNameWithoutExtension(rel.Replace('\\', '/')));
                gfxRoot.transform.SetParent(parent, false);

                if (ReadFloat(info, "GfxDelayTime", 0f) != 0f)
                {
                    Warn(
                        warnings,
                        "CoGfx " + gfxRoot.name +
                        ": GfxDelayTime (delayed follow) not implemented (same as Godot)");
                }

                LinkParams(info, gfxRoot, modelRoot, warnings);
                count++;
            }

            return count;
        }

        public static Quaternion BuildHookRotation(EcmCoGfxEvent info)
        {
            // Godot: Quaternion(BACK, HookRot) * Quaternion(UP, HookYaw) * Quaternion(RIGHT, HookPitch)
            // with radians; Unity AngleAxis uses degrees.
            var pitch = ReadFloat(info, "HookPitch", 0f) * Mathf.Rad2Deg;
            var yaw = ReadFloat(info, "HookYaw", 0f) * Mathf.Rad2Deg;
            var rot = ReadFloat(info, "HookRot", 0f) * Mathf.Rad2Deg;
            return Quaternion.AngleAxis(rot, Vector3.back) *
                Quaternion.AngleAxis(yaw, Vector3.up) *
                Quaternion.AngleAxis(pitch, Vector3.right);
        }

        public static Vector3 BuildHookOffset(EcmCoGfxEvent info)
        {
            string raw;
            if (info == null ||
                info.Properties == null ||
                !info.Properties.TryGetValue("HookOffset", out raw) ||
                string.IsNullOrWhiteSpace(raw))
                return Vector3.zero;

            var parts = raw.Split(',');
            if (parts.Length < 3)
                return Vector3.zero;

            float x, y, z;
            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
                !float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out y) ||
                !float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out z))
                return Vector3.zero;

            return new Vector3(x, y, z);
        }

        public static void ApplyTranMatrix(Transform target, EcmCoGfxEvent info, float scale)
        {
            if (target == null)
                return;
            target.localPosition = BuildHookOffset(info);
            target.localRotation = BuildHookRotation(info);
            target.localScale = Vector3.one * scale;
        }

        public static Transform FindHook(Transform root, string hookName)
        {
            if (root == null || string.IsNullOrWhiteSpace(hookName))
                return null;
            return FindHookRecursive(root, hookName.Trim());
        }

        /// <summary>Godot ecm_cogfx._as_bool: strip_edges().to_int() != 0.</summary>
        public static bool AsIntBool(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            int n;
            return int.TryParse(
                       value.Trim(),
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out n) &&
                   n != 0;
        }

        public static string ScanfS(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            var cleaned = value.Replace('\t', ' ').Trim();
            var space = cleaned.IndexOf(' ');
            return space < 0 ? cleaned : cleaned.Substring(0, space);
        }

        public static string SanitizeNodeName(string n)
        {
            if (string.IsNullOrEmpty(n))
                return "GFX";
            foreach (var c in new[] { ".", ":", "@", "/", "\"", "%" })
                n = n.Replace(c, "_");
            return string.IsNullOrEmpty(n) ? "GFX" : n;
        }

        private static GameObject BuildGfx(
            string rel,
            GameDataIndex index,
            IList<string> warnings)
        {
            if (BuildGfxOverride != null)
                return BuildGfxOverride(rel, index, warnings);

            if (index == null)
                return null;

            var list = warnings as List<string> ?? new List<string>();
            var built = GfxSceneBuilder.TryBuildFromGameData(rel, index, null, list);
            if (!ReferenceEquals(list, warnings) && warnings != null)
            {
                foreach (var w in list)
                    Warn(warnings, w);
            }

            return built != null ? built.Root : null;
        }

        private static Transform ResolveParent(
            EcmCoGfxEvent info,
            GameObject modelRoot,
            IList<string> warnings)
        {
            string hook;
            info.Properties.TryGetValue("HookName", out hook);
            hook = (hook ?? string.Empty).Trim();
            if (hook.Length == 0)
            {
                string rotWith;
                if (info.Properties.TryGetValue("GfxRotWithModel", out rotWith) &&
                    rotWith != null &&
                    rotWith.Trim() == "0")
                {
                    Warn(
                        warnings,
                        "CoGfx without Hook with GfxRotWithModel 0: rotates with the model here");
                }

                return modelRoot.transform;
            }

            var found = FindHook(modelRoot.transform, hook);
            if (found == null)
            {
                Warn(warnings, "CoGfx hook " + hook + " missing on skeleton; GFX on model root");
                return modelRoot.transform;
            }

            // Godot creates BoneAttachment "CoGfx_<bone>" under the skeleton; parent GFX there.
            var attName = SanitizeNodeName(AttachPrefix + found.gameObject.name);
            var existing = found.Find(attName);
            if (existing != null)
                return existing;

            var att = new GameObject(attName);
            att.transform.SetParent(found, false);
            return att.transform;
        }

        private static Transform FindHookRecursive(Transform node, string hookName)
        {
            if (node.gameObject != null &&
                string.Equals(node.gameObject.name, hookName, StringComparison.OrdinalIgnoreCase))
                return node;

            for (var i = 0; i < node.childCount; i++)
            {
                var hit = FindHookRecursive(node.GetChild(i), hookName);
                if (hit != null)
                    return hit;
            }

            return null;
        }

        private static void LinkParams(
            EcmCoGfxEvent info,
            GameObject gfxRoot,
            GameObject modelRoot,
            IList<string> warnings)
        {
            if (info.Parameters == null || info.Parameters.Count == 0)
                return;

            var links = new List<EcmCoGfxParamDriver.Link>();
            foreach (var p in info.Parameters)
            {
                if (p == null)
                    continue;

                string idRaw;
                if (!p.TryGetValue("ParamId", out idRaw))
                {
                    Warn(warnings, "CoGfx " + gfxRoot.name + ": GfxParam without ParamId");
                    continue;
                }

                int id;
                if (!int.TryParse(idRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out id) ||
                    (id != ParamLtnPos1 && id != ParamLtnPos2))
                {
                    Warn(
                        warnings,
                        "CoGfx " + gfxRoot.name + ": GfxParam " + idRaw + " not supported");
                    continue;
                }

                string eleRaw;
                p.TryGetValue("ele", out eleRaw);
                var eleName = ScanfS(eleRaw);
                var element = FindElement(gfxRoot.transform, eleName);
                if (element == null)
                {
                    Warn(
                        warnings,
                        "CoGfx " + gfxRoot.name + ": element " + eleName +
                        " for lightning endpoint missing in GFX");
                    continue;
                }

                // Godot: first element with that Name; if it is not a built 150, skip.
                var lightning = element as GfxLightningDriver;
                if (lightning == null || element.SystemId != "150")
                {
                    Warn(
                        warnings,
                        "CoGfx " + gfxRoot.name + ": element " + eleName + " (ID " +
                        element.SystemId + ") is not lightning; endpoint not set");
                    continue;
                }

                var lp = lightning.Params;
                if (lp == null || !(lp.Pos1Enable || lp.Pos2Enable))
                {
                    Warn(
                        warnings,
                        "CoGfx " + gfxRoot.name + ": lightning " + eleName +
                        " without Pos1Enable / Pos2Enable; endpoint not set");
                    continue;
                }

                if (!(id == ParamLtnPos1 ? lp.HasStartPos : lp.HasEndPos))
                {
                    Warn(
                        warnings,
                        "CoGfx " + gfxRoot.name + ": lightning " + eleName + " without " +
                        (id == ParamLtnPos1 ? "StartPos" : "EndPos") + " in the .gfx; endpoint not set");
                    continue;
                }

                string isCmd;
                p.TryGetValue("ParamDataIsCmd", out isCmd);
                // Godot ecm_cogfx._as_bool: strip_edges().to_int() != 0
                var cmd = AsIntBool(isCmd);
                string hookRaw;
                p.TryGetValue(cmd ? "ParamDataCmd" : "ParamDataHook", out hookRaw);
                var hookName = ScanfS(hookRaw);
                var hook = FindHook(modelRoot.transform, hookName);
                if (hook == null)
                {
                    Warn(
                        warnings,
                        "CoGfx " + gfxRoot.name + ": hook " + hookName +
                        " for lightning endpoint missing on skeleton");
                    continue;
                }

                links.Add(new EcmCoGfxParamDriver.Link
                {
                    Lightning = lightning,
                    Hook = hook,
                    IsStart = id == ParamLtnPos1
                });
            }

            if (links.Count == 0)
                return;

            var driver = gfxRoot.GetComponent<EcmCoGfxParamDriver>();
            if (driver == null)
                driver = gfxRoot.AddComponent<EcmCoGfxParamDriver>();
            driver.SetLinks(links);
        }

        /// <summary>ecm_cogfx._find_element: first direct element child whose Name (scanf) matches.</summary>
        private static GfxElementDriver FindElement(Transform gfx, string eleName)
        {
            if (gfx == null || string.IsNullOrEmpty(eleName))
                return null;

            for (var i = 0; i < gfx.childCount; i++)
            {
                var child = gfx.GetChild(i);
                if (child == null || child.gameObject == null)
                    continue;
                var driver = child.gameObject.GetComponent<GfxElementDriver>();
                if (driver == null || string.IsNullOrEmpty(driver.ElementName))
                    continue;
                if (ScanfS(driver.ElementName) == eleName)
                    return driver;
            }

            return null;
        }

        private static float ReadFloat(EcmCoGfxEvent info, string key, float def)
        {
            string raw;
            if (info == null ||
                info.Properties == null ||
                !info.Properties.TryGetValue(key, out raw))
                return def;

            float v;
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out v)
                ? v
                : def;
        }

        private static void Warn(IList<string> warnings, string message)
        {
            if (warnings == null || string.IsNullOrEmpty(message))
                return;
            for (var i = 0; i < warnings.Count; i++)
            {
                if (warnings[i] == message)
                    return;
            }

            warnings.Add(message);
        }
    }
}
