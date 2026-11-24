#ifndef ANGELICA_TERRAIN_PARALLAX_INCLUDED
#define ANGELICA_TERRAIN_PARALLAX_INCLUDED

float4 _TerrainHeightChannels0, _TerrainHeightChannels1, _TerrainHeightChannels2, _TerrainHeightChannels3;
float _UseParallax0, _ParallaxDepth0, _ParallaxSteps0, _ParallaxFadeMip0;
float _UseParallax1, _ParallaxDepth1, _ParallaxSteps1, _ParallaxFadeMip1;
float _UseParallax2, _ParallaxDepth2, _ParallaxSteps2, _ParallaxFadeMip2;
float _UseParallax3, _ParallaxDepth3, _ParallaxSteps3, _ParallaxFadeMip3;
float _UseParallax4, _ParallaxDepth4, _ParallaxSteps4, _ParallaxFadeMip4;
float _UseParallax5, _ParallaxDepth5, _ParallaxSteps5, _ParallaxFadeMip5;
float _UseParallax6, _ParallaxDepth6, _ParallaxSteps6, _ParallaxFadeMip6;
float _UseParallax7, _ParallaxDepth7, _ParallaxSteps7, _ParallaxFadeMip7;
float _UseParallax8, _ParallaxDepth8, _ParallaxSteps8, _ParallaxFadeMip8;
float _UseParallax9, _ParallaxDepth9, _ParallaxSteps9, _ParallaxFadeMip9;
float _UseParallax10, _ParallaxDepth10, _ParallaxSteps10, _ParallaxFadeMip10;
float _UseParallax11, _ParallaxDepth11, _ParallaxSteps11, _ParallaxFadeMip11;
float _UseParallax12, _ParallaxDepth12, _ParallaxSteps12, _ParallaxFadeMip12;
float _UseParallax13, _ParallaxDepth13, _ParallaxSteps13, _ParallaxFadeMip13;
float _UseParallax14, _ParallaxDepth14, _ParallaxSteps14, _ParallaxFadeMip14;
float _UseParallax15, _ParallaxDepth15, _ParallaxSteps15, _ParallaxFadeMip15;

uint GetHeightChannels(int layer)
{
    if (layer < 4) return (uint)_TerrainHeightChannels0[layer];
    if (layer < 8) return (uint)_TerrainHeightChannels1[layer - 4];
    if (layer < 12) return (uint)_TerrainHeightChannels2[layer - 8];
    return (uint)_TerrainHeightChannels3[layer - 12];
}

float4 GetParallaxSettings(int layer)
{
    switch (layer)
    {
        case 0: return float4(_UseParallax0, _ParallaxDepth0, _ParallaxSteps0, _ParallaxFadeMip0);
        case 1: return float4(_UseParallax1, _ParallaxDepth1, _ParallaxSteps1, _ParallaxFadeMip1);
        case 2: return float4(_UseParallax2, _ParallaxDepth2, _ParallaxSteps2, _ParallaxFadeMip2);
        case 3: return float4(_UseParallax3, _ParallaxDepth3, _ParallaxSteps3, _ParallaxFadeMip3);
        case 4: return float4(_UseParallax4, _ParallaxDepth4, _ParallaxSteps4, _ParallaxFadeMip4);
        case 5: return float4(_UseParallax5, _ParallaxDepth5, _ParallaxSteps5, _ParallaxFadeMip5);
        case 6: return float4(_UseParallax6, _ParallaxDepth6, _ParallaxSteps6, _ParallaxFadeMip6);
        case 7: return float4(_UseParallax7, _ParallaxDepth7, _ParallaxSteps7, _ParallaxFadeMip7);
        case 8: return float4(_UseParallax8, _ParallaxDepth8, _ParallaxSteps8, _ParallaxFadeMip8);
        case 9: return float4(_UseParallax9, _ParallaxDepth9, _ParallaxSteps9, _ParallaxFadeMip9);
        case 10: return float4(_UseParallax10, _ParallaxDepth10, _ParallaxSteps10, _ParallaxFadeMip10);
        case 11: return float4(_UseParallax11, _ParallaxDepth11, _ParallaxSteps11, _ParallaxFadeMip11);
        case 12: return float4(_UseParallax12, _ParallaxDepth12, _ParallaxSteps12, _ParallaxFadeMip12);
        case 13: return float4(_UseParallax13, _ParallaxDepth13, _ParallaxSteps13, _ParallaxFadeMip13);
        case 14: return float4(_UseParallax14, _ParallaxDepth14, _ParallaxSteps14, _ParallaxFadeMip14);
        case 15: return float4(_UseParallax15, _ParallaxDepth15, _ParallaxSteps15, _ParallaxFadeMip15);
    }
    return 0.0;
}

struct PerPixelHeightDisplacementParam
{
    float2 uv;
    float slice;
};

real ComputePerPixelHeightDisplacement(float2 offset, float lod, PerPixelHeightDisplacementParam param)
{
    return SAMPLE_TEXTURE2D_ARRAY_LOD(_TerrainDetails, sampler_TrilinearRepeat, param.uv + offset, param.slice, lod).a;
}

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/PerPixelDisplacement.hlsl"

float2 EvaluateParallaxOffset(int layer, float slice, int axis, float2 uvScale, float2 uv,
    float2 uvDx, float2 uvDy, float3 tangentViewOS, float facing)
{
    float4 settings = GetParallaxSettings(layer);
    float depth = clamp(settings.y, 0.0, 10.0) * 0.01;
    if (settings.x < 0.5 || depth <= 0.0 || facing <= 0.1) return 0.0;

    // Convert the tangent component of the view into the layer's signed UV units.
    float2 slope = ProjectLayerUV(tangentViewOS, axis) * uvScale * depth;
    slope *= smoothstep(0.1, 0.3, facing);
    if (dot(slope, slope) <= 1e-10) return 0.0;

    uint width, height, slices;
    _TerrainDetails.GetDimensions(width, height, slices);
    float lod = max(0.0, ComputeTextureLOD(uvDx, uvDy, float2(width, height)));
    float fadeMip = clamp(settings.w, 0.0, 12.0);
    if (lod >= fadeMip + 1.0) return 0.0;

    int maxSteps = (int)round(clamp(settings.z, 4.0, 32.0));
    int steps = (int)round(lerp(min(6, maxSteps), maxSteps, 1.0 - facing));
    PerPixelHeightDisplacementParam param;
    param.uv = uv;
    param.slice = slice;
    real surfaceHeight;
    return ParallaxOcclusionMapping(lod, fadeMip, steps, float3(slope, max(facing, 0.2)), param, surfaceHeight);
}

#endif
