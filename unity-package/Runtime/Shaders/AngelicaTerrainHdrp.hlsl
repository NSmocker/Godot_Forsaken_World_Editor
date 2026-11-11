#ifndef ANGELICA_TERRAIN_HDRP_INCLUDED
#define ANGELICA_TERRAIN_HDRP_INCLUDED
#include "AngelicaHdrpCommon.hlsl"

TEXTURE2D(_Layer0); TEXTURE2D(_Layer1); TEXTURE2D(_Layer2); TEXTURE2D(_Layer3);
TEXTURE2D(_Layer4); TEXTURE2D(_Layer5); TEXTURE2D(_Layer6); TEXTURE2D(_Layer7);
TEXTURE2D(_Layer8); TEXTURE2D(_Layer9); TEXTURE2D(_Layer10); TEXTURE2D(_Layer11);
TEXTURE2D(_Layer12); TEXTURE2D(_Layer13); TEXTURE2D(_Layer14); TEXTURE2D(_Layer15);
SAMPLER(sampler_Layer0);
TEXTURE2D(_Splat0); TEXTURE2D(_Splat1); TEXTURE2D(_Splat2); TEXTURE2D(_Splat3);
SAMPLER(sampler_Splat0);
TEXTURE2D(_LayerParams);
TEXTURE2D(_Lightmap); SAMPLER(sampler_Lightmap);
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
CBUFFER_START(UnityPerMaterial)
    float _LayerCount, _MinHeight, _UseLightmap, _Brightness;
CBUFFER_END

struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 terrainPosition : TEXCOORD0;
    float2 uv : TEXCOORD1;
};

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
        float3 layer = SampleLayer((int)p.x, uv * p.yz);
        float mask = i == 0 ? 1 : SampleOverlay(i, input.uv);
        color = lerp(color, layer, mask);
    }
    if (_UseLightmap > 0.5) color *= SAMPLE_TEXTURE2D(_Lightmap, sampler_Lightmap, input.uv).rgb * 2;
    color = saturate(color * _Brightness);
#ifndef UNITY_COLORSPACE_GAMMA
    color = SRGBToLinear(color);
#endif
#ifdef ANGELICA_DEPTH_ONLY
    return 0;
#else
    return half4(color, 1);
#endif
}
#endif
