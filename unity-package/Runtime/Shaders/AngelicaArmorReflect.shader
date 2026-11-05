Shader "Forsaken World/Angelica Armor Reflect"
{
    // Godot angelica_model_builder.gd REFLECT_SHADER / armor.sdr:
    // ALBEDO = albedo.rgb * (base.rgb + reflection * base.a);
    // reflection sampled with camera-space normal → sphere map UV
    // (normalize(NORMAL).xy * vec2(0.5, -0.5) + 0.5). Texture alpha masks gloss/reflect.
    Properties
    {
        _AlbedoColor ("Albedo Color", Color) = (1,1,1,1)
        _Albedo ("Albedo", 2D) = "white" {}
        _ReflectMap ("Reflect Map", 2D) = "black" {}
        _Specular ("Specular", Range(0, 1)) = 0
        _EmissionColor ("Emission", Color) = (0,0,0,1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.3294
        [Enum(Opaque,0,Transparent,1)] _Surface ("Surface", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
        }
        Pass
        {
            Name "ArmorReflect"
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
            TEXTURE2D(_ReflectMap); SAMPLER(sampler_ReflectMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _AlbedoColor;
                float4 _Albedo_ST;
                float4 _EmissionColor;
                float _Specular;
                float _AlphaClip;
                float _Cutoff;
                float _Surface;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _Albedo);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float4 base = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv);
                // Camera-space normal for sphere-map UV (Godot NORMAL in view space).
                float3 normalVS = mul((float3x3)UNITY_MATRIX_V, normalize(input.normalWS));
                float2 reflectUv = normalize(normalVS).xy * float2(0.5, -0.5) + 0.5;
                float3 reflection = SAMPLE_TEXTURE2D(_ReflectMap, sampler_ReflectMap, reflectUv).rgb;
                float3 color = _AlbedoColor.rgb * (base.rgb + reflection * base.a);
                #ifndef UNITY_COLORSPACE_GAMMA
                    color = SRGBToLinear(color);
                #endif
                // Specular * base.a is stored for docs; unlit pass adds a soft highlight term.
                color += reflection * (_Specular * base.a) * 0.25;
                color += _EmissionColor.rgb;
                if (_AlphaClip > 0.5)
                    clip(base.a - _Cutoff);
                float alpha = _Surface > 0.5 ? base.a * _AlbedoColor.a : 1.0;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
