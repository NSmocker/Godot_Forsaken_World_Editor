#ifndef ANGELICA_GFX_SPRITE_HDRP_INCLUDED
#define ANGELICA_GFX_SPRITE_HDRP_INCLUDED
#include "AngelicaHdrpCommon.hlsl"

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_CameraOpaqueTexture); SAMPLER(sampler_CameraOpaqueTexture);
TEXTURE2D_X(_AngelicaGfxWarpSource);
float4 _AngelicaGfxWarpSourceScale;
float4 _AngelicaGfxViewportSize;

float2 AngelicaGfxScreenSize()
{
    return _AngelicaGfxViewportSize.x > 0.0 ? _AngelicaGfxViewportSize.xy : _ScreenParams.xy;
}

float3 SampleSceneColor(float2 uv)
{
    return SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv).rgb;
}
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseColor;
    float4 _UvRect;
    float _UvExchange;
    float _UseVertexColor;
    float _GfxAlpha;
    float _Warp;
    float _WarpNormalIsSRGB;
    float4 _GfxWarpDecal;
CBUFFER_END
struct Attributes
{
    UNITY_VERTEX_INPUT_INSTANCE_ID
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    float2 warpStrength : TEXCOORD1;
    half4 color : COLOR;
};
struct Varyings
{
    UNITY_VERTEX_OUTPUT_STEREO
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float2 warpStrength : TEXCOORD1;
    half4 color : COLOR;
};
Varyings Vert(Attributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    Varyings output;
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.positionCS.xy += _GfxWarpDecal.z * output.positionCS.w *
        float2(1.0, -_ProjectionParams.x) / AngelicaGfxScreenSize();
    float2 unitUV = input.uv;
    unitUV.y = lerp(unitUV.y, 1.0 - unitUV.y, _UseVertexColor);
    unitUV = lerp(unitUV, unitUV.yx, _UvExchange);
    float2 uv = unitUV * _UvRect.xy + _UvRect.zw;
    uv.y = 1.0 - uv.y;
    output.color = lerp(half4(1,1,1,1), input.color, _UseVertexColor);
    output.uv = uv;
    output.warpStrength = _GfxWarpDecal.z > 0.5 ? _GfxWarpDecal.xy : input.warpStrength;
    return output;
}
float2 WarpSourceUv(Varyings input, float4 tex)
{
    clip(input.warpStrength.x);
    clip(tex.a - 0.05);
    #ifndef UNITY_COLORSPACE_GAMMA
        if (_WarpNormalIsSRGB > 0.5) tex.rgb = LinearToSRGB(tex.rgb);
    #endif
    float2 offset = (tex.rg * 2.0 - 1.0) * float2(-1.0, 1.0) * input.warpStrength * tex.a;
    // Transform the integer-center correction and the Warp displacement together.
    float2 direction = float2(1.0, _ProjectionParams.x);
    return (input.positionCS.xy - 0.5 * direction) / AngelicaGfxScreenSize() + offset * direction;
}
half4 Frag(Varyings input) : SV_Target
{
    half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
    if (_Warp > 0.5h)
    {
        if (_GfxWarpDecal.z > 0.5h)
            return half4(SampleSceneColor(WarpSourceUv(input, tex)), 1.0h);
        // Godot create_id_100: normal from tex → warp_dir → screen_texture offset.
        half3 normalMap = tex.rgb * 2.0h - 1.0h;
        normalMap = lerp(half3(0.0h, 0.0h, 1.0h), normalMap, tex.a);
        normalMap = normalize(normalMap);
        half2 warpDir = normalMap.xy / max(normalMap.z, 0.1h);
        half strength = _BaseColor.a * _GfxAlpha;
        half2 offset = warpDir * 0.50h * strength;
        float2 screenUV = input.positionCS.xy / AngelicaGfxScreenSize();
        TransformNormalizedScreenUV(screenUV);
        half3 distorted = SampleSceneColor(screenUV + offset);
        return half4(distorted, tex.a);
    }
    half4 col = tex * _BaseColor * input.color;
    #ifndef UNITY_COLORSPACE_GAMMA
        // Angelica's MODULATE uses the packed vertex RGB in the texture's
        // encoded color space. Vertex attributes bypass Unity's sRGB decode.
        if (_UseVertexColor > 0.5h)
            col.rgb = SRGBToLinear(LinearToSRGB(tex.rgb) * input.color.rgb) * _BaseColor.rgb;
    #endif
    col.a *= _GfxAlpha;
    return col;
}
float4 FragEncoded(Varyings input) : SV_Target
{
    // The encoded pass composites into an ordinary UNORM attachment.
    // Distortion remains in the scene-color pass and is not drawn here.
    clip(0.5 - _Warp);
    float4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
    float4 tint = _BaseColor;
    #ifndef UNITY_COLORSPACE_GAMMA
        tex.rgb = LinearToSRGB(tex.rgb);
        tint.rgb = LinearToSRGB(tint.rgb);
    #endif
    float4 color = tex * tint * input.color;
    color.a *= _GfxAlpha;
    return color;
}
float4 FragWarp(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    clip(_Warp - 0.5);
    float4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
    float2 uv = WarpSourceUv(input, tex);
    return float4(SAMPLE_TEXTURE2D_X(_AngelicaGfxWarpSource, sampler_LinearClamp,
        UnityStereoTransformScreenSpaceTex(uv) * _AngelicaGfxWarpSourceScale.xy).rgb, 1.0);
}
#endif
