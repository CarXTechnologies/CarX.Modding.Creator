Shader "Hidden/Modding/ExportVatNormal"
{
    Properties { _MainTex ("Normal map", 2D) = "bump" {} }
    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 frag(v2f_img i) : SV_Target
            {
                return float4(UnpackNormal(tex2D(_MainTex, i.uv)) * 0.5 + 0.5, 1);
            }
            ENDCG
        }
    }
}
