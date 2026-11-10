#ifndef ANGELICA_LITMODEL_LIGHTING_INCLUDED
#define ANGELICA_LITMODEL_LIGHTING_INCLUDED

// A3DLitMesh::UpdateColors blends encoded RGB bytes before raster interpolation.
// Match its 8-bit weights and >> 8, including the original 255/256 endpoint scale.
float3 AngelicaLitModelVertexColor(float3 day, float3 night, float hasNightColors, float nightFactor)
{
    // Old imported meshes and grass have no night stream. Keep their existing color.
    if (hasNightColors < 0.5)
        return day;
    float n = floor(saturate(nightFactor) * 255.0);
    float3 dayBytes = floor(saturate(day) * 255.0 + 0.5);
    float3 nightBytes = floor(saturate(night) * 255.0 + 0.5);
    return floor((dayBytes * (255.0 - n) + nightBytes * n) / 256.0) / 255.0;
}

float3 AngelicaLitModelModulate(float3 albedo, float3 lighting, float brightness, float albedoIsSRGB)
{
#ifndef UNITY_COLORSPACE_GAMMA
    // Unity decodes sRGB textures on sampling. Angelica's fixed-function MODULATE2X
    // operated on encoded values, so restore them before the multiply and clamp.
    if (albedoIsSRGB > 0.5)
        albedo = LinearToSRGB(albedo);
#endif
    float3 color = saturate(albedo * lighting * 2.0 * brightness);
#ifndef UNITY_COLORSPACE_GAMMA
    color = SRGBToLinear(color);
#endif
    return color;
}
#endif
