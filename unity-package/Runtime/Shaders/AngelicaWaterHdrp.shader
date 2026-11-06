Shader "Forsaken World/HDRP/Angelica Water"
{
    // HDRP twin of Forsaken World/Angelica Water (ps\water_simple style).
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
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="HDRenderPipeline"
        }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="ForwardOnly" }
            Cull Off
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "AngelicaWaterHdrp.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
