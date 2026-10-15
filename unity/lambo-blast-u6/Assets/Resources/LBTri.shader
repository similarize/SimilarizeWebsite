// lambou6 URP port of lambo8 LB/Tri: world-space triplanar albedo (mean-0.5 CC0 detail x tint) + triplanar normal, snow on up
// faces (_Snow), vertex-red AO (_AO). Meshes without tangents keep the plain vertex normal (as in the Built-in version).
// u6d: _Full = scanned CC0 albedo + packed data map (roughness / normal / AO), tinted toward the palette colour; GPU instancing,
// wind sway + distance shrink + own trilight ambient for the instanced foliage (Gfx6).
Shader "LB/Tri"
{
    Properties
    {
        [MainColor] _Color ("Tint", Color) = (1,1,1,1)
        [MainTexture] _MainTex ("Detail (mean 0.5) / u6d scanned albedo", 2D) = "grey" {}
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _DataMap ("u6d data (r rough, g nY, b AO, a nX)", 2D) = "black" {}
        _Mean ("u6d scan mean (linear)", Color) = (0.5,0.5,0.5,1)
        _Full ("u6d full scanned PBR", Float) = 0
        _TintK ("u6d tint amount", Range(0,1)) = 0.7
        _Scale ("Tiles per metre", Float) = 0.25
        _BumpScale ("Bump", Float) = 1
        _Glossiness ("Smoothness", Range(0,1)) = 0.2
        _Metallic ("Metallic", Range(0,1)) = 0
        _Snow ("Snow cover", Range(0,1)) = 0
        _AO ("Vertex AO", Range(0,1)) = 0
        _Raw ("Full-colour map", Range(0,1)) = 0
        _Sway ("u6d wind sway (unit-height meshes)", Float) = 0
        _Fade ("u6d distance shrink start / end (0 = off)", Vector) = (0, 0, 0, 0)
        _OwnAmb ("u6d trilight ambient (instanced)", Float) = 0
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        LOD 300
        HLSLINCLUDE
        #include "Assets/Shaders/LBURP.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST, _Mean, _Fade;
            half4 _Color;
            half _Scale, _BumpScale, _Glossiness, _Metallic, _Snow, _AO, _Raw, _Full, _TintK, _Sway, _OwnAmb, _Cull;
        CBUFFER_END
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_DataMap); SAMPLER(sampler_DataMap);
        // u6d: wind sway + distance shrink for the instanced foliage (object space = unit tree, pivot at the base)
        #define LB_VERTEX_MOD 1
        void LBVertexMod(inout float3 pos, inout float3 nrm, inout float4 extra, LBAttr a)
        {
            float3 root = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
            if (_Fade.y > 0.0)
            {
                float d = distance(_WorldSpaceCameraPos.xz, root.xz);
                pos *= saturate((_Fade.y - d) / max(_Fade.y - _Fade.x, 1.0));
            }
            if (_Sway > 0.0001)
            {
                float h = saturate(pos.y);
                float ph = _Time.y * 1.3 + root.x * 0.11 + root.z * 0.13;
                float gust = 0.6 + 0.4 * sin(_Time.y * 0.45 + root.x * 0.02 + root.z * 0.015);
                float sw = (sin(ph) * 0.7 + sin(ph * 2.7 + 1.3) * 0.3) * _Sway * gust * h * h;
                pos.xz += float2(0.05, 0.035) * sw;
                pos += sin(_Time.y * 5.0 + dot(pos, float3(17.0, 23.0, 19.0))) * 0.006 * _Sway * h;
            }
        }
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
            #pragma multi_compile_instancing
            half4 TriFrag(LBVary i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 T, B, N; bool hasT;
                LBFrame(i, T, B, N, hasT);
                float3 n = N;
                float3 w = n * n; w *= w; w /= (w.x + w.y + w.z + 1e-4);
                float3 p = i.positionWS * _Scale;
                float2 ux = p.zy, uy = p.xz + 0.37, uz = p.xy + 0.71;
                half3 d = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, ux).rgb * w.x + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uy).rgb * w.y + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uz).rgb * w.z;
                half3 dg = LB_TOG(d);
                half3 alb; half gloss; float3 nw; half occ = 1.0;
                if (_Full > 0.5)
                {
                    // u6d: scanned CC0 albedo + packed roughness / normal / AO, tinted toward the old palette colour
                    half4 qx = SAMPLE_TEXTURE2D(_DataMap, sampler_DataMap, ux);
                    half4 qy = SAMPLE_TEXTURE2D(_DataMap, sampler_DataMap, uy);
                    half4 qz = SAMPLE_TEXTURE2D(_DataMap, sampler_DataMap, uz);
                    half3 tx = LBDataNormal(qx, 1.0), ty = LBDataNormal(qy, 1.0), tz = LBDataNormal(qz, 1.0);
                    float3 pert = float3(0, tx.y, tx.x) * w.x + float3(ty.x, 0, ty.y) * w.y + float3(tz.x, tz.y, 0) * w.z;
                    nw = normalize(n + pert * _BumpScale * 1.2);
                    half rough = qx.r * w.x + qy.r * w.y + qz.r * w.z;
                    occ = lerp(1.0, qx.b * w.x + qy.b * w.y + qz.b * w.z, 0.7);
                    alb = LBTintScan(d, _Mean.rgb, _Color.rgb, _TintK);
                    gloss = saturate((1.0 - rough) * (0.55 + _Glossiness * 1.4));
                }
                else
                {
                    half3 tx = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, ux));
                    half3 ty = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uy));
                    half3 tz = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uz));
                    float3 pert = float3(0, tx.y, tx.x) * w.x + float3(ty.x, 0, ty.y) * w.y + float3(tz.x, tz.y, 0) * w.z;
                    nw = normalize(n + pert * _BumpScale);
                    alb = _Color.rgb * d * LB_X2;
                    gloss = _Glossiness * (0.75 + 0.5 * dg.g);
                }
                if (_Snow > 0.001)
                {
                    half s = saturate((nw.y - (1.0 - _Snow * 0.75)) * 5.0) * saturate(0.6 + (dg.r - 0.5) * 3.0);
                    alb = lerp(alb, LB_TOLIN(half3(0.86, 0.9, 0.96)), s);
                    gloss = lerp(gloss, 0.45, s);
                }
                if (_AO > 0.001) alb *= lerp(1.0, LB_TOLIN(i.color.r), _AO);
                return LBShade2(i, alb, _Metallic, gloss, hasT || _Full > 0.5 ? nw : n, half3(0, 0, 0), 1, 0, 0, occ, _OwnAmb);
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
            #pragma multi_compile_instancing
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #pragma vertex LBVert
            #pragma fragment LBDepthNormalsFrag
            ENDHLSL
        }
    }
    Fallback Off
}
