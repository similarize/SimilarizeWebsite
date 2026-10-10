// Four Froggies REAL ROOM (ffu18): shared lighting for the photoreal room. The whole room is lit in LINEAR light inside
// these shaders (the project stays in Gamma colour space for the stylised game): albedo is linearised here, lighting comes
// from three baked lightmap groups (moonlit night through the window / the ceiling lamp / the TV, baked in Blender Cycles
// and blended at runtime), box-projected reflection panoramas (same 3 groups) and an L2 SH (from the panoramas) for moving
// things. Output: scene-referred radiance into an HDR target (RRPost does bloom + ACES), or display-referred directly
// (_RRDirect = 1 on the phone tier: exposure + ACES + sRGB encode in the shader, no post pass).
#ifndef RR_COMMON
#define RR_COMMON
#include "UnityCG.cginc"

float _RRDirect, _RRExposure, _RRTime;
float4 _RREnvCol, _RRLampCol, _RRTvCol;   // rgb weights of the three light groups (tv = video colour x brightness)
float4 _RRK;                              // lightmap decode scale per group (x env, y lamp, z tv)
float4 _RRPK;                             // panorama decode scale per group
sampler2D _RRLMEnv, _RRLMLamp, _RRLMTv;
sampler2D _RRPanoEnv, _RRPanoLamp, _RRPanoTv;
float4 _RRBoxMin, _RRBoxMax, _RRProbe;    // world-space room box + probe position (box-projected reflections)
float4 _RRSH[9];                          // combined SH irradiance / pi (same units as the lightmaps)
float4 _RRWaveO;                          // xyz origin of the "turning real" wave, w = radius (m); <0 = all real
float4 _RRWaveCol;                        // edge glow colour

inline float3 SrgbToLin(float3 c) { return c * (c * (c * 0.305306011 + 0.682171111) + 0.012522878); }
inline float3 LinToSrgb(float3 c) { c = max(c, 0.0); float3 s1 = sqrt(c), s2 = sqrt(s1), s3 = sqrt(s2); return saturate(0.662002687 * s1 + 0.684122060 * s2 - 0.323583601 * s3 - 0.0225411470 * c); }
inline float3 ACES(float3 x) { return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14)); }
inline float Lum(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }
// encoded e = sqrt(kL / (1 + kL)) in an 8-bit JPG
inline float3 DecodeRR(float3 e, float k) { float3 x = e * e; return x / max(1.0 - x, 0.004) / k; }

inline float3 SHIrr(float3 n)
{
    float3 r = _RRSH[0].rgb + _RRSH[1].rgb * n.y + _RRSH[2].rgb * n.z + _RRSH[3].rgb * n.x
             + _RRSH[4].rgb * (n.x * n.y) + _RRSH[5].rgb * (n.y * n.z) + _RRSH[6].rgb * (3.0 * n.z * n.z - 1.0)
             + _RRSH[7].rgb * (n.x * n.z) + _RRSH[8].rgb * (n.x * n.x - n.y * n.y);
    return max(r, 0.0);
}

inline float3 BoxProject(float3 R, float3 p)
{
    float3 rbmax = (_RRBoxMax.xyz - p) / R, rbmin = (_RRBoxMin.xyz - p) / R;
    float3 rb = (R > 0.0) ? rbmax : rbmin;
    float t = min(min(rb.x, rb.y), rb.z);
    return normalize(p + R * max(t, 0.0) - _RRProbe.xyz);
}

// panoramas: Blender equirect from the probe, u = 0.5 looks along +x, u grows towards -z; v = elevation
inline float2 PanoUV(float3 d) { return float2(atan2(-d.z, d.x) * 0.15915494 + 0.5, asin(clamp(d.y, -1.0, 1.0)) * 0.31830989 + 0.5); }

inline float3 Pano(float3 d, float rough)
{
    float4 uv = float4(PanoUV(d), 0.0, sqrt(rough) * 6.5);
    return DecodeRR(tex2Dlod(_RRPanoEnv, uv).rgb, _RRPK.x) * _RREnvCol.rgb
         + DecodeRR(tex2Dlod(_RRPanoLamp, uv).rgb, _RRPK.y) * _RRLampCol.rgb
         + DecodeRR(tex2Dlod(_RRPanoTv, uv).rgb, _RRPK.z) * _RRTvCol.rgb;
}

// Karis' mobile environment BRDF approximation
inline float2 EnvBRDF(float rough, float nv)
{
    const float4 c0 = float4(-1, -0.0275, -0.572, 0.022);
    const float4 c1 = float4(1, 0.0425, 1.04, -0.04);
    float4 r = rough * c0 + c1;
    float a004 = min(r.x * r.x, exp2(-9.28 * nv)) * r.x + r.y;
    return float2(-1.04, 1.04) * a004 + r.zw;
}

// how "real" this pixel is during the transformation (1 = real, 0 = the stylised game look)
inline float Realness(float3 wpos)
{
    if (_RRWaveO.w < 0.0) return 1.0;
    return saturate((_RRWaveO.w - distance(wpos, _RRWaveO.xyz)) / 0.45);
}
inline float WaveEdge(float3 wpos)
{
    if (_RRWaveO.w < 0.0 || _RRWaveO.w > 30.0) return 0.0;
    float d = (distance(wpos, _RRWaveO.xyz) - _RRWaveO.w) / 0.14;
    return exp(-d * d);
}
// cartoon version of a lit surface: flat mip-averaged colour, posterised (log-space) lighting
inline float3 Stylise(float3 flatAlb, float3 irr)
{
    float L = max(Lum(irr), 1e-4);
    float q = exp2(floor(log2(L) * 1.6 + 0.5) / 1.6);
    float3 hue = irr / L;
    float3 a = lerp(Lum(flatAlb).xxx, flatAlb, 1.45) * 1.08;
    return a * hue * q * 1.1;
}

inline float4 RROut(float3 col)
{
    if (_RRDirect > 0.5) return float4(LinToSrgb(ACES(col * _RRExposure)), 1.0);
    return float4(min(col, 60000.0), 1.0);
}
#endif
