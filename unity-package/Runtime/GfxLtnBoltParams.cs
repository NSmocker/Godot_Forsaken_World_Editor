using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// GFX ID 151 A3DGFXLtnBolt params + stroke builder (create_id_151.gd).
    /// Pure C#. Strokes become child 150 lightnings.
    /// </summary>
    public sealed class GfxLtnBoltParams
    {
        public const int MaxStrokes = 512;
        public const float DefaultDeviationDeg = 30f;

        public string SystemId = "151";
        public string Name = string.Empty;
        public int ElementIndex;
        public string TexFile = string.Empty;
        public int SrcBlend;
        public int DestBlend = 6;
        public bool ZEnable = true;
        public bool Warp;
        public int Priority;

        public int TexRow = 1;
        public int TexCol = 1;
        public float TexInterval = 50f;
        public bool TileMode;
        public float TexSpeed1;
        public float TexSpeed2;
        public bool UReverse;
        public bool VReverse;
        public bool UvExchange;

        public float Deviation = DefaultDeviationDeg * (float)Math.PI / 180f;
        public float StepMin = 5f;
        public float StepMax = 6f;
        public float WidthStart = 0.15f;
        public float WidthEnd = 0.03f;
        public float AlphaStart = 1f;
        public float AlphaEnd;
        public float StrokeAmp = 0.4f;
        public int MaxSteps = 1;
        public int MaxBranches = 1;
        public int Interval = 100;
        public int PerBolts = 3;
        public int Circles = 3;

        public int RandomSeed;

        public bool IsAdditiveBlend
        {
            get
            {
                if (Warp) return false;
                return DestBlend == 2 || DestBlend == 4;
            }
        }

        public static GfxLtnBoltParams FromProperties(
            IDictionary<string, string> properties,
            string systemId,
            int elementIndex)
        {
            var p = new GfxLtnBoltParams
            {
                SystemId = string.IsNullOrEmpty(systemId) ? "151" : systemId,
                ElementIndex = elementIndex
            };
            if (properties == null)
                return p;

            p.Name = GfxPropertyValues.Get(properties, "Name", string.Empty);
            p.TexFile = GfxPropertyValues.Get(properties, "TexFile", string.Empty);
            if (string.IsNullOrEmpty(p.TexFile))
                p.TexFile = GfxPropertyValues.Get(properties, "Tex File", string.Empty);
            p.SrcBlend = GfxPropertyValues.Int(properties, "SrcBlend", 0);
            p.DestBlend = GfxPropertyValues.Int(properties, "DestBlend", 6);
            p.ZEnable = GfxPropertyValues.Bool(properties, "ZEnable", true);
            p.Warp = GfxPropertyValues.Bool(properties, "Warp", false);
            p.Priority = GfxPropertyValues.Int(properties, "Priority", 0);

            p.TexRow = Math.Max(1, GfxPropertyValues.Int(properties, "TexRow", 1));
            p.TexCol = Math.Max(1, GfxPropertyValues.Int(properties, "TexCol", 1));
            p.TexInterval = Math.Max(1f, GfxPropertyValues.Float(properties, "TexInterval", 50f));
            p.TileMode = GfxPropertyValues.Bool(properties, "TileMode", false);
            p.TexSpeed1 = GfxPropertyValues.Float(properties, "TexSpeed_1", 0f);
            p.TexSpeed2 = GfxPropertyValues.Float(properties, "TexSpeed_2", 0f);
            p.UReverse = GfxPropertyValues.Bool(properties, "UReverse", false);
            p.VReverse = GfxPropertyValues.Bool(properties, "VReverse", false);
            p.UvExchange = GfxPropertyValues.Bool(properties, "UVExchg", false);

            p.Deviation = GfxPropertyValues.Float(
                properties, "Deviation", DefaultDeviationDeg * (float)Math.PI / 180f);
            p.StepMin = GfxPropertyValues.Float(properties, "StepMin", 5f);
            p.StepMax = GfxPropertyValues.Float(properties, "StepMax", 6f);
            p.WidthStart = GfxPropertyValues.Float(properties, "WidthStart", 0.15f);
            p.WidthEnd = GfxPropertyValues.Float(properties, "WidthEnd", 0.03f);
            p.AlphaStart = GfxPropertyValues.Float(properties, "AlphaStart", 1f);
            p.AlphaEnd = GfxPropertyValues.Float(properties, "AlphaEnd", 0f);
            p.StrokeAmp = GfxPropertyValues.Float(properties, "StrokeAmp", 0.4f);
            p.MaxSteps = GfxPropertyValues.Int(properties, "MaxSteps", 1);
            p.MaxBranches = GfxPropertyValues.Int(properties, "MaxBranches", 1);
            p.Interval = GfxPropertyValues.Int(properties, "Interval", 100);
            p.PerBolts = GfxPropertyValues.Int(properties, "PerBolts", 3);
            p.Circles = GfxPropertyValues.Int(properties, "Circles", 3);
            return p;
        }
    }

    public sealed class GfxLtnBoltStroke
    {
        public float[] Start = GfxMath.Vec3Zero();
        public float[] End = GfxMath.Vec3Zero();
        public float StepLen;
        public float WidthStart;
        public float WidthEnd;
        public float AlphaStart;
        public float AlphaEnd;
    }

    /// <summary>
    /// BuildStrokeDir / AddPath (create_id_151.gd). Uses Godot-like PCG32 randf.
    /// </summary>
    public static class GfxLtnBoltBuilder
    {
        public static List<float[]> StrokeDirs(int perBolts, int circles)
        {
            var dirs = new List<float[]>();
            if (perBolts < 1 || circles < 1)
                return dirs;
            var nBolts = perBolts * 2;
            var bas = new List<float[]>();
            for (var i = 0; i < nBolts; i++)
            {
                var a = i * GfxLightningEval.Tau / nBolts;
                bas.Add(GfxMath.Vec3((float)Math.Sin(a), (float)Math.Cos(a), 0f));
            }
            dirs.AddRange(bas);
            for (var c = 1; c < circles; c++)
            {
                var q = GfxMath.QuatFromAxisAngle(
                    GfxMath.Vec3(0f, 1f, 0f),
                    (float)Math.PI / circles * c);
                for (var j = 1; j < nBolts; j++)
                {
                    if (j == perBolts) continue;
                    dirs.Add(GfxMath.RotateVec3(q, bas[j]));
                }
            }
            return dirs;
        }

        public static List<GfxLtnBoltStroke> BuildStrokes(
            GfxLtnBoltParams bolt, float kp0Scale, int seed)
        {
            var strokes = new List<GfxLtnBoltStroke>();
            if (bolt == null) return strokes;
            var rng = new GodotPcg(seed);
            var dirs = StrokeDirs(bolt.PerBolts, bolt.Circles);
            var scale = kp0Scale <= 0f ? 1f : kp0Scale;
            foreach (var d in dirs)
            {
                AddPath(
                    strokes, rng, bolt, d, 1, 1, GfxMath.Vec3Zero(), scale);
            }
            return strokes;
        }

        private static void AddPath(
            List<GfxLtnBoltStroke> strokes,
            GodotPcg rng,
            GfxLtnBoltParams bolt,
            float[] dir,
            int step,
            int branch,
            float[] start,
            float scale)
        {
            if (step > bolt.MaxSteps || strokes.Count >= GfxLtnBoltParams.MaxStrokes)
                return;
            var f = rng.Randf();
            var stepLen = (bolt.StepMin * (1f - f) + bolt.StepMax * f) * scale;
            var end = GfxMath.Add3(start, GfxMath.Scale3(dir, stepLen));
            var maxSteps = (float)Math.Max(1, bolt.MaxSteps);
            var r0 = (step - 1) / maxSteps;
            var r1 = step / maxSteps;
            strokes.Add(new GfxLtnBoltStroke
            {
                Start = start,
                End = end,
                StepLen = stepLen,
                WidthStart = GfxMath.Lerp(bolt.WidthStart, bolt.WidthEnd, r0),
                WidthEnd = GfxMath.Lerp(bolt.WidthStart, bolt.WidthEnd, r1),
                AlphaStart = GfxMath.Lerp(bolt.AlphaStart, bolt.AlphaEnd, r0),
                AlphaEnd = GfxMath.Lerp(bolt.AlphaStart, bolt.AlphaEnd, r1)
            });
            step += 1;
            if (branch < bolt.MaxBranches)
            {
                branch += 1;
                if (rng.Randf() < bolt.MaxBranches / maxSteps)
                    AddPath(strokes, rng, bolt, dir, step, branch, end, scale);
            }
            AddPath(strokes, rng, bolt, dir, step, branch, end, scale);
        }

        /// <summary>Stroke → 150 lightning params (create_id_151 _stroke_data).</summary>
        public static GfxLightningParams ToLightningParams(
            GfxLtnBoltParams bolt,
            GfxLtnBoltStroke s,
            IDictionary<string, string> parentProps)
        {
            var p = GfxLightningParams.FromProperties(
                parentProps ?? new Dictionary<string, string>(), "150", bolt.ElementIndex);
            // Clear bolt-only keys by overwriting with stroke values
            p.SystemId = "150";
            p.Name = bolt.Name;
            p.TexFile = bolt.TexFile;
            p.DestBlend = bolt.DestBlend;
            p.ZEnable = bolt.ZEnable;
            p.Warp = bolt.Warp;
            p.TexRow = bolt.TexRow;
            p.TexCol = bolt.TexCol;
            p.TexInterval = bolt.TexInterval;
            p.TileMode = bolt.TileMode;
            p.TexSpeed1 = bolt.TexSpeed1;
            p.TexSpeed2 = bolt.TexSpeed2;
            p.UReverse = bolt.UReverse;
            p.VReverse = bolt.VReverse;
            p.UvExchange = bolt.UvExchange;

            p.BufLen = 256;
            p.Amplitude = 2f;
            p.WaveLen = 1f;
            p.Persistence = 0.8f;
            p.OctaveNum = 5;
            p.StartPos = GfxMath.Clone3(s.Start);
            p.EndPos = GfxMath.Clone3(s.End);
            p.Segs = 8;
            p.LightNum = 1;
            p.WaveLen2 = s.StepLen / 3f;
            p.Interval = bolt.Interval;
            p.Width = s.WidthStart;
            p.Width2 = s.WidthEnd;
            p.Width3 = (s.WidthStart + s.WidthEnd) * 0.5f;
            p.Alpha = s.AlphaStart;
            p.Alpha2 = s.AlphaEnd;
            p.Alpha3 = (s.AlphaStart + s.AlphaEnd) * 0.5f;
            p.Amplitude2 = bolt.StrokeAmp;
            p.Pos1Enable = false;
            p.Pos2Enable = false;
            p.UseNormal = false;
            p.Filter = 1;
            p.HasDeviation = true;
            p.Deviation = bolt.Deviation;
            p.RandomSeed = bolt.RandomSeed;
            p.HasAmpCurve = false;
            p.ElementStartTime = GfxPropertyValues.Float(
                parentProps, "StartTime", 0f);
            return p;
        }

        /// <summary>Godot 4 RandomNumberGenerator PCG32 (seed/randf).</summary>
        public sealed class GodotPcg
        {
            private ulong state;
            private ulong inc;

            public GodotPcg(int seed)
            {
                SetSeed((ulong)(uint)seed);
            }

            /// <summary>Godot RandomPCG::DEFAULT_INC (PCG_DEFAULT_INC_64); seed() keeps it.</summary>
            public const ulong DefaultInc = 1442695040888963407UL;

            public void SetSeed(ulong seed)
            {
                // pcg32_srandom_r(&pcg, seed, current_inc)
                state = 0;
                inc = (DefaultInc << 1) | 1UL;
                Next();
                state += seed;
                Next();
            }

            private uint Next()
            {
                var old = state;
                state = old * 6364136223846793005UL + inc;
                var xorshifted = (uint)(((old >> 18) ^ old) >> 27);
                var rot = (int)(old >> 59);
                return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
            }

            public uint Rand() => Next();

            /// <summary>
            /// Godot 4 RandomPCG::randf (CLZ32 path): exponent offset from one draw, significand
            /// from a second draw with MSB/LSB set.
            /// </summary>
            public float Randf()
            {
                var protoExpOffset = Next();
                if (protoExpOffset == 0)
                    return 0f;
                var significand = Next() | 0x80000001u;
                return (float)Math.Pow(2.0, -32 - LeadingZeros(protoExpOffset)) * (float)significand;
            }

            private static int LeadingZeros(uint x)
            {
                var n = 0;
                if (x == 0) return 32;
                while ((x & 0x80000000u) == 0) { n++; x <<= 1; }
                return n;
            }
        }
    }
}
