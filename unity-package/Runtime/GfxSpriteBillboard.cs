using System;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Facing modes for GFX sprites (create_id_100 vs create_id_101 billboard blocks).
    /// Pure C# quaternions as float[4] xyzw.
    /// </summary>
    public static class GfxSpriteBillboard
    {
        /// <summary>
        /// ID 100: RotFromView → Z-axis billboard; else keypoint Direction only.
        /// ID 101: RotFromView → Z-axis billboard; else full camera-facing billboard.
        /// </summary>
        public static float[] ComputeRotation(
            bool isId101,
            bool rotFromView,
            float[] keypointDir,
            float[] elementPos,
            float[] cameraPos,
            float radZ)
        {
            var dir = GfxMath.NormalizeQuat(
                keypointDir ?? GfxMath.QuatIdentity());
            float[] orient;

            if (rotFromView)
                orient = ZAxisBillboard(dir, elementPos, cameraPos);
            else if (isId101)
                orient = FullBillboard(elementPos, cameraPos);
            else
                orient = dir;

            if (Math.Abs(radZ) > GfxMath.Epsilon)
            {
                var zSpin = GfxMath.QuatFromAxisAngle(GfxMath.Vec3(0f, 0f, 1f), radZ);
                // Godot applies Z-spin in local mesh space before orientation.
                orient = GfxMath.NormalizeQuat(GfxMath.MulQuat(orient, zSpin));
            }

            return orient;
        }

        public static float[] FullBillboard(float[] elementPos, float[] cameraPos)
        {
            if (elementPos == null || cameraPos == null)
                return GfxMath.QuatIdentity();
            var look = GfxMath.Normalize3(GfxMath.Sub3(cameraPos, elementPos));
            if (GfxMath.Length3(look) < GfxMath.Epsilon)
                return GfxMath.QuatIdentity();
            return GfxMath.BuildLookQuat(look);
        }

        /// <summary>
        /// RotFromView Z-billboard: keep Direction's Z, yaw toward camera
        /// (create_id_100 vertex remap).
        /// </summary>
        public static float[] ZAxisBillboard(
            float[] keypointDir, float[] elementPos, float[] cameraPos)
        {
            var q = GfxMath.NormalizeQuat(keypointDir ?? GfxMath.QuatIdentity());
            var newZ = GfxMath.RotateVec3(q, GfxMath.Vec3(0f, 0f, 1f));
            if (elementPos == null || cameraPos == null)
                return q;

            var lookDir = GfxMath.Sub3(cameraPos, elementPos);
            var targetY = GfxMath.Sub3(
                lookDir, GfxMath.Scale3(newZ, GfxMath.Dot3(lookDir, newZ)));
            if (GfxMath.Length3(targetY) <= 0.001f)
                return q;

            targetY = GfxMath.Normalize3(targetY);
            var targetX = GfxMath.Normalize3(GfxMath.Cross3(targetY, newZ));
            // Godot: X → -target_x, Y → new_z, Z → target_y
            return GfxMath.QuatFromAxes(
                GfxMath.Neg3(targetX),
                newZ,
                targetY);
        }
    }
}
