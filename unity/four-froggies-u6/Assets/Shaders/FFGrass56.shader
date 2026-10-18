// ffu56 ranch grass (Terrain56 / Grass56): GPU-instanced (Graphics.DrawMeshInstanced) 3-card clumps, alpha-cut, double-sided,
// half-Lambert like FF/Foliage. Vertex: wind sway (two frequencies + slow gusts, phase from the clump root), BEND away from up to
// 8 movers (_FFBend xyz = foot / wheel / body point, w = radius; frogs, robots, animals, vehicles) and flatten under them,
// distance fade (clumps shrink into the ground between _FadeNear and _FadeFar). Root darkening in the vertex colour.
// ffu6: URP 17.6 hand port (main light + shadows received, SH ambient x SSAO, fog; depth / depth-normals for SSAO; no shadow casting).
Shader "FF/Grass56"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Grass card (RGBA)", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.42
        _Wind ("Wind", Range(0, 0.5)) = 0.12
        _FadeNear ("Fade start (m)", Float) = 40
        _FadeFar ("Fade end (m)", Float) = 55
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        LOD 200
        HLSLINCLUDE
        #include "Assets/Shaders/FFURP.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half _Cutoff, _Wind, _FadeNear, _FadeFar, _Cull;
        CBUFFER_END
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        float4 _FFBend[8];
        #define LB_VERTEX_MOD 1
        void LBVertexMod(inout float3 pos, inout float3 nrm, inout float4 extra, LBAttr a)
        {
            float3 root = TransformObjectToWorld(float3(0, 0, 0));
            float3 wp = TransformObjectToWorld(pos);
            float h = saturate(a.uv0.y);
            float hy = wp.y - root.y;
            float t = _Time.y;
            float gust = 0.55 + 0.45 * sin(t * 0.37 + root.x * 0.021 + root.z * 0.017);
            float s = sin(t * 1.7 + root.x * 0.35 + root.z * 0.27) * 0.65 + sin(t * 3.3 + root.x * 0.9 - root.z * 0.6) * 0.25;
            float3 off = float3(s, 0, s * 0.6) * _Wind * gust * h * h;
            float flat = 0;
            [unroll] for (int k = 0; k < 8; k++)
            {
                float4 b = _FFBend[k];
                if (b.w > 0.001)
                {
                    float3 dd = root - b.xyz; dd.y = 0;
                    float dist = length(dd);
                    float kk = saturate(1.0 - dist / b.w);
                    kk *= saturate(1.0 - abs(root.y - b.y) * 0.5);
                    float3 dir = dist > 0.001 ? dd / dist : float3(1, 0, 0);
                    off += dir * kk * hy * 0.85;
                    flat = max(flat, kk);
                }
            }
            float dc = distance(root, _WorldSpaceCameraPos);
            float fade = saturate((_FadeFar - dc) / max(0.01, _FadeFar - _FadeNear));
            wp.y = root.y + hy * fade * (1.0 - flat * 0.72);
            wp += off * fade;
            pos = TransformWorldToObject(wp);
            float n = frac(sin(dot(root.xz, float2(12.9898, 78.233))) * 43758.5453);
            // gamma-authored tint x root darkening -> linear multiplier
            // ffu6j: macro patches (lush / sun-dried, ~25 m blobs) so the lawn is not one flat colour, + lighter base (0.68 -> 0.8)
            float2 mp = root.xz * 0.04;
            float2 mi = floor(mp), mf = frac(mp); mf = mf * mf * (3.0 - 2.0 * mf);
            float h00 = frac(sin(dot(mi, float2(12.9898, 78.233))) * 43758.5453), h10 = frac(sin(dot(mi + float2(1, 0), float2(12.9898, 78.233))) * 43758.5453);
            float h01 = frac(sin(dot(mi + float2(0, 1), float2(12.9898, 78.233))) * 43758.5453), h11 = frac(sin(dot(mi + float2(1, 1), float2(12.9898, 78.233))) * 43758.5453);
            float mac = lerp(lerp(h00, h10, mf.x), lerp(h01, h11, mf.x), mf.y);
            float3 patchTint = lerp(float3(0.9, 1.04, 0.86), float3(1.12, 1.06, 0.74), smoothstep(0.35, 0.85, mac));
            extra = float4(LB_F(lerp(float3(0.92, 0.95, 0.82), float3(1.08, 1.04, 0.9), n) * patchTint * lerp(0.8, 1.0, h)), 1);   // ffu6i 0.55 -> 0.68, ffu6j -> 0.8
            nrm = TransformWorldToObjectDir(float3(0, 1, 0), true);
        }
        #include "Assets/Shaders/FFPasses.hlsl"
        void FFClip(LBVary i) { clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw).a * _Color.a - _Cutoff); }
        half4 FFShadowClipFrag(LBVary i) : SV_TARGET { FFClip(i); return 0; }
        half4 FFDepthClipFrag(LBVary i) : SV_TARGET { FFClip(i); return i.positionCS.z; }
        void FFDepthNormalsClipFrag(LBVary i, out half4 outNormalWS : SV_Target0
        #ifdef _WRITE_RENDERING_LAYERS
            , out uint outRenderingLayers : SV_Target1
        #endif
        )
        {
            FFClip(i);
            float3 n = normalize(i.normalWS);
        #if defined(_GBUFFER_NORMALS_OCT)
            float2 oct = PackNormalOctQuadEncode(n);
            outNormalWS = half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0.0);
        #else
            outNormalWS = half4(n, 0.0);
        #endif
        #ifdef _WRITE_RENDERING_LAYERS
            outRenderingLayers = EncodeMeshRenderingLayer();
        #endif
        }
        // main light + shadows (light-layer aware), additional lights via FF_ADD_LIGHTS (Forward+ cluster or per-object list)
        Light FFMain(InputData d)
        {
            Light L = GetMainLight(d.shadowCoord, d.positionWS, d.shadowMask);
        #ifdef _LIGHT_LAYERS
            if (!IsMatchingLightLayer(L.layerMask, GetMeshRenderingLayer())) L.color = 0;
        #endif
            return L;
        }
        half FFAO(InputData d)
        {
        #if defined(_SCREEN_SPACE_OCCLUSION)
            AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(d.normalizedScreenSpaceUV);
            return ao.indirectAmbientOcclusion;
        #else
            return 1.0;
        #endif
        }

        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma vertex LBVert
            #pragma fragment GrassFrag
            half4 GrassFrag(LBVary i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw) * _Color;
                clip(c.a - _Cutoff);
                half3 alb = c.rgb * i.extra.rgb;
                InputData d = LBInputData(i, float3(0, 1, 0));
                Light L = FFMain(d);
                half3 col = alb * L.color * (abs(L.direction.y) * 0.5 + 0.5) * L.shadowAttenuation;
                col += alb * L.color * L.shadowAttenuation * 0.3 * pow(saturate(dot(d.viewDirectionWS, -L.direction)), 3);   // ffu6i: sun shining through the blades
                col += alb * d.bakedGI * lerp(1.0, FFAO(d), 0.35) * 1.35;   // ffu6i: SSAO on dense clumps blackened the lawn; only a third of it. ffu6j ambient 1.15 -> 1.35
                col += L.color * L.shadowAttenuation * pow(saturate(dot(normalize(d.viewDirectionWS + L.direction), float3(0, 1, 0))), 24.0) * 0.06 * i.extra.g;   // ffu6j: soft waxy sheen on the blade tips facing the sun
                col = LBFog(col, i.positionWS, d.fogCoord);
                return half4(col, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma vertex LBVert
            #pragma fragment FFDepthClipFrag
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma vertex LBVert
            #pragma fragment FFDepthNormalsClipFrag
            ENDHLSL
        }
    }
    Fallback Off
}
