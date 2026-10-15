// lambou6 URP port of lambo8 LB/Road: markings map x two-scale CC0 asphalt grain + asphalt normal + damp patches from a linear
// macro noise (darker, smoothness up to 0.8, flatter normal -> sky / probe shows in them); paint (alpha) glossier.
Shader "LB/Road"
{
    Properties
    {
        [MainTexture] _MainTex ("Markings (a = paint)", 2D) = "white" {}
        _Grain ("Asphalt grain (mean 0.5)", 2D) = "grey" {}
        [Normal] _GrainBump ("Asphalt normal", 2D) = "bump" {}
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
            float4 _MainTex_ST, _GrainRep;
            half _Glossiness, _Wet, _BumpScale, _Cull;
        CBUFFER_END
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_Grain); SAMPLER(sampler_Grain);
        TEXTURE2D(_GrainBump); SAMPLER(sampler_GrainBump);
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
                half far = saturate((dist - 30.0) / 160.0);
                float2 g = uv * _GrainRep.xy;
                half3 d1 = SAMPLE_TEXTURE2D(_Grain, sampler_Grain, g).rgb;
                half m = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, uv * float2(0.6, 0.45)).r;
                half m2 = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, uv * float2(0.25, 0.11) + 0.4).g;
            #ifdef LB_LITE
                half3 d = lerp(d1, half3(LB_GREY, LB_GREY, LB_GREY), far * 0.6);
            #else
                half3 d2 = SAMPLE_TEXTURE2D(_Grain, sampler_Grain, float2(g.y, -g.x) * 0.31 + 0.5).rgb;
                half3 d = lerp(d1, d2, 0.35);
                d = lerp(d, lerp(d2, half3(LB_GREY, LB_GREY, LB_GREY), 0.5), far);
            #endif
                half wet = saturate((m - 0.56) * 5.0) * _Wet;
                half3 alb = mk.rgb * d * LB_X2 * LB_F((0.93 + 0.14 * m2) * (1.0 - 0.28 * wet));
                half dg = LB_TOG(d.g);
                half gloss = lerp(_Glossiness * (0.8 + 0.4 * dg), 0.8, wet);
                gloss = lerp(gloss, max(gloss, 0.45), mk.a);
                half3 nt = UnpackNormal(SAMPLE_TEXTURE2D(_GrainBump, sampler_GrainBump, g));
                nt.xy *= _BumpScale * (1.0 - far * 0.8) * (1.0 - wet * 0.85);
                float3 nw = hasT ? normalize(T * nt.x + B * nt.y + N * nt.z) : N;
                return LBShade(i, alb, 0, gloss, nw, half3(0, 0, 0), 1, 0, 0);
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
