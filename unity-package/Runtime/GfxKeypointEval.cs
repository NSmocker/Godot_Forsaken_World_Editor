using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// A3DGFXKeyPointSet::TickAnimation / GetCurKeyPoint (gfx_keypoint_eval.gd).
    /// Pure C#; no Unity types.
    /// </summary>
    public static class GfxKeypointEval
    {
        public const float InfiniteSpanSec = 4294967.295f;

        public static float ElementTime(
            float activeTimeMs,
            float globalLoopTime,
            float localStart,
            float localTotal,
            int repeatCount,
            float repeatDelay)
        {
            var fRepeat = (float)repeatCount;
            var loopActive = Math.Max(0.001f, localTotal - localStart);
            var loopFull = loopActive + Math.Max(0f, repeatDelay);
            if (localTotal <= 0f)
                return activeTimeMs;

            var tGlobal = activeTimeMs;
            if (fRepeat >= 0f)
                // GDScript fmod preserves the dividend's sign. A negative seek
                // must stay before the local window instead of wrapping to its end.
                tGlobal = activeTimeMs % Math.Max(0.001f, globalLoopTime);

            var sinceStart = tGlobal - localStart;
            if (sinceStart < 0f)
                return tGlobal;

            var currentLoop = (float)Math.Floor(sinceStart / loopFull);
            if (fRepeat >= 0f && currentLoop >= fRepeat)
                return tGlobal;

            return localStart + sinceStart % loopFull;
        }

        public static GfxKeypointState Evaluate(IList<GfxKeypointSpec> keypoints, float spanMs)
        {
            return Evaluate(keypoints, spanMs, null);
        }

        /// <param name="bindTransformSec">
        /// BindEle bound clock in seconds (create_id_100/101.gd:741–792): when set,
        /// transform ctrl types (100/101/102/103/104/110) run on
        /// max(0, boundTime - keypointBase) instead of the element's own keypoint
        /// time; local ctrl types (105–109/111) keep t_elem_local
        /// (create_id_101.gd:844).
        /// </param>
        /// <param name="spriteShaderTiming">
        /// Use the ID100/101 shader's controller timing; false preserves the
        /// shared gfx_keypoint_eval.gd CPU contract used by other element families.
        /// </param>
        public static GfxKeypointState Evaluate(
            IList<GfxKeypointSpec> keypoints, float spanMs, float? bindTransformSec,
            bool spriteShaderTiming = false)
        {
            if (keypoints == null || keypoints.Count == 0)
                return new GfxKeypointState();

            var state = CopyState(keypoints[0]);
            state.Noise = 0f;
            var axisOff = GfxMath.Vec3Zero();
            var remaining = spanMs;
            var last = keypoints.Count - 1;
            // ctrl_to_kp_basetime (create_id_101.gd:465–492): cumulative seconds of
            // the preceding keypoint TimeSpans; only ts > 0 advances the base.
            var kpBaseSec = 0f;
            var processedKeypoints = 0;
            HashSet<float> pathBases = null;
            if (spriteShaderTiming)
            {
                pathBases = new HashSet<float>();
                var pathBase = 0f;
                foreach (var kp in keypoints)
                {
                    if (kp.Ctrls != null)
                        foreach (var ctrl in kp.Ctrls)
                            if (ctrl != null && ctrl.Type == 110) pathBases.Add(pathBase);
                    if (kp.TimeSpan > 0f) pathBase += kp.TimeSpan / 1000f;
                }
            }

            for (var i = 0; i < keypoints.Count; i++)
            {
                var kp = keypoints[i];
                processedKeypoints = i + 1;
                var ts = kp.TimeSpan;
                var infinite = ts < 0f;
                var inside = infinite || remaining < ts || i == last;
                var tIn = inside ? remaining : ts;

                if (kp.InterpolateMode != 0)
                {
                    if (i == last)
                    {
                        var noise = state.Noise;
                        state = CopyState(kp);
                        state.Noise = noise;
                    }
                    else
                    {
                        var portion = infinite
                            ? 0f
                            : GfxMath.Clamp(tIn / Math.Max(ts, 0.001f), 0f, 1f);
                        Interpolate(state, kp, keypoints[i + 1], portion);
                    }
                }
                else
                {
                    var kpSpanSec = infinite ? InfiniteSpanSec : ts / 1000f;
                    var tLocalSec = tIn / 1000f;
                    var tTransformSec = bindTransformSec.HasValue
                        ? Math.Max(0f, bindTransformSec.Value - kpBaseSec)
                        : tLocalSec;
                    axisOff = ProcessCtrls(
                        kp.Ctrls, state, tLocalSec, tTransformSec, kpSpanSec, axisOff,
                        false, spriteShaderTiming, bindTransformSec.HasValue,
                        pathBases != null && pathBases.Contains(kpBaseSec));
                }

                if (inside)
                    break;
                remaining -= ts;
                if (ts > 0f)
                    kpBaseSec += ts / 1000f;
            }

            if (bindTransformSec.HasValue)
            {
                // The sprite shader emits transform controllers from every keypoint.
                // A wrapped local clock must not hide later controllers whose bound
                // clock has already reached them. Keep local color/scale on its own clock.
                var boundBaseSec = 0f;
                for (var i = 0; i < keypoints.Count; i++)
                {
                    var kp = keypoints[i];
                    if (i >= processedKeypoints)
                        axisOff = ProcessCtrls(kp.Ctrls, state, 0f,
                            Math.Max(0f, bindTransformSec.Value - boundBaseSec),
                            kp.TimeSpan < 0f ? InfiniteSpanSec : kp.TimeSpan / 1000f,
                            axisOff, true, spriteShaderTiming, true,
                            pathBases != null && pathBases.Contains(boundBaseSec));
                    if (kp.TimeSpan > 0f)
                        boundBaseSec += kp.TimeSpan / 1000f;
                }
            }

            state.Scale += state.Noise;
            state.Noise = 0f;
            return state;
        }

        private static GfxKeypointState CopyState(GfxKeypointSpec kp)
        {
            return new GfxKeypointState
            {
                Pos = GfxMath.Clone3(kp.Pos),
                Dir = GfxMath.Clone4(kp.Dir),
                Color = GfxMath.Clone4(kp.Color),
                Scale = kp.Scale,
                Rad = kp.Rad
            };
        }

        private static void Interpolate(
            GfxKeypointState state,
            GfxKeypointSpec kp1,
            GfxKeypointSpec kp2,
            float f)
        {
            state.Color = GfxMath.LerpColor(kp1.Color, kp2.Color, f);
            state.Rad = GfxMath.Lerp(kp1.Rad, kp2.Rad, f);
            state.Scale = GfxMath.Lerp(kp1.Scale, kp2.Scale, f);
            state.Pos = GfxMath.Lerp3(kp1.Pos, kp2.Pos, f);
            if (kp1.InterpolateMode == 1)
                state.Dir = GfxMath.SlerpQuat(kp1.Dir, kp2.Dir, f);
        }

        /// <summary>
        /// create_id_101.gd:844: ctrl types evaluated on t_elem_local
        /// (own element time, unaffected by BindEle); every other type runs on
        /// t_elem_transform (100/101/102/103/104/110).
        /// </summary>
        private static bool UsesLocalCtrlTime(int type)
        {
            return type == 105 || type == 106 || type == 107 ||
                   type == 108 || type == 109 || type == 111;
        }

        private static float[] ProcessCtrls(
            IList<GfxCtrlSpec> ctrls,
            GfxKeypointState state,
            float tLocal,
            float tTransform,
            float kpSpanSec,
            float[] axisOff,
            bool transformsOnly = false,
            bool spriteShaderTiming = false,
            bool boundClock = false,
            bool suppressLinear = false)
        {
            var lin = GfxMath.Vec3Zero();
            var jump = GfxMath.Vec3Zero();
            var pRel = GfxMath.Clone3(state.Pos);

            if (ctrls == null)
            {
                state.Pos = GfxMath.Add3(pRel, lin);
                return axisOff;
            }

            foreach (var c in ctrls)
            {
                if (c == null)
                    continue;
                if (transformsOnly && UsesLocalCtrlTime(c.Type))
                    continue;
                if (suppressLinear && (c.Type == 100 || c.Type == 104))
                    continue;
                var t = UsesLocalCtrlTime(c.Type) ? tLocal : tTransform;
                var s = c.Start;
                var e = c.End;
                if ((t < s || (e >= 0f && s > e)) && !(spriteShaderTiming && c.Type == 110))
                    continue;

                var a0 = s;
                var a1 = e < 0f ? t : Math.Min(t, e);
                var type = c.Type;
                var dist = 0f;
                if (c.HasVelocity)
                {
                    var elapsed = a1 - a0;
                    // Sprite shader controls start accelerating at StartTime.
                    // The shared Godot CPU evaluator uses absolute-time integration;
                    // keep that contract for other element families and CPU callers.
                    dist = c.Vel * elapsed + 0.5f * c.Acc *
                        (spriteShaderTiming ? elapsed * elapsed : a1 * a1 - a0 * a0);
                }

                switch (type)
                {
                    case 100:
                        lin = GfxMath.Add3(lin, GfxMath.Scale3(c.Dir, dist));
                        break;
                    case 101:
                        state.Rad += dist;
                        break;
                    case 102:
                    case 103:
                    {
                        var axis = c.Axis;
                        if (GfxMath.Length3(axis) > GfxMath.Epsilon && dist != 0f)
                        {
                            var q = GfxMath.QuatFromAxisAngle(GfxMath.Normalize3(axis), dist);
                            var ctr = GfxMath.Add3(
                                GfxMath.Add3(c.Pos, axisOff), jump);
                            pRel = GfxMath.Add3(
                                GfxMath.RotateVec3(q, GfxMath.Sub3(pRel, ctr)), ctr);
                            if (type == 102)
                                state.Dir = GfxMath.NormalizeQuat(
                                    GfxMath.MulQuat(q, state.Dir));
                        }

                        break;
                    }
                    case 104:
                    {
                        if (c.Vel != 0f)
                        {
                            var ctr = GfxMath.Add3(
                                GfxMath.Add3(c.Pos, axisOff), jump);
                            var r = GfxMath.Sub3(pRel, ctr);
                            var rLen = GfxMath.Length3(r);
                            if (rLen > GfxMath.Epsilon)
                            {
                                pRel = GfxMath.Add3(
                                    ctr,
                                    GfxMath.Scale3(
                                        GfxMath.Normalize3(r),
                                        Math.Max(0f, rLen + dist)));
                            }
                        }

                        break;
                    }
                    case 105:
                    {
                        var col = new[]
                        {
                            state.Color[0] + c.ColorDelta[0] * (a1 - a0),
                            state.Color[1] + c.ColorDelta[1] * (a1 - a0),
                            state.Color[2] + c.ColorDelta[2] * (a1 - a0),
                            state.Color[3] + c.ColorDelta[3] * (a1 - a0)
                        };
                        state.Color = GfxMath.ClampColor(col);
                        break;
                    }
                    case 106:
                    {
                        var sc = c.ScaleChange;
                        state.Scale = Math.Min(
                            Math.Max(state.Scale + sc[0] * (a1 - a0), sc[1]),
                            sc[2]);
                        break;
                    }
                    case 107:
                    {
                        var n = Fbm1d(
                            a1 * 1000f / Math.Max(1f, c.WaveLen),
                            c.Octaves,
                            c.Persistence);
                        var bas = c.BaseColor;
                        state.Color = new[]
                        {
                            bas[0],
                            bas[1],
                            bas[2],
                            GfxMath.Clamp(
                                bas[3] + (n * 2f - 1f) * c.Amplitude / 255f,
                                0f,
                                1f)
                        };
                        break;
                    }
                    case 108:
                    {
                        if (c.StageCount > 0)
                        {
                            var col = StageColor(
                                c.Colors, c.Times, c.StageCount, a1 * 1000f);
                            if (c.AlphaOnly)
                                state.Color = new[]
                                {
                                    state.Color[0],
                                    state.Color[1],
                                    state.Color[2],
                                    col[3]
                                };
                            else
                                state.Color = col;
                        }

                        break;
                    }
                    case 109:
                        state.Noise = Fbm1d(
                            a1 * 1000f / Math.Max(0.001f, c.WaveLen),
                            c.Octaves,
                            c.Persistence) * c.Amplitude;
                        break;
                    case 110:
                    {
                        var rawPath = spriteShaderTiming && c.CurveVertices != null && c.CurveVertices.Length >= 2;
                        if (c.CurvePath != null || rawPath)
                        {
                            var progress = a1 / Math.Max(kpSpanSec, 0.000001f);
                            if (spriteShaderTiming)
                            {
                                // ID100/101 uses elapsed controller time and an explicit
                                // duration. Parsed BindEle targets have no KeyPoint_0
                                // duration, so an open-ended bound path stays at point 0.
                                var duration = e >= 0f ? Math.Max(0.0001f, e - s)
                                    : boundClock || kpSpanSec >= InfiniteSpanSec ? 0f
                                    : Math.Max(0.0001f, kpSpanSec - s);
                                progress = duration > 0.001f
                                    ? GfxMath.Clamp((t - s) / duration, 0f, 1f) : 0f;
                            }
                            float[] ptPos;
                            float[] ptQuat;
                            if (rawPath)
                                GfxCurvePath.InterpSpriteVertices(c.CurveVertices, progress, out ptPos, out ptQuat);
                            else
                                GfxCurvePath.InterpPoint(c.CurvePath, progress, out ptPos, out ptQuat);
                            if (spriteShaderTiming)
                            {
                                pRel = GfxMath.Add3(pRel, ptPos);
                                jump = GfxMath.Add3(jump, ptPos);
                            }
                            else
                            {
                                jump = GfxMath.Add3(jump, GfxMath.Sub3(ptPos, GfxMath.Add3(pRel, lin)));
                                pRel = GfxMath.Sub3(ptPos, lin);
                            }
                            if (c.CalcDir)
                                state.Dir = ptQuat;
                        }

                        break;
                    }
                    case 111:
                        if (c.StageCount > 0)
                            state.Scale = StageFloat(
                                c.Scales, c.Times, c.StageCount, a1 * 1000f);
                        break;
                }
            }

            state.Pos = GfxMath.Add3(pRel, lin);
            return GfxMath.Add3(GfxMath.Add3(axisOff, lin), jump);
        }

        public static float[] StageColor(
            float[][] values, float[] times, int stageCount, float tMs)
        {
            var count = Math.Min(stageCount, times != null ? times.Length : 0);
            if (values == null || values.Length == 0 || count <= 0)
                return GfxMath.ColorWhite();

            var seg = 0;
            var span = tMs;
            while (seg < count)
            {
                if (span < times[seg])
                    break;
                span -= times[seg];
                seg++;
            }

            float portion;
            if (seg >= count)
            {
                seg = count - 1;
                portion = 1f;
            }
            else
            {
                portion = Math.Min(span / Math.Max(times[seg], 0.0001f), 1f);
            }

            var next = Math.Min(seg + 1, values.Length - 1);
            return GfxMath.LerpColor(values[seg], values[next], portion);
        }

        public static float StageFloat(
            float[] values, float[] times, int stageCount, float tMs)
        {
            var count = Math.Min(stageCount, times != null ? times.Length : 0);
            if (values == null || values.Length == 0 || count <= 0)
                return 1f;

            var seg = 0;
            var span = tMs;
            while (seg < count)
            {
                if (span < times[seg])
                    break;
                span -= times[seg];
                seg++;
            }

            float portion;
            if (seg >= count)
            {
                seg = count - 1;
                portion = 1f;
            }
            else
            {
                portion = Math.Min(span / Math.Max(times[seg], 0.0001f), 1f);
            }

            var next = Math.Min(seg + 1, values.Length - 1);
            return GfxMath.Lerp(values[seg], values[next], portion);
        }

        public static float Fbm1d(float x, int octaves, float persistence)
        {
            var v = 0f;
            var a = 0.5f;
            var n = Math.Min(Math.Max(octaves, 0), 8);
            for (var i = 0; i < n; i++)
            {
                v += a * Noise1d(x);
                x *= 2f;
                a *= persistence;
            }

            return v;
        }

        public static float Noise1d(float p)
        {
            var i = (float)Math.Floor(p);
            var f = p - i;
            var u = f * f * (3f - 2f * f);
            return GfxMath.Lerp(Hash12(i), Hash12(i + 1f), u);
        }

        public static float Hash12(float p)
        {
            var v = Math.Sin(p * 12.9898) * 43758.5453;
            var frac = v - Math.Floor(v);
            if (frac < 0) frac += 1.0;
            return (float)frac;
        }
    }
}
