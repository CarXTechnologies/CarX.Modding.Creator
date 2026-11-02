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
            // _AlphaScale - MTL d (dissolve), multiplies map_d alpha
            float _RoughnessScale, _MetalnessScale, _AlphaScale;
            fixed4 frag(v2f_img i) : SV_Target
            {
                return fixed4(tex2D(_RoughnessTex, i.uv).r * _RoughnessScale,
                    tex2D(_MetalnessTex, i.uv).r * _MetalnessScale, tex2D(_AlphaTex, i.uv).r * _AlphaScale, 1);
            }
            ENDCG
        }
    }
}
