using System;
using System.Collections.Generic;
using System.Globalization;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Parses .gfx textual property values the same way Godot element/ctrl scripts do.
    /// </summary>
    public static class GfxPropertyValues
    {
        public static string Get(
            IDictionary<string, string> properties, string key, string fallback = "")
        {
            if (properties == null || string.IsNullOrEmpty(key))
                return fallback;
            string value;
            return properties.TryGetValue(key, out value) ? value : fallback;
        }

        public static float Float(
            IDictionary<string, string> properties, string key, float fallback = 0f)
        {
            return ParseFloat(Get(properties, key, null), fallback);
        }

        public static int Int(
            IDictionary<string, string> properties, string key, int fallback = 0)
        {
            return ParseInt(Get(properties, key, null), fallback);
        }

        public static bool Bool(
            IDictionary<string, string> properties, string key, bool fallback = false)
        {
            return ParseBool(Get(properties, key, null), fallback);
        }

        public static float[] Vec3(
            IDictionary<string, string> properties, string key, float[] fallback = null)
        {
            return ParseVec3(Get(properties, key, null), fallback ?? GfxMath.Vec3Zero());
        }

        public static float[] Quat(
            IDictionary<string, string> properties, string key, float[] fallback = null)
        {
            return ParseQuat(Get(properties, key, null), fallback ?? GfxMath.QuatIdentity());
        }

        public static float[] ArgbColor(
            IDictionary<string, string> properties, string key, float[] fallback = null)
        {
            return ParseArgbColor(Get(properties, key, null), fallback ?? GfxMath.ColorWhite());
        }

        public static float ParseFloat(string raw, float fallback = 0f)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;
            float value;
            if (float.TryParse(
                raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return value;
            return fallback;
        }

        public static int ParseInt(string raw, int fallback = 0)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;
            long value;
            if (long.TryParse(
                raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                if (value > int.MaxValue) return int.MaxValue;
                if (value < int.MinValue) return int.MinValue;
                return (int)value;
            }

            return fallback;
        }

        /// <summary>
        /// Godot gfx_data_node param read (TYPE_BOOL): <c>val == "1" or val.to_lower() == "true"</c>.
        /// Absent key (null) → fallback; any present value other than 1/true → false.
        /// </summary>
        public static bool ParseBool(string raw, bool fallback = false)
        {
            if (raw == null)
                return fallback;
            var s = raw.Trim().ToLowerInvariant();
            return s == "1" || s == "true";
        }

        public static float[] ParseVec3(string raw, float[] fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return GfxMath.Clone3(fallback);
            var clean = raw.Replace("(", "").Replace(")", "").Replace(" ", "");
            var parts = clean.Split(',');
            if (parts.Length < 3)
                return GfxMath.Clone3(fallback);
            return new[]
            {
                ParseFloat(parts[0], 0f),
                ParseFloat(parts[1], 0f),
                ParseFloat(parts[2], 0f)
            };
        }

        public static float[] ParseQuat(string raw, float[] fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return GfxMath.Clone4(fallback);
            var clean = raw.Replace("(", "").Replace(")", "").Replace(" ", "");
            var parts = clean.Split(',');
            if (parts.Length < 4)
                return GfxMath.Clone4(fallback);
            return GfxMath.NormalizeQuat(new[]
            {
                ParseFloat(parts[0], 0f),
                ParseFloat(parts[1], 0f),
                ParseFloat(parts[2], 0f),
                ParseFloat(parts[3], 1f)
            });
        }

        /// <summary>
        /// Packed ARGB signed/unsigned int string -> RGBA floats (gfx_ctrl_107 / create_id_130).
        /// </summary>
        public static float[] ParseArgbColor(string raw, float[] fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return GfxMath.Clone4(fallback);
            long signed;
            if (!long.TryParse(
                raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out signed))
                return GfxMath.Clone4(fallback);
            var u = unchecked((uint)(int)signed);
            var a = ((u >> 24) & 0xFFu) / 255f;
            var r = ((u >> 16) & 0xFFu) / 255f;
            var g = ((u >> 8) & 0xFFu) / 255f;
            var b = (u & 0xFFu) / 255f;
            return new[] { r, g, b, a };
        }
    }
}
