Shader "Forsaken World/HDRP/Angelica Terrain"
{
    // HDRP twin of Forsaken World/Angelica Terrain.
    Properties
    {
        _Layers ("Ground textures", 2DArray) = "" {}
        _Masks ("Layer masks", 2DArray) = "" {}
        _LayerParams ("Layer parameters", 2D) = "black" {}
        _Lightmap ("Daytime lightmap", 2D) = "white" {}
        _LayerCount ("Layer count", Int) = 1
        _MinHeight ("Subterrain minimum height", Float) = 0
        [Toggle] _UseLightmap ("Use daytime lightmap", Float) = 0
        _Brightness ("Brightness", Range(0, 4)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="HDRenderPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "DepthForwardOnly"
            Tags { "LightMode"="DepthForwardOnly" }
            Cull Back
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #define ANGELICA_DEPTH_ONLY 1
            #include "AngelicaTerrainHdrp.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="ForwardOnly" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "AngelicaTerrainHdrp.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
