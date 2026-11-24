using System;
using System.Collections.Generic;
using System.Globalization;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// GFX ID 210/211 grid decal params (create_id_210.gd / create_id_211.gd).
    /// </summary>
    [Serializable]
    public sealed class GfxGridParams
    {
        public const int MaxAnimVerts = 100;
        public const int MaxAnimKeys = 5;

        public string SystemId = "210";
        public int Index;
        public int W;
        public int H;
        public float[] Positions; // xyz * W*H
        public float[] Colors;    // rgba 0..1 * W*H
        public float[] KeyTimes;  // ms
        public float[] AnimData;  // keys*2 rows * n cols as rgba floats (pos.xyz+0 / col 0..255)
        public bool UReverse;
        public bool VReverse;
        public bool UVExchg;
        public bool AffByScl;
        public bool RotFromView;
        public float ZOffset;
        public string TexFile = string.Empty;
        public bool Is2D;
        public GfxSpriteParamsSerial RenderSettings;

        public int VertCount => Math.Max(0, W) * Math.Max(0, H);
        public bool HasAnimation =>
            KeyTimes != null && KeyTimes.Length > 0 && AnimData != null && AnimData.Length > 0;

        public static GfxGridParams FromElement(
            IDictionary<string, string> properties,
            IList<string> gridAnimations,
            string systemId,
            int index)
        {
            var p = new GfxGridParams
            {
                SystemId = string.IsNullOrEmpty(systemId) ? "210" : systemId,
                Index = index,
                Is2D = systemId == "211"
            };
            if (properties == null)
                return p;

            p.W = Math.Max(0, GfxPropertyValues.Int(properties, "wNumber", 0));
            p.H = Math.Max(0, GfxPropertyValues.Int(properties, "hNumber", 0));
            p.UReverse = GfxPropertyValues.Bool(properties, "UReverse", false);
            p.VReverse = GfxPropertyValues.Bool(properties, "VReverse", false);
            p.UVExchg = GfxPropertyValues.Bool(properties, "UVExchg", false);
            p.AffByScl = GfxPropertyValues.Bool(properties, "AffByScl", false);
            p.RotFromView = GfxPropertyValues.Bool(properties, "RotFromView", false);
            p.ZOffset = GfxPropertyValues.Float(properties, "fZOffset", 0f);
            p.TexFile = GfxPropertyValues.Get(properties, "TexFile", string.Empty);
            var render = GfxSpriteParams.FromProperties(properties, systemId, index);
            // Grid reverses and exchanges the absolute coordinates after
            // resolving the base atlas rectangle.
            render.UReverse = render.VReverse = render.UvExchange = false;
            p.RenderSettings = GfxSpriteParamsSerial.FromParams(render);

            var n = p.VertCount;
            p.Positions = new float[n * 3];
            for (var i = 0; i < n; i++)
            {
                var key = i == 0 ? "GridVector_1" : "GridVector_" + (i + 1);
                // Godot uses GridVector_1..n; first may also be stored as GridVector via NumberedKeys miss — try both
                var raw = GfxPropertyValues.Get(properties, key, null);
                if (raw == null && i == 0)
                    raw = GfxPropertyValues.Get(properties, "GridVector", "");
                if (raw == null)
                    raw = string.Empty;
                var parts = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var o = i * 3;
                p.Positions[o] = parts.Length > 0 ? GfxPropertyValues.ParseFloat(parts[0], 0f) : 0f;
                p.Positions[o + 1] = parts.Length > 1 ? GfxPropertyValues.ParseFloat(parts[1], 0f) : 0f;
                p.Positions[o + 2] = parts.Length > 2 ? GfxPropertyValues.ParseFloat(parts[2], 0f) : 0f;
            }

            p.Colors = ReadVertexColors(properties, n);
            ReadAnimation(properties, gridAnimations, p);
            return p;
        }

        internal static float[] ReadVertexColors(IDictionary<string, string> properties, int count)
        {
            var colors = new float[count * 4];
            for (var i = 0; i < count; i++)
            {
                var key = i == 0 ? "dwColor" : "dwColor_" + (i + 1);
                var raw = GfxPropertyValues.Get(properties, key).Trim();
                if (i == 0 && raw.Length == 0)
                    raw = GfxPropertyValues.Get(properties, "dwColor_1").Trim();
                if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    raw = raw.Substring(2);
                // A3DGridDecalEx serializes vertex colors with %x, without zero padding.
                if (!uint.TryParse(raw, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var argb))
                    argb = uint.MaxValue;
                var offset = i * 4;
                colors[offset] = ((argb >> 16) & 0xff) / 255f;
                colors[offset + 1] = ((argb >> 8) & 0xff) / 255f;
                colors[offset + 2] = (argb & 0xff) / 255f;
                colors[offset + 3] = (argb >> 24) / 255f;
            }
            return colors;
        }

        private static void ReadAnimation(
            IDictionary<string, string> properties,
            IList<string> gridAnimations,
            GfxGridParams p)
        {
            var n = p.VertCount;
            var keys = Math.Min(GfxPropertyValues.Int(properties, "keyNumber", 0), MaxAnimKeys);
            if (keys <= 0 || n <= 0 || n > MaxAnimVerts ||
                gridAnimations == null || gridAnimations.Count == 0)
                return;

            var text = string.Concat(gridAnimations).Trim();
            if (string.IsNullOrEmpty(text))
                return;

            byte[] raw;
            try
            {
                raw = Convert.FromBase64String(text);
            }
            catch (FormatException)
            {
                return;
            }

            var keyBytes = 4 + 16 * n;
            var times = new float[keys];
            // rows: keys*2, cols: n, each texel rgba float
            var data = new float[keys * 2 * n * 4];
            for (var k = 0; k < keys; k++)
            {
                var baseOff = k * keyBytes;
                if (baseOff + keyBytes > raw.Length)
                    return;
                times[k] = BitConverter.ToInt32(raw, baseOff);
                for (var i = 0; i < n; i++)
                {
                    var o = baseOff + 4 + 16 * i;
                    var x = BitConverter.ToSingle(raw, o);
                    var y = BitConverter.ToSingle(raw, o + 4);
                    var z = BitConverter.ToSingle(raw, o + 8);
                    var argb = BitConverter.ToUInt32(raw, o + 12);
                    // pos row
                    var posIdx = (k * 2 * n + i) * 4;
                    data[posIdx] = x;
                    data[posIdx + 1] = y;
                    data[posIdx + 2] = z;
                    data[posIdx + 3] = 0f;
                    // color row (0..255)
                    var colIdx = ((k * 2 + 1) * n + i) * 4;
                    data[colIdx] = (argb >> 16) & 0xFF;
                    data[colIdx + 1] = (argb >> 8) & 0xFF;
                    data[colIdx + 2] = argb & 0xFF;
                    data[colIdx + 3] = (argb >> 24) & 0xFF;
                }
            }

            p.KeyTimes = times;
            p.AnimData = data;
        }
    }

    [Serializable]
    internal sealed class GfxGridPixelConstant
    {
        public float Initial;
        public int LoopCount;
        public int[] Intervals = Array.Empty<int>();
        public float[] Values = Array.Empty<float>();

        internal static GfxGridPixelConstant From(GfxPixelShaderConstant source)
        {
            var result = new GfxGridPixelConstant
            {
                Initial = ReadRed(source.Properties),
                LoopCount = GfxPropertyValues.Int(source.Properties, "PSLoopCount"),
                Intervals = new int[source.Targets.Count],
                Values = new float[source.Targets.Count]
            };
            for (int i = 0; i < result.Intervals.Length; ++i)
            {
                result.Intervals[i] = GfxPropertyValues.Int(source.Targets[i], "PSInterval");
                result.Values[i] = ReadRed(source.Targets[i]);
            }
            return result;
        }

        private static float ReadRed(IDictionary<string, string> properties)
        {
            var value = GfxPropertyValues.Get(properties, "PSConstValue");
            int separator = value.IndexOf(',');
            return GfxPropertyValues.ParseFloat(separator < 0 ? value : value.Substring(0, separator));
        }

        internal float Evaluate(float timeMs)
        {
            if (Intervals.Length == 0)
                return Initial;
            float total = 0f;
            foreach (int interval in Intervals)
            {
                if (interval == 0)
                {
                    total = 0f;
                    break;
                }
                total += Math.Abs((float)interval);
            }
            if (total > 0f)
            {
                if (LoopCount >= 0 && timeMs >= LoopCount * total)
                    return Values[Values.Length - 1];
                timeMs %= total;
            }

            float from = Initial;
            float elapsed = timeMs;
            for (int i = 0; i < Intervals.Length; i++)
            {
                int interval = Intervals[i];
                float to = Values[i];
                if (interval == 0)
                    return from + to * elapsed * .001f;
                float duration = Math.Abs((float)interval);
                if (timeMs <= duration)
                    return interval < 0 ? from : GfxMath.Lerp(from, to, timeMs / duration);
                timeMs -= duration;
                from = to;
            }
            return from;
        }
    }
}
