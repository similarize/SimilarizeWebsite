// ffu38: shared pond-ripple sampling for FF/RealWater + LB/Water (Core/WaveSim.cs sets the globals).
//   _FFRipTex   wave sim state (R height m, B foam), window rect _FFRipRect (x0, z0, 1/size, on)
//   _FFRipP     (1/res, metres per texel, patch on, normal gain)
//   _FFSwell[4] analytic big slow swells from big mechs: (x, z, front radius m, amp m); _FFSwellK[4] (k, width m, r0, 0)
#ifndef FF_RIPPLE_INCLUDED
#define FF_RIPPLE_INCLUDED
sampler2D _FFRipTex;
float4 _FFRipRect, _FFRipP;
float4 _FFSwell[4], _FFSwellK[4];

float2 FFRipUV(float2 xz) { return (xz - _FFRipRect.xy) * _FFRipRect.z; }
float FFRipIn(float2 uv) { float2 e = saturate(min(uv, 1.0 - uv) * 14.0); return e.x * e.y; }

// swell height + gradient (xz) at a point
float3 FFSwell(float2 xz)
{
    float3 o = 0;
    for (int i = 0; i < 4; i++)
    {
        float4 s = _FFSwell[i]; float4 k = _FFSwellK[i];
        if (s.w <= 0.0) continue;
        float2 dv = xz - s.xy;
        float r = length(dv) + 1e-3;
        float d = r - k.z - s.z;
        float env = exp(-(d * d) / (k.y * k.y)) * s.w / (1.0 + max(r - k.z, 0.0) * 0.035);
        float ph = k.x * d;
        o.x += env * cos(ph);
        o.yz += (-env * k.x * sin(ph)) * (dv / r);
    }
    return o;
}

// fragment: height + gradient (xz) + foam. Returns (dhdx, dhdz, foam, height)
float4 FFRipple(float2 xz)
{
    float4 o = 0;
    if (_FFRipRect.w > 0.5)
    {
        float2 uv = FFRipUV(xz);
        float m = FFRipIn(uv);
        if (m > 0.0)
        {
            float2 d = float2(_FFRipP.x, 0);
            // tex2Dlod: no mips, and no gradient ops inside the (uniform) branch
            float4 c = tex2Dlod(_FFRipTex, float4(uv, 0, 0));
            float hl = tex2Dlod(_FFRipTex, float4(uv - d.xy, 0, 0)).r, hr = tex2Dlod(_FFRipTex, float4(uv + d.xy, 0, 0)).r;
            float hb = tex2Dlod(_FFRipTex, float4(uv - d.yx, 0, 0)).r, ht = tex2Dlod(_FFRipTex, float4(uv + d.yx, 0, 0)).r;
            float inv = 0.5 / _FFRipP.y;
            o.xy = float2(hr - hl, ht - hb) * inv * m;
            o.z = c.b * m;
            o.w = c.r * m;
        }
    }
    float3 sw = FFSwell(xz);
    o.xy += sw.yz; o.w += sw.x;
    o.xy *= _FFRipP.w;
    return o;
}

// vertex: height only (the near-view patch mesh)
float FFRippleH(float2 xz)
{
    float h = 0;
    if (_FFRipRect.w > 0.5)
    {
        float2 uv = FFRipUV(xz);
        h = tex2Dlod(_FFRipTex, float4(uv, 0, 0)).r * FFRipIn(uv);
    }
    return h + FFSwell(xz).x;
}
#endif
