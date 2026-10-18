// world-space triplanar PBR for scanned rock / bark on meshes without good UVs or tangents (procedural boulders, Quaternius
// trunks). The normal map is blended in WORLD space (whiteout blend), so no mesh tangents are needed.
// ffu6: URP 17.6 hand port -> UniversalFragmentPBR (main + Forward+ lights, shadows, probes, SSAO), instanced.
Shader "FF/RealTri"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _Scale ("Metres per tile", Float) = 2
        _Gloss ("Smoothness", Range(0, 1)) = 0.2
        _BumpScale ("Normal strength", Float) = 1
        _TopTint ("Top-facing tint", Color) = (1, 1, 1, 1)
        _TopK ("Top tint amount", Float) = 0
        _Desat ("Desaturate", Float) = 0
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        LOD 300
        HLSLINCLUDE
        #include "Assets/Shaders/FFURP.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST, _BumpMap_ST;
            half4 _Color, _TopTint;
            half _Scale, _Gloss, _BumpScale, _TopK, _Desat, _Cull;
        CBUFFER_END
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        #include "Assets/Shaders/FFPasses.hlsl"
        half3 Unpack(float2 uv)
        {
            half4 p = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv);
            half3 n; n.xy = (p.xy * 2.0 - 1.0) * _BumpScale; n.z = sqrt(saturate(1.0 - dot(n.xy, n.xy)));
            return n;
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
            #pragma multi_compile_instancing
            #pragma vertex LBVert
            #pragma fragment TriFrag
            half4 TriFrag(LBVary i) : SV_Target
            {
                float3 wn = normalize(i.normalWS);
                float3 bl = pow(abs(wn), 4.0); bl /= dot(bl, 1.0);
                float3 p = i.positionWS / _Scale;
                float2 ux = p.zy, uy = p.xz, uz = p.xy;
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ux) * bl.x + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uy) * bl.y + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uz) * bl.z;
                half3 cc = lerp(c.rgb, dot(c.rgb, half3(0.3, 0.59, 0.11)).xxx, _Desat);
                half3 alb = cc * _Color.rgb * lerp(half3(1, 1, 1), _TopTint.rgb, saturate(wn.y * 1.4 - 0.2) * _TopK);
                half3 nx = Unpack(ux), ny = Unpack(uy), nz = Unpack(uz);
                nx = half3(nx.xy + wn.zy, abs(nx.z) * wn.x);
                ny = half3(ny.xy + wn.xz, abs(ny.z) * wn.y);
                nz = half3(nz.xy + wn.xy, abs(nz.z) * wn.z);
                float3 N = normalize(nx.zyx * bl.x + ny.xzy * bl.y + nz.xyz * bl.z);
                return LBShade(i, alb, 0, _Gloss * (0.6 + 0.4 * LB_TOG(c.g)), N, 0, 1, 0, 0);
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
            #pragma multi_compile_instancing
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
            #pragma multi_compile_instancing
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
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma vertex LBVert
            #pragma fragment LBDepthNormalsFrag
            ENDHLSL
        }
    }
    Fallback Off
}
