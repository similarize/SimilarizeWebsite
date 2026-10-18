// ?realism=1 test pond water: three scrolling normal-map ripple layers (detail fades with distance), Schlick fresnel
// (F0 0.02), reflection of the ranch reflection probe (HDRI sky + ranch), tight + broad sun glints (HDR, they bloom),
// depth tint from the baked pond map (R = depth 0..1 over 3.5 m) with a soft, see-through shoreline. Premultiplied
// alpha so reflections are not dimmed by the transparency.
// ffu39: real pond look - no contact / wake foam (only ~1 s spray), muddy silt plumes, darker glassier water whose moving
// ripples show as distorted reflections, meniscus round legs, floating leaves, masked to the shoreline ellipse.
// ffu6f: livelier: slow broad swell in the normals, full Schlick fresnel (mirror sky at grazing angles), brighter reflection,
// a hint of sky in the water body, deeper water more opaque (the seabed clutter only shows in the shallows), gentle lapping line
// at the shore.
// ffu6: URP 17.6 port (UniversalForward; main light / SH / probe via URP; ripple sim via FFRipple.hlsl; gamma colour constants -> linear).
Shader "FF/RealWater"
{
    Properties
    {
        _SeaMap ("Depth map", 2D) = "black" {}
        _SeaRect ("Depth map rect (x0, z0, 1/w, 1/h)", Vector) = (0, 0, 0.002, 0.002)
        _NormalMap ("Ripple normals", 2D) = "bump" {}
        _Shallow ("Shallow", Color) = (0.30, 0.33, 0.2, 1)
        _Deep ("Deep", Color) = (0.03, 0.075, 0.06, 1)
        _Strength ("Ripple strength", Float) = 0.55
        _Speed ("Speed", Float) = 1
        _Detail ("Fine ripples", Float) = 1
        _Patch ("ffu38 ripple patch mesh (vertex displacement)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma target 3.5
            #include "Assets/Shaders/FFURP.hlsl"
            #include "Assets/Shaders/FFRipple.hlsl"

            TEXTURE2D(_SeaMap); SAMPLER(sampler_SeaMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _SeaRect, _Shallow, _Deep;
            float _Strength, _Speed, _Detail, _Patch;
            CBUFFER_END

            struct appdata_base { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; float fogCoord : TEXCOORD1; };

            v2f vert(appdata_base v)
            {
                v2f o;
                float4 wp = float4(TransformObjectToWorld(v.vertex.xyz), 1);
                // ffu38: the patch mesh near the view really moves with the wave sim + swells
                if (_Patch > 0.5) wp.y += FFRippleH(wp.xz);
                o.wp = wp.xyz;
                o.pos = TransformWorldToHClip(wp.xyz);
                o.fogCoord = ComputeFogFactor(o.pos.z);
                return o;
            }

            half2 Rip(float2 uv) { return SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv).xy * 2.0 - 1.0; }

            // ffu39: procedural floating debris - a few leaves per 2.6 m cell, drifting slowly, pushed about by the ripples
            // (they ride the ripple gradient, so they bob / slide on passing waves). Returns (coverage, tint 0..1).
            float2 Hash2(float2 p) { p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3))); return frac(sin(p) * 43758.5453); }
            float2 Leaves(float2 w, float2 push, float t)
            {
                float2 p = w / 2.6 + push * 0.6 + float2(t * 0.004, t * 0.003);
                float2 cell = floor(p), f = frac(p);
                float2 h = Hash2(cell);
                if (h.x > 0.22) return 0;
                float2 c = 0.25 + 0.5 * Hash2(cell + 7.3);
                float ang = h.y * 6.2832 + sin(t * 0.3 + h.x * 20.0) * 0.15;
                float2 d = f - c; float cs = cos(ang), sn = sin(ang);
                d = float2(d.x * cs - d.y * sn, d.x * sn + d.y * cs);
                float sz = 0.045 + 0.05 * h.x * 4.0;
                float e = length(d / float2(sz, sz * 0.48));
                return float2(1.0 - smoothstep(0.75, 1.0, e), frac(h.y * 13.7));
            }

            half4 frag(v2f i) : SV_Target
            {
                // ffu38: the big flat plane leaves the window to the displaced patch mesh (no double layer)
                if (_Patch < 0.5 && _FFRipP.z > 0.5)
                {
                    float2 pu = FFRipUV(i.wp.xz);
                    if (pu.x > 0.0 && pu.y > 0.0 && pu.x < 1.0 && pu.y < 1.0) discard;
                }
                // ffu39: nothing renders past the shoreline ellipse (no flooding onto the bank)
                half pmask = FFPondMask(i.wp.xz);
                if (pmask <= 0.001) discard;
                float t = _Time.y * _Speed;
                float2 w = i.wp.xz;
                float silt;
                float4 rip = FFRippleS(w, silt);
                float2 cont = FFContact(w);
                float dist = length(_WorldSpaceCameraPos - i.wp);
                float near = saturate(1.0 - dist / 160.0);
                // ffu39: glassier - calm pond: the fine normal-map ripples are ~half as strong; the sim's moving ripples
                // (rip.xy) do the work, as distorted reflections
                half2 s = Rip(w * 0.045 + float2(t * 0.006, t * 0.004)) * 0.6
                        + Rip(w * 0.13 + float2(-t * 0.011, t * 0.015)) * 0.35
                        + Rip(w * 0.41 + float2(t * 0.027, -t * 0.019)) * 0.2 * near * _Detail;
                s *= _Strength * lerp(0.4, 0.8, near);
                s += Rip(w * 0.011 + float2(t * 0.0021, -t * 0.0016)).yx * (0.16 * _Strength);   // ffu6f: broad, slow swell
                float3 N = normalize(float3(s.x - rip.x, 1.0, s.y - rip.y));
                float3 V = normalize(_WorldSpaceCameraPos - i.wp);
                half ndv = saturate(dot(N, V));
                half F = 0.02 + 0.86 * pow(1.0 - ndv, 5.0);   // ffu6f: 0.6 -> 0.86 (real water is a mirror at grazing angles)   // ffu39: Schlick for water, a bit more mirror at grazing angles

                float3 R = reflect(-V, N);
                R.y = max(R.y, 0.05);
                R = normalize(R);
                half3 refl = FFEnv(R, 0.35, i.wp, i.pos) * 1.0 * LB_TOLIN(half3(0.9, 0.95, 0.98));   // sharper: trees + sky read in the ripples

                float2 suv = (i.wp.xz - _SeaRect.xy) * _SeaRect.zw;
                // refraction: the bed / depth tint is looked up through the moving surface
                half depth = SAMPLE_TEXTURE2D(_SeaMap, sampler_SeaMap, saturate(suv) + (s + rip.xy) * 0.004).r;
                float3 L = normalize(FFLightDir()); half3 _LightColor0 = FFLightCol();
                half3 amb = FFSH(float3(0, 1, 0));
                half3 light = amb + _LightColor0 * saturate(L.y) * 0.3;
                half dk = saturate(depth * 1.7);
                // ffu39: dark, peaty pond water; suspended silt turns it murky brown-green and opaque
                half3 body = lerp(_Shallow.rgb * 0.85, _Deep.rgb * 0.7, dk) * light;
                body += refl * (0.07 * (1.0 - dk * 0.5));   // ffu6f: a little sky scattered in the water body (not flat brown)
                half sl = saturate(silt * 1.6);
                half3 siltC = LB_TOLIN(half3(0.25, 0.21, 0.12)) * (amb * 0.9 + _LightColor0 * saturate(L.y) * 0.45);
                body = lerp(body, siltC, sl * 0.85);
                half aBody = lerp(0.24, 0.97, saturate(depth * 2.9));   // ffu6f: deep water reads as water, not glass over the seabed
                aBody = lerp(aBody, 0.97, sl * 0.8);

                float3 H = normalize(L + V);
                half nh = saturate(dot(N, H));
                half3 sun = _LightColor0 * (pow(nh, 900.0) * 30.0 + pow(nh, 120.0) * 0.6) * saturate(L.y * 3.0);
                sun *= 1.0 / (1.0 + dot(rip.xy, rip.xy) * 40.0);
                sun *= 1.0 - sl * 0.5;

                half3 col = body * aBody * (1.0 - F) + refl * F * (1.0 - sl * 0.35) + sun;
                half a = 1.0 - (1.0 - aBody) * (1.0 - F);
                // meniscus: a dark wet line where a leg / hull meets the water + a thin bright lip just outside it
                col = lerp(col, col * 0.45, cont.y * 0.7);
                col += refl * cont.x * 0.35 + _LightColor0 * cont.x * 0.05 * saturate(L.y);
                // floating debris: leaves (brown / olive) riding the ripples (near view only)
                if (near > 0.35)
                {
                    float2 lf = Leaves(w, rip.xy * 0.8, t);
                    half3 lc = LB_TOLIN(lerp(half3(0.28, 0.2, 0.09), half3(0.24, 0.27, 0.1), lf.y)) * (amb * 0.8 + _LightColor0 * saturate(L.y) * 0.55);
                    half lk = lf.x * saturate((near - 0.35) * 4.0);
                    col = lerp(col, lc, lk); a = lerp(a, 1.0, lk);
                }
                // brief white spray only (real splashes / footfalls), broken up, fades in ~1 s (sim A channel)
                half fn = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, w * 0.61 + float2(t * 0.013, -t * 0.009)).r * 0.6 + SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, w * 1.7 - float2(t * 0.02, t * 0.011)).g * 0.4;
                half fm = smoothstep(0.25, 0.75, rip.z * 1.3 - fn * 0.5) * 0.7;
                half3 foamC = LB_TOLIN(half3(0.82, 0.86, 0.86)) * (amb * 0.85 + _LightColor0 * saturate(L.y) * 0.6);
                col = lerp(col, foamC, fm);
                a = max(a, fm);
                // ffu6f: gentle lapping line just off the shore (thin, slow, broken by the ripple normals)
                half lap = saturate(1.0 - abs(depth * 22.0 - 1.1) * 1.4) * (0.55 + 0.45 * sin(t * 1.1 - depth * 90.0 + fn * 3.0));
                col = lerp(col, foamC, lap * 0.22 * near); a = max(a, lap * 0.22 * near);
                half edge = saturate(depth * 16.0) * pmask;    // soft shoreline
                col *= edge; a *= edge;
                col = MixFogColor(col, unity_FogColor.rgb * a, InitializeInputDataFog(float4(i.wp, 1.0), i.fogCoord));
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
