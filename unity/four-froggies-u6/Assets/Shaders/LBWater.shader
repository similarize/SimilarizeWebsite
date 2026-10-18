// Lambo Blast sea: animated normals from two scrolling noise layers, shallow -> deep colour from a baked sea map,
// sky reflection from the reflection probe (fresnel), sun glint, and shoreline foam bands rolling in to the beach.
// Sea map (baked from the island height field by Track.cs): R = depth 0..1, G = shore foam mask.
// ffu6: URP 17.6 port (UniversalForward; main light / probe via URP; ripple sim via FFRipple.hlsl).
Shader "LB/Water"
{
    Properties
    {
        _Shallow ("Shallow", Color) = (0.22, 0.86, 0.82, 0.5)
        _Deep ("Deep", Color) = (0.02, 0.36, 0.56, 0.92)
        _FoamColor ("Foam", Color) = (1, 1, 1, 1)
        _SeaMap ("Sea map", 2D) = "black" {}
        _Noise ("Noise", 2D) = "gray" {}
        _SeaRect ("Sea map rect (x0, z0, 1/w, 1/h)", Vector) = (0, 0, 0.002, 0.002)
        _WaveScale ("Wave tiling", Float) = 0.045
        _Speed ("Speed", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
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
            TEXTURE2D(_Noise); SAMPLER(sampler_Noise);
            CBUFFER_START(UnityPerMaterial)
            float4 _Shallow, _Deep, _FoamColor, _SeaRect;
            float _WaveScale, _Speed;
            CBUFFER_END

            struct appdata_base { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wp : TEXCOORD0;
                float fogCoord : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                float4 wp = float4(TransformObjectToWorld(v.vertex.xyz), 1);
                o.wp = wp.xyz;
                o.pos = TransformWorldToHClip(wp.xyz);
                o.fogCoord = ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 w = i.wp.xz * _WaveScale;
                half3 n1 = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, w + float2(t * 0.021, t * 0.013)).rgb * 2.0 - 1.0;
                half3 n2 = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, w * 2.3 + float2(-t * 0.017, t * 0.027)).rgb * 2.0 - 1.0;
                half2 nn = (n1.xy + n2.yx * 0.7) * 0.32;
                half pmask = FFPondMask(i.wp.xz);   // ffu39: masked to the shoreline ellipse
                if (pmask <= 0.001) discard;
                float silt;
                float4 rip = FFRippleS(i.wp.xz, silt);   // ffu38 wave sim ripples + swells (ffu39: + silt)
                float3 N = normalize(float3(nn.x - rip.x, 1.0, nn.y - rip.y));
                float3 V = normalize(_WorldSpaceCameraPos - i.wp);

                float2 suv = (i.wp.xz - _SeaRect.xy) * _SeaRect.zw;
                half4 sm = SAMPLE_TEXTURE2D(_SeaMap, sampler_SeaMap, saturate(suv) + nn * 0.002);
                bool inside = suv.x > 0.0 && suv.y > 0.0 && suv.x < 1.0 && suv.y < 1.0;
                half depth = inside ? sm.r : 1.0;
                half shore = inside ? sm.g : 0.0;

                half ndv = saturate(dot(N, V));
                half fres = pow(1.0 - ndv, 4.0);
                half3 col = lerp(_Shallow.rgb, _Deep.rgb, depth);
                float3 R = reflect(-V, N);
                half3 sky = FFEnv(R, 0.0, i.wp, i.pos);
                col = lerp(col, sky, saturate(0.1 + fres * 0.8));
                float3 L = normalize(FFLightDir());
                float3 H = normalize(L + V);
                col += FFLightCol() * pow(saturate(dot(N, H)), 180.0) * 1.4;

                // foam: bands rolling towards the beach + broken up by noise
                half wave = sin(depth * 46.0 - t * 2.1 + n1.z * 2.5) * 0.5 + 0.5;
                half brk = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, i.wp.xz * 0.21 + float2(t * 0.03, -t * 0.02)).b;
                half foam = shore * (0.55 + wave * 0.6) - brk * 0.5;
                foam = smoothstep(0.18, 0.5, foam) * 0.12;   // ffu39: a pond barely foams at the shore
                col = lerp(col, _FoamColor.rgb, foam * 0.9);
                half sl = saturate(silt * 1.6);              // ffu39: muddy silt plume
                col = lerp(col, LB_TOLIN(half3(0.3, 0.26, 0.15)), sl * 0.8);
                half fm = smoothstep(0.25, 0.75, rip.z * 1.3 - brk * 0.5) * 0.7;   // brief spray only
                col = lerp(col, _FoamColor.rgb * 0.92, fm * 0.85);
                foam = max(foam, max(fm * 0.85, sl * 0.6));

                half a = lerp(_Shallow.a, _Deep.a, depth);
                a = saturate(max(a + fres * 0.25, foam)) * pmask;
                col = MixFog(col, InitializeInputDataFog(float4(i.wp, 1.0), i.fogCoord));
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
