Shader "Forsaken World/HDRP/Angelica Gfx Trail"
{
    // HDRP twin of Forsaken World/Angelica Gfx Trail (ID 110/180, rings, lightning).
    // Warp samples _CameraOpaqueTexture; bind HDRP camera color there.
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _GfxAlpha ("GFX alpha", Range(0, 1)) = 1
        _Warp ("Warp heat distortion", Float) = 0
        _CameraOpaqueTexture ("Camera opaque", 2D) = "black" {}
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        _ZWrite ("ZWrite", Float) = 0
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
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="ForwardOnly" }
            Cull Off
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "AngelicaGfxTrailHdrp.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
