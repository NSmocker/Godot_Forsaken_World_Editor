using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// CPU lightning-trail strip for GFX ID 180 (create_id_180.gd A3DLtnTrail math).
    /// Deterministic lt_rnd zigzag; samples keypoints on a Tick_Ms grid.
    /// Pure C#; no Unity types.
    /// </summary>
    public static class GfxLtnTrailEval
    {
        public const float EndlessTicks = 16777216f;
        public const float Tau = 6.28318530718f;

        public sealed class MeshData
        {
            public float[] Positions = Array.Empty<float>();
            public float[] Uvs = Array.Empty<float>();
            public float[] Colors = Array.Empty<float>();
            public int[] Triangles = Array.Empty<int>();
            public int PairCount;
        }

        public sealed class TickGrid
        {
            public float Cycle;
            public float TauSinceStart;
            public float J = EndlessTicks;
            public float Full;
            public float T = 40f;
            public float Now;
        }

        public static float UnitRandom(float n, int ch, int seed)
        {
            unchecked
            {
                uint h = (uint)(int)n * 747796405u +
                         (uint)ch * 2891336453u +
                         (uint)seed * 2654435761u;
                h = ((h >> (int)((h >> 28) + 4)) ^ h) * 277803737u;
                h = (h >> 22) ^ h;
                return (h % 16777216u) / 16777216f;
            }
        }

        public static float[] VertVec(float[] d)
        {
            var n = GfxMath.Normalize3(d);
            if (Math.Abs(n[1]) > 0.999f)
                return GfxMath.Normalize3(GfxMath.Cross3(n, GfxMath.Vec3(1f, 0f, 0f)));
            return GfxMath.Normalize3(GfxMath.Cross3(GfxMath.Vec3(0f, 1f, 0f), n));
        }

        public static float TexU(float m)
        {
            if (m < 0.5f)
                return 0f;
            return 0.2f * (1f + GfxMath.Fmod(m - 1f, 5f));
        }

        public static float TickTime(float k, float j, float full, float t) =>
            (float)Math.Floor(k / j) * full + GfxMath.Fmod(k, j) * t;

        public static float TickSpan(float k, float j, float t) =>
            GfxMath.Fmod(k, j) * t;

        public static TickGrid BuildGrid(
            float activeTimeMs,
            float localStart,
            float localTotal,
            float globalLoop,
            int repeatCount,
            float repeatDelay,
            bool endless,
            float tickMs)
        {
            var g = new TickGrid { T = Math.Max(1f, tickMs) };
            var fRepeat = (float)repeatCount;
            var tGlob = activeTimeMs;
            if (fRepeat >= 0f)
            {
                var loop = Math.Max(0.001f, globalLoop);
                g.Cycle = (float)Math.Floor(activeTimeMs / loop);
                tGlob = GfxMath.Fmod(activeTimeMs, loop);
            }

            var tau = Math.Max(0f, tGlob - localStart);
            g.TauSinceStart = tau;
            var r = 0f;
            if (!endless)
            {
                var activeDur = Math.Max(0.001f, localTotal - localStart);
                g.J = Math.Max(1f, (float)Math.Ceiling(activeDur / g.T));
                g.Full = activeDur + Math.Max(0f, repeatDelay);
                r = (float)Math.Floor(tau / g.Full);
            }

            g.Now = r * g.J + Math.Min(
                (float)Math.Floor((tau - r * g.Full) / g.T),
                g.J - 1f);
            return g;
        }

        public static void Step(
            float n,
            float[] p,
            float[] qd,
            ref float[] c,
            ref float[] q,
            float amp,
            float amp2,
            int seed)
        {
            var portion = UnitRandom(n, 0, seed);
            var vdir = GfxMath.Normalize3(GfxMath.Sub3(p, c));
            var ampDir = GfxMath.RotateVec3(
                GfxMath.QuatFromAxisAngle(vdir, UnitRandom(n, 1, seed) * Tau),
                VertVec(vdir));
            var ampLen = amp + (amp2 - amp) * UnitRandom(n, 2, seed);
            ampDir = GfxMath.Scale3(ampDir, ampLen);
            c = GfxMath.Add3(
                GfxMath.Add3(
                    GfxMath.Scale3(c, 1f - portion),
                    GfxMath.Scale3(p, portion)),
                ampDir);
            q = GfxMath.SlerpQuat(q, qd, portion);
        }

        /// <summary>
        /// Evaluates kp transform at element-local span (ms): pos + dir*Rad about Z.
        /// Optional parent world (Bind=0) applied by caller.
        /// </summary>
        public static void PointAt(
            IList<GfxKeypointSpec> keypoints,
            float spanMs,
            out float[] pos,
            out float[] quat)
        {
            GfxKeypointState state;
            if (keypoints != null && keypoints.Count > 0)
                state = GfxKeypointEval.Evaluate(keypoints, spanMs);
            else
                state = new GfxKeypointState();
            pos = GfxMath.Clone3(state.Pos);
            var radQ = GfxMath.QuatFromAxisAngle(GfxMath.Vec3(0f, 0f, 1f), state.Rad);
            quat = GfxMath.MulQuat(state.Dir ?? GfxMath.QuatIdentity(), radQ);
        }

        public static void Center(
            IList<GfxKeypointSpec> keypoints,
            float k,
            float s,
            float j,
            float t,
            float amp,
            float amp2,
            int seed,
            Func<float, int, float[]> parentOrigin,
            Func<float, int, float[]> parentRot,
            Func<float, int, float> parentScale,
            bool bind,
            out float[] c,
            out float[] q)
        {
            var k0 = Math.Max(s, k - GfxLtnTrailParams.ChainSteps);
            var span0 = TickSpan(k0, j, t);
            var slot0 = (int)GfxMath.Fmod(k0, 1e9f); // slot resolved by caller helpers
            PointAt(keypoints, span0, out c, out q);
            if (!bind)
            {
                var o = parentOrigin(k0, slot0);
                var r = parentRot(k0, slot0);
                var sc = parentScale(k0, slot0);
                c = GfxMath.Add3(GfxMath.RotateVec3(r, GfxMath.Scale3(c, sc)), o);
                q = GfxMath.MulQuat(r, q);
            }

            if (k0 > s)
            {
                float[] ps, qs;
                PointAt(keypoints, TickSpan(s, j, t), out ps, out qs);
                if (!bind)
                {
                    var o = parentOrigin(s, (int)s);
                    var r = parentRot(s, (int)s);
                    var sc = parentScale(s, (int)s);
                    ps = GfxMath.Add3(GfxMath.RotateVec3(r, GfxMath.Scale3(ps, sc)), o);
                }

                if (GfxMath.Length3(GfxMath.Sub3(ps, c)) > 0.00001f)
                {
                    var th = UnitRandom(k0, 3, seed) * Tau;
                    var z = UnitRandom(k0, 4, seed) * 2f - 1f;
                    var rxy = (float)Math.Sqrt(Math.Max(0f, 1f - z * z));
                    var ampLen = amp + (amp2 - amp) * UnitRandom(k0, 5, seed);
                    c = GfxMath.Add3(
                        c,
                        GfxMath.Scale3(
                            GfxMath.Vec3(rxy * (float)Math.Cos(th), rxy * (float)Math.Sin(th), z),
                            ampLen));
                }
            }

            for (var i = 1; i <= GfxLtnTrailParams.ChainSteps; i++)
            {
                var kk = k0 + i;
                if (kk > k)
                    break;
                float[] p, qd;
                PointAt(keypoints, TickSpan(kk, j, t), out p, out qd);
                if (!bind)
                {
                    var o = parentOrigin(kk, (int)kk);
                    var r = parentRot(kk, (int)kk);
                    var sc = parentScale(kk, (int)kk);
                    p = GfxMath.Add3(GfxMath.RotateVec3(r, GfxMath.Scale3(p, sc)), o);
                    qd = GfxMath.MulQuat(r, qd);
                }

                Step(kk, p, qd, ref c, ref q, amp, amp2, seed);
            }
        }

        public static MeshData Build(
            GfxLtnTrailParams p,
            IList<GfxKeypointSpec> keypoints,
            float localStartMs,
            float localTotalMs,
            float globalLoopMs,
            int repeatCount,
            float repeatDelay,
            bool endless,
            float activeTimeMs,
            float gfxAlpha,
            Func<float, float[]> histOrigin,
            Func<float, float[]> histRot,
            Func<float, float> histScale,
            float[] curOrigin,
            float[] curRot,
            float curScale)
        {
            if (p == null)
                throw new ArgumentNullException(nameof(p));

            var pairs = p.PairCount;
            var vertCount = pairs * 2;
            var positions = new float[vertCount * 3];
            var uvs = new float[vertCount * 2];
            var colors = new float[vertCount * 4];
            var tris = new int[Math.Max(0, pairs - 1) * 6];

            var g = BuildGrid(
                activeTimeMs, localStartMs, localTotalMs, globalLoopMs,
                repeatCount, repeatDelay, endless, p.TickMs);
            var life = Math.Max(1f, (float)p.SegLife);
            var ltT = g.T;
            var ltJ = g.J;
            var ltFull = g.Full;
            var ltTau = g.TauSinceStart;
            var ltR = endless ? 0f : (float)Math.Floor(ltTau / Math.Max(0.001f, ltFull));
            var ltNow = g.Now;
            var ltGap = endless ? 0f : ltFull - (ltJ - 1f) * ltT;
            var ltS = ltT >= life ? ltNow : (ltGap >= life ? ltR * ltJ : 0f);
            var ltTarget = ltTau - life;
            var ltKmin = 0f;
            if (ltTarget >= 0f)
            {
                if (endless)
                    ltKmin = (float)Math.Floor(ltTarget / ltT) + 1f;
                else
                {
                    var rt = (float)Math.Floor(ltTarget / ltFull);
                    var pt = ltTarget - rt * ltFull;
                    ltKmin = pt >= (ltJ - 1f) * ltT
                        ? (rt + 1f) * ltJ
                        : rt * ltJ + (float)Math.Floor(pt / ltT) + 1f;
                }
            }

            var ltOld = Math.Max(ltS, ltKmin);
            var ltTnow = TickTime(ltNow, ltJ, ltFull, ltT);
            var ltHead = ltTau > ltTnow || ltNow > ltS;
            var ltDraw = ltHead && ltOld <= ltNow;
            var ltSpanNow = endless ? ltTau : ltTau - ltR * ltFull;

            Func<float, int, float[]> parentO = (k, slot) =>
            {
                if (histOrigin == null)
                    return curOrigin ?? GfxMath.Vec3Zero();
                return histOrigin(k) ?? curOrigin ?? GfxMath.Vec3Zero();
            };
            Func<float, int, float[]> parentR = (k, slot) =>
            {
                if (histRot == null)
                    return curRot ?? GfxMath.QuatIdentity();
                return histRot(k) ?? curRot ?? GfxMath.QuatIdentity();
            };
            Func<float, int, float> parentS = (k, slot) =>
            {
                if (histScale == null)
                    return curScale;
                return histScale(k);
            };

            // Current keypoint color for MultiplyAlpha.
            GfxKeypointState nowState;
            if (keypoints != null && keypoints.Count > 0)
                nowState = GfxKeypointEval.Evaluate(keypoints, ltSpanNow);
            else
                nowState = new GfxKeypointState();
            var kColor = nowState.Color ?? GfxMath.ColorWhite();
            var mix = p.MixAlphaScale;
            var alphaMul = GfxMath.Clamp(gfxAlpha, 0f, 1f) * mix;
            var bind = p.Bind;
            var posEdge = p.Pos ?? GfxMath.Vec3Zero();
            var pos2Edge = p.Pos2 ?? GfxMath.Vec3Zero();

            for (var i = 0; i < pairs; i++)
            {
                float[] ltC;
                float[] ltQ;
                float ltU;
                var ltAge = 0f;

                if (i <= 1)
                {
                    float[] ltP, ltQd;
                    PointAt(keypoints, ltSpanNow, out ltP, out ltQd);
                    if (!bind)
                    {
                        var o = curOrigin ?? GfxMath.Vec3Zero();
                        var r = curRot ?? GfxMath.QuatIdentity();
                        ltP = GfxMath.Add3(GfxMath.RotateVec3(r, GfxMath.Scale3(ltP, curScale)), o);
                        ltQd = GfxMath.MulQuat(r, ltQd);
                    }

                    if (i == 0)
                    {
                        ltC = ltP;
                        ltQ = ltQd;
                        ltU = TexU(ltNow - ltS + 2f);
                    }
                    else
                    {
                        Center(
                            keypoints, ltNow, ltS, ltJ, ltT, p.Amp, p.Amp2, p.RandomSeed,
                            parentO, parentR, parentS, bind, out ltC, out ltQ);
                        Step(ltNow + 1f, ltP, ltQd, ref ltC, ref ltQ, p.Amp, p.Amp2, p.RandomSeed);
                        ltU = TexU(ltNow - ltS + 1f);
                    }
                }
                else
                {
                    var ltK = Math.Max(ltNow - (i - 2), ltOld);
                    Center(
                        keypoints, ltK, ltS, ltJ, ltT, p.Amp, p.Amp2, p.RandomSeed,
                        parentO, parentR, parentS, bind, out ltC, out ltQ);
                    ltU = TexU(ltK - ltS);
                    ltAge = ltTau - TickTime(ltK, ltJ, ltFull, ltT);
                }

                var ltAlpha = ltDraw
                    ? GfxMath.Clamp(1f - ltAge / life, 0f, 1f)
                    : 0f;

                for (var side = 0; side < 2; side++)
                {
                    var vi = i * 2 + side;
                    var edge = side == 0 ? posEdge : pos2Edge;
                    var scale = bind ? 1f : curScale;
                    var local = GfxMath.RotateVec3(ltQ, GfxMath.Scale3(edge, scale));
                    var world = GfxMath.Add3(local, ltC);
                    if (!ltDraw)
                        world = curOrigin ?? GfxMath.Vec3Zero();

                    positions[vi * 3] = world[0];
                    positions[vi * 3 + 1] = world[1];
                    positions[vi * 3 + 2] = world[2];
                    uvs[vi * 2] = ltU;
                    uvs[vi * 2 + 1] = side;
                    var a = GfxMath.Clamp(kColor[3] * ltAlpha * alphaMul, 0f, 1f);
                    colors[vi * 4] = kColor[0];
                    colors[vi * 4 + 1] = kColor[1];
                    colors[vi * 4 + 2] = kColor[2];
                    colors[vi * 4 + 3] = a;
                }
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
    }
}
