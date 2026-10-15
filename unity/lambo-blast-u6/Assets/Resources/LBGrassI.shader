// u6d LB/GrassI: GPU-instanced grass tufts (Graphics.RenderMeshInstanced), wind sway + gust wave, distance shrink, back-light
// translucency + sheen, own trilight ambient (instanced draws), receives the sun shadow. No ShadowCaster (like LB/Grass).
Shader "LB/GrassI"
{
    Properties
    {
        _ColA ("Colour A", Color) = (0.2, 0.42, 0.14, 1)
        _ColB ("Colour B", Color) = (0.32, 0.55, 0.18, 1)
        _Fade ("Fade start / end (m)", Vector) = (60, 110, 0, 0)
        _Wind ("Wind", Float) = 1
        _Trans ("Back-light translucency", Float) = 0.6
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        LOD 300
        HLSLINCLUDE
        #include "Assets/Shaders/LBURP.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _ColA, _ColB, _Fade;
            half _Wind, _Trans, _Cull;
        CBUFFER_END
        // u6d: one tuft mesh (9 bent blades, uv0.x = height 0..1, uv0.y = blade phase, vertex colour = per-blade shade) drawn
        // with Graphics.RenderMeshInstanced (Gfx6). Per instance: position / yaw / scale in the matrix. Distance LOD = the tuft
        // shrinks into its root between _Fade.x and _Fade.y; wind = sway + a travelling gust wave. No geometry shader.
        #define LB_VERTEX_MOD 1
        void LBVertexMod(inout float3 pos, inout float3 nrm, inout float4 extra, LBAttr a)
        {
            float3 root = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
            float d = distance(_WorldSpaceCameraPos.xz, root.xz);
            float s = saturate((_Fade.y - d) / max(_Fade.y - _Fade.x, 1.0));
            float h = a.uv0.x;
            float gust = sin(dot(root.xz, float2(0.045, 0.03)) - _Time.y * 1.4) * 0.5 + 0.5;
            float ph = _Time.y * 2.1 + root.x * 0.23 + root.z * 0.19 + a.uv0.y * 6.2831;
            float sway = (sin(ph) * 0.45 + sin(ph * 2.3 + 1.7) * 0.2 + gust * 0.9) * _Wind * h * h;
            pos *= s;
            pos.xz += float2(0.16, 0.1) * sway * s;
            pos.y -= abs(sway) * 0.05 * s;
            nrm = normalize(float3(nrm.x * 0.35, 1.0, nrm.z * 0.35));
            float hs = frac(sin(dot(root.xz, float2(12.9898, 78.233))) * 43758.5453);
            extra = float4(h, s, hs, gust);
        }
        #include "Assets/Shaders/LBPasses.hlsl"
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
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
            #pragma fragment GrassIFrag
            #pragma multi_compile_instancing
            half4 GrassIFrag(LBVary i) : SV_Target
            {
                if (i.extra.y <= 0.001) discard;
                half h = i.extra.x;
                half3 base = lerp(_ColA.rgb, _ColB.rgb, i.extra.z) * LB_TOLIN(i.color.rgb) * 1.6;
                half3 alb = base * (0.3 + 0.7 * h) * (1.0 + 0.12 * i.extra.w);
                float3 up = normalize(i.normalWS);
                InputData d = LBInputData(i, up);
                Light L = GetMainLight(d.shadowCoord, d.positionWS, d.shadowMask);
                half sh = L.shadowAttenuation * L.distanceAttenuation;
                half ndl = saturate(dot(d.normalWS, L.direction)) * 0.75 + 0.25;
                half back = pow(saturate(dot(-d.viewDirectionWS, L.direction)), 3.0) * _Trans * h;
                half sheen = pow(saturate(dot(normalize(L.direction + d.viewDirectionWS), d.normalWS)), 24.0) * 0.08 * h;
                half3 c = alb * (L.color * (ndl * sh) + LBTrilight(d.normalWS) * (0.55 + 0.45 * h));
                c += alb * L.color * back * sh * 1.6 + L.color * sheen * sh;
                c = LBFog(c, i.positionWS, d.fogCoord);
                return half4(c, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off
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
            Cull Off
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
