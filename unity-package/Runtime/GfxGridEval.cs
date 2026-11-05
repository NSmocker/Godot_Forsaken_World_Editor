using System;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Grid animation + color compose for ID 210/211 (A3DGfxGridAnimation::GetValue /
    /// _setColorComposed). Pure C#.
    /// </summary>
    public static class GfxGridEval
    {
        public sealed class Sample
        {
            public float[] Positions; // xyz * n
            public float[] Colors;    // rgba 0..1 * n
            public bool Hidden;       // KP alpha < 5/255
        }

        public static Sample Evaluate(
            GfxGridParams p,
            float spanMs,
            float[] kpColor)
        {
            var sample = new Sample();
            if (p == null || p.VertCount <= 0)
            {
                sample.Hidden = true;
                return sample;
            }

            var n = p.VertCount;
            sample.Positions = new float[n * 3];
            sample.Colors = new float[n * 4];

            var kp = kpColor ?? GfxMath.ColorWhite();
            var kpA255 = kp[3] * 255f;
            if (kpA255 < 5f)
            {
                sample.Hidden = true;
                Array.Copy(p.Positions ?? new float[n * 3], sample.Positions, sample.Positions.Length);
                Array.Copy(p.Colors ?? new float[n * 4], sample.Colors, sample.Colors.Length);
                return sample;
            }

            var kd = new[] { kp[0] * 255f, kp[1] * 255f, kp[2] * 255f, kp[3] * 255f };

            for (var i = 0; i < n; i++)
            {
                float px, py, pz, cr, cg, cb, ca;
                if (p.HasAnimation)
                    SampleAnim(p, spanMs, i, out px, out py, out pz, out cr, out cg, out cb, out ca);
                else
                {
                    var po = i * 3;
                    var co = i * 4;
                    px = p.Positions[po];
                    py = p.Positions[po + 1];
                    pz = p.Positions[po + 2];
                    cr = p.Colors[co] * 255f;
                    cg = p.Colors[co + 1] * 255f;
                    cb = p.Colors[co + 2] * 255f;
                    ca = p.Colors[co + 3] * 255f;
                }

                // (point * kp) >> 8  → /256 then /255
                var o = i * 3;
                sample.Positions[o] = px;
                sample.Positions[o + 1] = py;
                sample.Positions[o + 2] = pz;
                var co2 = i * 4;
                sample.Colors[co2] = (float)Math.Floor(cr * kd[0] / 256f) / 255f;
                sample.Colors[co2 + 1] = (float)Math.Floor(cg * kd[1] / 256f) / 255f;
                sample.Colors[co2 + 2] = (float)Math.Floor(cb * kd[2] / 256f) / 255f;
                sample.Colors[co2 + 3] = (float)Math.Floor(ca * kd[3] / 256f) / 255f;
            }

            return sample;
        }

        private static void SampleAnim(
            GfxGridParams p,
            float t,
            int idx,
            out float px, out float py, out float pz,
            out float cr, out float cg, out float cb, out float ca)
        {
            var keys = p.KeyTimes.Length;
            var n = p.VertCount;
            var a = 0;
            var b = 0;
            var r = 0f;
            if (keys > 1)
            {
                if (t < p.KeyTimes[0])
                {
                    a = 0;
                    b = 0;
                }
                else if (t >= p.KeyTimes[keys - 1])
                {
                    a = keys - 1;
                    b = a;
                }
                else
                {
                    for (var k = 1; k < keys; k++)
                    {
                        if (t < p.KeyTimes[k])
                        {
                            a = k - 1;
                            b = k;
                            var den = p.KeyTimes[b] - p.KeyTimes[a];
                            r = den > 0f ? (t - p.KeyTimes[a]) / den : 0f;
                            break;
                        }
                    }
                }
            }

            Fetch(p, a, idx, n, out var ax, out var ay, out var az, out var ar, out var ag, out var ab, out var aa);
            Fetch(p, b, idx, n, out var bx, out var by, out var bz, out var br, out var bg, out var bb, out var ba);
            px = ax * (1f - r) + bx * r;
            py = ay * (1f - r) + by * r;
            pz = az * (1f - r) + bz * r;
            // Interp_Color: floor channel mix
            cr = (float)Math.Floor(ar * (1f - r) + br * r);
            cg = (float)Math.Floor(ag * (1f - r) + bg * r);
            cb = (float)Math.Floor(ab * (1f - r) + bb * r);
            ca = (float)Math.Floor(aa * (1f - r) + ba * r);
        }

        private static void Fetch(
            GfxGridParams p, int key, int idx, int n,
            out float x, out float y, out float z,
            out float r, out float g, out float b, out float a)
        {
            var posIdx = (key * 2 * n + idx) * 4;
            var colIdx = ((key * 2 + 1) * n + idx) * 4;
            x = p.AnimData[posIdx];
            y = p.AnimData[posIdx + 1];
            z = p.AnimData[posIdx + 2];
            r = p.AnimData[colIdx];
            g = p.AnimData[colIdx + 1];
            b = p.AnimData[colIdx + 2];
            a = p.AnimData[colIdx + 3];
        }
    }
}
