// lambou6 URP port of lambo8 LB/Grass (Lambert blades, vertex colour, darker roots, wind sway, distance shrink). No ShadowCaster
// (the Built-in version had none either).
Shader "LB/Grass"
{
    Properties
    {
        _Fade ("Fade start / end (m)", Vector) = (40, 70, 0, 0)
        _Wind ("Wind", Float) = 1
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        LOD 300
        HLSLINCLUDE
        #include "Assets/Shaders/LBURP.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _Fade;
            half _Wind, _Cull;
        CBUFFER_END
        // blade meshes are chunked at the world origin (object space = world space). Every blade shrinks into its root
        // (uv0.y height + uv1 xz) between _Fade.x and _Fade.y metres: distance LOD without popping. No geometry shader
        // (WebGPU has none): plain vertex displacement on pre-built blade meshes, identical in every pass.
        #define LB_VERTEX_MOD 1
        void LBVertexMod(inout float3 pos, inout float3 nrm, inout float4 extra, LBAttr a)
        {
            float h = a.uv0.x;
            float3 root = float3(a.uv1.x, a.uv0.y, a.uv1.y);
            float d = distance(_WorldSpaceCameraPos.xz, root.xz);
            float s = saturate((_Fade.y - d) / max(_Fade.y - _Fade.x, 1.0));
            float3 wp = root + (pos - root) * s;
            float ph = _Time.y * 1.9 + root.x * 0.23 + root.z * 0.19;
            float sway = (sin(ph) * 0.6 + sin(ph * 2.3 + 1.7) * 0.25) * _Wind * h * h * s;
            wp.xz += float2(0.16, 0.1) * sway;
            pos = wp;
            nrm = float3(0, 1, 0);
            extra = float4(h, s, 0, 0);
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
            #pragma fragment GrassFrag
            half4 GrassFrag(LBVary i) : SV_Target
            {
                if (i.extra.y <= 0.0) discard;
                half3 alb = LB_TOLIN(i.color.rgb) * LB_F(0.45 + 0.55 * i.extra.x);
                return LBLambert(i, alb, float3(0, 1, 0));
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
            #pragma vertex LBVert
            #pragma fragment LBDepthNormalsFrag
            ENDHLSL
        }
    }
    Fallback Off
}
