Shader "Forsaken World/Angelica LitModel"
{
    Properties
    {
        _Albedo ("Albedo", 2D) = "white" {}
        _AlphaRef ("Alpha reference", Range(0, 1)) = 0.3294
        _Brightness ("Brightness", Range(0, 4)) = 1
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
            TEXTURE2D(_Albedo); SAMPLER(sampler_Albedo);
            CBUFFER_START(UnityPerMaterial)
                float4 _Albedo_ST;
                float _AlphaRef;
                float _Brightness;
            CBUFFER_END
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
                output.uv = TRANSFORM_TEX(input.uv, _Albedo);
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Godot litmodel: COLOR = vertex day color * texture * 2 * brightness (encoded RGB),
                // ALPHA = texture alpha with ALPHA_SCISSOR_THRESHOLD = 84/255.
                float4 tex = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv);
                float3 color = saturate(tex.rgb * input.color.rgb * 2.0 * _Brightness);
                #ifndef UNITY_COLORSPACE_GAMMA
                    color = SRGBToLinear(color);
                #endif
                clip(tex.a - _AlphaRef);
                return half4(color, tex.a);
            }
            ENDHLSL
        }
    }
}
