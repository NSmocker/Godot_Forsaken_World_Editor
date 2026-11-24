Shader "Forsaken World/HDRP/Angelica Gfx Sprite"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _UvRect ("UV scale.xy offset.zw", Vector) = (1, 1, 0, 0)
        _UvExchange ("Exchange UV axes", Float) = 0
        _UseVertexColor ("Particle vertex color", Float) = 0
        _GfxAlpha ("GFX alpha", Range(0, 1)) = 1
        _Warp ("Warp heat distortion", Float) = 0
        [HideInInspector] _WarpNormalIsSRGB ("Warp normal uses sRGB import", Float) = 0
        [HideInInspector] _GfxWarpDecal ("Screen Warp decal", Vector) = (0, 0, 0, 0)
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
            #include "AngelicaGfxSpriteHdrp.hlsl"
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
            Name "GfxSpriteEncoded"
            Tags { "LightMode"="AngelicaGfxEncoded" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragEncoded
            ENDHLSL
        }
        Pass
        {
            Name "GfxParticleWarp"
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
