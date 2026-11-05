Shader "Forsaken World/Angelica Gfx Sprite"
{
    // URP CPU-sprite port of create_id_100/101.gd (Sprite3D + generated spatial shader).
    // Blend DestBlend 2/4 → additive; else SrcAlpha OneMinusSrcAlpha (mix). Warp screen grab not ported.
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _UvRect ("UV scale.xy offset.zw", Vector) = (1, 1, 0, 0)
        _GfxAlpha ("GFX alpha", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        _ZWrite ("ZWrite", Float) = 0
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
        Pass
        {
            Name "GfxSprite"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _UvRect;
                float _GfxAlpha;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float2 uv = input.uv * _UvRect.xy + _UvRect.zw;
                output.uv = uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 col = tex * _BaseColor;
                col.a *= _GfxAlpha;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
