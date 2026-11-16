using UnityEngine.Rendering;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Maps Angelica A3DBLEND blend-factor values (the D3D9 BLEND enum, see
    /// PW_ClientSource/SDK/Angelica3D/Header/A3DTypes.h:178-194) onto
    /// <see cref="BlendMode"/>. GFX elements store raw factors in SrcBlend/DestBlend
    /// (A3DGFXElement.cpp:34-35) and forward them to the render state directly
    /// (e.g. A3DGFXModel.cpp:342-343 SetSrcBlend/SetDstBlend), so the port must map
    /// each factor, not pick a URP preset:
    ///   A3DBLEND     ZERO=1  ONE=2  SRCCOLOR=3  INVSRCCOLOR=4  SRCALPHA=5
    ///                INVSRCALPHA=6  DESTALPHA=7  INVDESTALPHA=8  DESTCOLOR=9
    ///                INVDESTCOLOR=10  SRCALPHASAT=11
    ///   BlendMode    Zero=0  One=1  DstColor=2  SrcColor=3  OneMinusDstColor=4
    ///                SrcAlpha=5  OneMinusSrcColor=6  DstAlpha=7  OneMinusDstAlpha=8
    ///                SrcAlphaSaturate=9  OneMinusSrcAlpha=10
    /// </summary>
    public static class GfxBlendFactors
    {
        /// <summary>A3DBLEND_SRCALPHA (5) — the element ctor default SrcBlend (A3DGFXElement.cpp:540).</summary>
        public const int A3dBlendSrcAlpha = 5;
        /// <summary>A3DBLEND_INVSRCALPHA (6) — the alpha-blend DestBlend (A3DGFXModel.cpp:325).</summary>
        public const int A3dBlendInvSrcAlpha = 6;

        /// <summary>
        /// Map a raw A3DBLEND value; unknown values fall back to <paramref name="fallback"/>
        /// (the value a standard pipeline preset would have used).
        /// </summary>
        public static BlendMode ToUnity(int a3dBlend, BlendMode fallback)
        {
            switch (a3dBlend)
            {
                case 1: return BlendMode.Zero;
                case 2: return BlendMode.One;
                case 3: return BlendMode.SrcColor;
                case 4: return BlendMode.OneMinusSrcColor;
                case 5: return BlendMode.SrcAlpha;
                case 6: return BlendMode.OneMinusSrcAlpha;
                case 7: return BlendMode.DstAlpha;
                case 8: return BlendMode.OneMinusDstAlpha;
                case 9: return BlendMode.DstColor;
                case 10: return BlendMode.OneMinusDstColor;
                case 11: return BlendMode.SrcAlphaSaturate;
                default: return fallback;
            }
        }
    }
}
