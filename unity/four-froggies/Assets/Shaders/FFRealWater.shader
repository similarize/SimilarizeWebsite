// ?realism=1 test pond water: three scrolling normal-map ripple layers (detail fades with distance), Schlick fresnel
// (F0 0.02), reflection of the ranch reflection probe (HDRI sky + ranch), tight + broad sun glints (HDR, they bloom),
// depth tint from the baked pond map (R = depth 0..1 over 3.5 m) with a soft, see-through shoreline. Premultiplied
// alpha so reflections are not dimmed by the transparency.
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
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _SeaMap, _NormalMap;
            float4 _SeaRect, _Shallow, _Deep;
            float _Strength, _Speed, _Detail;

            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; UNITY_FOG_COORDS(1) };

            v2f vert(appdata_base v)
            {
                v2f o;
                float4 wp = mul(unity_ObjectToWorld, v.vertex);
                o.wp = wp.xyz;
                o.pos = UnityWorldToClipPos(wp.xyz);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half2 Rip(float2 uv) { return tex2D(_NormalMap, uv).xy * 2.0 - 1.0; }

            half4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 w = i.wp.xz;
                float dist = length(_WorldSpaceCameraPos - i.wp);
                float near = saturate(1.0 - dist / 160.0);
                half2 s = Rip(w * 0.045 + float2(t * 0.006, t * 0.004)) * 0.8
                        + Rip(w * 0.13 + float2(-t * 0.011, t * 0.015)) * 0.6
                        + Rip(w * 0.41 + float2(t * 0.027, -t * 0.019)) * 0.35 * near * _Detail;
                s *= _Strength * lerp(0.45, 1.0, near);
                float3 N = normalize(float3(s.x, 1.0, s.y));
                float3 V = normalize(_WorldSpaceCameraPos - i.wp);
                half ndv = saturate(dot(N, V));
                half F = 0.02 + 0.46 * pow(1.0 - ndv, 4.0);   // ffu23: lower cap - the low-angle view was a grey mirror

                float3 R = reflect(-V, N);
                R.y = max(R.y, 0.07);   // ffu23: skip the hazy grey horizon band of the probe
                R = normalize(R);
                half4 env = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, R, 0.6);
                half3 refl = DecodeHDR(env, unity_SpecCube0_HDR) * 0.78 * half3(0.86, 0.93, 0.97);

                float2 suv = (i.wp.xz - _SeaRect.xy) * _SeaRect.zw;
                half depth = tex2D(_SeaMap, saturate(suv) + s * 0.003).r;
                // body colour lit by the sky ambient (SH) + a little sun
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                half3 amb = ShadeSH9(float4(0, 1, 0, 1));
                half3 light = amb + _LightColor0.rgb * saturate(L.y) * 0.35;
                half dk = saturate(depth * 1.7);
                half3 body = lerp(_Shallow.rgb, _Deep.rgb, dk) * light;
                half aBody = lerp(0.18, 0.94, saturate(depth * 2.4));

                float3 H = normalize(L + V);
                half nh = saturate(dot(N, H));
                half3 sun = _LightColor0.rgb * (pow(nh, 900.0) * 30.0 + pow(nh, 120.0) * 0.8) * saturate(L.y * 3.0);

                half3 col = body * aBody * (1.0 - F) + refl * F + sun;
                half a = 1.0 - (1.0 - aBody) * (1.0 - F);
                half edge = saturate(depth * 16.0);            // soft shoreline
                col *= edge; a *= edge;
                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, half4(unity_FogColor.rgb * a, 1));
                return half4(col, a);
            }
            ENDCG
        }
    }
    Fallback Off
}
