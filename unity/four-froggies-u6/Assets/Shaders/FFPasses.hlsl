// ffu6 (copied from Lambo Blast u6 LBPasses.hlsl): shared vertex function + ShadowCaster / DepthOnly / DepthNormals fragments for the LB URP shaders.
// Include AFTER the shader's CBUFFER (and after its optional vertex hook: #define LB_VERTEX_MOD + void LBVertexMod(...)).
#ifndef LB_PASSES_INCLUDED
#define LB_PASSES_INCLUDED

float3 _LightDirection;
float3 _LightPosition;

LBVary LBVertCore(LBAttr a, bool shadowPass)
{
    LBVary o = (LBVary)0;
    UNITY_SETUP_INSTANCE_ID(a);
    UNITY_TRANSFER_INSTANCE_ID(a, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 pos = a.positionOS.xyz;
    float3 nrm = a.normalOS;
    float4 extra = float4(0, 0, 0, 0);
#ifdef LB_VERTEX_MOD
    LBVertexMod(pos, nrm, extra, a);
#endif
    float3 wp = TransformObjectToWorld(pos);
    o.positionWS = wp;
    o.normalWS = TransformObjectToWorldNormal(nrm);
    float tl = dot(a.tangentOS.xyz, a.tangentOS.xyz);
    o.tangentWS = tl > 0.0001 ? float4(TransformObjectToWorldDir(a.tangentOS.xyz), a.tangentOS.w * GetOddNegativeScale()) : float4(0, 0, 0, 1);
    o.uv = float4(a.uv0, a.uv1);
    o.color = a.color;
    o.extra = extra;
    if (shadowPass)
    {
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
        float3 ld = normalize(_LightPosition - wp);
    #else
        float3 ld = _LightDirection;
    #endif
        o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(wp, o.normalWS, ld)));
    }
    else o.positionCS = TransformWorldToHClip(wp);
    o.fogFactor = ComputeFogFactor(o.positionCS.z);
    return o;
}

LBVary LBVert(LBAttr a) { return LBVertCore(a, false); }
LBVary LBShadowVert(LBAttr a) { return LBVertCore(a, true); }

half4 LBShadowFrag(LBVary i) : SV_TARGET { return 0; }
half4 LBDepthFrag(LBVary i) : SV_TARGET { return i.positionCS.z; }

void LBDepthNormalsFrag(LBVary i, out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    float3 n = normalize(i.normalWS);
#if defined(_GBUFFER_NORMALS_OCT)
    float2 oct = PackNormalOctQuadEncode(n);
    outNormalWS = half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0.0);
#else
    outNormalWS = half4(n, 0.0);
#endif
#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
