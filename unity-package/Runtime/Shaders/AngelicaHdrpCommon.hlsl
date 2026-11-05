#ifndef ANGELICA_HDRP_COMMON_INCLUDED
#define ANGELICA_HDRP_COMMON_INCLUDED

// Core only: HDRP package headers are not required, so these shaders still
// compile in a URP host. HDRP draws the ForwardOnly / DepthForwardOnly passes.
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

float3 AngelicaCameraPositionWS()
{
    return UNITY_MATRIX_I_V._m03_m13_m23;
}

float2 AngelicaNormalizedScreenUV(float4 positionCS)
{
    float2 uv = positionCS.xy * rcp(max(positionCS.w, 1e-6));
    uv = uv * 0.5 + 0.5;
#if UNITY_UV_STARTS_AT_TOP
    uv.y = 1.0 - uv.y;
#endif
    return uv;
}

#endif
