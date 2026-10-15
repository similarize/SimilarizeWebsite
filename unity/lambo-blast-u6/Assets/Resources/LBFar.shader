// lambou6 URP port of lambo9 LB/Far: distant horizon ring, vertex colour, soft sun lambert, aerial-perspective haze towards the
// fog colour (no scene fog: it would erase the ring at 1 km).
Shader "LB/Far"
{
    Properties
    {
        _Haze ("Haze", Range(0, 1)) = 0.5
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" "RenderType" = "Opaque" }
        LOD 300
        HLSLINCLUDE
        #include "Assets/Shaders/LBURP.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half _Haze, _Cull;
        CBUFFER_END
        #include "Assets/Shaders/LBPasses.hlsl"
        ENDHLSL
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile_fog
            #pragma vertex LBVert
            #pragma fragment FarFrag
            half4 FarFrag(LBVary i) : SV_Target
            {
                Light L = GetMainLight();
                half lit = 0.62 + 0.38 * abs(dot(normalize(i.normalWS), L.direction));
                half3 c = LB_TOLIN(i.color.rgb) * LB_F(lit) * L.color * 1.1;
                half h = saturate(_Haze * (1.25 - 0.55 * i.color.a));
                c = lerp(c, unity_FogColor.rgb, h);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
