Shader "Forsaken World/Angelica Terrain"
{
    // Mesh terrain, not Unity Terrain. Color slots are direct Texture2D links to the
    // imported DDS files (ps/t2_4layer_batch_14.txt t0-t3). Splat, height and lightmap
    // are generated assets. Blend matches angelica_terrain.gdshader / A3DTerrain2:
    // layer 0 is opaque, each later layer lerps over it by its mask, then lightmap x2.
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
            TEXTURE2D(_Layer0); TEXTURE2D(_Layer1); TEXTURE2D(_Layer2); TEXTURE2D(_Layer3);
            TEXTURE2D(_Layer4); TEXTURE2D(_Layer5); TEXTURE2D(_Layer6); TEXTURE2D(_Layer7);
            TEXTURE2D(_Layer8); TEXTURE2D(_Layer9); TEXTURE2D(_Layer10); TEXTURE2D(_Layer11);
            TEXTURE2D(_Layer12); TEXTURE2D(_Layer13); TEXTURE2D(_Layer14); TEXTURE2D(_Layer15);
            SAMPLER(sampler_Layer0);
            TEXTURE2D(_Splat0); TEXTURE2D(_Splat1); TEXTURE2D(_Splat2); TEXTURE2D(_Splat3);
            SAMPLER(sampler_Splat0);
            TEXTURE2D(_LayerParams);
            TEXTURE2D(_Lightmap); SAMPLER(sampler_Lightmap);
            TEXTURE2D(_HeightMap);
            CBUFFER_START(UnityPerMaterial)
                float _LayerCount, _MinHeight, _UseLightmap, _Brightness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 terrainPosition : TEXCOORD0; float2 uv : TEXCOORD1; };
            float3 SampleLayer(int index, float2 uv)
            {
                if (index <= 0) return SAMPLE_TEXTURE2D(_Layer0, sampler_Layer0, uv).rgb;
                if (index == 1) return SAMPLE_TEXTURE2D(_Layer1, sampler_Layer0, uv).rgb;
                if (index == 2) return SAMPLE_TEXTURE2D(_Layer2, sampler_Layer0, uv).rgb;
                if (index == 3) return SAMPLE_TEXTURE2D(_Layer3, sampler_Layer0, uv).rgb;
                if (index == 4) return SAMPLE_TEXTURE2D(_Layer4, sampler_Layer0, uv).rgb;
                if (index == 5) return SAMPLE_TEXTURE2D(_Layer5, sampler_Layer0, uv).rgb;
                if (index == 6) return SAMPLE_TEXTURE2D(_Layer6, sampler_Layer0, uv).rgb;
                if (index == 7) return SAMPLE_TEXTURE2D(_Layer7, sampler_Layer0, uv).rgb;
                if (index == 8) return SAMPLE_TEXTURE2D(_Layer8, sampler_Layer0, uv).rgb;
                if (index == 9) return SAMPLE_TEXTURE2D(_Layer9, sampler_Layer0, uv).rgb;
                if (index == 10) return SAMPLE_TEXTURE2D(_Layer10, sampler_Layer0, uv).rgb;
                if (index == 11) return SAMPLE_TEXTURE2D(_Layer11, sampler_Layer0, uv).rgb;
                if (index == 12) return SAMPLE_TEXTURE2D(_Layer12, sampler_Layer0, uv).rgb;
                if (index == 13) return SAMPLE_TEXTURE2D(_Layer13, sampler_Layer0, uv).rgb;
                if (index == 14) return SAMPLE_TEXTURE2D(_Layer14, sampler_Layer0, uv).rgb;
                return SAMPLE_TEXTURE2D(_Layer15, sampler_Layer0, uv).rgb;
            }
            // t2_4layer_batch_14.txt packs four overlay masks as alpha, red, green, blue.
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
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 color = 0;
                int count = min((int)_LayerCount, 16);
                [loop] for (int i = 0; i < count; i++)
                {
                    float4 p = LOAD_TEXTURE2D(_LayerParams, int2(i, 0));
                    float3 pos = input.terrainPosition;
                    float2 uv = p.w < 0.5 ? float2(pos.z, pos.y - _MinHeight) :
                        (p.w < 1.5 ? pos.xz : float2(pos.x, pos.y - _MinHeight));
                    float3 layer = SampleLayer((int)p.x, uv * p.yz);
                    float mask = i == 0 ? 1 : SampleOverlay(i, input.uv);
                    color = lerp(color, layer, mask);
                }
                if (_UseLightmap > 0.5) color *= SAMPLE_TEXTURE2D(_Lightmap, sampler_Lightmap, input.uv).rgb * 2;
                color = saturate(color * _Brightness);
                #ifndef UNITY_COLORSPACE_GAMMA
                    color = SRGBToLinear(color);
                #endif
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
