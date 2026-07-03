Shader "Hidden/ConvertingEx"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM

            #include "ConvertingEx.hlsl"

            #pragma vertex vert
            #pragma fragment CopyFirstChannel

            ENDCG
        }

        Pass
        {
            CGPROGRAM

            #include "ConvertingEx.hlsl"

            #pragma vertex vert
            #pragma fragment CopyFourthChannel

            ENDCG
        }

        Pass
        {
            CGPROGRAM

            #include "ConvertingEx.hlsl"

            #pragma vertex vert
            #pragma fragment CopyFourthInvertedChannel

            ENDCG
        }
    }
}