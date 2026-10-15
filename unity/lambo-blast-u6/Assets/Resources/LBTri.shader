// lambou6 URP port of lambo8 LB/Tri: world-space triplanar albedo (mean-0.5 CC0 detail x tint) + triplanar normal, snow on up
// faces (_Snow), vertex-red AO (_AO). Meshes without tangents keep the plain vertex normal (as in the Built-in version).
Shader "LB/Tri"
{
    Properties
    {
        [MainColor] _Color ("Tint", Color) = (1,1,1,1)
        [MainTexture] _MainTex ("Detail (mean 0.5)", 2D) = "grey" {}
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _Scale ("Tiles per metre", Float) = 0.25
        _BumpScale ("Bump", Float) = 1
        _Glossiness ("Smoothness", Range(0,1)) = 0.2
        _Metallic ("Metallic", Range(0,1)) = 0
        _Snow ("Snow cover", Range(0,1)) = 0
        _AO ("Vertex AO", Range(0,1)) = 0
        _Raw ("Full-colour map", Range(0,1)) = 0
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        LOD 300
        HLSLINCLUDE
        #include "Assets/Shaders/LBURP.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half _Scale, _BumpScale, _Glossiness, _Metallic, _Snow, _AO, _Raw, _Cull;
        CBUFFER_END
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
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
            #pragma fragment TriFrag
            half4 TriFrag(LBVary i) : SV_Target
            {
                float3 T, B, N; bool hasT;
                LBFrame(i, T, B, N, hasT);
                float3 n = N;
                float3 w = n * n; w *= w; w /= (w.x + w.y + w.z + 1e-4);
                float3 p = i.positionWS * _Scale;
                float2 ux = p.zy, uy = p.xz + 0.37, uz = p.xy + 0.71;
                half3 d = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ux).rgb * w.x + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uy).rgb * w.y + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uz).rgb * w.z;
                half3 dg = LB_TOG(d);
                half3 tx = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, ux));
                half3 ty = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uy));
                half3 tz = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uz));
                float3 pert = float3(0, tx.y, tx.x) * w.x + float3(ty.x, 0, ty.y) * w.y + float3(tz.x, tz.y, 0) * w.z;
                float3 nw = normalize(n + pert * _BumpScale);
                half3 alb = _Color.rgb * d * LB_X2;
                half gloss = _Glossiness * (0.75 + 0.5 * dg.g);
                if (_Snow > 0.001)
                {
                    half s = saturate((nw.y - (1.0 - _Snow * 0.75)) * 5.0) * saturate(0.6 + (dg.r - 0.5) * 3.0);
                    alb = lerp(alb, LB_TOLIN(half3(0.86, 0.9, 0.96)), s);
                    gloss = lerp(gloss, 0.45, s);
                }
                if (_AO > 0.001) alb *= lerp(1.0, LB_TOLIN(i.color.r), _AO);
                return LBShade(i, alb, _Metallic, gloss, hasT ? nw : n, half3(0, 0, 0), 1, 0, 0);
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
