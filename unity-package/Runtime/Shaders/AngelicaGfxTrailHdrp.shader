Shader "Forsaken World/HDRP/Angelica Gfx Trail"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _GfxAlpha ("GFX alpha", Range(0, 1)) = 1
        _Warp ("Warp heat distortion", Float) = 0
        [HideInInspector] _WarpNormalIsSRGB ("Normal texture is sRGB", Float) = 0
        [HideInInspector] _GfxGridRaster ("D3D9 pixel center", Float) = 0
        [HideInInspector] _GfxGridTextureMode ("Grid texture address mode", Float) = 0
        [HideInInspector] _GfxDissolve ("Dissolve", Float) = 0
        [HideInInspector] _GfxDissolveBaseIsRaw ("Dissolve base stores encoded samples", Float) = 0
        [HideInInspector] _GfxDissolveMask ("Dissolve mask", 2D) = "white" {}
        [HideInInspector] _GfxDissolveMaskIsSRGB ("Dissolve mask is sRGB", Float) = 0
        [HideInInspector] _GfxDissolveParameters ("Dissolve threshold and edge", Vector) = (0, 0, 0, 0)
        _CameraOpaqueTexture ("Camera opaque", 2D) = "black" {}
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        _ZWrite ("ZWrite", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="HDRenderPipeline"
            "IgnoreProjector"="True"
        }
        Cull Off
        ZWrite [_ZWrite]
        ZTest [_ZTest]
        Blend [_SrcBlend] [_DstBlend]
        HLSLINCLUDE
            #include "AngelicaGfxTrailHdrp.hlsl"
        ENDHLSL
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="ForwardOnly" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
        Pass
        {
            Name "GfxTrailEncoded"
            Tags { "LightMode"="AngelicaGfxEncoded" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragEncoded
            ENDHLSL
        }
        Pass
        {
            Name "GfxGridWarp"
            Tags { "LightMode"="AngelicaGfxWarp" }
            Blend One Zero
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragWarp
            ENDHLSL
        }
    }
    FallBack Off
}
