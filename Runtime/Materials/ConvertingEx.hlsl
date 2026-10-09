#include "UnityCG.cginc"

sampler2D _MainTex;
float4 _MainTex_ST;

float _NormalScale;
// Alpha pass: x - remap min, y - remap max, z - alpha multiplier before remap (HDRP Lit: lerp(min, max, a * baseColor.a))
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