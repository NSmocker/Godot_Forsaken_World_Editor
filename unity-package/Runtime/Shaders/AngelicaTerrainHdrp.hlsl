#ifndef ANGELICA_TERRAIN_HDRP_SURFACE_INCLUDED
#define ANGELICA_TERRAIN_HDRP_SURFACE_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

// Layer blend matches Angelica mask-area slots. Samples stay linear: an sRGB
// texture is already decoded by the sampler, and a linear texture is used as stored.
// HDRP Lit then applies exposure, fog, direct light, shadows and indirect GI.

// Direct references to the imported textures. No generated copies.
TEXTURE2D(_Layer0); TEXTURE2D(_Layer1); TEXTURE2D(_Layer2); TEXTURE2D(_Layer3);
TEXTURE2D(_Layer4); TEXTURE2D(_Layer5); TEXTURE2D(_Layer6); TEXTURE2D(_Layer7);
TEXTURE2D(_Layer8); TEXTURE2D(_Layer9); TEXTURE2D(_Layer10); TEXTURE2D(_Layer11);
TEXTURE2D(_Layer12); TEXTURE2D(_Layer13); TEXTURE2D(_Layer14); TEXTURE2D(_Layer15);
#if !defined(ANGELICA_TERRAIN_DETAIL_ARRAY) && !defined(ANGELICA_TERRAIN_PARALLAX)
TEXTURE2D(_Normal0); TEXTURE2D(_Roughness0);
#endif
// Layer count changes texture bindings, so keep separate compiled variants.
#if defined(ANGELICA_TERRAIN_WIDE)
#define ANGELICA_TERRAIN_LAYER_LIMIT 16
#else
#define ANGELICA_TERRAIN_LAYER_LIMIT 8
#endif
TEXTURE2D(_Splat0); TEXTURE2D(_Splat1); TEXTURE2D(_Splat2); TEXTURE2D(_Splat3);
SAMPLER(sampler_Splat0);
TEXTURE2D(_LayerParams);
TEXTURE2D(_LayerMask1); TEXTURE2D(_LayerMask2); TEXTURE2D(_LayerMask3); TEXTURE2D(_LayerMask4);
TEXTURE2D(_LayerMask5); TEXTURE2D(_LayerMask6); TEXTURE2D(_LayerMask7); TEXTURE2D(_LayerMask8);
TEXTURE2D(_LayerMask9); TEXTURE2D(_LayerMask10); TEXTURE2D(_LayerMask11); TEXTURE2D(_LayerMask12);
TEXTURE2D(_LayerMask13); TEXTURE2D(_LayerMask14); TEXTURE2D(_LayerMask15);
TEXTURE2D(_Lightmap); SAMPLER(sampler_Lightmap);
TEXTURE2D(_NightLightmap);

// Material uniforms use the non-batched HDRP path.
    float _LayerCount, _MinHeight, _UseLightmap, _Brightness, _ReceiveShadows;
    float _UseNightLightmap, _MapDayNight;
    float _FlipLayers, _SrgbLayers;
    float _SourceLayerCount, _UseDirectLayers;
    float4 _Layer0_ST;
    float4 _Layer1_ST;
    float4 _Layer2_ST;
    float4 _Layer3_ST;
    float4 _Layer4_ST;
    float4 _Layer5_ST;
    float4 _Layer6_ST;
    float4 _Layer7_ST;
    float4 _Layer8_ST;
    float4 _Layer9_ST;
    float4 _Layer10_ST;
    float4 _Layer11_ST;
    float4 _Layer12_ST;
    float4 _Layer13_ST;
    float4 _Layer14_ST;
    float4 _Layer15_ST;
#if defined(ANGELICA_TERRAIN_DETAIL_ARRAY) || defined(ANGELICA_TERRAIN_PARALLAX)

