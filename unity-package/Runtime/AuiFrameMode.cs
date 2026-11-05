using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Godot angelica_ui_builder.gd AUIFrame modes: 3×3 / 4×4 / 3×1 / 1×3 / 1×1 → nine-slice margins.
    /// </summary>
    public enum AuiFrameMode
    {
        Unknown = 0,
        Mode1x1 = 1,
        Mode3x3 = 2,
        Mode4x4 = 3,
        Mode3x1 = 4,
        Mode1x3 = 5
    }

    public struct AuiSliceMargins
    {
        public int Horizontal;
        public int Vertical;
        public AuiFrameMode Resolved;

        public AuiSliceMargins(int horizontal, int vertical, AuiFrameMode resolved)
        {
            Horizontal = horizontal;
            Vertical = vertical;
            Resolved = resolved;
        }

        /// <summary>Unity Sprite.border = (L, B, R, T).</summary>
        public Vector4 SpriteBorder =>
            new Vector4(Horizontal, Vertical, Horizontal, Vertical);
    }

    public static class AuiNineSlice
    {
        /// <summary>
        /// AUIFrame::SetMode → (left/right border, top/bottom border, resolved mode).
        /// </summary>
        public static AuiSliceMargins ComputeMargins(int width, int height, AuiFrameMode mode)
        {
            var w = width;
            var h = height;
            if ((mode == AuiFrameMode.Unknown || mode == AuiFrameMode.Mode3x3) &&
                w % 3 == 0 && h % 3 == 0 && w > 0 && h > 0)
                return new AuiSliceMargins(w / 3, h / 3, AuiFrameMode.Mode3x3);
            if ((mode == AuiFrameMode.Unknown || mode == AuiFrameMode.Mode4x4) &&
                w % 4 == 0 && h % 4 == 0 && w > 0 && h > 0)
                return new AuiSliceMargins(w / 4, h / 4, AuiFrameMode.Mode4x4);
            if ((mode == AuiFrameMode.Unknown || mode == AuiFrameMode.Mode3x1) &&
                w % 3 == 0 && w > 0)
                return new AuiSliceMargins(w / 3, 0, AuiFrameMode.Mode3x1);
            if ((mode == AuiFrameMode.Unknown || mode == AuiFrameMode.Mode1x3) &&
                h % 3 == 0 && h > 0)
                return new AuiSliceMargins(0, h / 3, AuiFrameMode.Mode1x3);
            return new AuiSliceMargins(0, 0, AuiFrameMode.Mode1x1);
        }

        public static AuiFrameMode FromInt(int v)
        {
            if (v >= 0 && v <= 5)
                return (AuiFrameMode)v;
            return AuiFrameMode.Unknown;
        }
    }
}
