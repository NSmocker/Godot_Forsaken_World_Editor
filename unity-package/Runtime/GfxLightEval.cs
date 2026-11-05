using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// One evaluated light frame (Unity-free), mirroring gfx_light_130.gd apply_time.
    /// </summary>
    public sealed class GfxLightFrame
    {
        public bool Active;
        public float[] Pos = GfxMath.Vec3Zero();
        /// <summary>Shine direction (Angelica +Z of keypoint quat). Null/unused for Point.</summary>
        public float[] Forward = GfxMath.Vec3(0f, 0f, 1f);
        public float[] ColorRgb = GfxMath.Color(1f, 1f, 1f, 1f);
        public float Intensity = 1f;
        public float[] Rotation = GfxMath.QuatIdentity();
    }

    /// <summary>
    /// CPU evaluation for GFX ID 130 (gfx_light_130.gd + keypoints via GfxKeypointEval).
    /// </summary>
    public static class GfxLightEval
    {
        public static GfxLightFrame Evaluate(
            GfxLightParams parameters,
            IList<GfxKeypointSpec> keypoints,
            float elementTimeMs,
            float localStartTime,
            float localTotalTime,
            bool isEndless)
        {
            var frame = new GfxLightFrame();
            if (parameters == null)
                return frame;

            var active = isEndless ||
                (elementTimeMs >= localStartTime && elementTimeMs <= localTotalTime);
            if (!active || keypoints == null || keypoints.Count == 0)
            {
                frame.Active = false;
                return frame;
            }

            var span = elementTimeMs - localStartTime;
            if (span < 0f)
            {
                frame.Active = false;
                return frame;
            }

            var state = GfxKeypointEval.Evaluate(keypoints, span);
            if (state == null)
            {
                frame.Active = false;
                return frame;
            }

            frame.Active = true;
            frame.Pos = GfxMath.Clone3(state.Pos);
            var color = state.Color ?? GfxMath.ColorWhite();
            frame.ColorRgb = new[] { color[0], color[1], color[2], color[3] };

            var energyMul = parameters.UseAlphaEnergy
                ? color[3] * 2f
                : 1f;
            frame.Intensity = parameters.EnergyBase * energyMul;

            if (parameters.LightType != GfxLightParams.LightPoint)
            {
                // Angelica shines along +Z of keypoint direction (Godot: quat * Vector3.BACK).
                var forward = GfxMath.RotateVec3(
                    state.Dir ?? GfxMath.QuatIdentity(),
                    GfxMath.Vec3(0f, 0f, 1f));
                if (GfxMath.Length3(forward) > GfxMath.Epsilon)
                {
                    forward = GfxMath.Normalize3(forward);
                    frame.Forward = forward;
                    // Unity Light forward = +Z; BuildLookQuat matches Godot looking_at(dir).
                    frame.Rotation = GfxMath.BuildLookQuat(forward);
                }
            }

            return frame;
        }
    }
}