TEXTURE2D_ARRAY(_TerrainDetails);
float4 _TerrainDetailSlices0, _TerrainDetailSlices1, _TerrainDetailSlices2, _TerrainDetailSlices3;
float _NormalStrength0, _RoughnessStrength0;
float _NormalStrength1, _RoughnessStrength1;
float _NormalStrength2, _RoughnessStrength2;
float _NormalStrength3, _RoughnessStrength3;
float _NormalStrength4, _RoughnessStrength4;
float _NormalStrength5, _RoughnessStrength5;
float _NormalStrength6, _RoughnessStrength6;
float _NormalStrength7, _RoughnessStrength7;
float _NormalStrength8, _RoughnessStrength8;
float _NormalStrength9, _RoughnessStrength9;
float _NormalStrength10, _RoughnessStrength10;
float _NormalStrength11, _RoughnessStrength11;
float _NormalStrength12, _RoughnessStrength12;
float _NormalStrength13, _RoughnessStrength13;
float _NormalStrength14, _RoughnessStrength14;
float _NormalStrength15, _RoughnessStrength15;

float GetDetailSlice(int layer)
{
    if (layer < 4) return _TerrainDetailSlices0[layer];
    if (layer < 8) return _TerrainDetailSlices1[layer - 4];
    if (layer < 12) return _TerrainDetailSlices2[layer - 8];
    return _TerrainDetailSlices3[layer - 12];
}

float2 GetDetailStrength(int layer)
{
    switch (layer)
    {
        case 0: return float2(_NormalStrength0, _RoughnessStrength0);
        case 1: return float2(_NormalStrength1, _RoughnessStrength1);
        case 2: return float2(_NormalStrength2, _RoughnessStrength2);
        case 3: return float2(_NormalStrength3, _RoughnessStrength3);
        case 4: return float2(_NormalStrength4, _RoughnessStrength4);
        case 5: return float2(_NormalStrength5, _RoughnessStrength5);
        case 6: return float2(_NormalStrength6, _RoughnessStrength6);
        case 7: return float2(_NormalStrength7, _RoughnessStrength7);
        case 8: return float2(_NormalStrength8, _RoughnessStrength8);
        case 9: return float2(_NormalStrength9, _RoughnessStrength9);
        case 10: return float2(_NormalStrength10, _RoughnessStrength10);
        case 11: return float2(_NormalStrength11, _RoughnessStrength11);
        case 12: return float2(_NormalStrength12, _RoughnessStrength12);
        case 13: return float2(_NormalStrength13, _RoughnessStrength13);
        case 14: return float2(_NormalStrength14, _RoughnessStrength14);
        case 15: return float2(_NormalStrength15, _RoughnessStrength15);
    }
    return 0.0;
}
#else
    float _NormalStrength0, _RoughnessStrength0;
    float _HasNormal0, _HasRoughness0;
#endif
    float _HasAlbedo0;
    float _HasAlbedo1;
    float _HasAlbedo2;
    float _HasAlbedo3;
    float _HasAlbedo4;
    float _HasAlbedo5;
    float _HasAlbedo6;
    float _HasAlbedo7;
    float _HasAlbedo8;
    float _HasAlbedo9;
    float _HasAlbedo10;
    float _HasAlbedo11;
    float _HasAlbedo12;
    float _HasAlbedo13;
    float _HasAlbedo14;
    float _HasAlbedo15;
    float4 _LayerMapping0;
    float4 _LayerMapping1;
    float4 _LayerMapping2;
    float4 _LayerMapping3;
    float4 _LayerMapping4;
    float4 _LayerMapping5;
    float4 _LayerMapping6;
    float4 _LayerMapping7;
    float4 _LayerMapping8;
    float4 _LayerMapping9;
    float4 _LayerMapping10;
    float4 _LayerMapping11;
    float4 _LayerMapping12;
    float4 _LayerMapping13;
    float4 _LayerMapping14;
    float4 _LayerMapping15;

#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

