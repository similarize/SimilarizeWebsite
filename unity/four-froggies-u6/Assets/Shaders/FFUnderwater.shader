// Four Froggies underwater surfaces: Lambert + albedo texture, with animated world-space caustics (two scrolling
// layers of a Worley-edge pattern, min-combined so the web shimmers) added on up-facing surfaces and fading with depth.
// ffu6: URP 17.6 hand port (Lambert per light, SH x SSAO, fog; caustics maths kept in gamma then linearised).
Shader "FF/Underwater"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Caustics ("Caustics", 2D) = "black" {}
        _CausticColor ("Caustic colour", Color) = (0.75, 0.95, 1.0, 1)
        _CausticStrength ("Caustic strength", Range(0, 2)) = 0.55
        _CausticScale ("Caustic tiling (per metre)", Float) = 0.11
        _SurfaceY ("Water surface height", Float) = 0
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        LOD 200
        HLSLINCLUDE
        #include "Assets/Shaders/FFURP.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST, _Caustics_ST;
            half4 _Color, _CausticColor;
            half _CausticStrength, _CausticScale, _SurfaceY, _Cull;
        CBUFFER_END
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_Caustics); SAMPLER(sampler_Caustics);
        #include "Assets/Shaders/FFPasses.hlsl"
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
            #pragma fragment UnderFrag
            half4 UnderFrag(LBVary i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw) * _Color;
                InputData d = LBInputData(i, normalize(i.normalWS));
                Light L = FFMain(d);
                half3 col = c.rgb * L.color * saturate(dot(d.normalWS, L.direction)) * L.shadowAttenuation;
                FF_ADD_LIGHTS(d, col += c.rgb * L.color * saturate(dot(d.normalWS, L.direction)) * L.distanceAttenuation * L.shadowAttenuation;)
                col += c.rgb * d.bakedGI * FFAO(d);
                float t = _Time.y;
                float2 w = i.positionWS.xz * _CausticScale;
                half a = LB_TOG(SAMPLE_TEXTURE2D(_Caustics, sampler_Caustics, w + float2(t * 0.031, t * 0.017)).r);
                half b = LB_TOG(SAMPLE_TEXTURE2D(_Caustics, sampler_Caustics, w * 1.37 + float2(-t * 0.023, t * 0.029)).r);
                half caus = min(a, b) * 1.6;
                half up = saturate(d.normalWS.y * 0.8 + 0.2);
                half depthFade = saturate(1.0 - (_SurfaceY - i.positionWS.y) / 45.0);
                half3 e = LB_TOG(_CausticColor.rgb) * caus * _CausticStrength * up * (0.35 + 0.65 * depthFade) * (0.5 + 0.5 * LB_TOG(c.rgb));
                col += LB_TOLIN(e);
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
