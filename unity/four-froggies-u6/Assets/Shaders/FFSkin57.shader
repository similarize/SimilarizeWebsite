// ffu57 Four Froggies animal + frog skin (Core/Animals57.cs). One shader for every animal and every froggy:
//  * FUR (_Fur 1): object-space triplanar strand texture (_MainTex, made in code, linear data) tints the albedo along the hair,
//    a Kajiya-Kay style anisotropic sheen across the strands, a soft fuzzy rim, darker partings.
//  * WET SKIN (_Wet 1): frogs - a broad soft lobe plus a tight wet highlight, fresnel reflection of the reflection probe(s)
//    and a warm sub-surface tint at the terminator, faint mottling.
// Vertex colours multiply the albedo (Quaternius farm packs keep their painted patches).
// ffu6: URP 17.6 hand port: same model per light (main + Forward+ lamps), probe reflections through URP (box / blend / atlas),
// SH ambient x SSAO, fog. Gamma vertex colours -> linear.
Shader "FF/Skin57"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Strands / mottle (grey)", 2D) = "white" {}
        _Scale ("Texture metres", Float) = 0.25
        _Gloss ("Gloss", Range(0, 1)) = 0.3
        _Wrap ("Wrap", Range(0, 1)) = 0.5
        _Rim ("Rim", Range(0, 1)) = 0.3
        _SSS ("Subsurface tint", Color) = (1, 0.55, 0.35, 1)
        _Fur ("Fur", Range(0, 1)) = 0
        _Wet ("Wet", Range(0, 1)) = 0
        _VC ("Use vertex colour", Range(0, 1)) = 1
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        LOD 200
        HLSLINCLUDE
        #include "Assets/Shaders/FFURP.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color, _SSS;
            half _Gloss, _Wrap, _Rim, _Fur, _Wet, _Scale, _VC, _Cull;
        CBUFFER_END
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        #define LB_VERTEX_MOD 1
        void LBVertexMod(inout float3 pos, inout float3 nrm, inout float4 extra, LBAttr a)
        {
            float s = length(TransformObjectToWorldDir(float3(0, 1, 0), false));
            extra = float4(pos * s, 0);      // metres, so prints / strands keep their size on scaled primitives
        }
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
        half3 Skin57Light(half3 alb, float3 n, float3 v, float3 tang, Light L, half atten)
        {
            half nl = dot(n, L.direction);
            half diff = saturate((nl + _Wrap) / (1.0 + _Wrap));
            half term = saturate(diff - saturate(nl)) * (0.7 + _Wet * 0.5);
            half3 h = normalize(L.direction + v);
            half nh = saturate(dot(n, h));
            half spec = pow(nh, 10.0 + 90.0 * _Gloss) * _Gloss * 0.45 * (1.0 - _Fur * 0.7);
            spec += pow(nh, 260.0) * _Wet * 1.6;
            half th = dot(tang, h);
            half sinTH = sqrt(saturate(1.0 - th * th));
            spec += pow(sinTH, 30.0 + 60.0 * _Gloss) * _Fur * (0.08 + 0.22 * _Gloss) * saturate(nl * 2.0 + 0.3);
            return (alb * (diff + _SSS.rgb * term) + spec * lerp(half3(1, 1, 1), alb * 1.4 + 0.2, _Fur * 0.6)) * L.color * atten;
        }
        half Tri(float3 p, float3 n)
        {
            float3 w = abs(n); w = w / (w.x + w.y + w.z + 1e-4);
            float k = 1.0 / max(_Scale, 0.01);
            half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, p.yz * k).r, b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, p.zx * k).r, c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, p.xy * k).r;
            return a * w.x + b * w.y + c * w.z;
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
            #pragma fragment Skin57Frag
            half4 Skin57Frag(LBVary i) : SV_Target
            {
                float3 ln = TransformWorldToObjectNormal(normalize(i.normalWS));
                half3 vc = LB_TOLIN(i.color.rgb);
                half3 base = _Color.rgb * lerp(half3(1, 1, 1), vc, _VC);
                half t = Tri(i.extra.xyz, ln);
                half k = lerp(1.0 + (t - 0.5) * 0.16, 0.78 + t * 0.44, _Fur);
                half3 alb = saturate(base * LB_F(k));
                half3 tangO = normalize(cross(ln, float3(1, 0, 0)) + float3(0, -0.3, 0) + 1e-3);
                float3 tang = normalize(TransformObjectToWorldDir(tangO, false));
                InputData d = LBInputData(i, normalize(i.normalWS));
                float3 n = d.normalWS, v = d.viewDirectionWS;
                Light L = FFMain(d);
                half3 col = Skin57Light(alb, n, v, tang, L, L.shadowAttenuation);
                FF_ADD_LIGHTS(d, col += Skin57Light(alb, n, v, tang, L, L.distanceAttenuation * L.shadowAttenuation);)
                half ao = FFAO(d);
                col += alb * d.bakedGI * ao;
                half rim = 1.0 - saturate(dot(v, n));
                half3 sky = FFEnv(reflect(-v, n), 2.0, i.positionWS, i.positionCS) * ao;
                half fres = pow(rim, 4.0);
                col += sky * fres * _Wet * 0.55
                     + (sky * 0.5 + base * 0.4) * pow(rim, 2.2) * _Fur * 0.28
                     + (_Color.rgb * 0.6 + LB_TOLIN(0.25)) * pow(rim, 3.0) * _Rim * 0.4 * (1.0 - _Fur);
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
