Shader "Hidden/CarX Modding/PreparePackedMap"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #include "UnityCG.cginc"
            #pragma vertex vert_img
            #pragma fragment frag
            sampler2D _RoughnessTex, _MetalnessTex, _AlphaTex;
            float _RoughnessScale, _MetalnessScale;
            fixed4 frag(v2f_img i) : SV_Target
            {
                return fixed4(tex2D(_RoughnessTex, i.uv).r * _RoughnessScale,
                    tex2D(_MetalnessTex, i.uv).r * _MetalnessScale, tex2D(_AlphaTex, i.uv).r, 1);
            }
            ENDCG
        }
    }
}
