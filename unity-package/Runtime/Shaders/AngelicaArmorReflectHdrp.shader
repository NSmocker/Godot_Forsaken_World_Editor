Shader "Forsaken World/HDRP/Angelica Armor Reflect"
{
    // HDRP twin of Forsaken World/Angelica Armor Reflect.
    // ALBEDO = albedo.rgb * (base.rgb + reflection * base.a);
    // sphere-map UV from view-space normal.
    Properties
    {
        _AlbedoColor ("Albedo Color", Color) = (1,1,1,1)
        _Albedo ("Albedo", 2D) = "white" {}
        _ReflectMap ("Reflect Map", 2D) = "black" {}
        _Specular ("Specular", Range(0, 1)) = 0
        _EmissionColor ("Emission", Color) = (0,0,0,1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.3294
        [Enum(Opaque,0,Transparent,1)] _Surface ("Surface", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="HDRenderPipeline"
            "Queue"="Geometry"
        }
        Pass
        {
            Name "DepthForwardOnly"
            Tags { "LightMode"="DepthForwardOnly" }
            Cull [_Cull]
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #define ANGELICA_DEPTH_ONLY 1
            #include "AngelicaArmorReflectHdrp.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="ForwardOnly" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "AngelicaArmorReflectHdrp.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
