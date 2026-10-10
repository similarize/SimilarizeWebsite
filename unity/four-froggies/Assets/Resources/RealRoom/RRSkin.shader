// REAL ROOM frog hands: wet amphibian skin. Detail (mottling, spots, warts) is triplanar in the hand's REST pose
// (uv2 = rest position, uv3 = rest normal) so it sticks to the skin while the fingers bend. Lighting: SH probe with a
// soft subsurface wrap + reddish scatter, wet GGX highlights from the ceiling lamp and the TV, box-projected room
// reflections (Fresnel). _Morph 0 -> 1 turns the cartoon hand into the real one (dissolve from the fingertips with a
// glowing edge).
Shader "FF/RRSkin"
{
    Properties
    {
        _SkinTex ("Skin detail (R mottle, G spots, B warts)", 2D) = "gray" {}
        _Dorsal ("Back colour (linear)", Color) = (0.05, 0.16, 0.03, 1)
        _Ventral ("Belly colour (linear)", Color) = (0.62, 0.56, 0.36, 1)
        _Toon ("Cartoon colour (linear)", Color) = (0.2, 0.7, 0.15, 1)
        _Morph ("Real", Range(0,1)) = 1
        _TexScale ("Detail scale", Float) = 18
        _LampPos ("Lamp position", Vector) = (0,2,0,0)
        _TvPos ("TV position", Vector) = (0,1,2,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="Always" }
            Cull Back ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "RRCommon.cginc"
            sampler2D _SkinTex;
            float4 _Dorsal, _Ventral, _Toon, _LampPos, _TvPos;
            float _Morph, _TexScale;
            struct a2v { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; float3 rest : TEXCOORD2; float3 restN : TEXCOORD3; };
            struct v2f { float4 pos : SV_POSITION; float3 wpos : TEXCOORD0; float3 nrm : TEXCOORD1; float3 rest : TEXCOORD2; float3 restN : TEXCOORD3; float4 col : TEXCOORD4; };
            v2f vert (a2v v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.nrm = UnityObjectToWorldNormal(v.normal);
                o.rest = v.rest; o.restN = v.restN; o.col = v.color;
                return o;
            }
            float4 Tri(float3 p, float3 n)
            {
                float3 w = pow(abs(n), 4.0); w /= (w.x + w.y + w.z);
                return tex2D(_SkinTex, p.yz) * w.x + tex2D(_SkinTex, p.xz) * w.y + tex2D(_SkinTex, p.xy) * w.z;
            }
            float GGX(float3 N, float3 V, float3 L, float a)
            {
                float3 H = normalize(V + L);
                float nh = saturate(dot(N, H)), nl = saturate(dot(N, L));
                float a2 = a * a, d = nh * nh * (a2 - 1.0) + 1.0;
                return a2 / (3.14159 * d * d) * nl * 0.25;
            }
            float4 frag (v2f i) : SV_Target
            {
                float3 rp = i.rest * _TexScale;
                float3 rn = normalize(i.restN);
                float4 d = Tri(rp, rn);
                float4 d2 = Tri(rp * 2.7 + 0.37, rn);
                float pad = i.col.a;
                // geometry normal + bumps from the warts / pores (surface gradient via screen derivatives)
                float3 N = normalize(i.nrm);
                float h = d.b * 0.65 + d2.b * 0.35;
                float3 dpx = ddx(i.wpos), dpy = ddy(i.wpos);
                float dhx = ddx(h), dhy = ddy(h);
                float3 r1 = cross(dpy, N), r2 = cross(N, dpx);
                float det = dot(dpx, r1);
                float3 grad = (dhx * r1 + dhy * r2) / (abs(det) > 1e-12 ? det : 1e-12);
                N = normalize(N - grad * 0.0016 * (1.0 - pad * 0.7));
                float dorsal = smoothstep(-0.45, 0.35, rn.y);
                float3 back = _Dorsal.rgb * (0.7 + 0.6 * d.r) * lerp(1.0, 0.32, smoothstep(0.55, 0.75, d.g) * dorsal);
                float3 belly = _Ventral.rgb * (0.85 + 0.3 * d2.r);
                float3 alb = lerp(belly, back, dorsal);
                alb = lerp(alb, float3(0.55, 0.45, 0.22) * (0.9 + 0.2 * d.r), pad * 0.65);   // pale adhesive toe pads
                float3 V = normalize(_WorldSpaceCameraPos - i.wpos);
                // diffuse: SH with a soft wrap (thin, translucent skin) + warm scatter at the terminator
                float3 irr = SHIrr(N);
                float3 irrSoft = (SHIrr(N) * 0.6 + SHIrr(normalize(N + float3(0, 0.6, 0))) * 0.4);
                float3 diff = alb * lerp(irr, irrSoft, 0.35) + alb * float3(0.9, 0.3, 0.2) * 0.12 * irrSoft;
                // wet highlights: lamp + TV as point lights (their brightness comes from the light group weights)
                float rough = lerp(0.16, 0.34, d2.r) + pad * 0.12;
                float3 Ll = _LampPos.xyz - i.wpos, Lt = _TvPos.xyz - i.wpos;
                float il = 1.0 / max(dot(Ll, Ll), 0.3), it = 1.0 / max(dot(Lt, Lt), 0.3);
                float fres = 0.035 + 0.965 * pow(1.0 - saturate(dot(N, V)), 5.0);
                float3 spec = GGX(N, V, normalize(Ll), rough * rough) * il * _RRLampCol.rgb * float3(1.0, 0.78, 0.55) * 9.0
                            + GGX(N, V, normalize(Lt), rough * rough) * it * _RRTvCol.rgb * 0.25;
                spec += GGX(N, V, normalize(Ll), 0.012) * il * _RRLampCol.rgb * 1.5;       // tight wet glint
                spec *= fres * 4.0;
                float3 R = reflect(-V, N);
                spec += Pano(BoxProject(R, i.wpos), rough * 0.8) * fres;
                float3 real = diff + spec;
                // cartoon hand + morph
                float3 toon = _Toon.rgb * (0.55 + 0.45 * smoothstep(0.1, 0.5, Lum(SHIrr(normalize(i.nrm))) / max(Lum(SHIrr(float3(0, 1, 0))), 1e-4)));
                toon = toon * Lum(SHIrr(float3(0, 1, 0))) * 1.25 + float3(0.02, 0.05, 0.02) * Lum(SHIrr(float3(0, 1, 0)));
                float along = saturate((i.rest.z + 0.30) / 0.46);                  // 0 forearm .. 1 fingertips
                float m = _Morph * 1.7 - (1.0 - along) * 0.7 - d.r * 0.3;
                float k = smoothstep(0.0, 0.05, m);
                float edge = (_Morph > 0.001 && _Morph < 0.999) ? exp(-pow((m - 0.02) / 0.05, 2.0)) : 0.0;
                float3 col = lerp(toon, real, k) + _RRWaveCol.rgb * edge * 1.6 / max(_RRExposure, 0.05);
                return RROut(col);
            }
            ENDCG
        }
    }
    Fallback Off
}
