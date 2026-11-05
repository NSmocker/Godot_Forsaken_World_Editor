using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Pure CPU helpers for GFX ID 160 (gfx_model_160.gd TickAnimation / UpdateModel).
    /// </summary>
    public static class GfxModelEval
    {
        public sealed class Frame
        {
            public bool Active;
            public float[] Pos = GfxMath.Vec3Zero();
            public float[] Rotation = GfxMath.QuatIdentity(); // xyzw
            public float Scale = 1f;
            public float[] Color = GfxMath.ColorWhite();
            public float SpanMs;
        }

        /// <summary>
        /// Evaluate keypoint transform/color for element time (ms), including FacingDir
        /// movement basis when ActionName is empty.
        /// </summary>
        public static Frame Evaluate(
            GfxModelParams modelParams,
            IList<GfxKeypointSpec> keypoints,
            float elementTimeMs,
            float localStartTime,
            float localTotalTime,
            bool isEndless,
            float previousSpanMs,
            float[] previousMoveDir,
            out float[] moveDirOut)
        {
            moveDirOut = previousMoveDir != null && previousMoveDir.Length >= 3
                ? GfxMath.Clone3(previousMoveDir)
                : GfxMath.Vec3Zero();

            var frame = new Frame();
            if (modelParams == null)
                return frame;

            var active = isEndless ||
                (elementTimeMs >= localStartTime && elementTimeMs <= localTotalTime);
            if (!active || keypoints == null || keypoints.Count == 0)
                return frame;

            var span = Math.Max(0f, elementTimeMs - localStartTime);
            frame.SpanMs = span;
            var state = GfxKeypointEval.Evaluate(keypoints, span);
            var s = state != null ? state.Scale : 0f;
            if (Math.Abs(s) < GfxMath.Epsilon)
                return frame;

            var q = state.Dir != null && state.Dir.Length >= 4
                ? GfxMath.Clone4(state.Dir)
                : GfxMath.QuatIdentity();
            var rad = state.Rad;
            if (Math.Abs(rad) > GfxMath.Epsilon)
            {
                // Godot: q * Quaternion(Vector3.BACK, rad) — Z axis.
                q = GfxMath.MulQuat(q, GfxMath.QuatFromAxisAngle(GfxMath.Vec3(0f, 0f, 1f), rad));
            }

            var facing = modelParams.FacingDir &&
                string.IsNullOrWhiteSpace(modelParams.ModelActName);
            if (facing)
            {
                // Reset move cache on new loop (span went backwards).
                if (span < previousSpanMs)
                    moveDirOut = GfxMath.Vec3Zero();

                var move = MovementDir(keypoints, span, state.Pos, ref moveDirOut);
                if (GfxMath.Length3(move) > GfxMath.Epsilon)
                {
                    var up = GfxMath.RotateVec3(q, GfxMath.Vec3(0f, 1f, 0f));
                    q = DirUpQuat(move, up);
                }
            }

            frame.Active = true;
            frame.Pos = GfxMath.Clone3(state.Pos ?? GfxMath.Vec3Zero());
            frame.Rotation = q;
            frame.Scale = s;
            frame.Color = GfxMath.Clone4(state.Color ?? GfxMath.ColorWhite());
            return frame;
        }

        /// <summary>
        /// Active action clock in ms (gfx_model_160.gd _action_ms).
        /// Action restarts with a new GFX cycle, not on RepeatCount alone.
        /// </summary>
        public static float ActionMs(
            float activeTimeMs,
            float globalLoopTime,
            float localStartTime,
            float localTotalTime,
            int repeatCount,
            float repeatDelay,
            bool isEndless)
        {
            if (localTotalTime <= 0f)
                return Math.Max(0f, activeTimeMs - localStartTime);

            var fRepeat = (float)repeatCount;
            var tGlobal = activeTimeMs;
            if (fRepeat >= 0f)
            {
                var loopTime = Math.Max(0.001f, globalLoopTime);
                tGlobal = activeTimeMs - (float)Math.Floor(activeTimeMs / loopTime) * loopTime;
                if (tGlobal < 0f) tGlobal += loopTime;
            }

            var tSinceStart = tGlobal - localStartTime;
            if (tSinceStart < 0f)
                return 0f;
            if (isEndless)
                return tSinceStart;

            var activeDuration = Math.Max(0.001f, localTotalTime - localStartTime);
            var fullDuration = activeDuration + Math.Max(0f, repeatDelay);
            var loop = (float)Math.Floor(tSinceStart / fullDuration);
            if (fRepeat >= 0f && loop >= fRepeat)
                return fRepeat * activeDuration;
            var within = tSinceStart - loop * fullDuration;
            return loop * activeDuration + Math.Min(within, activeDuration);
        }

        /// <summary>
        /// PlayActionByNameDC seek position in seconds (gfx_model_160.gd _apply_action).
        /// </summary>
        public static float ActionSeekSeconds(float actionMs, float clipLengthSec, int actionLoops)
        {
            if (clipLengthSec <= 0f)
                return 0f;
            var pos = actionMs / 1000f;
            if (actionLoops >= 0 && pos >= clipLengthSec * Math.Max(1, actionLoops))
                return Math.Max(0f, clipLengthSec - 0.0001f);
            return pos - (float)Math.Floor(pos / clipLengthSec) * clipLengthSec;
        }

        /// <summary>Match Angelica action name to Animation clip (case / leaf path).</summary>
        public static string MatchAnimationName(
            IEnumerable<string> clipNames,
            string actName)
        {
            if (string.IsNullOrEmpty(actName) || clipNames == null)
                return null;
            string exact = null;
            string leaf = null;
            foreach (var n in clipNames)
            {
                if (n == null) continue;
                if (string.Equals(n, actName, StringComparison.Ordinal))
                    return n;
                if (exact == null &&
                    string.Equals(n, actName, StringComparison.OrdinalIgnoreCase))
                    exact = n;
                var slash = n.LastIndexOf('/');
                var leafName = slash >= 0 ? n.Substring(slash + 1) : n;
                if (leaf == null &&
                    string.Equals(leafName, actName, StringComparison.OrdinalIgnoreCase))
                    leaf = n;
            }

            return exact ?? leaf;
        }

        /// <summary>
        /// Scale albedo/emission RGB by KP color; multiply alpha (MTL_SCALED).
        /// </summary>
        public static float[] TintColor(float[] baseRgba, float[] kpColor)
        {
            var b = baseRgba != null && baseRgba.Length >= 4
                ? baseRgba
                : GfxMath.ColorWhite();
            var c = kpColor != null && kpColor.Length >= 4
                ? kpColor
                : GfxMath.ColorWhite();
            return new[]
            {
                b[0] * c[0],
                b[1] * c[1],
                b[2] * c[2],
                b[3] * c[3]
            };
        }

        private static float[] MovementDir(
            IList<GfxKeypointSpec> keypoints,
            float span,
            float[] pos,
            ref float[] lastMoveDir)
        {
            float[] d;
            if (span >= GfxModelParams.FacingSampleMs)
            {
                var prev = GfxKeypointEval.Evaluate(keypoints, span - GfxModelParams.FacingSampleMs);
                d = GfxMath.Sub3(pos, prev.Pos);
            }
            else
            {
                var next = GfxKeypointEval.Evaluate(keypoints, span + GfxModelParams.FacingSampleMs);
                d = GfxMath.Sub3(next.Pos, pos);
            }

            if (GfxMath.Length3(d) > GfxMath.Epsilon)
                lastMoveDir = GfxMath.Normalize3(d);
            return lastMoveDir;
        }

        /// <summary>a3d_TransformMatrix(vDir, vUp): Z=dir, X=up×Z, Y=Z×X → quat.</summary>
        public static float[] DirUpQuat(float[] dir, float[] up)
        {
            var z = GfxMath.Normalize3(dir);
            var x = GfxMath.Cross3(up, z);
            if (GfxMath.Length3(x) < GfxMath.Epsilon)
            {
                var fallback = Math.Abs(z[1]) > 0.999f
                    ? GfxMath.Vec3(1f, 0f, 0f)
                    : GfxMath.Vec3(0f, 1f, 0f);
                x = GfxMath.Cross3(fallback, z);
            }

            x = GfxMath.Normalize3(x);
            var y = GfxMath.Cross3(z, x);
            return GfxMath.QuatFromAxes(x, y, z);
        }
    }
}
