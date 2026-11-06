Shader "Forsaken World/Angelica Terrain"
{
    Properties
    {
        _Layers ("Ground textures", 2DArray) = "" {}
        _Masks ("Layer masks", 2DArray) = "" {}
        _LayerParams ("Layer parameters", 2D) = "black" {}
        _Lightmap ("Daytime lightmap", 2D) = "white" {}
        _LayerCount ("Layer count", Int) = 1
        _MinHeight ("Subterrain minimum height", Float) = 0
        [Toggle] _UseLightmap ("Use daytime lightmap", Float) = 0
        _Brightness ("Brightness", Range(0, 4)) = 2
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D_ARRAY(_Layers); SAMPLER(sampler_Layers);
            TEXTURE2D_ARRAY(_Masks); SAMPLER(sampler_Masks);
            TEXTURE2D(_LayerParams);
            TEXTURE2D(_Lightmap); SAMPLER(sampler_Lightmap);
            CBUFFER_START(UnityPerMaterial)
                float _LayerCount, _MinHeight, _UseLightmap, _Brightness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 terrainPosition : TEXCOORD0; float2 uv : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.terrainPosition = input.positionOS.xyz;
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 color = 0;
                [loop] for (int i = 0; i < min((int)_LayerCount, 16); i++)
                {
                    float4 p = LOAD_TEXTURE2D(_LayerParams, int2(i, 0));
                    float3 pos = input.terrainPosition;
                    float2 uv = p.w < 0.5 ? float2(pos.z, pos.y - _MinHeight) :
                        (p.w < 1.5 ? pos.xz : float2(pos.x, pos.y - _MinHeight));
                    float3 layer = SAMPLE_TEXTURE2D_ARRAY(_Layers, sampler_Layers, uv * p.yz, p.x).rgb;
                    float mask = i == 0 ? 1 : SAMPLE_TEXTURE2D_ARRAY(_Masks, sampler_Masks, input.uv, i - 1).r;
                    color = lerp(color, layer, mask);
                }
                if (_UseLightmap > 0.5) color *= SAMPLE_TEXTURE2D(_Lightmap, sampler_Lightmap, input.uv).rgb * 2;
                color = saturate(color * _Brightness);
                // Angelica/Godot blend encoded source RGB, then convert the finished color.
                #ifndef UNITY_COLORSPACE_GAMMA
                    color = SRGBToLinear(color);
                #endif
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
