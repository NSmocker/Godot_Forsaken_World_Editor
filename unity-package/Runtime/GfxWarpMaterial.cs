using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Applies Godot create_id_100/120/140 Warp heat-distortion flag to URP materials
    /// that expose <c>_Warp</c> (AngelicaGfxSprite / AngelicaGfxTrail). Host URP asset
    /// must enable Opaque Texture for <c>_CameraOpaqueTexture</c> sampling.
    /// </summary>
    public static class GfxWarpMaterial
    {
        public const string WarpProperty = "_Warp";

        /// <summary>True when the material shader exposes <c>_Warp</c>.</summary>
        public static bool SupportsWarp(Material mat)
        {
            return mat != null && mat.HasProperty(WarpProperty);
        }

        /// <summary>
        /// Sets <c>_Warp</c> to 1/0. Returns true when applied; false if the shader
        /// has no Warp property (fallback Unlit/Lit — caller may warn).
        /// </summary>
        public static bool Apply(Material mat, bool warp)
        {
            if (!SupportsWarp(mat))
                return false;
            mat.SetFloat(WarpProperty, warp ? 1f : 0f);
            return true;
        }
    }
}
