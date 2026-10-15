// lambou6 URP port of lambo8 LB/Terrain: colour map x two-scale CC0 grain (rotated 2nd layer + linear macro noise), grain fades
// with distance, triplanar CC0 rock on slopes > _RockSlope. LB_LITE (global keyword) = one grain layer, no rock.
Shader "LB/Terrain"
{
    Properties
    {
        _ColorMap ("Colour map", 2D) = "grey" {}
        [MainTexture] _MainTex ("Grain (mean 0.5)", 2D) = "grey" {}
        [Normal] _BumpMap ("Grain normal", 2D) = "bump" {}
        _Macro ("Macro noise (linear)", 2D) = "grey" {}
        _RockTex ("Rock (mean 0.5)", 2D) = "grey" {}
        [Normal] _RockBump ("Rock normal", 2D) = "bump" {}
        _RockTint ("Rock tint", Color) = (0.5,0.5,0.5,1)
        _Rep ("Grain repeats", Float) = 100
        _RockScale ("Rock tiles per metre", Float) = 0.08
        _RockAmt ("Rock amount", Range(0,1)) = 1
        _RockSlope ("Rock slope start", Range(0,1)) = 0.35
        _BumpScale ("Bump", Float) = 1
        _Glossiness ("Smoothness", Range(0,1)) = 0.1
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        LOD 300
        HLSLINCLUDE
        #include "Assets/Shaders/LBURP.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _ColorMap_ST, _MainTex_ST;
            half4 _RockTint;
            half _Rep, _RockScale, _RockAmt, _RockSlope, _BumpScale, _Glossiness, _Cull;
        CBUFFER_END
        TEXTURE2D(_ColorMap); SAMPLER(sampler_ColorMap);
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_Macro); SAMPLER(sampler_Macro);
        TEXTURE2D(_RockTex); SAMPLER(sampler_RockTex);
        TEXTURE2D(_RockBump); SAMPLER(sampler_RockBump);
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
            #pragma fragment TerrFrag
            #pragma multi_compile _ LB_LITE
            half4 TerrFrag(LBVary i) : SV_Target
            {
                float3 T, B, N; bool hasT;
                LBFrame(i, T, B, N, hasT);
                float3 n = N;
                float2 uv = i.uv.xy * _ColorMap_ST.xy + _ColorMap_ST.zw;
                half3 col = SAMPLE_TEXTURE2D(_ColorMap, sampler_ColorMap, uv).rgb;
                float dist = distance(i.positionWS, _WorldSpaceCameraPos);
                half far = saturate((dist - 45.0) / 230.0);
                float2 g1 = uv * _Rep;
                half m = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, uv * (_Rep * 0.045)).r;
                half m2 = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, uv * (_Rep * 0.012) + 0.31).g;
                half3 d1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, g1).rgb;
            #ifdef LB_LITE
                half3 d = lerp(d1, half3(LB_GREY, LB_GREY, LB_GREY), far * 0.5);
            #else
                float2 g2 = float2(uv.x * 0.866 - uv.y * 0.5, uv.x * 0.5 + uv.y * 0.866) * (_Rep * 0.29) + 0.19;
                half3 d2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, g2).rgb;
                half3 d = lerp(d1, d2, 0.3 + 0.35 * m);
                d = lerp(d, lerp(d2, half3(LB_GREY, LB_GREY, LB_GREY), 0.45), far);
            #endif
                half3 alb = col * d * LB_X2 * LB_F(0.88 + 0.24 * m2);
                half3 nt = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, g1));
                nt.xy *= _BumpScale * (1.0 - far * 0.75);
                float3 nw = hasT ? normalize(T * nt.x + B * nt.y + n * nt.z) : n;
            #ifndef LB_LITE
                half rk = saturate((1.0 - n.y - _RockSlope) * 5.0) * _RockAmt;
                float3 w = n * n; w *= w; w /= (w.x + w.y + w.z + 1e-4);
                float3 p = i.positionWS * _RockScale;
                float2 ux = p.zy, uy = p.xz + 0.37, uz = p.xy + 0.71;
                half3 rd = SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, ux).rgb * w.x + SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, uy).rgb * w.y + SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, uz).rgb * w.z;
                half3 tx = UnpackNormal(SAMPLE_TEXTURE2D(_RockBump, sampler_RockBump, ux));
                half3 ty = UnpackNormal(SAMPLE_TEXTURE2D(_RockBump, sampler_RockBump, uy));
                half3 tz = UnpackNormal(SAMPLE_TEXTURE2D(_RockBump, sampler_RockBump, uz));
                float3 pert = float3(0, tx.y, tx.x) * w.x + float3(ty.x, 0, ty.y) * w.y + float3(tz.x, tz.y, 0) * w.z;
                float3 rn = normalize(n + pert * 1.3);
                half3 ralb = lerp(col, _RockTint.rgb, 0.5) * rd * LB_X2;
                alb = lerp(alb, ralb, rk);
                nw = normalize(lerp(nw, rn, rk));
            #endif
                half dg = LB_TOG(d.g);
                return LBShade(i, alb, 0, _Glossiness * (0.7 + 0.6 * dg), nw, half3(0, 0, 0), 1, 0, 0);
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