float4 SampleLayer(int index, float2 uv, float2 uvDx, float2 uvDy)
{
    if (index <= 0) return SAMPLE_TEXTURE2D_GRAD(_Layer0, sampler_TrilinearRepeat, uv, uvDx, uvDy);
    if (index == 1) return SAMPLE_TEXTURE2D_GRAD(_Layer1, sampler_TrilinearRepeat, uv, uvDx, uvDy);
    if (index == 2) return SAMPLE_TEXTURE2D_GRAD(_Layer2, sampler_TrilinearRepeat, uv, uvDx, uvDy);
    if (index == 3) return SAMPLE_TEXTURE2D_GRAD(_Layer3, sampler_TrilinearRepeat, uv, uvDx, uvDy);
    if (index == 4) return SAMPLE_TEXTURE2D_GRAD(_Layer4, sampler_TrilinearRepeat, uv, uvDx, uvDy);
    if (index == 5) return SAMPLE_TEXTURE2D_GRAD(_Layer5, sampler_TrilinearRepeat, uv, uvDx, uvDy);
    if (index == 6) return SAMPLE_TEXTURE2D_GRAD(_Layer6, sampler_TrilinearRepeat, uv, uvDx, uvDy);
    if (index == 7) return SAMPLE_TEXTURE2D_GRAD(_Layer7, sampler_TrilinearRepeat, uv, uvDx, uvDy);
#if defined(ANGELICA_TERRAIN_WIDE)
    if (index == 8) return SAMPLE_TEXTURE2D_GRAD(_Layer8, sampler_TrilinearRepeat, uv, uvDx, uvDy);
    if (index == 9) return SAMPLE_TEXTURE2D_GRAD(_Layer9, sampler_TrilinearRepeat, uv, uvDx, uvDy);
    if (index == 10) return SAMPLE_TEXTURE2D_GRAD(_Layer10, sampler_TrilinearRepeat, uv, uvDx, uvDy);
    if (index == 11) return SAMPLE_TEXTURE2D_GRAD(_Layer11, sampler_TrilinearRepeat, uv, uvDx, uvDy);
    if (index == 12) return SAMPLE_TEXTURE2D_GRAD(_Layer12, sampler_TrilinearRepeat, uv, uvDx, uvDy);
    if (index == 13) return SAMPLE_TEXTURE2D_GRAD(_Layer13, sampler_TrilinearRepeat, uv, uvDx, uvDy);
    if (index == 14) return SAMPLE_TEXTURE2D_GRAD(_Layer14, sampler_TrilinearRepeat, uv, uvDx, uvDy);
    if (index == 15) return SAMPLE_TEXTURE2D_GRAD(_Layer15, sampler_TrilinearRepeat, uv, uvDx, uvDy);
#endif
    return 0.0;
}

float GetHasAlbedo(int index)
{
    if (index == 0) return _HasAlbedo0;
    if (index == 1) return _HasAlbedo1;
    if (index == 2) return _HasAlbedo2;
    if (index == 3) return _HasAlbedo3;
    if (index == 4) return _HasAlbedo4;
    if (index == 5) return _HasAlbedo5;
    if (index == 6) return _HasAlbedo6;
    if (index == 7) return _HasAlbedo7;
    if (index == 8) return _HasAlbedo8;
    if (index == 9) return _HasAlbedo9;
    if (index == 10) return _HasAlbedo10;
    if (index == 11) return _HasAlbedo11;
    if (index == 12) return _HasAlbedo12;
    if (index == 13) return _HasAlbedo13;
    if (index == 14) return _HasAlbedo14;
    if (index == 15) return _HasAlbedo15;
    return 0.0;
}

float4 GetLayerMapping(int index)
{
    if (index == 0) return _LayerMapping0;
    if (index == 1) return _LayerMapping1;
    if (index == 2) return _LayerMapping2;
    if (index == 3) return _LayerMapping3;
    if (index == 4) return _LayerMapping4;
    if (index == 5) return _LayerMapping5;
    if (index == 6) return _LayerMapping6;
    if (index == 7) return _LayerMapping7;
    if (index == 8) return _LayerMapping8;
    if (index == 9) return _LayerMapping9;
    if (index == 10) return _LayerMapping10;
    if (index == 11) return _LayerMapping11;
    if (index == 12) return _LayerMapping12;
    if (index == 13) return _LayerMapping13;
    if (index == 14) return _LayerMapping14;
    if (index == 15) return _LayerMapping15;
    return float4(1, 1, 1, 0);
}

