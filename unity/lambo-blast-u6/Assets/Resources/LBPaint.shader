// lambou6 URP port of lambo9 LB/Paint: metallic base coat + URP's physically based clear coat (_CLEARCOAT: second specular lobe
// + fresnel environment reflection from the Forward+ reflection probes) + the tight sun glint on the lacquer.
Shader "LB/Paint"
{
    Properties
    {
        [MainColor] _Color ("Colour", Color) = (1, 0, 0, 1)
        _Glossiness ("Base smoothness", Range(0, 1)) = 0.7
        _Metallic ("Metal flake", Range(0, 1)) = 0.35
        _Clear ("Clear coat", Range(0, 1)) = 1
        _ClearSmooth ("Clear coat smoothness", Range(0, 1)) = 0.94
        _Rim ("u6d rim light", Range(0, 2)) = 1
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        LOD 300
        HLSLINCLUDE
        #define _CLEARCOAT 1
        #include "Assets/Shaders/LBURP.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _Glossiness, _Metallic, _Clear, _ClearSmooth, _Rim, _Cull;
        CBUFFER_END
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
            #pragma fragment PaintFrag
            half4 PaintFrag(LBVary i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light L = GetMainLight();
                half glint = pow(saturate(dot(N, normalize(L.direction + V))), 700.0) * 2.5;
                half3 em = L.color * glint * 0.5 * _Clear;
                // u6d: rim light (sky-coloured fresnel edge, stronger on the sun side) so the silhouette reads against the scene
                half ndv = saturate(dot(N, V));
                half rim = pow(1.0 - ndv, 4.0) * (0.35 + 0.65 * saturate(dot(N, L.direction) + 0.4));
                em += (LBTrilight(N) * 0.5 + L.color * 0.25) * rim * _Rim;
                return LBShade(i, _Color.rgb, _Metallic, _Glossiness, N, em, 1, _Clear, _ClearSmooth);
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
