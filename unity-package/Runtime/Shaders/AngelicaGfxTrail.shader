Shader "Forsaken World/Angelica Gfx Trail"
{
    // URP CPU-trail port for GFX ID 110/180 (+ rings/lightning Mesh via same shader).
    // Warp:1 → heat/lens via _CameraOpaqueTexture (Godot create_id_140 screen_texture).
    // Host URP asset must enable Opaque Texture for Warp. ID 110 Godot fragment ignores Warp.
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _GfxAlpha ("GFX alpha", Range(0, 1)) = 1
        _Warp ("Warp heat distortion", Float) = 0
        [HideInInspector] _WarpNormalIsSRGB ("Normal texture is sRGB", Float) = 0
        [HideInInspector] _GfxGridRaster ("D3D9 pixel center", Float) = 0
        [HideInInspector] _GfxGridTextureMode ("Grid texture address mode", Float) = 0
        [HideInInspector] _GfxDissolve ("Dissolve", Float) = 0
        [HideInInspector] _GfxDissolveBaseIsRaw ("Dissolve base stores encoded samples", Float) = 0
        [HideInInspector] _GfxDissolveMask ("Dissolve mask", 2D) = "white" {}
        [HideInInspector] _GfxDissolveMaskIsSRGB ("Dissolve mask is sRGB", Float) = 0
        [HideInInspector] _GfxDissolveParameters ("Dissolve threshold and edge", Vector) = (0, 0, 0, 0)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        _ZWrite ("ZWrite", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }
        Cull Off
        ZWrite [_ZWrite]
        ZTest [_ZTest]
        Blend [_SrcBlend] [_DstBlend]
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D_X(_AngelicaGfxWarpSource);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _GfxAlpha;
                float _Warp;
                float _WarpNormalIsSRGB;
                float _GfxGridRaster;
                float _GfxGridTextureMode;
                float _GfxDissolve;
                float _GfxDissolveBaseIsRaw;
                float _GfxDissolveMaskIsSRGB;
                float4 _GfxDissolveParameters;
            CBUFFER_END
            #include "AngelicaGfxGrid.hlsl"
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
                // Translate D3D9 integer pixel centers to the modern viewport.
                output.positionCS.xy += _GfxGridRaster * output.positionCS.w *
                    float2(1.0, -_ProjectionParams.x) / GetScaledScreenParams().xy;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = AngelicaSampleGridTexture(input.uv);
                if (_Warp > 0.5h)
                {
                    half3 normalMap = tex.rgb * 2.0h - 1.0h;
                    normalMap = lerp(half3(0.0h, 0.0h, 1.0h), normalMap, tex.a);
                    normalMap = normalize(normalMap);
                    half2 warpDir = normalMap.xy / max(normalMap.z, 0.1h);
                    half strength = input.color.a * _GfxAlpha * _BaseColor.a;
                    half2 offset = warpDir * 0.50h * strength;
                    float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    half3 distorted = SampleSceneColor(screenUV + offset);
                    return half4(distorted, tex.a);
                }
                #ifndef UNITY_COLORSPACE_GAMMA
                    if (_GfxDissolveBaseIsRaw > 0.5) tex.rgb = SRGBToLinear(tex.rgb);
                #endif
                half4 col = tex * _BaseColor * input.color;
                col.a = AngelicaDissolveAlpha(input.uv, col.a * _GfxAlpha);
                return col;
            }
            float4 FragEncoded(Varyings input) : SV_Target
            {
                clip(0.5 - _Warp);
                float4 tex = AngelicaSampleGridTexture(input.uv);
                float4 tint = _BaseColor;
                #ifndef UNITY_COLORSPACE_GAMMA
                    if (_GfxDissolveBaseIsRaw < 0.5) tex.rgb = LinearToSRGB(tex.rgb);
                    tint.rgb = LinearToSRGB(tint.rgb);
                #endif
                float4 col = tex * tint * input.color;
                col.a = AngelicaDissolveAlpha(input.uv, col.a * _GfxAlpha);
                return col;
            }
            float4 FragWarp(Varyings input) : SV_Target
            {
                clip(_Warp - 0.5);
                clip(input.color.a);
                float4 tex = AngelicaSampleGridTexture(input.uv);
                clip(tex.a - 0.05);
                #ifndef UNITY_COLORSPACE_GAMMA
                    if (_WarpNormalIsSRGB > 0.5) tex.rgb = LinearToSRGB(tex.rgb);
                #endif
                float2 offset = (tex.rg * 2.0 - 1.0) * float2(-1.0, 1.0) * input.color.a * tex.a;
                // Convert the D3D9 offset and integer pixel center to the copied attachment.
                float2 direction = float2(1.0, _ProjectionParams.x);
                float2 uv = (input.positionCS.xy - 0.5 * direction) /
                    GetScaledScreenParams().xy + offset * direction;
                return float4(SAMPLE_TEXTURE2D_X(_AngelicaGfxWarpSource, sampler_LinearClamp,
                    UnityStereoTransformScreenSpaceTex(uv)).rgb, 1.0);
            }
        ENDHLSL
        Pass
        {
            Name "GfxTrail"
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
        Pass
        {
            Name "GfxTrailEncoded"
            Tags { "LightMode"="AngelicaGfxEncoded" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragEncoded
            ENDHLSL
        }
        Pass
        {
            Name "GfxGridWarp"
            Tags { "LightMode"="AngelicaGfxWarp" }
            Blend One Zero
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragWarp
            ENDHLSL
        }
    }
    FallBack Off
}
