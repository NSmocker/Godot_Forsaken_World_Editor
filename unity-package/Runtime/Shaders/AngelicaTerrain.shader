Shader "Forsaken World/Angelica Terrain"
{
    // Mesh terrain using mask-area-local color slots. Ground DDS samples retain
    // file row order and numeric RGB values until the final framebuffer conversion.
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
        [HideInInspector][Normal] _Normal0 ("Layer 0 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness0 ("Layer 0 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength0 ("Layer 0 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength0 ("Layer 0 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector] _HasNormal0 ("Layer 0 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness0 ("Layer 0 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo0 ("Layer 0 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping0 ("Layer 0 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal1 ("Layer 1 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness1 ("Layer 1 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength1 ("Layer 1 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength1 ("Layer 1 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector] _HasNormal1 ("Layer 1 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness1 ("Layer 1 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo1 ("Layer 1 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping1 ("Layer 1 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal2 ("Layer 2 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness2 ("Layer 2 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength2 ("Layer 2 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength2 ("Layer 2 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector] _HasNormal2 ("Layer 2 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness2 ("Layer 2 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo2 ("Layer 2 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping2 ("Layer 2 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal3 ("Layer 3 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness3 ("Layer 3 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength3 ("Layer 3 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength3 ("Layer 3 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector] _HasNormal3 ("Layer 3 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness3 ("Layer 3 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo3 ("Layer 3 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping3 ("Layer 3 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal4 ("Layer 4 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness4 ("Layer 4 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength4 ("Layer 4 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength4 ("Layer 4 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector] _HasNormal4 ("Layer 4 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness4 ("Layer 4 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo4 ("Layer 4 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping4 ("Layer 4 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal5 ("Layer 5 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness5 ("Layer 5 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength5 ("Layer 5 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength5 ("Layer 5 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector] _HasNormal5 ("Layer 5 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness5 ("Layer 5 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo5 ("Layer 5 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping5 ("Layer 5 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal6 ("Layer 6 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness6 ("Layer 6 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength6 ("Layer 6 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength6 ("Layer 6 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector] _HasNormal6 ("Layer 6 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness6 ("Layer 6 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo6 ("Layer 6 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping6 ("Layer 6 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal7 ("Layer 7 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness7 ("Layer 7 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength7 ("Layer 7 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength7 ("Layer 7 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector] _HasNormal7 ("Layer 7 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness7 ("Layer 7 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo7 ("Layer 7 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping7 ("Layer 7 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal8 ("Layer 8 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness8 ("Layer 8 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength8 ("Layer 8 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength8 ("Layer 8 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector] _HasNormal8 ("Layer 8 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness8 ("Layer 8 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo8 ("Layer 8 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping8 ("Layer 8 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal9 ("Layer 9 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness9 ("Layer 9 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength9 ("Layer 9 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength9 ("Layer 9 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector] _HasNormal9 ("Layer 9 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness9 ("Layer 9 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo9 ("Layer 9 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping9 ("Layer 9 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal10 ("Layer 10 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness10 ("Layer 10 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength10 ("Layer 10 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength10 ("Layer 10 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector] _HasNormal10 ("Layer 10 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness10 ("Layer 10 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo10 ("Layer 10 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping10 ("Layer 10 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal11 ("Layer 11 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness11 ("Layer 11 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength11 ("Layer 11 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength11 ("Layer 11 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector] _HasNormal11 ("Layer 11 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness11 ("Layer 11 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo11 ("Layer 11 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping11 ("Layer 11 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal12 ("Layer 12 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness12 ("Layer 12 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength12 ("Layer 12 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength12 ("Layer 12 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector] _HasNormal12 ("Layer 12 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness12 ("Layer 12 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo12 ("Layer 12 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping12 ("Layer 12 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal13 ("Layer 13 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness13 ("Layer 13 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength13 ("Layer 13 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength13 ("Layer 13 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector] _HasNormal13 ("Layer 13 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness13 ("Layer 13 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo13 ("Layer 13 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping13 ("Layer 13 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal14 ("Layer 14 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness14 ("Layer 14 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength14 ("Layer 14 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength14 ("Layer 14 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector] _HasNormal14 ("Layer 14 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness14 ("Layer 14 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo14 ("Layer 14 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping14 ("Layer 14 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal15 ("Layer 15 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness15 ("Layer 15 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength15 ("Layer 15 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength15 ("Layer 15 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector] _HasNormal15 ("Layer 15 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness15 ("Layer 15 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo15 ("Layer 15 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping15 ("Layer 15 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector] _SourceLayerCount ("Imported layer count", Float) = -1
        _Splat0 ("Splat 0", 2D) = "black" {}
        _Splat1 ("Splat 1", 2D) = "black" {}
        _Splat2 ("Splat 2", 2D) = "black" {}
        _Splat3 ("Splat 3", 2D) = "black" {}
        _LayerMask1 ("Layer 1 Mask", 2D) = "black" {}
        _LayerMask2 ("Layer 2 Mask", 2D) = "black" {}
        _LayerMask3 ("Layer 3 Mask", 2D) = "black" {}
        _LayerMask4 ("Layer 4 Mask", 2D) = "black" {}
        _LayerMask5 ("Layer 5 Mask", 2D) = "black" {}
        _LayerMask6 ("Layer 6 Mask", 2D) = "black" {}
        _LayerMask7 ("Layer 7 Mask", 2D) = "black" {}
        _LayerMask8 ("Layer 8 Mask", 2D) = "black" {}
        _LayerMask9 ("Layer 9 Mask", 2D) = "black" {}
        _LayerMask10 ("Layer 10 Mask", 2D) = "black" {}
        _LayerMask11 ("Layer 11 Mask", 2D) = "black" {}
        _LayerMask12 ("Layer 12 Mask", 2D) = "black" {}
        _LayerMask13 ("Layer 13 Mask", 2D) = "black" {}
        _LayerMask14 ("Layer 14 Mask", 2D) = "black" {}
        _LayerMask15 ("Layer 15 Mask", 2D) = "black" {}
        _LayerParams ("Layer parameters", 2D) = "black" {}
        _Lightmap ("Daytime lightmap", 2D) = "white" {}
        _NightLightmap ("Nighttime lightmap", 2D) = "white" {}
        [HideInInspector] _UseNightLightmap ("Use nighttime lightmap", Float) = 0
        [HideInInspector] _MapDayNight ("Map day/night factor", Float) = 0
        _HeightMap ("Height map", 2D) = "black" {}
        [HideInInspector] _FlipLayers ("Flip linked texture V", Float) = 0
        [HideInInspector] _SrgbLayers ("sRGB linked textures", Float) = 0
        _LayerCount ("Layer count", Int) = 1
        _MinHeight ("Subterrain minimum height", Float) = 0
        [Toggle] _UseLightmap ("Use daytime lightmap", Float) = 0
        [Toggle] _ReceiveShadows ("Receive Shadows", Float) = 1
        _Brightness ("Brightness", Range(0, 4)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "Terrain"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_Layer0); TEXTURE2D(_Layer1); TEXTURE2D(_Layer2); TEXTURE2D(_Layer3);
            TEXTURE2D(_Layer4); TEXTURE2D(_Layer5); TEXTURE2D(_Layer6); TEXTURE2D(_Layer7);
            TEXTURE2D(_Layer8); TEXTURE2D(_Layer9); TEXTURE2D(_Layer10); TEXTURE2D(_Layer11);
            TEXTURE2D(_Layer12); TEXTURE2D(_Layer13); TEXTURE2D(_Layer14); TEXTURE2D(_Layer15);
            TEXTURE2D(_Normal0); TEXTURE2D(_Roughness0);
            TEXTURE2D(_Normal1); TEXTURE2D(_Roughness1);
            TEXTURE2D(_Normal2); TEXTURE2D(_Roughness2);
            TEXTURE2D(_Normal3); TEXTURE2D(_Roughness3);
            TEXTURE2D(_Normal4); TEXTURE2D(_Roughness4);
            TEXTURE2D(_Normal5); TEXTURE2D(_Roughness5);
            TEXTURE2D(_Normal6); TEXTURE2D(_Roughness6);
            TEXTURE2D(_Normal7); TEXTURE2D(_Roughness7);
            TEXTURE2D(_Normal8); TEXTURE2D(_Roughness8);
            TEXTURE2D(_Normal9); TEXTURE2D(_Roughness9);
            TEXTURE2D(_Normal10); TEXTURE2D(_Roughness10);
            TEXTURE2D(_Normal11); TEXTURE2D(_Roughness11);
            TEXTURE2D(_Normal12); TEXTURE2D(_Roughness12);
            TEXTURE2D(_Normal13); TEXTURE2D(_Roughness13);
            TEXTURE2D(_Normal14); TEXTURE2D(_Roughness14);
            TEXTURE2D(_Normal15); TEXTURE2D(_Roughness15);
            SAMPLER(sampler_trilinear_repeat);
            TEXTURE2D(_Splat0); TEXTURE2D(_Splat1); TEXTURE2D(_Splat2); TEXTURE2D(_Splat3);
            SAMPLER(sampler_Splat0);
            TEXTURE2D(_LayerParams);
            TEXTURE2D(_Lightmap); SAMPLER(sampler_Lightmap);
            TEXTURE2D(_NightLightmap);
            TEXTURE2D(_HeightMap);
            CBUFFER_START(UnityPerMaterial)
                float _LayerCount, _MinHeight, _UseLightmap, _Brightness, _ReceiveShadows;
                float _UseNightLightmap, _MapDayNight;
                float _FlipLayers, _SrgbLayers;
                float _SourceLayerCount;
                float _NormalStrength0, _RoughnessStrength0;
                float _HasNormal0, _HasRoughness0, _HasAlbedo0;
                float _HasNormal1, _HasRoughness1, _HasAlbedo1;
                float _HasNormal2, _HasRoughness2, _HasAlbedo2;
                float _HasNormal3, _HasRoughness3, _HasAlbedo3;
                float _HasNormal4, _HasRoughness4, _HasAlbedo4;
                float _HasNormal5, _HasRoughness5, _HasAlbedo5;
                float _HasNormal6, _HasRoughness6, _HasAlbedo6;
                float _HasNormal7, _HasRoughness7, _HasAlbedo7;
                float _HasNormal8, _HasRoughness8, _HasAlbedo8;
                float _HasNormal9, _HasRoughness9, _HasAlbedo9;
                float _HasNormal10, _HasRoughness10, _HasAlbedo10;
                float _HasNormal11, _HasRoughness11, _HasAlbedo11;
                float _HasNormal12, _HasRoughness12, _HasAlbedo12;
                float _HasNormal13, _HasRoughness13, _HasAlbedo13;
                float _HasNormal14, _HasRoughness14, _HasAlbedo14;
                float _HasNormal15, _HasRoughness15, _HasAlbedo15;
                float _NormalStrength1, _RoughnessStrength1;
                float _NormalStrength2, _RoughnessStrength2;
                float _NormalStrength3, _RoughnessStrength3;
                float _NormalStrength4, _RoughnessStrength4;
                float _NormalStrength5, _RoughnessStrength5;
                float _NormalStrength6, _RoughnessStrength6;
                float _NormalStrength7, _RoughnessStrength7;
                float _NormalStrength8, _RoughnessStrength8;
                float _NormalStrength9, _RoughnessStrength9;
                float _NormalStrength10, _RoughnessStrength10;
                float _NormalStrength11, _RoughnessStrength11;
                float _NormalStrength12, _RoughnessStrength12;
                float _NormalStrength13, _RoughnessStrength13;
                float _NormalStrength14, _RoughnessStrength14;
                float _NormalStrength15, _RoughnessStrength15;
                float4 _LayerMapping0;
                float4 _LayerMapping1;
                float4 _LayerMapping2;
                float4 _LayerMapping3;
                float4 _LayerMapping4;
                float4 _LayerMapping5;
                float4 _LayerMapping6;
                float4 _LayerMapping7;
                float4 _LayerMapping8;
                float4 _LayerMapping9;
                float4 _LayerMapping10;
                float4 _LayerMapping11;
                float4 _LayerMapping12;
                float4 _LayerMapping13;
                float4 _LayerMapping14;
                float4 _LayerMapping15;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 terrainPosition : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
            };
            float3 SampleLayer(int index, float2 uv)
            {
                if (index <= 0) return SAMPLE_TEXTURE2D(_Layer0, sampler_trilinear_repeat, uv).rgb;
                if (index == 1) return SAMPLE_TEXTURE2D(_Layer1, sampler_trilinear_repeat, uv).rgb;
                if (index == 2) return SAMPLE_TEXTURE2D(_Layer2, sampler_trilinear_repeat, uv).rgb;
                if (index == 3) return SAMPLE_TEXTURE2D(_Layer3, sampler_trilinear_repeat, uv).rgb;
                if (index == 4) return SAMPLE_TEXTURE2D(_Layer4, sampler_trilinear_repeat, uv).rgb;
                if (index == 5) return SAMPLE_TEXTURE2D(_Layer5, sampler_trilinear_repeat, uv).rgb;
                if (index == 6) return SAMPLE_TEXTURE2D(_Layer6, sampler_trilinear_repeat, uv).rgb;
                if (index == 7) return SAMPLE_TEXTURE2D(_Layer7, sampler_trilinear_repeat, uv).rgb;
                if (index == 8) return SAMPLE_TEXTURE2D(_Layer8, sampler_trilinear_repeat, uv).rgb;
                if (index == 9) return SAMPLE_TEXTURE2D(_Layer9, sampler_trilinear_repeat, uv).rgb;
                if (index == 10) return SAMPLE_TEXTURE2D(_Layer10, sampler_trilinear_repeat, uv).rgb;
                if (index == 11) return SAMPLE_TEXTURE2D(_Layer11, sampler_trilinear_repeat, uv).rgb;
                if (index == 12) return SAMPLE_TEXTURE2D(_Layer12, sampler_trilinear_repeat, uv).rgb;
                if (index == 13) return SAMPLE_TEXTURE2D(_Layer13, sampler_trilinear_repeat, uv).rgb;
                if (index == 14) return SAMPLE_TEXTURE2D(_Layer14, sampler_trilinear_repeat, uv).rgb;
                return SAMPLE_TEXTURE2D(_Layer15, sampler_trilinear_repeat, uv).rgb;
            }
            float SampleLayerAlpha(int index, float2 uv)
            {
                if (index == 0) return SAMPLE_TEXTURE2D(_Layer0, sampler_trilinear_repeat, uv).a;
                if (index == 1) return SAMPLE_TEXTURE2D(_Layer1, sampler_trilinear_repeat, uv).a;
                if (index == 2) return SAMPLE_TEXTURE2D(_Layer2, sampler_trilinear_repeat, uv).a;
                if (index == 3) return SAMPLE_TEXTURE2D(_Layer3, sampler_trilinear_repeat, uv).a;
                if (index == 4) return SAMPLE_TEXTURE2D(_Layer4, sampler_trilinear_repeat, uv).a;
                if (index == 5) return SAMPLE_TEXTURE2D(_Layer5, sampler_trilinear_repeat, uv).a;
                if (index == 6) return SAMPLE_TEXTURE2D(_Layer6, sampler_trilinear_repeat, uv).a;
                if (index == 7) return SAMPLE_TEXTURE2D(_Layer7, sampler_trilinear_repeat, uv).a;
                if (index == 8) return SAMPLE_TEXTURE2D(_Layer8, sampler_trilinear_repeat, uv).a;
                if (index == 9) return SAMPLE_TEXTURE2D(_Layer9, sampler_trilinear_repeat, uv).a;
                if (index == 10) return SAMPLE_TEXTURE2D(_Layer10, sampler_trilinear_repeat, uv).a;
                if (index == 11) return SAMPLE_TEXTURE2D(_Layer11, sampler_trilinear_repeat, uv).a;
                if (index == 12) return SAMPLE_TEXTURE2D(_Layer12, sampler_trilinear_repeat, uv).a;
                if (index == 13) return SAMPLE_TEXTURE2D(_Layer13, sampler_trilinear_repeat, uv).a;
                if (index == 14) return SAMPLE_TEXTURE2D(_Layer14, sampler_trilinear_repeat, uv).a;
                if (index == 15) return SAMPLE_TEXTURE2D(_Layer15, sampler_trilinear_repeat, uv).a;
                return 0.0;
            }
            float4 SampleLayerNormal(int index, float2 uv)
            {
                if (index == 0) return SAMPLE_TEXTURE2D(_Normal0, sampler_trilinear_repeat, uv);
                if (index == 1) return SAMPLE_TEXTURE2D(_Normal1, sampler_trilinear_repeat, uv);
                if (index == 2) return SAMPLE_TEXTURE2D(_Normal2, sampler_trilinear_repeat, uv);
                if (index == 3) return SAMPLE_TEXTURE2D(_Normal3, sampler_trilinear_repeat, uv);
                if (index == 4) return SAMPLE_TEXTURE2D(_Normal4, sampler_trilinear_repeat, uv);
                if (index == 5) return SAMPLE_TEXTURE2D(_Normal5, sampler_trilinear_repeat, uv);
                if (index == 6) return SAMPLE_TEXTURE2D(_Normal6, sampler_trilinear_repeat, uv);
                if (index == 7) return SAMPLE_TEXTURE2D(_Normal7, sampler_trilinear_repeat, uv);
                if (index == 8) return SAMPLE_TEXTURE2D(_Normal8, sampler_trilinear_repeat, uv);
                if (index == 9) return SAMPLE_TEXTURE2D(_Normal9, sampler_trilinear_repeat, uv);
                if (index == 10) return SAMPLE_TEXTURE2D(_Normal10, sampler_trilinear_repeat, uv);
                if (index == 11) return SAMPLE_TEXTURE2D(_Normal11, sampler_trilinear_repeat, uv);
                if (index == 12) return SAMPLE_TEXTURE2D(_Normal12, sampler_trilinear_repeat, uv);
                if (index == 13) return SAMPLE_TEXTURE2D(_Normal13, sampler_trilinear_repeat, uv);
                if (index == 14) return SAMPLE_TEXTURE2D(_Normal14, sampler_trilinear_repeat, uv);
                if (index == 15) return SAMPLE_TEXTURE2D(_Normal15, sampler_trilinear_repeat, uv);
                return float4(0.5, 0.5, 1, 1);
            }
            float SampleLayerRoughness(int index, float2 uv)
            {
                if (index == 0) return SAMPLE_TEXTURE2D(_Roughness0, sampler_trilinear_repeat, uv).r;
                if (index == 1) return SAMPLE_TEXTURE2D(_Roughness1, sampler_trilinear_repeat, uv).r;
                if (index == 2) return SAMPLE_TEXTURE2D(_Roughness2, sampler_trilinear_repeat, uv).r;
                if (index == 3) return SAMPLE_TEXTURE2D(_Roughness3, sampler_trilinear_repeat, uv).r;
                if (index == 4) return SAMPLE_TEXTURE2D(_Roughness4, sampler_trilinear_repeat, uv).r;
                if (index == 5) return SAMPLE_TEXTURE2D(_Roughness5, sampler_trilinear_repeat, uv).r;
                if (index == 6) return SAMPLE_TEXTURE2D(_Roughness6, sampler_trilinear_repeat, uv).r;
                if (index == 7) return SAMPLE_TEXTURE2D(_Roughness7, sampler_trilinear_repeat, uv).r;
                if (index == 8) return SAMPLE_TEXTURE2D(_Roughness8, sampler_trilinear_repeat, uv).r;
                if (index == 9) return SAMPLE_TEXTURE2D(_Roughness9, sampler_trilinear_repeat, uv).r;
                if (index == 10) return SAMPLE_TEXTURE2D(_Roughness10, sampler_trilinear_repeat, uv).r;
                if (index == 11) return SAMPLE_TEXTURE2D(_Roughness11, sampler_trilinear_repeat, uv).r;
                if (index == 12) return SAMPLE_TEXTURE2D(_Roughness12, sampler_trilinear_repeat, uv).r;
                if (index == 13) return SAMPLE_TEXTURE2D(_Roughness13, sampler_trilinear_repeat, uv).r;
                if (index == 14) return SAMPLE_TEXTURE2D(_Roughness14, sampler_trilinear_repeat, uv).r;
                if (index == 15) return SAMPLE_TEXTURE2D(_Roughness15, sampler_trilinear_repeat, uv).r;
                return 1.0;
            }
            float GetNormalStrength(int index)
            {
                if (index == 0) return _NormalStrength0;
                if (index == 1) return _NormalStrength1;
                if (index == 2) return _NormalStrength2;
                if (index == 3) return _NormalStrength3;
                if (index == 4) return _NormalStrength4;
                if (index == 5) return _NormalStrength5;
                if (index == 6) return _NormalStrength6;
                if (index == 7) return _NormalStrength7;
                if (index == 8) return _NormalStrength8;
                if (index == 9) return _NormalStrength9;
                if (index == 10) return _NormalStrength10;
                if (index == 11) return _NormalStrength11;
                if (index == 12) return _NormalStrength12;
                if (index == 13) return _NormalStrength13;
                if (index == 14) return _NormalStrength14;
                if (index == 15) return _NormalStrength15;
                return 0.0;
            }
            float GetRoughnessStrength(int index)
            {
                if (index == 0) return _RoughnessStrength0;
                if (index == 1) return _RoughnessStrength1;
                if (index == 2) return _RoughnessStrength2;
                if (index == 3) return _RoughnessStrength3;
                if (index == 4) return _RoughnessStrength4;
                if (index == 5) return _RoughnessStrength5;
                if (index == 6) return _RoughnessStrength6;
                if (index == 7) return _RoughnessStrength7;
                if (index == 8) return _RoughnessStrength8;
                if (index == 9) return _RoughnessStrength9;
                if (index == 10) return _RoughnessStrength10;
                if (index == 11) return _RoughnessStrength11;
                if (index == 12) return _RoughnessStrength12;
                if (index == 13) return _RoughnessStrength13;
                if (index == 14) return _RoughnessStrength14;
                if (index == 15) return _RoughnessStrength15;
                return 0.0;
            }
            float GetHasNormal(int index)
{
    if (index == 0) return _HasNormal0;
    if (index == 1) return _HasNormal1;
    if (index == 2) return _HasNormal2;
    if (index == 3) return _HasNormal3;
    if (index == 4) return _HasNormal4;
    if (index == 5) return _HasNormal5;
    if (index == 6) return _HasNormal6;
    if (index == 7) return _HasNormal7;
    if (index == 8) return _HasNormal8;
    if (index == 9) return _HasNormal9;
    if (index == 10) return _HasNormal10;
    if (index == 11) return _HasNormal11;
    if (index == 12) return _HasNormal12;
    if (index == 13) return _HasNormal13;
    if (index == 14) return _HasNormal14;
    if (index == 15) return _HasNormal15;
    return 0.0;
}
float GetHasRoughness(int index)
{
    if (index == 0) return _HasRoughness0;
    if (index == 1) return _HasRoughness1;
    if (index == 2) return _HasRoughness2;
    if (index == 3) return _HasRoughness3;
    if (index == 4) return _HasRoughness4;
    if (index == 5) return _HasRoughness5;
    if (index == 6) return _HasRoughness6;
    if (index == 7) return _HasRoughness7;
    if (index == 8) return _HasRoughness8;
    if (index == 9) return _HasRoughness9;
    if (index == 10) return _HasRoughness10;
    if (index == 11) return _HasRoughness11;
    if (index == 12) return _HasRoughness12;
    if (index == 13) return _HasRoughness13;
    if (index == 14) return _HasRoughness14;
    if (index == 15) return _HasRoughness15;
    return 0.0;
}
            float GetHasAlbedo(int index)
            {
                if (index == 0) return _HasAlbedo0;
                if (index == 1) return _HasAlbedo1;
                if (index == 2) return _HasAlbedo2;
                if (index == 3) return _HasAlbedo3;
                if (index == 4) return _HasAlbedo4;
                if (index == 5) return _HasAlbedo5;
                if (index == 6) return _HasAlbedo6;
                if (index == 7) return _HasAlbedo7;
                if (index == 8) return _HasAlbedo8;
                if (index == 9) return _HasAlbedo9;
                if (index == 10) return _HasAlbedo10;
                if (index == 11) return _HasAlbedo11;
                if (index == 12) return _HasAlbedo12;
                if (index == 13) return _HasAlbedo13;
                if (index == 14) return _HasAlbedo14;
                if (index == 15) return _HasAlbedo15;
                return 0.0;
            }
float4 GetLayerMapping(int index)
            {
                if (index == 0) return _LayerMapping0;
                if (index == 1) return _LayerMapping1;
                if (index == 2) return _LayerMapping2;
                if (index == 3) return _LayerMapping3;
                if (index == 4) return _LayerMapping4;
                if (index == 5) return _LayerMapping5;
                if (index == 6) return _LayerMapping6;
                if (index == 7) return _LayerMapping7;
                if (index == 8) return _LayerMapping8;
                if (index == 9) return _LayerMapping9;
                if (index == 10) return _LayerMapping10;
                if (index == 11) return _LayerMapping11;
                if (index == 12) return _LayerMapping12;
                if (index == 13) return _LayerMapping13;
                if (index == 14) return _LayerMapping14;
                if (index == 15) return _LayerMapping15;
                return float4(1, 1, 1, 1);
            }
            // Unity's mask packing order is alpha, red, green, blue.
            float3 ProjectLayerNormal(float3 tangentNormal, int axis, float3 geometricNormal)
{
    float3 projected = axis == 0 ? float3(-tangentNormal.z, tangentNormal.y, tangentNormal.x) :
        (axis == 1 ? float3(tangentNormal.x, -tangentNormal.z, tangentNormal.y) : tangentNormal);
    if (dot(projected, geometricNormal) < 0.0) projected = -projected;
    return normalize(projected);
}
float3 EvaluateTerrainSpecular(float3 normalWS, float3 viewDir, float3 lightDir, float3 lightColor, float roughness)
{
    float ndotl = saturate(dot(normalWS, lightDir));
    float ndotv = saturate(dot(normalWS, viewDir));
    if (ndotl <= 0.0 || ndotv <= 0.0) return 0.0;
    float3 halfDir = normalize(viewDir + lightDir);
    float ndoth = saturate(dot(normalWS, halfDir));
    float vdoth = saturate(dot(viewDir, halfDir));
    float alpha = max(roughness * roughness, 0.002);
    float alpha2 = alpha * alpha;
    float d = ndoth * ndoth * (alpha2 - 1.0) + 1.0;
    float distribution = alpha2 / max(3.14159265 * d * d, 1e-5);
    float k = (roughness + 1.0);
    k = k * k * 0.125;
    float geometryV = ndotv / max(ndotv * (1.0 - k) + k, 1e-5);
    float geometryL = ndotl / max(ndotl * (1.0 - k) + k, 1e-5);
    float fresnel = 0.04 + 0.96 * pow(1.0 - vdoth, 5.0);
    return lightColor * (distribution * geometryV * geometryL * fresnel * ndotl / max(4.0 * ndotv * ndotl, 1e-4));
}
float SampleOverlay(int layer, float2 uv)
            {
                int splat = (layer - 1) / 4;
                int channel = (layer - 1) - splat * 4;
                float4 mask = splat == 0 ? SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, uv) :
                    splat == 1 ? SAMPLE_TEXTURE2D(_Splat1, sampler_Splat0, uv) :
                    splat == 2 ? SAMPLE_TEXTURE2D(_Splat2, sampler_Splat0, uv) :
                    SAMPLE_TEXTURE2D(_Splat3, sampler_Splat0, uv);
                if (channel == 0) return mask.a;
                if (channel == 1) return mask.r;
                if (channel == 2) return mask.g;
                return mask.b;
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.terrainPosition = input.positionOS.xyz;
                output.uv = input.uv;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.shadowCoord = GetShadowCoord(positionInputs);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 color = 0;
                float3 surfaceNormal = normalize(input.normalWS);
                float normalInfluence = 0.0;
                float roughness = 1.0;
                float roughnessInfluence = 0.0;
                int count = min((int)_LayerCount, 16);
                [loop] for (int i = 0; i < count; i++)
                {
                    bool importedLayer = _SourceLayerCount < -0.5 || i < (int)_SourceLayerCount;
                    float4 p = importedLayer ? LOAD_TEXTURE2D(_LayerParams, int2(i, 0)) : GetLayerMapping(i);
                    float3 pos = input.terrainPosition;
                    // FillVertexBuffer / ApplyLayerTexture: YZ, XZ, XY.
                    // The retail world uses zero X/Z texture offsets.
                    int axis = importedLayer ? (int)round(p.w) : (int)round(p.z);
                    float2 uv = importedLayer
                        ? (p.w < 0.5 ? float2(pos.z, pos.y - _MinHeight) :
                            (p.w < 1.5 ? pos.xz : float2(pos.x, pos.y - _MinHeight)))
                        : (axis == 0 ? float2(pos.z, pos.y - _MinHeight) :
                            (axis == 1 ? pos.xz : float2(pos.x, pos.y - _MinHeight)));
                    int slot = importedLayer ? (int)p.x : i;
                    int layerBit = 1 << slot;
                    uv *= importedLayer ? p.yz : p.xy;
                    if (((int)_FlipLayers & layerBit) != 0) uv.y = -uv.y;
                    float3 layer = SampleLayer(slot, uv);
                    #ifndef UNITY_COLORSPACE_GAMMA
                        if (((int)_SrgbLayers & layerBit) != 0) layer = LinearToSRGB(layer);
                    #endif
                    float mask = i == 0 ? 1.0 :
                (importedLayer ? SampleOverlay(i, input.uv) :
                (GetHasAlbedo(i) > 0.5 ? SampleLayerAlpha(slot, uv) : 0.0));
                    color = lerp(color, layer, mask);
                    float hasNormal = GetHasNormal(i);
                    float normalStrength = saturate(GetNormalStrength(i) * 0.5) * hasNormal;
                    if (normalStrength > 0.0)
                    {
                        float3 tangentNormal = UnpackNormalScale(SampleLayerNormal(i, uv), normalStrength * 2.0);
                        float3 layerNormal = ProjectLayerNormal(tangentNormal, axis, normalize(input.normalWS));
                        surfaceNormal = normalize(lerp(surfaceNormal, layerNormal, mask));
                        normalInfluence = max(normalInfluence, normalStrength * mask);
                    }
                    float hasRoughness = GetHasRoughness(i);
                    float roughnessStrength = saturate(GetRoughnessStrength(i)) * hasRoughness;
                    if (roughnessStrength > 0.0)
                    {
                        float layerRoughness = lerp(1.0, saturate(SampleLayerRoughness(i, uv)), roughnessStrength);
                        roughness = lerp(roughness, layerRoughness, mask);
                        roughnessInfluence = max(roughnessInfluence, roughnessStrength * mask);
                    }
                }
                if (_UseLightmap > 0.5)
                {
                    float3 light = SAMPLE_TEXTURE2D(_Lightmap, sampler_Lightmap, input.uv).rgb;
                    if (_UseNightLightmap > 0.5)
                        light = lerp(light, SAMPLE_TEXTURE2D(_NightLightmap, sampler_Lightmap, input.uv).rgb, saturate(_MapDayNight));
                    color *= light * 2;
                }
                color = saturate(color * _Brightness);
                Light mainLight = GetMainLight(input.shadowCoord);
                float shadowAttenuation = _ReceiveShadows > 0.5 ? mainLight.shadowAttenuation : 1.0;
                color *= shadowAttenuation;
                float3 viewDir = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float ndotl = saturate(dot(surfaceNormal, mainLight.direction));
                color *= lerp(1.0, ndotl, normalInfluence);
                float specularWeight = max(normalInfluence, roughnessInfluence);
                color += EvaluateTerrainSpecular(surfaceNormal, viewDir, mainLight.direction, mainLight.color,
                    roughness) * (specularWeight * shadowAttenuation);
                #ifndef UNITY_COLORSPACE_GAMMA
                    color = SRGBToLinear(color);
                #endif
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    CustomEditor "SilverEcho.ForsakenWorld.Editor.TerrainLayerShaderGUI"
}
