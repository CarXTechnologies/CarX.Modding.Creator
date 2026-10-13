#include "UnityCG.cginc"

sampler2D _MainTex;
float4 _MainTex_ST;

float _NormalScale;
// Проходы альфы: x - remap min, y - remap max, z - множитель альфы до ремапа (HDRP Lit: lerp(min, max, a * baseColor.a)),
// w - порог Alpha Clipping HDRP (только проход альфа-теста)
float4 _AlphaRemap;

struct appdata
{
	float4 vertex : POSITION;
	float2 uv : TEXCOORD0;
};

struct v2f
{
	float2 uv : TEXCOORD0;
	float4 vertex : SV_POSITION;
};

v2f vert(appdata v)
{
	v2f o;
	o.vertex = UnityObjectToClipPos(v.vertex);
	o.uv = TRANSFORM_TEX(v.uv, _MainTex);
	return o;
}

fixed4 CopyFirstChannel(v2f i) : SV_Target
{
	fixed4 col = fixed4(tex2D(_MainTex, i.uv).r, tex2D(_MainTex, i.uv).r, tex2D(_MainTex, i.uv).r, 1.0);
	return col;
}

fixed4 CopyFourthChannel(v2f i) : SV_Target
{
	float alpha = lerp(_AlphaRemap.x, _AlphaRemap.y, tex2D(_MainTex, i.uv).a * _AlphaRemap.z);
	return fixed4(alpha, alpha, alpha, 1.0);
}

// Альфа-тест rendercore оставляет texel при B - dither * 0.45 - 0.15 > 0 (полоса дизеринга 0.15..0.6), HDRP режет жёстко по порогу.
// Порог HDRP переносится в середину полосы (покрытие 50%), порог -/+ 0.05 - на её края. Та же формула - RenderCoreAlphaClip.ToRenderCore.
fixed4 CopyFourthChannelAlphaTest(v2f i) : SV_Target
{
	float alpha = lerp(_AlphaRemap.x, _AlphaRemap.y, tex2D(_MainTex, i.uv).a * _AlphaRemap.z);
	// RenderCoreAlphaClip.ToRenderCore: окно min(0.05, порог, 1 - порог), у порога почти 0 или 1 - жёсткий шаг.
	float window = min(_AlphaRemap.w, 1.0 - _AlphaRemap.w);
	alpha = window <= 0.5 / 255.0 ? step(_AlphaRemap.w, alpha) : saturate(0.375 + (alpha - _AlphaRemap.w) * (0.225 / min(window, 0.05)));
	return fixed4(alpha, alpha, alpha, 1.0);
}

fixed4 CopyFourthInvertedChannel(v2f i) : SV_Target
{
	fixed4 col = fixed4(1.0 - tex2D(_MainTex, i.uv).a, 1.0 - tex2D(_MainTex, i.uv).a, 1.0 - tex2D(_MainTex, i.uv).a, 1.0);
	return col;
}

// r*a handles both plain RGB(A) normals (a=1) and DXT5nm-swizzled ones (r=1, x in a); z is always rebuilt
fixed4 UnpackNormalMap(v2f i) : SV_Target
{
	fixed4 col = tex2D(_MainTex, i.uv);
	float3 n;
	n.xy = (float2(col.r * col.a, col.g) * 2.0 - 1.0) * _NormalScale;
	n.z = sqrt(saturate(1.0 - dot(n.xy, n.xy)));
	return fixed4(n * 0.5 + 0.5, 1.0);
}