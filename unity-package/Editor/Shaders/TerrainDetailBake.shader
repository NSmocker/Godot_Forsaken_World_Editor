Shader "Hidden/Forsaken World/Terrain Detail Bake"
{
    Properties
    {
        _NormalSource ("Normal source", 2D) = "bump" {}
        _RoughnessSource ("Roughness source", 2D) = "white" {}
        _HeightSource ("Height source", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _NormalSource;
            sampler2D _RoughnessSource;
            sampler2D _HeightSource;
            float4 _Presence;

            float4 Frag(v2f_img input) : SV_Target
            {
                float2 normal = _Presence.x > 0 ? UnpackNormal(tex2D(_NormalSource, input.uv)).xy : 0;
                float roughness = _Presence.y > 0 ? tex2D(_RoughnessSource, input.uv).r : 1;
                float alpha = _Presence.z > 0 ? tex2D(_HeightSource, input.uv).r : (_Presence.x + 2 * _Presence.y) / 3.0;
                return float4(normal * 0.5 + 0.5, roughness, alpha);
            }
            ENDCG
        }
    }
    FallBack Off
}
