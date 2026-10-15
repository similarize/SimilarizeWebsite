// u6d LB/Shafts: full-screen pass (URP FullScreenPassRendererFeature, before post) = screen-space god rays (radial blur of the
// bright sky pixels toward the sun's screen position, occluded by anything with depth) + heat haze (lambo9's Canyon / Volcano UV
// shimmer, restored). Globals from Gfx6: _LBSunSS (sun uv xy, visibility, intensity), _LBShaftCol, _LBHaze (strength, speed).
Shader "LB/Shafts"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off
        Pass
        {
            Name "LBShafts"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment ShaftFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            float4 _LBSunSS, _LBShaftCol, _LBHaze;

            half SkyMask(float2 uv)
            {
                float d = SampleSceneDepth(uv);
                return Linear01Depth(d, _ZBufferParams) > 0.995 ? 1.0 : 0.0;
            }

            half4 ShaftFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                // heat haze: shimmer grows with distance, strongest just above the ground line
                float raw = SampleSceneDepth(uv);
                float lin = Linear01Depth(raw, _ZBufferParams);
                float ds = saturate(lin * 12.0) * (lin > 0.995 ? 0.35 : 1.0);
                float t = _Time.y * _LBHaze.y;
                float2 off = float2(sin(uv.y * 160.0 + t * 3.1) + sin(uv.y * 71.0 - t * 2.2), cos(uv.x * 120.0 + t * 2.6)) * 0.0012 * _LBHaze.x * ds;
                half3 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + off).rgb;
                // god rays
                const int NS = 20;
                float2 dl = (_LBSunSS.xy - uv) * (0.85 / NS);
                float2 su = uv;
                half dec = 1.0, acc = 0.0;
                [unroll] for (int k = 0; k < NS; k++)
                {
                    su += dl;
                    float2 cu = saturate(su);
                    half3 s = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, cu, 0).rgb;
                    float sd = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_PointClamp, cu, 0).r;
                    half sky = Linear01Depth(sd, _ZBufferParams) > 0.995 ? 1.0 : 0.0;
                    half lum = dot(s, half3(0.2126, 0.7152, 0.0722));
                    acc += sky * saturate(lum - 0.55) * dec;
                    dec *= 0.93;
                }
                float2 sv = (uv - _LBSunSS.xy) * float2(_ScreenParams.x / _ScreenParams.y, 1.0);
                half fall = 1.0 / (1.0 + dot(sv, sv) * 3.0);
                c += _LBShaftCol.rgb * (acc / NS) * _LBSunSS.z * _LBSunSS.w * fall * 2.2;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
