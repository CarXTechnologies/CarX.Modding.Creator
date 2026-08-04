#include "UnityCG.cginc"

sampler2D _MainTex;
float4 _MainTex_ST;

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
	fixed4 col = fixed4(tex2D(_MainTex, i.uv).a, tex2D(_MainTex, i.uv).a, tex2D(_MainTex, i.uv).a, 1.0);
	return col;
}

fixed4 CopyFourthInvertedChannel(v2f i) : SV_Target
{
	fixed4 col = fixed4(1.0 - tex2D(_MainTex, i.uv).a, 1.0 - tex2D(_MainTex, i.uv).a, 1.0 - tex2D(_MainTex, i.uv).a, 1.0);
	return col;
}