// lambou6: URP stand-in for 'Legacy Shaders/Particles/Additive' (2 x vertex colour x _TintColor x texture, fog to black).
Shader "LB/FxAdd"
{
    Properties
    {
        _TintColor ("Tint", Color) = (0.5,0.5,0.5,0.5)
        _MainTex ("Particle", 2D) = "white" {}
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        LOD 300
        HLSLINCLUDE
        #include "Assets/Shaders/LBURP.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _TintColor;
            half _Cull;
        CBUFFER_END
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        #include "Assets/Shaders/LBPasses.hlsl"
        ENDHLSL
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile_fog
            #pragma vertex LBVert
            #pragma fragment AddFrag
            half4 AddFrag(LBVary i) : SV_Target
            {
                half4 c = 2.0 * i.color * _TintColor * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw);
                half f = InitializeInputDataFog(float4(i.positionWS, 1.0), i.fogFactor);
                c.rgb = MixFogColor(c.rgb, half3(0, 0, 0), f);
                return c;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
