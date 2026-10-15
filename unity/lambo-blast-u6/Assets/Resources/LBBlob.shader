// lambou6 URP port of lambo9 LB/Blob: contact shadow / AO pad, multiply blend, fades to white with fog. _Strength via MPB.
Shader "LB/Blob"
{
    Properties
    {
        _MainTex ("Shape", 2D) = "white" {}
        _Strength ("Strength", Range(0, 1)) = 0.78
        _Shade ("Shade", Color) = (0.18, 0.19, 0.22, 1)
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-20" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 300
        HLSLINCLUDE
        #include "Assets/Shaders/LBURP.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Shade;
            half _Strength, _Cull;
        CBUFFER_END
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        #include "Assets/Shaders/LBPasses.hlsl"
        ENDHLSL
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Offset -2, -2
            Blend DstColor Zero
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile_fog
            #pragma vertex LBVert
            #pragma fragment BlobFrag
            half4 BlobFrag(LBVary i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy).a * _Strength;
                half3 c = lerp(half3(1, 1, 1), LB_TOG(_Shade.rgb), a);       // gamma-space multiply as before ...
                half f = InitializeInputDataFog(float4(i.positionWS, 1.0), i.fogFactor);
                c = MixFogColor(c, half3(1, 1, 1), f);
                return half4(LB_TOLIN(c), 1);                                // ... converted so the darkening matches
            }
            ENDHLSL
        }
    }
    Fallback Off
}
