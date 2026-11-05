#ifndef ANGELICA_LITMODEL_HDRP_INCLUDED
#define ANGELICA_LITMODEL_HDRP_INCLUDED
#include "AngelicaHdrpCommon.hlsl"

TEXTURE2D(_Albedo); SAMPLER(sampler_Albedo);
CBUFFER_START(UnityPerMaterial)
    float4 _Albedo_ST;
    float _AlphaRef;
    float _Brightness;
CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
};
struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
};

Varyings Vert(Attributes input)
{
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = TRANSFORM_TEX(input.uv, _Albedo);
    output.color = input.color;
    return output;
}

half4 Frag(Varyings input) : SV_Target
{
    float4 tex = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv);
    clip(tex.a - _AlphaRef);
#ifdef ANGELICA_DEPTH_ONLY
    return 0;
#else
    float3 color = saturate(tex.rgb * input.color.rgb * 2.0 * _Brightness);
#ifndef UNITY_COLORSPACE_GAMMA
    color = SRGBToLinear(color);
#endif
    return half4(color, tex.a);
#endif
}
#endif