float3 ProjectLayerNormal(float3 tangentNormal, int axis, float3 geometricNormal)
{
    float3 projected = axis == 0 ? float3(-tangentNormal.z, tangentNormal.y, tangentNormal.x) :
        (axis == 1 ? float3(tangentNormal.x, -tangentNormal.z, tangentNormal.y) : tangentNormal);
    if (dot(projected, geometricNormal) < 0.0) projected = -projected;
    return normalize(projected);
}

float4 GetLayerTextureTransform(int index)
{
    if (index == 0) return _Layer0_ST;
    if (index == 1) return _Layer1_ST;
    if (index == 2) return _Layer2_ST;
    if (index == 3) return _Layer3_ST;
    if (index == 4) return _Layer4_ST;
    if (index == 5) return _Layer5_ST;
    if (index == 6) return _Layer6_ST;
    if (index == 7) return _Layer7_ST;
    if (index == 8) return _Layer8_ST;
    if (index == 9) return _Layer9_ST;
    if (index == 10) return _Layer10_ST;
    if (index == 11) return _Layer11_ST;
    if (index == 12) return _Layer12_ST;
    if (index == 13) return _Layer13_ST;
    if (index == 14) return _Layer14_ST;
    return _Layer15_ST;
}

float SampleLayerMask(int index, float2 uv, float2 uvDx, float2 uvDy)
{
    if (index == 1) return SAMPLE_TEXTURE2D_GRAD(_LayerMask1, sampler_LinearClamp, uv, uvDx, uvDy).r;
    if (index == 2) return SAMPLE_TEXTURE2D_GRAD(_LayerMask2, sampler_LinearClamp, uv, uvDx, uvDy).r;
    if (index == 3) return SAMPLE_TEXTURE2D_GRAD(_LayerMask3, sampler_LinearClamp, uv, uvDx, uvDy).r;
    if (index == 4) return SAMPLE_TEXTURE2D_GRAD(_LayerMask4, sampler_LinearClamp, uv, uvDx, uvDy).r;
    if (index == 5) return SAMPLE_TEXTURE2D_GRAD(_LayerMask5, sampler_LinearClamp, uv, uvDx, uvDy).r;
    if (index == 6) return SAMPLE_TEXTURE2D_GRAD(_LayerMask6, sampler_LinearClamp, uv, uvDx, uvDy).r;
    if (index == 7) return SAMPLE_TEXTURE2D_GRAD(_LayerMask7, sampler_LinearClamp, uv, uvDx, uvDy).r;
#if defined(ANGELICA_TERRAIN_WIDE)
    if (index == 8) return SAMPLE_TEXTURE2D_GRAD(_LayerMask8, sampler_LinearClamp, uv, uvDx, uvDy).r;
    if (index == 9) return SAMPLE_TEXTURE2D_GRAD(_LayerMask9, sampler_LinearClamp, uv, uvDx, uvDy).r;
    if (index == 10) return SAMPLE_TEXTURE2D_GRAD(_LayerMask10, sampler_LinearClamp, uv, uvDx, uvDy).r;
    if (index == 11) return SAMPLE_TEXTURE2D_GRAD(_LayerMask11, sampler_LinearClamp, uv, uvDx, uvDy).r;
    if (index == 12) return SAMPLE_TEXTURE2D_GRAD(_LayerMask12, sampler_LinearClamp, uv, uvDx, uvDy).r;
    if (index == 13) return SAMPLE_TEXTURE2D_GRAD(_LayerMask13, sampler_LinearClamp, uv, uvDx, uvDy).r;
    if (index == 14) return SAMPLE_TEXTURE2D_GRAD(_LayerMask14, sampler_LinearClamp, uv, uvDx, uvDy).r;
    if (index == 15) return SAMPLE_TEXTURE2D_GRAD(_LayerMask15, sampler_LinearClamp, uv, uvDx, uvDy).r;
#endif
    return 0.0;
}

