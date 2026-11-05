using System;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// CPU lightning strip for GFX ID 150 / 152 (create_id_150/152.gd A3DGFXLightning*).
    /// Pure C#; no Unity types. Noise/filter match Godot's approximated APerlinNoise1D.
    /// </summary>
    public static class GfxLightningEval
    {
        public const float Tau = 6.28318530718f;
        public const float Pi = 3.14159265359f;

        public sealed class MeshData
        {
            public float[] Positions = Array.Empty<float>();
            public float[] Uvs = Array.Empty<float>();
            public float[] Colors = Array.Empty<float>();
            public int[] Triangles = Array.Empty<int>();
            public int PairCount;
        }

        public sealed class SheetUv
        {
            public float ScaleU = 1f;
            public float ScaleV = 1f;
            public float OffsetU;
            public float OffsetV;
            public static SheetUv Identity => new SheetUv();
        }

        public static float Fract(float x) => x - (float)Math.Floor(x);

        public static float LtnRnd(float n, int seed) =>
            Fract((float)Math.Sin(n * 91.3458f + seed * 1.731f) * 47453.5453f);

        public static float LtnHash(float n, float ch, int seed) =>
            Fract((float)Math.Sin(n * 127.1f + ch * 311.7f + seed * 0.0131f) * 43758.5453f) * 2f - 1f;

        public static float LtnVNoise(float x, float ch, int seed, int bufLen)
        {
            var buf = Math.Max(1f, bufLen);
            var i = (float)Math.Floor(x);
            var f = x - i;
            var a = LtnHash(GfxMath.Fmod(i, buf), ch, seed);
            var b = LtnHash(GfxMath.Fmod(i + 1f, buf), ch, seed);
            return GfxMath.Lerp(a, b, f * f * (3f - 2f * f));
        }

        public static float LtnPerlin(float x, float ch, GfxLightningParams p)
        {
            if (p == null) return 0f;
            var v = 0f;
            var amp = p.Amplitude;
            var freq = 1f / Math.Max(1f, p.WaveLen);
            var oct = Math.Max(0, p.OctaveNum);
            for (var o = 0; o < 8; o++)
            {
                if (o >= oct) break;
                v += amp * LtnVNoise(x * freq, ch * 7f + o * 13f, p.RandomSeed, p.BufLen);
                amp *= p.Persistence;
                freq *= 2f;
            }
            return v;
        }

        /// <summary>filter(): 0 triangle, 1 rectangle, 2 sine, 3 trapezoid.</summary>
        public static float LtnFilter(int type, float d, float v)
        {
            if (d < 0f) return v;
            if (d > 1f) d = d - (float)Math.Floor(d);
            if (type == 0) return 2f * (d > 0.5f ? 1f - d : d);
            if (type == 2) return v * (float)Math.Sin(d * Pi);
            if (type == 3) return v * (d < 0.2f ? d * 5f : (d > 0.8f ? (1f - d) * 5f : 1f));
            return v; // rectangle
        }

        public static float LtnMid(float r, float s, float m, float e) =>
            r < 0.5f ? GfxMath.Lerp(s, m, r * 2f) : GfxMath.Lerp(m, e, (r - 0.5f) * 2f);

        public static float LtnAmplitude(GfxLightningParams p, float tMs)
        {
            if (p == null) return 0f;
            if (!p.HasAmpCurve)
                return p.Amplitude2;
            if (tMs <= p.AmpStartTime || p.AmpDestNum == 0)
                return p.AmpValues[0];
            var acc = p.AmpStartTime;
            for (var k = 0; k < 5; k++)
            {
                if (k >= p.AmpDestNum) break;
                if (tMs > acc + p.AmpTimes[k])
                {
                    acc += p.AmpTimes[k];
                    continue;
                }
                return GfxMath.Lerp(
                    p.AmpValues[k],
                    p.AmpValues[k + 1],
                    (tMs - acc) / Math.Max(p.AmpTimes[k], 0.0001f));
            }
            var idx = p.AmpDestNum < 5 ? p.AmpDestNum : 5;
            return p.AmpValues[idx];
        }

        public static float[] CalcVertVec(float[] d)
        {
            var n = GfxMath.Normalize3(d);
            if (Math.Abs(n[1]) > 0.999f)
                return GfxMath.Normalize3(GfxMath.Cross3(n, GfxMath.Vec3(1f, 0f, 0f)));
            return GfxMath.Normalize3(GfxMath.Cross3(GfxMath.Vec3(0f, 1f, 0f), n));
        }

        public static float[] SafeNorm(float[] v)
        {
            var len = GfxMath.Length3(v);
            if (len > 0.000001f)
                return new[] { v[0] / len, v[1] / len, v[2] / len };
            return GfxMath.Vec3Zero();
        }

        /// <summary>
        /// Sample KP0 CurveMove into IntPos/Dir/Up arrays (create_id_150 InitBuf curve).
        /// </summary>
        public static void BuildCurveSamples(
            GfxCurvePathData path,
            int segs,
            out float[][] intPos,
            out float[][] intDir,
            out float[][] intUp,
            out float curveLen)
        {
            segs = Math.Max(1, segs);
            intPos = new float[segs + 1][];
            intDir = new float[segs + 1][];
            intUp = new float[segs + 1][];
            curveLen = 0f;
            if (path == null || path.Points == null || path.Points.Length == 0)
            {
                for (var i = 0; i <= segs; i++)
                {
                    intPos[i] = GfxMath.Vec3Zero();
                    intDir[i] = GfxMath.Vec3(0f, 0f, 1f);
                    intUp[i] = GfxMath.Vec3(0f, 1f, 0f);
                }
                return;
            }

            curveLen = path.TotalLength;
            for (var i = 0; i <= segs; i++)
            {
                float[] pos;
                float[] quat;
                GfxCurvePath.InterpPoint(path, i / (float)segs, out pos, out quat);
                intPos[i] = pos;
            }
            for (var i = 1; i <= segs; i++)
            {
                var d = SafeNorm(GfxMath.Sub3(intPos[i], intPos[i - 1]));
                intDir[i - 1] = d;
                intUp[i - 1] = CalcVertVec(d);
            }
            intDir[segs] = GfxMath.Clone3(intDir[segs - 1]);
            intUp[segs] = CalcVertVec(intDir[segs]);
            // Godot: int_dir.append(int_dir[segs-1]); int_up for last via calc on that
            // Also fills int_dir[0] from first segment above for i=1 only — fix index 0:
            // create_id_150: for i in range(_segs+1): append pos; if i>0: dir/up for i-1
            // then append dir[segs-1] and up(dir[segs]). So intDir[0]..[segs-1] from loop,
            // intDir[segs] = intDir[segs-1]. intUp same. Good — but intDir[0] set when i=1.
        }

        public static SheetUv EvaluateSheet(GfxLightningParams p, float currentTimeMs)
        {
            if (p == null) return SheetUv.Identity;
            float scaleU = 1f, scaleV = 1f, offsetU = 0f, offsetV = 0f;
            if (p.TileMode)
            {
                var cols = Math.Max(1f, p.TexCol);
                var rows = Math.Max(1f, p.TexRow);
                var tSec = currentTimeMs / 1000f;
                scaleU = cols;
                scaleV = rows;
                offsetU = tSec * p.TexSpeed1;
                offsetV = tSec * p.TexSpeed2;
            }
            else if (p.TexCol > 1 || p.TexRow > 1)
            {
                var cols = (float)Math.Max(1, p.TexCol);
                var rows = (float)Math.Max(1, p.TexRow);
                var total = cols * rows;
                var interval = Math.Max(1f, p.TexInterval);
                var frame = GfxMath.Fmod((float)Math.Floor(currentTimeMs / interval), total);
                var frameX = GfxMath.Fmod(frame, cols);
                var frameY = (float)Math.Floor(frame / cols);
                scaleU = 1f / cols;
                scaleV = 1f / rows;
                offsetU = frameX / cols;
                offsetV = frameY / rows;
            }

            // Lightning applies UReverse/VReverse on strip UV (create_id_150), then sheet.
            // UVExchg after cell_uv in Godot _gen_uv_post.
            return new SheetUv
            {
                ScaleU = scaleU,
                ScaleV = scaleV,
                OffsetU = offsetU,
                OffsetV = offsetV
            };
        }

        public static MeshData Build(
            GfxLightningParams p,
            GfxKeypointState kp,
            float elementTimeMs,
            float activeTimeMs,
            float gfxAlpha,
            float[] camLocal)
        {
            var data = new MeshData();
            if (p == null || !p.IsValid)
                return EmptyMesh();

            if (kp == null)
            {
                kp = new GfxKeypointState
                {
                    Pos = GfxMath.Vec3Zero(),
                    Dir = GfxMath.QuatIdentity(),
                    Color = GfxMath.ColorWhite(),
                    Scale = 1f
                };
            }

            var elapsed = Math.Max(0f, elementTimeMs - p.ElementStartTime);
            // When LocalStartTime already equals ElementStartTime, elapsed = span.
            // Drivers pass elementTime; ElementStartTime used for amp curve relative clock.
            // Prefer span-style: if ElementStartTime was applied as LocalStartTime, elapsed≈span.
            var interval = Math.Max(50f, p.Interval);
            var epoch = (float)Math.Floor(elapsed / interval);
            var fStart = (float)Math.Floor(LtnRnd(epoch, p.RandomSeed) * 1023f);
            var amp = p.WaveMoving
                ? LtnAmplitude(p, elapsed)
                : LtnAmplitude(p, epoch * interval);
            var waveT = activeTimeMs * 0.01f * p.WaveMovingSpeed;
            var cam = camLocal != null && camLocal.Length >= 3
                ? camLocal
                : GfxMath.Vec3(0f, 0f, -10f);

            if (p.Curved)
            {
                if (p.IsEx)
                    return BuildExCurve(p, kp, elapsed, activeTimeMs, gfxAlpha, fStart, amp, waveT, cam, epoch, interval);
                return BuildCurve(p, kp, elapsed, gfxAlpha, fStart, amp, waveT, cam);
            }
            return BuildStraight(p, kp, elapsed, gfxAlpha, fStart, amp, waveT, cam, epoch);
        }

        private static MeshData EmptyMesh()
        {
            return new MeshData
            {
                Positions = new float[6],
                Uvs = new float[4],
                Colors = new float[8],
                Triangles = new[] { 0, 1, 0 },
                PairCount = 1
            };
        }

        private static MeshData BuildStraight(
            GfxLightningParams p,
            GfxKeypointState kp,
            float elapsed,
            float gfxAlpha,
            float fStart,
            float amp,
            float waveT,
            float[] cam,
            float epoch)
        {
            var segs = p.Segs;
            var pairs = p.PairCount;
            var rot = GfxMath.MulQuat(
                kp.Dir,
                GfxMath.QuatFromAxisAngle(GfxMath.Vec3(0f, 0f, 1f), kp.Rad));
            var external = p.Pos1Enable || p.Pos2Enable;
            var startLocal = GfxMath.Clone3(p.StartPos);
            var endLocal = GfxMath.Clone3(p.EndPos);

            if (p.HasDeviation)
            {
                var axis = GfxMath.Sub3(endLocal, startLocal);
                var dir0 = SafeNorm(axis);
                var up0 = GfxMath.RotateVec3(
                    GfxMath.QuatFromAxisAngle(dir0, LtnRnd(epoch * 3f + 1f, p.RandomSeed) * Tau),
                    CalcVertVec(dir0));
                var len = GfxMath.Length3(axis);
                endLocal = GfxMath.Add3(
                    startLocal,
                    GfxMath.Scale3(
                        GfxMath.RotateVec3(
                            GfxMath.QuatFromAxisAngle(
                                up0, LtnRnd(epoch * 3f + 2f, p.RandomSeed) * p.Deviation),
                            dir0),
                        len));
            }

            float[] vs, ve;
            if (external)
            {
                vs = startLocal;
                ve = endLocal;
            }
            else
            {
                vs = GfxMath.Add3(GfxMath.RotateVec3(rot, startLocal), kp.Pos);
                ve = GfxMath.Add3(GfxMath.RotateVec3(rot, endLocal), kp.Pos);
            }

            var dir = GfxMath.Sub3(ve, vs);
            var dist = GfxMath.Length3(dir);
            dir = SafeNorm(dir);
            var up = CalcVertVec(dir);
            var fwaves = dist / Math.Max(p.WaveLen2, 0.000001f);
            var fseg = fwaves / segs;

            var wStart = p.Width * kp.Scale;
            var wEnd = p.EffectiveWidth2 * kp.Scale;
            var wMid = p.EffectiveWidth3 * kp.Scale;
            var aStart = p.Alpha;
            var aEnd = p.EffectiveAlpha2;
            var aMid = p.EffectiveAlpha3;
            var sheet = EvaluateSheet(p, elapsed + p.ElementStartTime);
            var cr = kp.Color[0];
            var cg = kp.Color[1];
            var cb = kp.Color[2];
            var caBase = kp.Color[3] * gfxAlpha;

            var positions = new float[pairs * 2 * 3];
            var uvs = new float[pairs * 2 * 2];
            var colors = new float[pairs * 2 * 4];
            var tris = new int[Math.Max(0, pairs - 1) * 6];
            if (pairs < 2)
                tris = new[] { 0, 1, 0 };

            for (var i = 0; i < pairs; i++)
            {
                float[] pos;
                float[] viewUp;
                float width;
                float alpha;
                var ratio = pairs > 1 ? i / (float)(pairs - 1) : 0f;
                var nMod = i % (segs * 2);
                if (nMod == 0)
                {
                    pos = vs;
                    width = wStart;
                    alpha = aStart;
                    viewUp = SafeNorm(GfxMath.Cross3(dir, GfxMath.Sub3(vs, cam)));
                }
                else if (nMod == segs)
                {
                    pos = ve;
                    width = wEnd;
                    alpha = aEnd;
                    viewUp = SafeNorm(GfxMath.Cross3(dir, GfxMath.Sub3(ve, cam)));
                }
                else
                {
                    var nModSeg = i % segs;
                    var odd = ((i / segs) % 2) == 1;
                    var portion = (odd ? segs - nModSeg : nModSeg) / (float)segs;
                    var nCount = i - ((i - 1) / segs + 1);
                    var fin = nCount / (float)segs;
                    if (!p.WaveMoving)
                    {
                        var x = fStart + nCount * fseg;
                        var randUp = GfxMath.RotateVec3(
                            GfxMath.QuatFromAxisAngle(dir, LtnPerlin(x, 0f, p) * Tau),
                            up);
                        pos = GfxMath.Add3(
                            GfxMath.Add3(vs, GfxMath.Scale3(dir, portion * dist)),
                            GfxMath.Scale3(
                                randUp,
                                LtnFilter(p.Filter, fin, LtnPerlin(x, 1f, p)) * amp));
                        viewUp = SafeNorm(GfxMath.Cross3(dir, GfxMath.Sub3(pos, cam)));
                    }
                    else
                    {
                        var loops = p.FixWaveLength ? fwaves : p.NumWaves;
                        pos = GfxMath.Add3(vs, GfxMath.Scale3(dir, portion * dist));
                        viewUp = SafeNorm(GfxMath.Cross3(dir, GfxMath.Sub3(pos, cam)));
                        pos = GfxMath.Add3(
                            pos,
                            GfxMath.Scale3(
                                viewUp,
                                (float)Math.Sin(fin * Pi * loops) * (float)Math.Sin(waveT) * amp));
                    }
                    width = LtnMid(portion, wStart, wMid, wEnd);
                    alpha = LtnMid(portion, aStart, aMid, aEnd);
                }

                EmitPair(
                    positions, uvs, colors, i, pos, viewUp, width, alpha,
                    ratio, p, sheet, cr, cg, cb, caBase);
            }

            for (var i = 0; i < pairs - 1; i++)
            {
                var b = i * 2;
                var t = i * 6;
                tris[t] = b;
                tris[t + 1] = b + 1;
                tris[t + 2] = b + 2;
                tris[t + 3] = b + 2;
                tris[t + 4] = b + 1;
                tris[t + 5] = b + 3;
            }

            return new MeshData
            {
                Positions = positions,
                Uvs = uvs,
                Colors = colors,
                Triangles = tris,
                PairCount = pairs
            };
        }

        private static MeshData BuildCurve(
            GfxLightningParams p,
            GfxKeypointState kp,
            float elapsed,
            float gfxAlpha,
            float fStart,
            float amp,
            float waveT,
            float[] cam)
        {
            var segs = p.Segs;
            var pairs = segs + 1;
            var intPos = p.CurveIntPos;
            var intDir = p.CurveIntDir;
            var intUp = p.CurveIntUp;
            if (intPos == null || intPos.Length < segs + 1)
                return EmptyMesh();

            var fwaves = p.CurveLen / Math.Max(p.WaveLen2, 0.000001f);
            var fseg = fwaves / segs;
            var wStart = p.Width * kp.Scale;
            var wEnd = p.EffectiveWidth2 * kp.Scale;
            var wMid = p.EffectiveWidth3 * kp.Scale;
            var aStart = p.Alpha;
            var aEnd = p.EffectiveAlpha2;
            var aMid = p.EffectiveAlpha3;
            var sheet = EvaluateSheet(p, elapsed + p.ElementStartTime);
            var cr = kp.Color[0];
            var cg = kp.Color[1];
            var cb = kp.Color[2];
            var caBase = kp.Color[3] * gfxAlpha;

            var positions = new float[pairs * 2 * 3];
            var uvs = new float[pairs * 2 * 2];
            var colors = new float[pairs * 2 * 4];
            var tris = new int[Math.Max(0, pairs - 1) * 6];

            var centers = new float[pairs][];
            var ups = new float[pairs][];
            var last = intPos[0];
            var lastUp = SafeNorm(GfxMath.Cross3(
                intDir[0],
                p.UseNormal ? p.Normal : GfxMath.Sub3(last, cam)));
            centers[0] = last;
            ups[0] = lastUp;
            for (var j = 1; j <= segs; j++)
            {
                var pt = CurvePos(p, j, fStart, fseg, fwaves, amp, waveT);
                var nu = SafeNorm(GfxMath.Cross3(
                    GfxMath.Sub3(pt, last),
                    p.UseNormal ? p.Normal : GfxMath.Sub3(pt, cam)));
                if (GfxMath.Dot3(nu, lastUp) < 0f)
                    nu = GfxMath.Neg3(nu);
                last = pt;
                lastUp = nu;
                centers[j] = pt;
                ups[j] = nu;
            }

            for (var i = 0; i < pairs; i++)
            {
                var ratio = pairs > 1 ? i / (float)(pairs - 1) : 0f;
                float width, alpha;
                if (i == 0) { width = wStart; alpha = aStart; }
                else if (i == segs) { width = wEnd; alpha = aEnd; }
                else
                {
                    width = LtnMid(ratio, wStart, wMid, wEnd);
                    alpha = LtnMid(ratio, aStart, aMid, aEnd);
                }
                EmitPair(
                    positions, uvs, colors, i, centers[i], ups[i], width, alpha,
                    ratio, p, sheet, cr, cg, cb, caBase);
            }

            for (var i = 0; i < pairs - 1; i++)
            {
                var b = i * 2;
                var t = i * 6;
                tris[t] = b;
                tris[t + 1] = b + 1;
                tris[t + 2] = b + 2;
                tris[t + 3] = b + 2;
                tris[t + 4] = b + 1;
                tris[t + 5] = b + 3;
            }

            return new MeshData
            {
                Positions = positions,
                Uvs = uvs,
                Colors = colors,
                Triangles = tris,
                PairCount = pairs
            };
        }

        private static float[] CurvePos(
            GfxLightningParams p, int j, float fStart, float fseg, float fwaves,
            float amp, float waveT)
        {
            var segs = p.Segs;
            var intPos = p.CurveIntPos;
            var intDir = p.CurveIntDir;
            var intUp = p.CurveIntUp;
            if (j <= 0) return intPos[0];
            if (j >= segs) return intPos[segs];
            var k = j - 1;
            var fin = k / (float)segs;
            if (p.WaveMoving)
            {
                var loops = p.FixWaveLength ? fwaves : p.NumWaves;
                var s = (float)Math.Sin(fin * Pi * loops) * (float)Math.Sin(waveT) * amp;
                // Angelica adds scalar onto all three components
                return GfxMath.Add3(intPos[k + 1], GfxMath.Vec3(s, s, s));
            }
            var x = fStart + k * fseg;
            var q = GfxMath.QuatFromAxisAngle(intDir[k + 1], LtnPerlin(x, 0f, p) * Tau);
            return GfxMath.Add3(
                intPos[k + 1],
                GfxMath.Scale3(
                    GfxMath.RotateVec3(q, intUp[k]),
                    LtnFilter(p.Filter, fin, LtnPerlin(x, 1f, p)) * amp));
        }

        /// <summary>152 curved RenderCurveMethod with append/tail life.</summary>
        private static MeshData BuildExCurve(
            GfxLightningParams p,
            GfxKeypointState kp,
            float elapsed,
            float activeTimeMs,
            float gfxAlpha,
            float fStart,
            float amp,
            float waveT,
            float[] cam,
            float epoch,
            float interval)
        {
            var segs = p.Segs;
            var pairs = segs + 1;
            var intPos = p.CurveIntPos;
            var intDir = p.CurveIntDir;
            if (intPos == null || intPos.Length < segs + 1)
                return EmptyMesh();

            // lx_t: for endless / LocalTotal<=0 use elapsed; otherwise loop-aware.
            // Driver passes elapsed already relative to element start; ResetLoopEnd
            // handled approximately via elapsed when LocalTotal mapped externally.
            var lxT = elapsed;
            if (p.ResetLoopEnd)
            {
                // Approximate: treat LocalTotalTime from ElementStart via Amp — driver
                // should pass loop-local elapsed when ResetLoopEnd. Keep elapsed as-is
                // when not separately provided.
            }

            var lxLife = Math.Max(1f, p.VertsLife);
            var lxFirst = 0;
            var lxCount = pairs;
            var lxPer = 0f;
            if (p.IsAppend)
            {
                lxPer = (float)Math.Truncate(p.Kp0TimeSpan / pairs);
                if (lxPer <= 0f)
                    lxCount = 0;
                else
                {
                    var lxAdded = (int)Math.Min(pairs, Math.Floor(lxT / lxPer));
                    if (p.IsTailDisappear)
                        lxFirst = (int)Math.Max(0f, Math.Floor((lxT - lxLife) / lxPer));
                    lxCount = Math.Max(0, lxAdded - lxFirst);
                }
            }

            var fwaves = p.CurveLen / Math.Max(p.WaveLen2, 0.000001f);
            var fseg = fwaves / segs;
            var wStart = p.Width * kp.Scale;
            var wEnd = p.EffectiveWidth2 * kp.Scale;
            var wMid = p.EffectiveWidth3 * kp.Scale;
            var aStart = p.Alpha;
            var aEnd = p.EffectiveAlpha2;
            var aMid = p.EffectiveAlpha3;
            var sheet = EvaluateSheet(p, elapsed + p.ElementStartTime);
            var cr = kp.Color[0];
            var cg = kp.Color[1];
            var cb = kp.Color[2];
            var caBase = kp.Color[3] * gfxAlpha;

            // Allocate full pair strip; invisible pairs get width 0
            var positions = new float[pairs * 2 * 3];
            var uvs = new float[pairs * 2 * 2];
            var colors = new float[pairs * 2 * 4];
            var tris = new int[Math.Max(0, pairs - 1) * 6];

            var k0 = Math.Min(lxFirst, segs);
            var centers = new float[pairs][];
            var ups = new float[pairs][];
            var last = intPos[k0];
            var lastUp = SafeNorm(GfxMath.Cross3(
                intDir[k0],
                p.UseNormal ? p.Normal : GfxMath.Sub3(last, cam)));
            centers[0] = last;
            ups[0] = lastUp;
            for (var j = 1; j < pairs; j++)
            {
                var nidx = k0 + j;
                if (nidx > segs) nidx = segs;
                float[] pt;
                if (j == lxCount - 1 || nidx >= segs)
                    pt = intPos[nidx];
                else
                    pt = ExRandPos(p, nidx, fStart, fseg, fwaves, amp, waveT);
                var nu = SafeNorm(GfxMath.Cross3(
                    GfxMath.Sub3(pt, last),
                    p.UseNormal ? p.Normal : GfxMath.Sub3(pt, cam)));
                if (GfxMath.Dot3(nu, lastUp) < 0f)
                    nu = GfxMath.Neg3(nu);
                last = pt;
                lastUp = nu;
                centers[j] = pt;
                ups[j] = nu;
            }

            for (var i = 0; i < pairs; i++)
            {
                var lxVis = lxCount > 0 ? Math.Min(i, lxCount - 1) : 0;
                var ratio = lxCount > 1 ? lxVis / (float)(lxCount - 1) : 0f;
                float width, alpha;
                if (lxVis == 0) { width = wStart; alpha = aStart; }
                else if (lxVis == lxCount - 1) { width = wEnd; alpha = aEnd; }
                else
                {
                    width = LtnMid(ratio, wStart, wMid, wEnd);
                    alpha = LtnMid(ratio, aStart, aMid, aEnd);
                }
                if (p.TailFadeOut && p.IsAppend && lxPer > 0f)
                {
                    var age = GfxMath.Clamp(
                        lxT - (k0 + lxVis + 1) * lxPer, 0f, lxLife);
                    alpha *= 1f - age / lxLife;
                }
                if (i >= lxCount || lxCount <= 0)
                {
                    width = 0f;
                    alpha = 0f;
                }

                var center = centers[lxVis] ?? intPos[0];
                var viewUp = ups[lxVis] ?? CalcVertVec(GfxMath.Vec3(0f, 0f, 1f));
                // renderside offset
                EmitPairEx(
                    positions, uvs, colors, i, center, viewUp, width, alpha,
                    ratio, p, sheet, cr, cg, cb, caBase);
            }

            for (var i = 0; i < pairs - 1; i++)
            {
                var b = i * 2;
                var t = i * 6;
                tris[t] = b;
                tris[t + 1] = b + 1;
                tris[t + 2] = b + 2;
                tris[t + 3] = b + 2;
                tris[t + 4] = b + 1;
                tris[t + 5] = b + 3;
            }

            return new MeshData
            {
                Positions = positions,
                Uvs = uvs,
                Colors = colors,
                Triangles = tris,
                PairCount = pairs
            };
        }

        private static float[] ExRandPos(
            GfxLightningParams p, int k, float fStart, float fseg, float fwaves,
            float amp, float waveT)
        {
            var segs = p.Segs;
            var fin = k / (float)segs;
            if (p.WaveMoving)
            {
                var loops = p.FixWaveLength ? fwaves : p.NumWaves;
                var s = (float)Math.Sin(fin * Pi * loops) * (float)Math.Sin(waveT) * amp;
                return GfxMath.Add3(p.CurveIntPos[k + 1], GfxMath.Vec3(s, s, s));
            }
            var x = fStart + k * fseg;
            var q = GfxMath.QuatFromAxisAngle(p.CurveIntDir[k + 1], LtnPerlin(x, 0f, p) * Tau);
            return GfxMath.Add3(
                p.CurveIntPos[k + 1],
                GfxMath.Scale3(
                    GfxMath.RotateVec3(q, p.CurveIntUp[k]),
                    LtnFilter(p.Filter, fin, LtnPerlin(x, 1f, p)) * amp));
        }

        private static void EmitPair(
            float[] positions, float[] uvs, float[] colors, int i,
            float[] pos, float[] viewUp, float width, float alpha, float ratio,
            GfxLightningParams p, SheetUv sheet,
            float cr, float cg, float cb, float caBase)
        {
            for (var s = 0; s < 2; s++)
            {
                var side = (float)s;
                var off = side * 2f - 1f;
                var vx = pos[0] + viewUp[0] * width * off;
                var vy = pos[1] + viewUp[1] * width * off;
                var vz = pos[2] + viewUp[2] * width * off;
                var vi = (i * 2 + s) * 3;
                positions[vi] = vx;
                positions[vi + 1] = vy;
                positions[vi + 2] = vz;

                var cu = p.UReverse ? 1f - ratio : ratio;
                var cv = (side < 0.5f) != p.VReverse ? 1f : 0f;
                if (p.UvExchange)
                {
                    var tmp = cu;
                    cu = cv;
                    cv = tmp;
                }
                cu = cu * sheet.ScaleU + sheet.OffsetU;
                cv = cv * sheet.ScaleV + sheet.OffsetV;
                var ui = (i * 2 + s) * 2;
                uvs[ui] = cu;
                uvs[ui + 1] = cv;

                var ci = (i * 2 + s) * 4;
                colors[ci] = cr;
                colors[ci + 1] = cg;
                colors[ci + 2] = cb;
                colors[ci + 3] = alpha * caBase;
            }
        }

        private static void EmitPairEx(
            float[] positions, float[] uvs, float[] colors, int i,
            float[] pos, float[] viewUp, float width, float alpha, float ratio,
            GfxLightningParams p, SheetUv sheet,
            float cr, float cg, float cb, float caBase)
        {
            for (var s = 0; s < 2; s++)
            {
                var side = (float)s;
                float off;
                if (p.RenderSide == 1) off = side * 2f;
                else if (p.RenderSide == 2) off = side * 2f - 2f;
                else off = side * 2f - 1f;
                var vx = pos[0] + viewUp[0] * width * off;
                var vy = pos[1] + viewUp[1] * width * off;
                var vz = pos[2] + viewUp[2] * width * off;
                var vi = (i * 2 + s) * 3;
                positions[vi] = vx;
                positions[vi + 1] = vy;
                positions[vi + 2] = vz;

                var cu = p.UReverse ? 1f - ratio : ratio;
                var cv = (side < 0.5f) != p.VReverse ? 1f : 0f;
                if (p.UvExchange)
                {
                    var tmp = cu;
                    cu = cv;
                    cv = tmp;
                }
                cu = cu * sheet.ScaleU + sheet.OffsetU;
                cv = cv * sheet.ScaleV + sheet.OffsetV;
                var ui = (i * 2 + s) * 2;
                uvs[ui] = cu;
                uvs[ui + 1] = cv;

                var ci = (i * 2 + s) * 4;
                colors[ci] = cr;
                colors[ci + 1] = cg;
                colors[ci + 2] = cb;
                colors[ci + 3] = alpha * caBase;
            }
        }
    }
}
