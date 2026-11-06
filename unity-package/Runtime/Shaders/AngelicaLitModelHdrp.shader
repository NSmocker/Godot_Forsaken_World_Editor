Shader "Forsaken World/HDRP/Angelica LitModel"
{
    // HDRP twin of Forsaken World/Angelica LitModel.
    // vertex day color * texture * 2 * brightness, alpha clip 84/255.
    Properties
    {
        _Albedo ("Albedo", 2D) = "white" {}
        _AlphaRef ("Alpha reference", Range(0, 1)) = 0.3294
        _Brightness ("Brightness", Range(0, 4)) = 2
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags
        {
            "RenderType"="TransparentCutout"
            "RenderPipeline"="HDRenderPipeline"
            "Queue"="AlphaTest"
        }
        Pass
        {
            Name "DepthForwardOnly"
            Tags { "LightMode"="DepthForwardOnly" }
            Cull [_Cull]
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #define ANGELICA_DEPTH_ONLY 1
            #include "AngelicaLitModelHdrp.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="ForwardOnly" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "AngelicaLitModelHdrp.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
