Shader "Forsaken World/Angelica Water"
{
    // URP port of gfx_importer/angelica_water.gdshader (ps\water_simple.txt style).
    Properties
    {
        _NormalMap ("Normal map", 2D) = "bump" {}
        _DeepColor ("Deep color", Color) = (0, 0.25, 0.35, 1)
        _SurfaceColor ("Surface color", Color) = (0.2, 0.65, 0.65, 1)
        _AlphaFacing ("Alpha facing", Range(0,1)) = 0.8
        _AlphaGrazing ("Alpha grazing", Range(0,1)) = 1
        _RippleScale ("Ripple scale", Float) = 0.04
        _RippleScale2 ("Ripple scale 2", Float) = 1.7
        _RippleSpeed ("Ripple speed", Vector) = (0.012, 0.007, -0.009, 0.011)
        _RippleStrength ("Ripple strength", Range(0,1)) = 0.6
        _SunDirection ("Sun direction", Vector) = (-0.4, -0.8, -0.45, 0)
        _SpecularColor ("Specular color", Color) = (1, 0.95, 0.85, 1)
        _SpecularPower ("Specular power", Float) = 96
        _SpecularStrength ("Specular strength", Range(0,2)) = 0.8
        _Brightness ("Brightness", Range(0,4)) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Water"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor, _SurfaceColor, _SpecularColor, _RippleSpeed, _SunDirection;
                float _AlphaFacing, _AlphaGrazing, _RippleScale, _RippleScale2;
                float _RippleStrength, _SpecularPower, _SpecularStrength, _Brightness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 worldPos : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.worldPos = world;
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.worldPos.xz * _RippleScale;
                float3 n1 = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv + _Time.y * _RippleSpeed.xy).rgb * 2.0 - 1.0;
                float3 n2 = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv * _RippleScale2 + _Time.y * _RippleSpeed.zw).rgb * 2.0 - 1.0;
                float3 n = normalize(lerp(float3(0,1,0), normalize(n1 + n2), _RippleStrength));
                float3 view = normalize(GetCameraPositionWS() - input.worldPos);
                if (view.y < 0.0) n.y = -n.y;
                float c = saturate(dot(view, n));
                float f4 = pow(1.0 - c, 4.0);
                float f5 = f4 * (1.0 - c);
                float3 col = lerp(_DeepColor.rgb, _SurfaceColor.rgb, f4);
                float3 r = reflect(-view, n);
                col += _SpecularColor.rgb * pow(max(dot(r, -normalize(_SunDirection.xyz)), 0.0), _SpecularPower) * _SpecularStrength;
                col = saturate(col * _Brightness);
                float alpha = lerp(_AlphaFacing, _AlphaGrazing, f5);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
