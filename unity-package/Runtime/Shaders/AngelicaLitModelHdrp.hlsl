#ifndef ANGELICA_LITMODEL_HDRP_INCLUDED
#define ANGELICA_LITMODEL_HDRP_INCLUDED
#include "AngelicaHdrpCommon.hlsl"
#include "AngelicaLitModelLighting.hlsl"

TEXTURE2D(_Albedo); SAMPLER(sampler_Albedo);
CBUFFER_START(UnityPerMaterial)
    float4 _Albedo_ST;
    float _AlphaRef;
    float _Brightness;
    float _DayNight;
    float _UseMapDayNight;
    float _MapDayNight;
    float _HasDayNightColors;
    float _AlbedoIsSRGB;
CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    float3 nightColor : TEXCOORD2;
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
    float nightFactor = _UseMapDayNight > 0.5 && _MapDayNight >= 0.0 ? _MapDayNight : _DayNight;
    output.color = float4(AngelicaLitModelVertexColor(
        input.color.rgb, input.nightColor, _HasDayNightColors, nightFactor), input.color.a);
    return output;
}

half4 Frag(Varyings input) : SV_Target
{
    float4 tex = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv);
    clip(tex.a - _AlphaRef);
#ifdef ANGELICA_DEPTH_ONLY
    return 0;
#else
    float3 color = AngelicaLitModelModulate(tex.rgb, input.color.rgb, _Brightness, _AlbedoIsSRGB);
    return half4(color, tex.a);
#endif
}
#endif
