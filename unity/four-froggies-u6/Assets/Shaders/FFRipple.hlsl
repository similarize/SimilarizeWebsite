// ffu38: shared pond-ripple sampling (ffu6: URP HLSL copy of FFRipple.cginc) for FF/RealWater + LB/Water (Core/WaveSim.cs sets the globals).
//   _FFRipTex   wave sim state (R height m, G previous, B silt (ffu39; was foam), A spray (ffu39)), window rect _FFRipRect
//               (x0, z0, 1/size, on)
//   _FFRipP     (1/res, metres per texel, patch on, normal gain)
//   _FFSwell[4] analytic big slow swells from big mechs: (x, z, front radius m, amp m); _FFSwellK[4] (k, width m, r0, 0)
//   ffu39: _FFCont[8] live contacts (x, z, r, k) for the meniscus; _FFPond (cx, cz, 1/rx, 1/rz) + _FFPondQ (fade q0, q1)
//          mask every pond surface (plane, patch, displacement) to the shoreline ellipse.
#ifndef FF_RIPPLE_HLSL
#define FF_RIPPLE_HLSL
TEXTURE2D(_FFRipTex); SAMPLER(sampler_FFRipTex);
float4 _FFRipRect, _FFRipP;
float4 _FFSwell[4], _FFSwellK[4];
float4 _FFCont[8];
float4 _FFPond, _FFPondQ;

float2 FFRipUV(float2 xz) { return (xz - _FFRipRect.xy) * _FFRipRect.z; }
float FFRipIn(float2 uv) { float2 e = saturate(min(uv, 1.0 - uv) * 14.0); return e.x * e.y; }

// ffu39: 1 inside the pond's shoreline ellipse, 0 outside (soft over _FFPondQ.x..y); 1 everywhere if not set
float FFPondQ(float2 xz) { float2 d = (xz - _FFPond.xy) * _FFPond.zw; return length(d); }
float FFPondMask(float2 xz)
{
    if (_FFPond.z <= 0.0) return 1.0;
    return 1.0 - smoothstep(_FFPondQ.x, _FFPondQ.y, FFPondQ(xz));
}

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

// fragment: returns (dhdx, dhdz, spray, height); silt = suspended sediment 0..1 (ffu39)
float4 FFRippleS(float2 xz, out float silt)
{
    float4 o = 0;
    silt = 0;
    if (_FFRipRect.w > 0.5)
    {
        float2 uv = FFRipUV(xz);
        float m = FFRipIn(uv);
        if (m > 0.0)
        {
            float2 d = float2(_FFRipP.x, 0);
            // tex2Dlod: no mips, and no gradient ops inside the (uniform) branch
            float4 c = SAMPLE_TEXTURE2D_LOD(_FFRipTex, sampler_FFRipTex, uv, 0);
            float hl = SAMPLE_TEXTURE2D_LOD(_FFRipTex, sampler_FFRipTex, uv - d.xy, 0).r, hr = SAMPLE_TEXTURE2D_LOD(_FFRipTex, sampler_FFRipTex, uv + d.xy, 0).r;
            float hb = SAMPLE_TEXTURE2D_LOD(_FFRipTex, sampler_FFRipTex, uv - d.yx, 0).r, ht = SAMPLE_TEXTURE2D_LOD(_FFRipTex, sampler_FFRipTex, uv + d.yx, 0).r;
            float inv = 0.5 / _FFRipP.y;
            o.xy = float2(hr - hl, ht - hb) * inv * m;
            o.z = c.a * m;
            o.w = c.r * m;
            silt = c.b * m;
        }
    }
    float3 sw = FFSwell(xz);
    o.xy += sw.yz; o.w += sw.x;
    o.xy *= _FFRipP.w;
    // ffu39: real water is never steeper than ~25 deg in ripples - keeps reflections distorted, not shattered
    float gl = length(o.xy);
    if (gl > 0.45) o.xy *= 0.45 / gl;
    float pm = FFPondMask(xz);
    o.xyw *= pm; silt *= pm;
    return o;
}
float4 FFRipple(float2 xz) { float s; return FFRippleS(xz, s); }

// ffu39: meniscus round live contacts: x = thin bright raised line just outside the edge, y = dark wet contact band
float2 FFContact(float2 xz)
{
    float2 o = 0;
    for (int i = 0; i < 8; i++)
    {
        float4 c = _FFCont[i];
        if (c.z <= 0.0) continue;
        float d = length(xz - c.xy) - c.z * 0.82;
        float w = 0.05 + c.z * 0.03;
        o.x = max(o.x, exp(-(d - w) * (d - w) / (w * w * 0.5)) * c.w);
        o.y = max(o.y, saturate(1.0 - abs(d) / (w * 1.6)) * c.w);
    }
    return o;
}

// vertex: height only (the near-view patch mesh)
float FFRippleH(float2 xz)
{
    float h = 0;
    if (_FFRipRect.w > 0.5)
    {
        float2 uv = FFRipUV(xz);
        h = SAMPLE_TEXTURE2D_LOD(_FFRipTex, sampler_FFRipTex, uv, 0).r * FFRipIn(uv);
    }
    return (h + FFSwell(xz).x) * FFPondMask(xz);
}
#endif
