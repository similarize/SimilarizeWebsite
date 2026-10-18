// Four Froggies soft stylised skin: wrapped (half-soft) diffuse with a warm sub-surface tint at the terminator,
// a broad soft highlight and a gentle rim, on top of the ambient / SH. Cheap: one forward pass + shadows.
// ffu6: URP 17.6 hand port (same lighting model per light: main + Forward+ lamps; SH ambient x SSAO; fog).
Shader "FF/Skin"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 0.75, 0.3, 1)
        _Gloss ("Gloss", Range(0, 1)) = 0.45
        _Wrap ("Wrap", Range(0, 1)) = 0.5
        _Rim ("Rim", Range(0, 1)) = 0.35
        _SSS ("Subsurface tint", Color) = (1, 0.55, 0.35, 1)
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        LOD 200
        HLSLINCLUDE
        #include "Assets/Shaders/FFURP.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Color, _SSS;
            half _Gloss, _Wrap, _Rim, _Cull;
        CBUFFER_END
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
        half3 SkinLight(half3 alb, float3 n, float3 v, Light L, half atten)
        {
            half nl = dot(n, L.direction);
            half diff = saturate((nl + _Wrap) / (1.0 + _Wrap));
            half term = saturate(diff - saturate(nl)) * 0.7;
            half3 h = normalize(L.direction + v);
            half spec = pow(saturate(dot(n, h)), 12.0 + 120.0 * _Gloss) * _Gloss * 0.55;
            return (alb * (diff + _SSS.rgb * term) + spec) * L.color * atten;
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
            #pragma fragment SkinFrag
            half4 SkinFrag(LBVary i) : SV_Target
            {
                InputData d = LBInputData(i, normalize(i.normalWS));
                float3 n = d.normalWS, v = d.viewDirectionWS;
                half3 alb = _Color.rgb;
                Light L = FFMain(d);
                half3 col = SkinLight(alb, n, v, L, L.shadowAttenuation);
                FF_ADD_LIGHTS(d, col += SkinLight(alb, n, v, L, L.distanceAttenuation * L.shadowAttenuation);)
                col += alb * d.bakedGI * FFAO(d);
                half rim = 1.0 - saturate(dot(v, n));
                col += (_Color.rgb * 0.6 + LB_TOLIN(0.25)) * pow(rim, 3.0) * _Rim * 0.55;
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
