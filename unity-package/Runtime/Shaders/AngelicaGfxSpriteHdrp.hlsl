#ifndef ANGELICA_GFX_SPRITE_HDRP_INCLUDED
#define ANGELICA_GFX_SPRITE_HDRP_INCLUDED
#include "AngelicaHdrpCommon.hlsl"

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_CameraOpaqueTexture); SAMPLER(sampler_CameraOpaqueTexture);
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseColor;
    float4 _UvRect;
    float _UvExchange;
    float _UseVertexColor;
                float _GfxAlpha;
    float _Warp;
CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
                half4 color : COLOR;
};
struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
                half4 color : COLOR;
};

Varyings Vert(Attributes input)
{
    Varyings output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    float2 unitUV = input.uv;
    unitUV.y = lerp(unitUV.y, 1.0 - unitUV.y, _UseVertexColor);
    unitUV = lerp(unitUV, unitUV.yx, _UvExchange);
    output.uv = unitUV * _UvRect.xy + _UvRect.zw;
    output.uv.y = 1.0 - output.uv.y;
    output.color = lerp(half4(1,1,1,1), input.color, _UseVertexColor);
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
        half strength = _BaseColor.a * _GfxAlpha;
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
