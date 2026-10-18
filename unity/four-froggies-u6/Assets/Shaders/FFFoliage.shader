// Four Froggies foliage (Quaternius leaf cards): alpha-cut, double-sided, half-Lambert so back faces stay soft,
// and a small two-frequency wind sway in the vertex shader (world-space phase, so merged meshes sway unevenly).
// ffu6: URP 17.6 hand port (main light + shadows, Forward+ lamps, SH ambient x AO, fog; instanced).
Shader "FF/Foliage"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Leaves (RGBA)", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.45
        _Wind ("Wind", Range(0, 0.3)) = 0.06
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
            half _Cutoff, _Wind, _Cull;
        CBUFFER_END
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        #define LB_VERTEX_MOD 1
        void LBVertexMod(inout float3 pos, inout float3 nrm, inout float4 extra, LBAttr a)
        {
            float3 wp = TransformObjectToWorld(pos);
            float t = _Time.y;
            float s = sin(t * 1.3 + wp.x * 0.21 + wp.z * 0.17) + 0.4 * sin(t * 3.1 + wp.y * 0.9 + wp.x * 0.5);
            float3 off = float3(s, 0.15 * s, s * 0.6) * _Wind;
            pos += TransformWorldToObjectDir(off, false);
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma vertex LBVert
            #pragma fragment FoliageFrag
            half4 FoliageFrag(LBVary i, bool front : SV_IsFrontFace) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw) * _Color;
                clip(c.a - _Cutoff);
                float3 n = normalize(i.normalWS) * (front ? 1.0 : -1.0);
                InputData d = LBInputData(i, n);
                Light L = FFMain(d);
                half3 col = c.rgb * L.color * (abs(dot(d.normalWS, L.direction)) * 0.6 + 0.4) * L.shadowAttenuation;
                FF_ADD_LIGHTS(d, col += c.rgb * L.color * L.distanceAttenuation * L.shadowAttenuation * (abs(dot(d.normalWS, L.direction)) * 0.6 + 0.4);)
                col += c.rgb * d.bakedGI * FFAO(d);
                col = LBFog(col, i.positionWS, d.fogCoord);
                return half4(col, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma vertex LBShadowVert
            #pragma fragment FFShadowClipFrag
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
