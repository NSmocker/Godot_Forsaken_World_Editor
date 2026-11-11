Shader "Forsaken World/HDRP/Angelica Terrain"
{
    // HDRP twin of Forsaken World/Angelica Terrain. Same mesh slots: direct DDS
    // color links plus generated splat, height and lightmap textures.
    Properties
    {
        _Layer0 ("Layer 0", 2D) = "white" {}
        _Layer1 ("Layer 1", 2D) = "white" {}
        _Layer2 ("Layer 2", 2D) = "white" {}
        _Layer3 ("Layer 3", 2D) = "white" {}
        _Layer4 ("Layer 4", 2D) = "white" {}
        _Layer5 ("Layer 5", 2D) = "white" {}
        _Layer6 ("Layer 6", 2D) = "white" {}
        _Layer7 ("Layer 7", 2D) = "white" {}
        _Layer8 ("Layer 8", 2D) = "white" {}
        _Layer9 ("Layer 9", 2D) = "white" {}
        _Layer10 ("Layer 10", 2D) = "white" {}
        _Layer11 ("Layer 11", 2D) = "white" {}
        _Layer12 ("Layer 12", 2D) = "white" {}
        _Layer13 ("Layer 13", 2D) = "white" {}
        _Layer14 ("Layer 14", 2D) = "white" {}
        _Layer15 ("Layer 15", 2D) = "white" {}
        _Splat0 ("Splat 0", 2D) = "black" {}
        _Splat1 ("Splat 1", 2D) = "black" {}
        _Splat2 ("Splat 2", 2D) = "black" {}
        _Splat3 ("Splat 3", 2D) = "black" {}
        _LayerParams ("Layer parameters", 2D) = "black" {}
        _Lightmap ("Daytime lightmap", 2D) = "white" {}
        _HeightMap ("Height map", 2D) = "black" {}
        _LayerCount ("Layer count", Int) = 1
        _MinHeight ("Subterrain minimum height", Float) = 0
        [Toggle] _UseLightmap ("Use daytime lightmap", Float) = 0
        _Brightness ("Brightness", Range(0, 4)) = 2
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
