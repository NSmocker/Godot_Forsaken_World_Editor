Shader "Forsaken World/Angelica LitModel"
{
    Properties
    {
        _Albedo ("Albedo", 2D) = "white" {}
        _AlphaRef ("Alpha reference", Range(0, 1)) = 0.3294
        _Brightness ("Brightness (1 = original)", Range(0, 4)) = 1
        _DayNight ("Day / Night (0 = day, 1 = night)", Range(0, 1)) = 0
        [ToggleUI] _UseMapDayNight ("Follow map day/night", Float) = 1
        [HideInInspector] _MapDayNight ("Map day/night override", Float) = -1
        [HideInInspector] _HasDayNightColors ("Has baked day/night colors", Float) = 0
        [HideInInspector] _AlbedoIsSRGB ("Albedo uses sRGB sampling", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags
        {
            "RenderType"="TransparentCutout"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="AlphaTest"
        }
        Pass
        {
            Name "LitModel"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
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
                // Original LitModel: baked vertex lighting * texture (MODULATE2X).
                float4 tex = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv);
                float3 color = AngelicaLitModelModulate(tex.rgb, input.color.rgb, _Brightness, _AlbedoIsSRGB);
                clip(tex.a - _AlphaRef);
                return half4(color, tex.a);
            }
            ENDHLSL
        }
    }
}
