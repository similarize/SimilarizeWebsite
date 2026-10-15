// lambou6 URP port of LB/Water: animated normals from two scrolling noise layers, shallow -> deep colour from the baked sea map,
// probe reflection (fresnel) via URP GlossyEnvironmentReflection, sun glint, rolling shore foam. Sea map + noise are linear.
Shader "LB/Water"
{
    Properties
    {
        _Shallow ("Shallow", Color) = (0.22, 0.86, 0.82, 0.5)
        _Deep ("Deep", Color) = (0.02, 0.36, 0.56, 0.92)
        _FoamColor ("Foam", Color) = (1, 1, 1, 1)
        _SeaMap ("Sea map (linear)", 2D) = "black" {}
        _Noise ("Noise (linear)", 2D) = "gray" {}
        _SeaRect ("Sea map rect (x0, z0, 1/w, 1/h)", Vector) = (0, 0, 0.002, 0.002)
        _WaveScale ("Wave tiling", Float) = 0.045
        _Speed ("Speed", Float) = 1
        [HideInInspector] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 300
        HLSLINCLUDE
        #include "Assets/Shaders/LBURP.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _Shallow, _Deep, _FoamColor, _SeaRect;
            float _WaveScale, _Speed, _Cull;
        CBUFFER_END
        TEXTURE2D(_SeaMap); SAMPLER(sampler_SeaMap);
        TEXTURE2D(_Noise); SAMPLER(sampler_Noise);
        #include "Assets/Shaders/LBPasses.hlsl"
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
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
            #pragma fragment WaterFrag
            half4 WaterFrag(LBVary i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 w = i.positionWS.xz * _WaveScale;
                half3 n1 = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, w + float2(t * 0.021, t * 0.013)).rgb * 2.0 - 1.0;
                half3 n2 = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, w * 2.3 + float2(-t * 0.017, t * 0.027)).rgb * 2.0 - 1.0;
                half2 nn = (n1.xy + n2.yx * 0.7) * 0.32;
                float3 N = normalize(float3(nn.x, 1.0, nn.y));
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float2 suv = (i.positionWS.xz - _SeaRect.xy) * _SeaRect.zw;
                half4 sm = SAMPLE_TEXTURE2D(_SeaMap, sampler_SeaMap, saturate(suv) + nn * 0.002);
                bool inside = suv.x > 0.0 && suv.y > 0.0 && suv.x < 1.0 && suv.y < 1.0;
                half depth = inside ? sm.r : 1.0;
                half shore = inside ? sm.g : 0.0;
                half ndv = saturate(dot(N, V));
                half fres = pow(1.0 - ndv, 4.0);
                half3 col = lerp(_Shallow.rgb, _Deep.rgb, depth);
                float3 R = reflect(-V, N);
                half3 sky = GlossyEnvironmentReflection(R, i.positionWS, 0.0, 1.0, GetNormalizedScreenSpaceUV(i.positionCS));
                col = lerp(col, sky, saturate(0.1 + fres * 0.8));
                InputData d = LBInputData(i, N);
                Light L = GetMainLight(d.shadowCoord, d.positionWS, d.shadowMask);
                float3 H = normalize(L.direction + V);
                col += L.color * pow(saturate(dot(N, H)), 180.0) * 1.4 * L.shadowAttenuation;
                half wave = sin(depth * 46.0 - t * 2.1 + n1.z * 2.5) * 0.5 + 0.5;
                half brk = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, i.positionWS.xz * 0.21 + float2(t * 0.03, -t * 0.02)).b;
                half foam = shore * (0.55 + wave * 0.6) - brk * 0.5;
                foam = smoothstep(0.18, 0.5, foam);
                col = lerp(col, _FoamColor.rgb, foam * 0.9);
                half a = lerp(_Shallow.a, _Deep.a, depth);
                a = saturate(max(a + fres * 0.25, foam));
                col = LBFog(col, i.positionWS, d.fogCoord);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
