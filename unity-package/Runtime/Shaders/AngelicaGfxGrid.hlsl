#ifndef ANGELICA_GFX_GRID_INCLUDED
#define ANGELICA_GFX_GRID_INCLUDED

TEXTURE2D(_GfxDissolveMask);

float4 AngelicaSampleGridTexture(float2 uv)
{
    if (_GfxGridTextureMode < 0.5)
        return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);

    // Warp runs after the render slots restore stage zero to wrap.
    if (_GfxGridTextureMode > 1.5 || _Warp > 0.5)
        return SAMPLE_TEXTURE2D(_BaseMap, sampler_TrilinearRepeat, uv);

    return SAMPLE_TEXTURE2D(_BaseMap, sampler_TrilinearClamp, uv);
}

float AngelicaDissolveAlpha(float2 uv, float alpha)
{
    if (_GfxDissolve <= 0.5)
        return alpha;

    float mask = SAMPLE_TEXTURE2D(_GfxDissolveMask, sampler_TrilinearRepeat, uv).r;
    #ifndef UNITY_COLORSPACE_GAMMA
        if (_GfxDissolveMaskIsSRGB > 0.5)
            mask = LinearToSRGB(mask);
    #endif
    // dissolve_edgealpha.txt: cmp rejects the closed side of the threshold.
    float delta = mask - _GfxDissolveParameters.x;
    if (delta <= 0.0)
        return 0.0;
    return delta <= _GfxDissolveParameters.y ? saturate(delta * 8.0) : alpha;
}

#endif
