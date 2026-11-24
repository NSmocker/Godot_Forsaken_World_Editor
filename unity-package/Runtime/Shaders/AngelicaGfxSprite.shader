Shader "Forsaken World/Angelica Gfx Sprite"
{
    // URP CPU-sprite port of create_id_100/101.gd (Sprite3D + generated spatial shader).
    // Blend DestBlend 2/4 → additive; else SrcAlpha OneMinusSrcAlpha (mix).
    // Warp:1 → heat/lens distortion via _CameraOpaqueTexture (Godot hint_screen_texture).
    // Host URP asset must enable Opaque Texture (or Screen Space Opaque) for Warp.
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _UvRect ("UV scale.xy offset.zw", Vector) = (1, 1, 0, 0)
        _UvExchange ("Exchange UV axes", Float) = 0
        _UseVertexColor ("Particle vertex color", Float) = 0
        _GfxAlpha ("GFX alpha", Range(0, 1)) = 1
        _Warp ("Warp heat distortion", Float) = 0
        [HideInInspector] _WarpNormalIsSRGB ("Warp normal uses sRGB import", Float) = 0
        [HideInInspector] _GfxWarpDecal ("Screen Warp decal", Vector) = (0, 0, 0, 0)
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
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D_X(_AngelicaGfxWarpSource);
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
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 warpStrength : TEXCOORD1;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 warpStrength : TEXCOORD1;
                half4 color : COLOR;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionCS.xy += _GfxWarpDecal.z * output.positionCS.w *
                    float2(1.0, -_ProjectionParams.x) / GetScaledScreenParams().xy;
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
                return (input.positionCS.xy - 0.5 * direction) / GetScaledScreenParams().xy + offset * direction;
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
                    float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
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
                clip(_Warp - 0.5);
                float4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float2 uv = WarpSourceUv(input, tex);
                return float4(SAMPLE_TEXTURE2D_X(_AngelicaGfxWarpSource, sampler_LinearClamp,
                    UnityStereoTransformScreenSpaceTex(uv)).rgb, 1.0);
            }
        ENDHLSL
        Pass
        {
            Name "GfxSprite"
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
        Pass
        {
            Name "GfxSpriteEncoded"
            Tags { "LightMode"="AngelicaGfxEncoded" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment FragEncoded
            ENDHLSL
        }
        Pass
        {
            Name "GfxParticleWarp"
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
