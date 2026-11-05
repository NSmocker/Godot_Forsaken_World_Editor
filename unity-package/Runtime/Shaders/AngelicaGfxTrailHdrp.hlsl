#ifndef ANGELICA_GFX_TRAIL_HDRP_INCLUDED
#define ANGELICA_GFX_TRAIL_HDRP_INCLUDED
#include "AngelicaHdrpCommon.hlsl"

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_CameraOpaqueTexture); SAMPLER(sampler_CameraOpaqueTexture);
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseColor;
    float _GfxAlpha;
    float _Warp;
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
    output.uv = input.uv;
    output.color = input.color;
    return output;
}

half4 Frag(Varyings input) : SV_Target
{
    half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
    if (_Warp > 0.5h)
    {
        half3 normalMap = tex.rgb * 2.0h - 1.0h;
        normalMap = lerp(half3(0.0h, 0.0h, 1.0h), normalMap, tex.a);
        normalMap = normalize(normalMap);
        half2 warpDir = normalMap.xy / max(normalMap.z, 0.1h);
        half strength = input.color.a * _GfxAlpha * _BaseColor.a;
        half2 offset = warpDir * 0.50h * strength;
        float2 screenUV = AngelicaNormalizedScreenUV(input.positionCS);
        half3 distorted = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + offset).rgb;
        return half4(distorted, tex.a);
    }
    half4 col = tex * _BaseColor * input.color;
    col.a *= _GfxAlpha;
    return col;
}
#endif
