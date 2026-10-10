// ffu23 REAL ROOM plush frog toy: fleece. Albedo = vertex colour (sRGB), fuzz = fine triplanar noise from the skin
// detail texture, wrap diffuse from the moving-object SH probe, velvet sheen at grazing angles (fibres catch the light
// at the silhouette), no mirror reflections. Stylised before the "turning real" wave reaches it.
Shader "FF/RRPlush"
{
    Properties { _FuzzTex ("Fuzz (skin detail R)", 2D) = "gray" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="Always" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "RRCommon.cginc"
            sampler2D _FuzzTex;
            struct a2v { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float3 wpos : TEXCOORD0; float3 nrm : TEXCOORD1; float3 o : TEXCOORD2; float4 col : TEXCOORD3; float3 on : TEXCOORD4; };
            v2f vert (a2v v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.nrm = UnityObjectToWorldNormal(v.normal); o.o = v.vertex.xyz; o.on = v.normal; o.col = v.color; return o;
            }
            float Tri(float3 p, float3 n)
            {
                float3 w = pow(abs(n), 4.0); w /= (w.x + w.y + w.z);
                return tex2D(_FuzzTex, p.yz).r * w.x + tex2D(_FuzzTex, p.xz).r * w.y + tex2D(_FuzzTex, p.xy).r * w.z;
            }
            float4 frag (v2f i) : SV_Target
            {
                float3 n0 = normalize(i.on);
                float fz = Tri(i.o * 38.0, n0) * 0.6 + Tri(i.o * 97.0 + 0.3, n0) * 0.4;
                float3 alb = SrgbToLin(i.col.rgb) * (0.80 + 0.36 * fz);
                float3 N = normalize(i.nrm), V = normalize(_WorldSpaceCameraPos - i.wpos);
                float3 irr = (SHIrrD(N) * 0.65 + SHIrrD(normalize(N + float3(0, 0.7, 0))) * 0.35) * 1.5;
                float nv = saturate(dot(N, V));
                float3 sheen = irr * pow(1.0 - nv, 2.5) * 0.45 * lerp(float3(1, 1, 1), alb * 2.2, 0.5);
                float3 col = alb * irr * (0.9 + 0.1 * nv) + sheen;
                float w = Realness(i.wpos);
                if (w < 0.999) col = lerp(Stylise(SrgbToLin(i.col.rgb), SHIrrD(N) * 1.5), col, w);
                col += _RRWaveCol.rgb * WaveEdge(i.wpos) / max(_RRExposure, 0.05);
                return RROut(col);
            }
            ENDCG
        }
    }
    Fallback Off
}
