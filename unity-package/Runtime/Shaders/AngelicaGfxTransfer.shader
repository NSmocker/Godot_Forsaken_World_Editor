Shader "Hidden/Forsaken World/Angelica Gfx Transfer"
{
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

        float4 Encode(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord);
            #ifndef UNITY_COLORSPACE_GAMMA
                color.rgb = LinearToSRGB(color.rgb);
            #endif
            return color;
        }

        float4 Decode(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord);
            #ifndef UNITY_COLORSPACE_GAMMA
                color.rgb = SRGBToLinear(color.rgb);
            #endif
            return color;
        }
        ENDHLSL

        Pass
        {
            Name "Encode"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Encode
            ENDHLSL
        }

        Pass
        {
            Name "Decode"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Decode
            ENDHLSL
        }
    }
    FallBack Off
}
