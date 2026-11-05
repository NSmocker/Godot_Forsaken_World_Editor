using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// GFX ID 210/211 grid decal params (create_id_210.gd / create_id_211.gd).
    /// </summary>
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

            var n = p.VertCount;
            p.Positions = new float[n * 3];
            p.Colors = new float[n * 4];
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

                var colorKey = i == 0 ? "dwColor" : "dwColor_" + (i + 1);
                // Godot: first dwColor, then dwColor_2.. — Unity tracker numbers first as dwColor or dwColor_1
                var col = GfxPropertyValues.ArgbColor(properties, colorKey, null);
                if (col == null || (i == 0 && !properties.ContainsKey(colorKey)))
                {
                    if (properties.ContainsKey("dwColor_1"))
                        col = GfxPropertyValues.ArgbColor(properties, "dwColor_1", GfxMath.ColorWhite());
                    else
                        col = GfxPropertyValues.ArgbColor(properties, "dwColor", GfxMath.ColorWhite());
                }
                var co = i * 4;
                p.Colors[co] = col[0];
                p.Colors[co + 1] = col[1];
                p.Colors[co + 2] = col[2];
                p.Colors[co + 3] = col[3];
            }

            ReadAnimation(properties, gridAnimations, p);
            return p;
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
}