float SampleOverlay(int layer, float2 uv)
{
    uint overlay = (uint)(layer - 1);
    uint splat = overlay >> 2;
    uint channel = overlay & 3;
    float4 mask = splat == 0 ? SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, uv) :
        splat == 1 ? SAMPLE_TEXTURE2D(_Splat1, sampler_Splat0, uv) :
        splat == 2 ? SAMPLE_TEXTURE2D(_Splat2, sampler_Splat0, uv) :
        SAMPLE_TEXTURE2D(_Splat3, sampler_Splat0, uv);
    if (channel == 0) return mask.a;
    if (channel == 1) return mask.r;
    if (channel == 2) return mask.g;
    return mask.b;
}

float2 ProjectLayerUV(float3 position, int axis)
{
    return axis == 0 ? position.zy : (axis == 1 ? position.xz : position.xy);
}

#if defined(ANGELICA_TERRAIN_PARALLAX)
#include "AngelicaTerrainParallax.hlsl"
#endif

struct AngelicaTerrainSurface
{
    float3 albedo;
    float3 normalWS;
    float perceptualSmoothness;
    float3 lightmap;
    bool hasLightmap;
};

AngelicaTerrainSurface EvaluateAngelicaTerrain(float3 positionOS, float2 splatUV, float3 geometricNormal, float3 viewWS)
{
    AngelicaTerrainSurface surface;
    surface.albedo = 0.0;
    surface.normalWS = geometricNormal;
    surface.perceptualSmoothness = 0.0;
    surface.lightmap = 1.0;
    surface.hasLightmap = false;
    float roughness = 1.0;
#if defined(ANGELICA_TERRAIN_PARALLAX)
    float facing = saturate(dot(viewWS, geometricNormal));
    float3 tangentViewOS = TransformWorldToObjectDir(viewWS - geometricNormal * facing, false);
#endif
    float2 maskDx = ddx(splatUV);
    float2 maskDy = ddy(splatUV);
#if defined(ANGELICA_TERRAIN_WIDE)
    const bool directLayers = true;
#else
    bool directLayers = _UseDirectLayers > 0.5;
#endif

    [loop] for (int i = 0; i < min((int)_LayerCount, ANGELICA_TERRAIN_LAYER_LIMIT); i++)
    {
        bool importedLayer = _SourceLayerCount < -0.5 || i < (int)_SourceLayerCount;
        bool packedLayer = importedLayer && !directLayers;
        float4 p = packedLayer ? LOAD_TEXTURE2D(_LayerParams, int2(i, 0)) : GetLayerMapping(i);
        int axis = packedLayer ? (int)round(p.w) : (int)round(p.z);
        float2 uv = ProjectLayerUV(positionOS - float3(0.0, _MinHeight, 0.0), axis);
        int slot = packedLayer ? (int)p.x : i;
        int layerBit = 1 << slot;
        float2 uvScale = packedLayer ? p.yz : p.xy;
        if (((int)_FlipLayers & layerBit) != 0) uvScale.y = -uvScale.y;
        uv *= uvScale;
        if (directLayers)
        {
            float4 textureTransform = GetLayerTextureTransform(slot);
            uv = uv * textureTransform.xy + textureTransform.zw;
#if defined(ANGELICA_TERRAIN_PARALLAX)
            uvScale *= textureTransform.xy;
#endif
        }
        float2 layerDx = ddx(uv);
        float2 layerDy = ddy(uv);
        float4 layer = SampleLayer(slot, uv, layerDx, layerDy);
        float mask = 1.0;
        if (i > 0)
        {
            // A custom layer can opt into a separate mask; its default is albedo alpha.
            if (directLayers && (importedLayer || p.w > 0.5))
                mask = SampleLayerMask(i, splatUV, maskDx, maskDy);
            else if (importedLayer)
                mask = SampleOverlay(i, splatUV);
            else
                mask = GetHasAlbedo(i) > 0.5 ? layer.a : 0.0;
        }
#if defined(ANGELICA_TERRAIN_DETAIL_ARRAY) || defined(ANGELICA_TERRAIN_PARALLAX)
        float slice = GetDetailSlice(i);
#endif
#if defined(ANGELICA_TERRAIN_PARALLAX)
        uint heightChannels = GetHeightChannels(i);
        // Keep mask coverage in the original coordinates, including albedo-alpha masks.
        UNITY_BRANCH if (heightChannels != 0u && slice > 0.0 && mask > 0.0)
        {
            float2 parallaxOffset = EvaluateParallaxOffset(i, slice - 1.0, axis, uvScale, uv,
                layerDx, layerDy, tangentViewOS, facing);
            if (any(parallaxOffset != 0.0))
            {
                uv += parallaxOffset;
                layer = SampleLayer(slot, uv, layerDx, layerDy);
            }
        }
#endif
        surface.albedo = lerp(surface.albedo, layer.rgb, mask);

#if defined(ANGELICA_TERRAIN_DETAIL_ARRAY) || defined(ANGELICA_TERRAIN_PARALLAX)
        if (slice > 0.0 && mask > 0.0)
        {
            float4 detail = SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainDetails, sampler_TrilinearRepeat, uv,
                slice - 1.0, layerDx, layerDy);
            uint channels = (uint)round(detail.a * 3.0);
#if defined(ANGELICA_TERRAIN_PARALLAX)
            if (heightChannels != 0u) channels = heightChannels;
#endif
            float2 strength = GetDetailStrength(i);
            if ((channels & 1u) != 0 && strength.x > 0.0)
            {
                float2 xy = detail.rg * 2.0 - 1.0;
                float3 tangentNormal = float3(xy * strength.x, sqrt(saturate(1.0 - dot(xy, xy))));
                float3 layerNormal = ProjectLayerNormal(tangentNormal, axis, geometricNormal);
                surface.normalWS = normalize(lerp(surface.normalWS, layerNormal, mask));
            }
            if ((channels & 2u) != 0 && strength.y > 0.0)
            {
                float layerRoughness = lerp(1.0, detail.b, saturate(strength.y));
                roughness = lerp(roughness, layerRoughness, mask);
            }
        }
#else
        // Keep the original direct base-layer path for materials without overlay details.
        if (i == 0)
        {
            float normalStrength = saturate(_NormalStrength0 * 0.5) * _HasNormal0;
            if (normalStrength > 0.0)
            {
                float4 normalSample = SAMPLE_TEXTURE2D_GRAD(_Normal0, sampler_TrilinearRepeat, uv, layerDx, layerDy);
                float3 tangentNormal = UnpackNormalScale(normalSample, normalStrength * 2.0);
                surface.normalWS = ProjectLayerNormal(tangentNormal, axis, geometricNormal);
            }

            float roughnessStrength = saturate(_RoughnessStrength0) * _HasRoughness0;
            if (roughnessStrength > 0.0)
            {
                float roughnessSample = SAMPLE_TEXTURE2D_GRAD(_Roughness0, sampler_TrilinearRepeat, uv, layerDx, layerDy).r;
                roughness = lerp(1.0, saturate(roughnessSample), roughnessStrength);
            }
        }
#endif
    }

    surface.perceptualSmoothness = 1.0 - saturate(roughness);
    surface.albedo = saturate(surface.albedo * _Brightness);
    if (_UseLightmap > 0.5)
    {
        float3 light = SAMPLE_TEXTURE2D(_Lightmap, sampler_Lightmap, splatUV).rgb;
        if (_UseNightLightmap > 0.5)
            light = lerp(light, SAMPLE_TEXTURE2D(_NightLightmap, sampler_Lightmap, splatUV).rgb, saturate(_MapDayNight));
        surface.lightmap = light * 2.0;
        surface.hasLightmap = true;
    }
    return surface;
}

