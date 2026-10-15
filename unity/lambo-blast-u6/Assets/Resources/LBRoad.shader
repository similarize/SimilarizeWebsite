// u6d LB/Road: markings map x scanned CC0 asphalt (ambientCG Asphalt025C: albedo detail at full contrast + packed roughness /
// normal / AO) at two scales, damp patches from a linear macro noise (darker, smooth, flat -> reflections), paint and kerbs
// glossier and flatter.
Shader "LB/Road"
{
    Properties
    {
        [MainTexture] _MainTex ("Markings (a = paint)", 2D) = "white" {}
        _Grain ("Scanned asphalt albedo", 2D) = "grey" {}
        _GrainData ("Asphalt data (r rough, g nY, b AO, a nX)", 2D) = "black" {}
        _GrainMean ("Asphalt mean (linear)", Color) = (0.03,0.03,0.03,1)
        _GrainCon ("Asphalt detail contrast", Range(0,1.5)) = 1
        _Macro ("Macro noise (linear)", 2D) = "grey" {}
        _GrainRep ("Grain repeats (u, v)", Vector) = (6, 12.8, 0, 0)
        _Glossiness ("Smoothness", Range(0,1)) = 0.3
        _Wet ("Wet patches", Range(0,1)) = 0.5
        _BumpScale ("Bump", Float) = 0.8
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        LOD 300
        HLSLINCLUDE
        #include "Assets/Shaders/LBURP.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST, _GrainRep, _GrainMean;
            half _GrainCon, _Glossiness, _Wet, _BumpScale, _Cull;
        CBUFFER_END
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_Grain); SAMPLER(sampler_Grain);
        TEXTURE2D(_GrainData); SAMPLER(sampler_GrainData);
        TEXTURE2D(_Macro); SAMPLER(sampler_Macro);
        #include "Assets/Shaders/LBPasses.hlsl"
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
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
            #pragma vertex LBVert
            #pragma fragment RoadFrag
            #pragma multi_compile _ LB_LITE
            half4 RoadFrag(LBVary i) : SV_Target
            {
                float3 T, B, N; bool hasT;
                LBFrame(i, T, B, N, hasT);
                float2 uv = i.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
                half4 mk = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                float dist = distance(i.positionWS, _WorldSpaceCameraPos);
                half far = saturate((dist - 30.0) / 170.0);
                float2 g = uv * _GrainRep.xy;
                half3 c1 = SAMPLE_TEXTURE2D(_Grain, sampler_Grain, g).rgb;
                half4 q1 = SAMPLE_TEXTURE2D(_GrainData, sampler_GrainData, g);
                half m = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, uv * float2(0.6, 0.45)).r;
                half m2 = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, uv * float2(0.25, 0.11) + 0.4).g;
            #ifdef LB_LITE
                half3 sc = c1; half4 q = q1;
            #else
                float2 g2 = float2(g.y, -g.x) * 0.31 + 0.5;
                half3 c2 = SAMPLE_TEXTURE2D(_Grain, sampler_Grain, g2).rgb;
                half4 q2 = SAMPLE_TEXTURE2D(_GrainData, sampler_GrainData, g2);
                half wb = 0.3 + 0.45 * far;
                half3 sc = lerp(c1, c2, wb); half4 q = lerp(q1, q2, wb);
            #endif
                half paint = smoothstep(0.85, 0.97, mk.a);
                half curb = saturate(1.0 - abs(mk.a - 0.6) * 8.0);
                half3 det = lerp(half3(1, 1, 1), sc / max(_GrainMean.rgb, half3(1e-3, 1e-3, 1e-3)), _GrainCon * (1.0 - far * 0.55));
                det = lerp(det, lerp(half3(1, 1, 1), det, 0.35), max(paint, curb * 0.6));
                half wet = saturate((m - 0.56) * 5.0) * _Wet * (1.0 - paint);
                half3 alb = min(mk.rgb * det * LB_F((0.93 + 0.14 * m2) * (1.0 - 0.28 * wet)), half3(1, 1, 1));
                half sm = _Glossiness * (0.55 + 0.9 * (1.0 - q.r));
                half gloss = lerp(sm, 0.82, wet);
                gloss = lerp(gloss, max(gloss, 0.45), paint);
                gloss = lerp(gloss, max(gloss, 0.5), curb);
                half3 nt = LBDataNormal(q, _BumpScale * (1.0 - far * 0.8) * (1.0 - wet * 0.85) * (1.0 - paint * 0.6));
                float3 nw = hasT ? normalize(T * nt.x + B * nt.y + N * nt.z) : N;
                half occ = lerp(1.0, q.b, 0.5 * (1.0 - far));
                return LBShade2(i, alb, 0, gloss, nw, half3(0, 0, 0), 1, 0, 0, occ, 0);
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
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma vertex LBShadowVert
            #pragma fragment LBShadowFrag
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LBVert
            #pragma fragment LBDepthFrag
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma vertex LBVert
            #pragma fragment LBDepthNormalsFrag
            ENDHLSL
        }
    }
    Fallback Off
}
