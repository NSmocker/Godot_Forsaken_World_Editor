#ifndef ANGELICA_ARMOR_REFLECT_HDRP_INCLUDED
#define ANGELICA_ARMOR_REFLECT_HDRP_INCLUDED
#include "AngelicaHdrpCommon.hlsl"

TEXTURE2D(_Albedo); SAMPLER(sampler_Albedo);
TEXTURE2D(_ReflectMap); SAMPLER(sampler_ReflectMap);
CBUFFER_START(UnityPerMaterial)
    float4 _AlbedoColor;
    float4 _Albedo_ST;
    float4 _EmissionColor;
    float _Specular;
    float _AlphaClip;
    float _Cutoff;
    float _Surface;
CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
};
struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
};

Varyings Vert(Attributes input)
{
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = TRANSFORM_TEX(input.uv, _Albedo);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

half4 Frag(Varyings input) : SV_Target
{
    float4 base = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv);
    if (_AlphaClip > 0.5)
        clip(base.a - _Cutoff);
#ifdef ANGELICA_DEPTH_ONLY
    return 0;
#else
    float3 normalVS = mul((float3x3)UNITY_MATRIX_V, normalize(input.normalWS));
    float2 reflectUv = normalize(normalVS).xy * float2(0.5, -0.5) + 0.5;
    float3 reflection = SAMPLE_TEXTURE2D(_ReflectMap, sampler_ReflectMap, reflectUv).rgb;
    float3 color = _AlbedoColor.rgb * (base.rgb + reflection * base.a);
#ifndef UNITY_COLORSPACE_GAMMA
    color = SRGBToLinear(color);
#endif
    color += reflection * (_Specular * base.a) * 0.25;
    color += _EmissionColor.rgb;
    float alpha = _Surface > 0.5 ? base.a * _AlbedoColor.a : 1.0;
    return half4(color, alpha);
#endif
}
#endif