float4 ConstructAngelicaTangent(float3 normalOS)
{
    float3 helper = abs(normalOS.y) < 0.999 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
    return float4(normalize(cross(helper, normalOS)), 1.0);
}

AttributesMesh ApplyMeshModification(AttributesMesh input, float3 timeParameters)
{
#ifdef ATTRIBUTES_NEED_TANGENT
    float3 normalOS = float3(0.0, 1.0, 0.0);
#ifdef ATTRIBUTES_NEED_NORMAL
    normalOS = input.normalOS;
#endif
    input.tangentOS = ConstructAngelicaTangent(normalOS);
#endif
    return input;
}

void GetSurfaceAndBuiltinData(FragInputs input, float3 V, inout PositionInputs posInput, out SurfaceData surfaceData, out BuiltinData builtinData RAY_TRACING_OPTIONAL_PARAMETERS)
{
    float3 geometricNormal = normalize(input.tangentToWorld[2]);
    // positionRWS and the world-to-object matrix are both camera-relative in HDRP.
    // Adding the camera position first makes layer UVs slide when the view moves.
    float3 positionOS = TransformWorldToObject(input.positionRWS);
    AngelicaTerrainSurface terrain = EvaluateAngelicaTerrain(positionOS, input.texCoord0.xy, geometricNormal, V);

    ZERO_INITIALIZE(SurfaceData, surfaceData);
    surfaceData.materialFeatures = MATERIALFEATUREFLAGS_LIT_STANDARD;
    surfaceData.baseColor = terrain.albedo;
    surfaceData.normalWS = terrain.normalWS;
    surfaceData.geomNormalWS = geometricNormal;
    surfaceData.tangentWS = Orthonormalize(input.tangentToWorld[0], terrain.normalWS);
    surfaceData.perceptualSmoothness = terrain.perceptualSmoothness;
    surfaceData.ambientOcclusion = 1.0;
    surfaceData.specularOcclusion = 1.0;
    surfaceData.metallic = 0.0;
    surfaceData.ior = 1.5;
    surfaceData.specularColor = 0.0;
    surfaceData.transmittanceColor = 1.0;
    surfaceData.atDistance = 1.0;

#if HAVE_DECALS
    if (_EnableDecals)
    {
        float decalAlpha = 1.0;
        DecalSurfaceData decalSurfaceData = GetDecalSurfaceData(posInput, input, decalAlpha);
    #if defined(DECAL_SURFACE_GRADIENT)
        float3 surfaceGradient = SurfaceGradientFromPerturbedNormal(geometricNormal, surfaceData.normalWS);
        ApplyDecalToSurfaceData(decalSurfaceData, geometricNormal, surfaceData, surfaceGradient);
        surfaceData.normalWS = SurfaceGradientResolveNormal(geometricNormal, surfaceGradient);
    #else
        ApplyDecalToSurfaceData(decalSurfaceData, geometricNormal, surfaceData);
    #endif
    }
#endif

#if defined(DEBUG_DISPLAY)
    ApplyDebugToSurfaceData(input.tangentToWorld, surfaceData);
#endif

    float4 lightmapUv = 0.0;
    float4 uv2 = 0.0;
#ifdef FRAG_INPUTS_USE_TEXCOORD1
    lightmapUv = input.texCoord1;
#endif
#ifdef FRAG_INPUTS_USE_TEXCOORD2
    uv2 = input.texCoord2;
#endif
    InitBuiltinData(posInput, 1.0, surfaceData.normalWS, -geometricNormal, lightmapUv, uv2, builtinData);
    if (terrain.hasLightmap)
    {
        builtinData.bakeDiffuseLighting = terrain.lightmap;
        builtinData.backBakeDiffuseLighting = terrain.lightmap;
    }
    builtinData.emissiveColor = 0.0;
    PostInitBuiltinData(V, posInput, surfaceData, builtinData);
}

#endif
