// ffu6j: Four Froggies ranch sky. Drop-in for Skybox/Panoramic (same _MainTex / _Tint / _Exposure / _Rotation, so Realism,
// Worlds storm dimming and GardenDemo keep working) that makes the HDRI read like a real clear-day sky under URP Linear + ACES:
//  - sky-only exposure trim (the game grade's +EV lift had washed the sky to white)
//  - chroma lift that grows towards the zenith: blue sky deepens, low-chroma clouds stay white (the "physical" gradient look)
//  - real sun disc + Mie glow around the main light (HDR -> the bloom gives the soft halo)
//  - horizon haze blended to the current fog colour so the far ranch melts into the sky
//  - optional drifting high wisps (cheap 4-octave value noise, only on the full tier: _FFSky.z = 0 on phones / Tesla)
// Globals _FFSky (x exposure mult, y zenith chroma, z wisps, w sun disc) are set by U6.SkyGlobals; unset -> safe defaults.
Shader "FF/Sky"
{
    Properties
    {
        _Tint ("Tint Color", Color) = (0.5, 0.5, 0.5, 0.5)
        [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
        _Rotation ("Rotation", Range(0, 360)) = 0
        [NoScaleOffset] _MainTex ("Spherical (HDR)", 2D) = "grey" {}
        _Mapping ("Mapping", Float) = 1
        _ImageType ("Image Type", Float) = 0
        _MirrorOnBack ("Mirror on Back", Float) = 0
        _Layout ("3D Layout", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off
        HLSLINCLUDE
        #include "Assets/Shaders/FFURP.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half _Exposure;
            float _Rotation;
            half _Mapping, _ImageType, _MirrorOnBack, _Layout;
        CBUFFER_END
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        float4 _FFSky;
        ENDHLSL
        Pass
        {
            Name "Sky"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SkyVert
            #pragma fragment SkyFrag
            struct SkyA { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct SkyV { float4 positionCS : SV_POSITION; float3 texDir : TEXCOORD0; float3 worldDir : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            float3 RotY(float3 p, float deg)
            {
                float a = deg * 3.14159265 / 180.0, s = sin(a), c = cos(a);
                return float3(c * p.x - s * p.z, p.y, s * p.x + c * p.z);
            }
            SkyV SkyVert(SkyA v)
            {
                SkyV o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 r = RotY(v.positionOS.xyz, _Rotation);
                o.positionCS = TransformWorldToHClip(TransformObjectToWorld(r));
                o.texDir = v.positionOS.xyz;
                o.worldDir = r;
                return o;
            }
            float SkyHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float SkyNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(SkyHash(i), SkyHash(i + float2(1, 0)), f.x), lerp(SkyHash(i + float2(0, 1)), SkyHash(i + float2(1, 1)), f.x), f.y);
            }
            half4 SkyFrag(SkyV i) : SV_Target
            {
                float4 k = _FFSky.x > 0.0 ? _FFSky : float4(0.66, 2.6, 0.0, 1.0);
                float3 td = normalize(i.texDir), wd = normalize(i.worldDir);
                float2 uv = float2(0.5, 1.0) - float2(atan2(td.z, td.x) * (0.5 / 3.14159265), acos(clamp(td.y, -1.0, 1.0)) * (1.0 / 3.14159265));
                half3 c = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).rgb * _Tint.rgb * LB_X2 * _Exposure;
                float el = wd.y, up = saturate(el * 2.5);
                // chroma lift towards the zenith (clouds have little chroma, so they stay white)
                half lum = dot(c, half3(0.2126, 0.7152, 0.0722));
                c = max(0, lum + (c - lum) * lerp(1.45, k.y, saturate(el * 4.0)));   // ffu6k: more blue lower down too (houseout sky still pale)
                half blue = saturate((c.b - c.r) / max(c.b, 1e-3) * 2.5);
                c *= lerp(1.0, 0.86, blue * up);
                c *= k.x;
                // high wisps, drifting slowly with the wind
                if (k.z > 0.001 && el > 0.02)
                {
                    float2 p = wd.xz / (el + 0.12) * 1.3 + float2(_Time.y * 0.006, _Time.y * 0.0025);
                    float n = SkyNoise(p) * 0.5 + SkyNoise(p * 2.03 + 7.1) * 0.25 + SkyNoise(p * 4.1 + 3.3) * 0.125 + SkyNoise(p * 8.3 + 1.7) * 0.0625;
                    float cov = smoothstep(0.5, 0.82, n) * saturate((el - 0.02) * 5.0) * k.z;
                    half3 cl = max(lum * k.x * 1.35, 0.5) * half3(1.0, 0.99, 0.97);
                    c = lerp(c, cl, cov * 0.55 * blue);
                }
                // sun disc + glow (the HDR disc feeds the bloom halo)
                float mu = dot(wd, normalize(_MainLightPosition.xyz));
                if (k.w > 0.001 && _MainLightPosition.y > -0.05)
                {
                    float disc = smoothstep(0.99995, 0.99999, mu);
                    float glow = pow(saturate(mu), 900.0) * 0.9 + pow(saturate(mu), 40.0) * 0.12 + pow(saturate(mu), 6.0) * 0.04;
                    c += _MainLightColor.rgb * (disc * 30.0 + glow) * saturate(el * 20.0 + 1.0) * k.w;
                }
                // horizon haze to the fog colour
                float hz = pow(1.0 - saturate(abs(el)), 6.0);
                c = lerp(c, unity_FogColor.rgb * 1.05, hz * 0.3);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
