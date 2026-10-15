// lambou6 (Unity 6.6 URP port): shared helpers for every hand-written LB URP shader.
// Pattern (reused by the Four Froggies port): each shader = HLSLINCLUDE { #include "LBURP.hlsl"; CBUFFER UnityPerMaterial;
// optional vertex hook; #include "LBPasses.hlsl" } + a UniversalForward pass whose fragment fills SurfaceData-like values and
// calls LBShade (URP's UniversalFragmentPBR = main light + shadows + Forward+ cluster lights + SH + reflection probes + SSAO
// + light layers) + ShadowCaster / DepthOnly / DepthNormals passes from LBPasses.hlsl (vertex hook applied in all of them).
#ifndef LB_URP_INCLUDED
#define LB_URP_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// The built-in project was Gamma; the port is Linear + HDR. Gamma-authored maths is kept faithful with these:
//  LB_X2   "mean 0.5 grey detail x2" on an sRGB-decoded sample (Standard's unity_ColorSpaceDouble)
//  LB_GREY neutral detail value (x LB_X2 = 1)
//  LB_TOG  linear sample -> the gamma value the old maths used (for data uses of a colour texture: gloss, snow masks)
//  LB_TOLIN gamma value / vertex colour -> linear; LB_F gamma-space multiplier (0..2) -> linear multiplier
#ifdef UNITY_COLORSPACE_GAMMA
    #define LB_X2 2.0
    #define LB_GREY 0.5
    #define LB_TOG(x) (x)
    #define LB_TOLIN(x) (x)
    #define LB_F(x) (x)
#else
    #define LB_X2 4.59479380
    #define LB_GREY 0.21763764
    #define LB_TOG(x) FastLinearToSRGB(x)
    #define LB_TOLIN(x) FastSRGBToLinear(x)
    #define LB_F(x) pow(abs(x), 2.2)
#endif

struct LBAttr
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 uv0        : TEXCOORD0;
    float2 uv1        : TEXCOORD1;
    half4  color      : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct LBVary
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
    float4 tangentWS  : TEXCOORD2;    // xyz tangent (0 = mesh has none), w sign
    float4 uv         : TEXCOORD3;    // uv0.xy, uv1.xy
    half4  color      : TEXCOORD4;
    half   fogFactor  : TEXCOORD5;
    float4 extra      : TEXCOORD6;    // per-shader (grass: blade height)
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// tangent frame of the interpolated vertex basis; hasT = false for meshes without tangents
void LBFrame(LBVary i, out float3 T, out float3 B, out float3 N, out bool hasT)
{
    N = normalize(i.normalWS);
    T = i.tangentWS.xyz;
    hasT = dot(T, T) > 0.01;
    T = hasT ? normalize(T) : float3(1, 0, 0);
    B = cross(N, T) * (i.tangentWS.w < 0.0 ? -1.0 : 1.0);
}

InputData LBInputData(LBVary i, float3 normalWS)
{
    InputData d = (InputData)0;
    d.positionWS = i.positionWS;
    d.positionCS = i.positionCS;
    d.normalWS = NormalizeNormalPerPixel(normalWS);
    d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
#else
    d.shadowCoord = float4(0, 0, 0, 0);
#endif
    d.fogCoord = InitializeInputDataFog(float4(i.positionWS, 1.0), i.fogFactor);
    d.vertexLighting = half3(0, 0, 0);
    d.bakedGI = SampleSHPixel(half3(0, 0, 0), d.normalWS);
    d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
    d.shadowMask = half4(1, 1, 1, 1);
    return d;
}

// full URP PBR lighting + fog. albedo linear.
half4 LBShade(LBVary i, half3 albedo, half metallic, half smoothness, float3 normalWS, half3 emission, half alpha, half clearCoat, half clearCoatSmooth)
{
    InputData d = LBInputData(i, normalWS);
    SurfaceData s = (SurfaceData)0;
    s.albedo = albedo;
    s.specular = half3(0, 0, 0);
    s.metallic = metallic;
    s.smoothness = smoothness;
    s.normalTS = half3(0, 0, 1);
    s.emission = emission;
    s.occlusion = 1.0;
    s.alpha = alpha;
    s.clearCoatMask = clearCoat;
    s.clearCoatSmoothness = clearCoatSmooth;
    half4 c = UniversalFragmentPBR(d, s);
    c.rgb = MixFog(c.rgb, d.fogCoord);
    c.a = alpha;
    return c;
}

// diffuse-only (old surface "Lambert"): main light with shadows + SH ambient + fog
half4 LBLambert(LBVary i, half3 albedo, float3 normalWS)
{
    InputData d = LBInputData(i, normalWS);
    Light L = GetMainLight(d.shadowCoord, d.positionWS, d.shadowMask);
    half ndl = saturate(dot(d.normalWS, L.direction));
    half3 c = albedo * (L.color * (ndl * L.shadowAttenuation * L.distanceAttenuation) + d.bakedGI);
    c = MixFog(c, d.fogCoord);
    return half4(c, 1);
}

#endif
