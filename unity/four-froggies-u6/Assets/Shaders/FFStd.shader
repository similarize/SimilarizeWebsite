// ffu6 (from Lambo Blast u6 LB/Std): URP stand-in for Built-in 'Standard' with the same property names (_Color, _MainTex, _Glossiness, _Metallic, _BumpMap,
// _DetailAlbedoMap + _UVSec, _SrcBlend/_DstBlend/_ZWrite) and keywords (_NORMALMAP, _DETAIL_MULX2, _ALPHABLEND_ON, _ALPHAPREMULTIPLY_ON),
// ffu6f: _Coat = clear-coat lobe (car / robot gloss paint, Mats.Paint gloss >= 0.75): a second, sharp fresnel reflection + sun glint
// over the base paint, like the lacquer on real car paint.
// so the game's material code (Mats.cs, BuildScript MakeTransparent) works unchanged. Transparent materials disable ShadowCaster.
Shader "FF/Std"
{
    Properties
    {
        [MainColor] _Color ("Color", Color) = (1,1,1,1)
        [MainTexture] _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0
        _BumpScale ("Normal scale", Float) = 1
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _DetailAlbedoMap ("Detail x2", 2D) = "grey" {}
        _UVSec ("Detail UV set", Float) = 0
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        _Coat ("Clear coat (ffu6f)", Range(0,1)) = 0
        [HideInInspector] _Mode ("Mode", Float) = 0
        [HideInInspector] _SrcBlend ("Src", Float) = 1
        [HideInInspector] _DstBlend ("Dst", Float) = 0
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        LOD 300
        HLSLINCLUDE
        #if defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
            #define _SURFACE_TYPE_TRANSPARENT 1
        #endif
        #include "Assets/Shaders/FFURP.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST, _DetailAlbedoMap_ST, _BumpMap_ST;
            half4 _Color, _EmissionColor;
            half _Glossiness, _Metallic, _BumpScale, _UVSec, _Mode, _SrcBlend, _DstBlend, _ZWrite, _Cull, _Coat;
        CBUFFER_END
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_DetailAlbedoMap); SAMPLER(sampler_DetailAlbedoMap);
        #include "Assets/Shaders/FFPasses.hlsl"
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]
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
            #pragma fragment StdFrag
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _DETAIL_MULX2
            #pragma shader_feature_local_fragment _ _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            // ffu6k: world-space macro + micro variation (kills flat tiling / flat-colour boxes: walls, wood, brick, roads, props).
            // Planar projection by the dominant normal axis, two value-noise scales; colour +-amp, gloss +-15 %. Skipped on car paint
            // (_Coat) and dimmed on metals. _FFMacro.x = amplitude (U6.MacroGlobals; 0 = off on the lowest Perf60 tier / u6macro=0).
            float4 _FFMacro;
            float StdHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float StdNoise(float2 p)
            {
                float2 q = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(StdHash(q), StdHash(q + float2(1, 0)), f.x), lerp(StdHash(q + float2(0, 1)), StdHash(q + float2(1, 1)), f.x), f.y);
            }
            half4 StdFrag(LBVary i) : SV_Target
            {
                float2 uv = i.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half3 alb = _Color.rgb * t.rgb;
                half a = _Color.a * t.a;
            #if defined(_DETAIL_MULX2)
                float2 duv = (_UVSec > 0.5 ? i.uv.zw : i.uv.xy) * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
                alb *= SAMPLE_TEXTURE2D(_DetailAlbedoMap, sampler_DetailAlbedoMap, duv).rgb * LB_X2;
            #endif
                float3 T, B, N; bool hasT;
                LBFrame(i, T, B, N, hasT);
                half gl = _Glossiness;
                float mamp = _FFMacro.x * (1.0 - saturate(_Coat * 4.0)) * (1.0 - _Metallic * 0.6);
                if (mamp > 0.001)
                {
                    float3 an = abs(N), wp = i.positionWS;
                    float2 p2 = an.y > 0.6 ? wp.xz : (an.x > an.z ? wp.zy : wp.xy);
                    float mac = StdNoise(p2 * 0.33) * 0.7 + StdNoise(p2 * 0.9 + 5.2) * 0.3;
                    float mic = StdNoise(p2 * 2.7 + 1.3);
                    float v = (mac - 0.5) * 2.0 * 0.75 + (mic - 0.5) * 2.0 * 0.25;
                    alb *= 1.0 + v * mamp * half3(1.0, 0.96, 0.88);
                    gl = saturate(gl * (1.0 + (mic - 0.5) * 0.3 * saturate(mamp * 6.0)));
                }
                float3 nw = N;
            #if defined(_NORMALMAP)
                half3 nt = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                if (hasT) nw = normalize(T * nt.x + B * nt.y + N * nt.z);
            #endif
            #if !defined(_SURFACE_TYPE_TRANSPARENT)
                a = 1.0;
            #endif
                half4 c = LBShade(i, alb, _Metallic, gl, nw, _EmissionColor.rgb, a, 0, 0);
            #if !defined(_SURFACE_TYPE_TRANSPARENT)
                if (_Coat > 0.001)
                {
                    // ffu6f clear coat: smooth lacquer on the geometric normal (no orange peel from the base normal map)
                    float3 Vv = GetWorldSpaceNormalizeViewDir(i.positionWS);
                    half ndv = saturate(dot(N, Vv));
                    half Fc = (0.04 + 0.96 * pow(1.0 - ndv, 5.0)) * _Coat;
                    half3 env = FFEnv(reflect(-Vv, N), 0.2, i.positionWS, i.positionCS);
                    float3 Ld = FFLightDir(); float3 Hh = normalize(Ld + Vv);
                    half sp = pow(saturate(dot(N, Hh)), 900.0) * 28.0 * saturate(dot(N, Ld)) * FFShadow(i.positionWS);
                    c.rgb = c.rgb * (1.0 - Fc) + (env + FFLightCol() * sp) * Fc;
                }
            #endif
                return c;
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
