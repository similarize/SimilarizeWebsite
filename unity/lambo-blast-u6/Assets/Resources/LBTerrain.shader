// u6d LB/Terrain: two scanned CC0 PBR ground layers (albedo + packed roughness / normal / AO, ambientCG) per track, layer A at
// two scales (rotated, distance-weighted against tiling), layer B on macro-noise patches / the colour map's green areas with a
// height blend on the AO channels, triplanar scanned rock on slopes, tinted toward the generated colour map (macro variation).
// LB_LITE (global keyword) = layer A only, no rock.
Shader "LB/Terrain"
{
    Properties
    {
        _ColorMap ("Colour map", 2D) = "grey" {}
        _MapMean ("Colour map mean (linear)", Color) = (0.3,0.3,0.3,1)
        _MapTint ("Palette tint amount", Range(0,1)) = 0.55
        _Macro ("Macro noise (linear)", 2D) = "grey" {}
        _ACol ("Layer A albedo", 2D) = "grey" {}
        _AData ("Layer A data", 2D) = "black" {}
        _AMean ("Layer A mean", Color) = (0.3,0.3,0.3,1)
        _BCol ("Layer B albedo", 2D) = "grey" {}
        _BData ("Layer B data", 2D) = "black" {}
        _BMean ("Layer B mean", Color) = (0.3,0.3,0.3,1)
        _BMask ("B mask: macro thr, sharpness, greenness weight, amount", Vector) = (0.6, 6, 0, 1)
        _RCol ("Rock albedo", 2D) = "grey" {}
        _RData ("Rock data", 2D) = "black" {}
        _RMean ("Rock mean", Color) = (0.3,0.3,0.3,1)
        _RockTint ("Rock tint", Color) = (0.5,0.5,0.5,1)
        _ARep ("Layer A repeats", Float) = 160
        _BRep ("Layer B repeats", Float) = 200
        _RockScale ("Rock tiles per metre", Float) = 0.08
        _RockAmt ("Rock amount", Range(0,1)) = 1
        _RockSlope ("Rock slope start", Range(0,1)) = 0.35
        _BumpScale ("Bump", Float) = 1
        _SmoothK ("Smoothness scale", Range(0,1)) = 0.8
        _Glossiness ("Smoothness (unused, u6c)", Range(0,1)) = 0.1
        [MainTexture] _MainTex ("u6c grain (unused; keeps Material.mainTexture valid)", 2D) = "grey" {}
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        LOD 300
        HLSLINCLUDE
        #include "Assets/Shaders/LBURP.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _ColorMap_ST, _MapMean, _AMean, _BMean, _RMean, _BMask;
            half4 _RockTint;
            half _MapTint, _ARep, _BRep, _RockScale, _RockAmt, _RockSlope, _BumpScale, _SmoothK, _Glossiness, _Cull;
        CBUFFER_END
        TEXTURE2D(_ColorMap); SAMPLER(sampler_ColorMap);
        TEXTURE2D(_Macro); SAMPLER(sampler_Macro);
        TEXTURE2D(_ACol); SAMPLER(sampler_ACol);
        TEXTURE2D(_AData); SAMPLER(sampler_AData);
        TEXTURE2D(_BCol); SAMPLER(sampler_BCol);
        TEXTURE2D(_BData); SAMPLER(sampler_BData);
        TEXTURE2D(_RCol); SAMPLER(sampler_RCol);
        TEXTURE2D(_RData); SAMPLER(sampler_RData);
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
                half far = saturate((dist - 40.0) / 260.0);
                half m = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, uv * (_ARep * 0.031)).r;
                half m2 = SAMPLE_TEXTURE2D(_Macro, sampler_Macro, uv * (_ARep * 0.0083) + 0.31).g;
                half3 tint = lerp(col, col * (0.86 + 0.28 * m), 0.6);
                float2 ga = uv * _ARep;
                half3 aC = SAMPLE_TEXTURE2D(_ACol, sampler_ACol, ga).rgb;
                half4 aD = SAMPLE_TEXTURE2D(_AData, sampler_AData, ga);
            #ifdef LB_LITE
                half3 lc = aC; half4 ld = aD; half3 lm = _AMean.rgb;
            #else
                // second, rotated, larger scale of layer A: breaks the tiling, takes over with distance
                float2 ga2 = float2(uv.x * 0.866 - uv.y * 0.5, uv.x * 0.5 + uv.y * 0.866) * (_ARep * 0.29) + 0.19;
                half3 aC2 = SAMPLE_TEXTURE2D(_ACol, sampler_ACol, ga2).rgb;
                half4 aD2 = SAMPLE_TEXTURE2D(_AData, sampler_AData, ga2);
                half wa = saturate(0.22 + 0.3 * m + 0.45 * far);
                aC = lerp(aC, aC2, wa); aD = lerp(aD, aD2, wa);
                // layer B: macro-noise patches and / or the colour map's green areas, height-blended on the AO channels
                float2 gb = uv * _BRep + 0.53;
                half3 bC = SAMPLE_TEXTURE2D(_BCol, sampler_BCol, gb).rgb;
                half4 bD = SAMPLE_TEXTURE2D(_BData, sampler_BData, gb);
                half green = col.g - max(col.r, col.b);
                half bm = saturate((m2 - _BMask.x + green * _BMask.z) * _BMask.y) * _BMask.w;
                half hb = saturate((bm - 0.5 + (bD.b - aD.b) * 0.45) * 3.0 + 0.5) * step(0.002, bm);
                half3 lc = lerp(aC, bC, hb); half4 ld = lerp(aD, bD, hb); half3 lm = lerp(_AMean.rgb, _BMean.rgb, hb);
            #endif
                half3 alb = LBTintScan(lc, lm, tint, _MapTint);
                half3 nt = LBDataNormal(ld, _BumpScale * (1.0 - far * 0.7));
                float3 nw = hasT ? normalize(T * nt.x + B * nt.y + n * nt.z) : n;
                half rough = ld.r, ao = ld.b;
            #ifndef LB_LITE
                // triplanar scanned rock on steep faces
                half rk = saturate((1.0 - n.y - _RockSlope) * 5.0) * _RockAmt;
                // (no branch: WGSL wants texture samples in uniform control flow)
                {
                    float3 w = n * n; w *= w; w /= (w.x + w.y + w.z + 1e-4);
                    float3 p = i.positionWS * _RockScale;
                    float2 ux = p.zy, uy = p.xz + 0.37, uz = p.xy + 0.71;
                    half3 rd = SAMPLE_TEXTURE2D(_RCol, sampler_RCol, ux).rgb * w.x + SAMPLE_TEXTURE2D(_RCol, sampler_RCol, uy).rgb * w.y + SAMPLE_TEXTURE2D(_RCol, sampler_RCol, uz).rgb * w.z;
                    half4 qx = SAMPLE_TEXTURE2D(_RData, sampler_RData, ux), qy = SAMPLE_TEXTURE2D(_RData, sampler_RData, uy), qz = SAMPLE_TEXTURE2D(_RData, sampler_RData, uz);
                    half3 tx = LBDataNormal(qx, 1.0), ty = LBDataNormal(qy, 1.0), tz = LBDataNormal(qz, 1.0);
                    float3 pert = float3(0, tx.y, tx.x) * w.x + float3(ty.x, 0, ty.y) * w.y + float3(tz.x, tz.y, 0) * w.z;
                    float3 rn = normalize(n + pert * 1.4);
                    half rao = qx.b * w.x + qy.b * w.y + qz.b * w.z;
                    half hr = saturate((rk - 0.5 + (rao - ao) * 0.4) * 3.0 + 0.5);
                    half3 ralb = LBTintScan(rd, _RMean.rgb, lerp(col, _RockTint.rgb, 0.5), 0.7);
                    alb = lerp(alb, ralb, hr);
                    nw = normalize(lerp(nw, rn, hr));
                    rough = lerp(rough, qx.r * w.x + qy.r * w.y + qz.r * w.z, hr);
                    ao = lerp(ao, rao, hr);
                }
            #endif
                // far away the palette colour takes over (no visible repeats at 300 m+)
                alb = lerp(alb, tint * 0.95, far * 0.3);
                half occ = lerp(1.0, ao, 0.65 * (1.0 - far * 0.5));
                return LBShade2(i, alb, 0, saturate((1.0 - rough) * _SmoothK), nw, half3(0, 0, 0), 1, 0, 0, occ, 0);
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
