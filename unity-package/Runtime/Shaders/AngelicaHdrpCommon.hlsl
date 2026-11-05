#ifndef ANGELICA_HDRP_COMMON_INCLUDED
#define ANGELICA_HDRP_COMMON_INCLUDED

// The package already depends on URP, so this include exists in every host.
// The shader tags are HDRenderPipeline / ForwardOnly, so HDRP draws these passes.
// Visual math matches the URP twins.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

float3 AngelicaCameraPositionWS()
{
    return GetCameraPositionWS();
}

float2 AngelicaNormalizedScreenUV(float4 positionCS)
{
    return GetNormalizedScreenSpaceUV(positionCS);
}

#endif
