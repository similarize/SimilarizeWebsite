// ffu38: GPU height-field ripple simulation for the ranch pond (Core/WaveSim.cs). A square window of the pond around the
// view (96 m / 256 px desktop, 80 m / 160 px phones) holds R = height (m), G = previous height, B = foam 0..1.
// One pass = one 30 Hz step of the damped 2D wave equation, plus up to 20 sources stamped in:
//   mode 0  additive kick (a footfall / splash): h += amount * gaussian            (one step only)
//   mode 1  press (a leg / hull / body crossing the surface): h eases toward -depth inside the gaussian, so a moving body
//           pushes a bow wave + V wake out and a still one only sits in a small dip
// Foam: deposited by sources (a contact ring at radius .z of the B vector or a gaussian), whitecaps where crests get high,
// spreads a little and fades. The window can move: _Shift.xy re-centres the content (texel-snapped). Absorbing border.
Shader "Hidden/FFWaveSim"
{
    Properties { _MainTex ("State", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _Shift;    // xy uv shift, z wave k (c^2 dt^2 / dx^2), w damping per step
            float4 _Foam;     // x decay per step, y spread 0..1, z crest height (m), w crest gain
            float4 _SrcA[20]; // uv.x, uv.y, radius (uv), amount (m)
            float4 _SrcB[20]; // mode (0 kick, 1 press), foam 0..1, foam ring radius (uv, 0 = disc), press strength
            float _SrcN;

            float4 S(float2 uv)
            {
                float4 c = tex2Dlod(_MainTex, float4(uv, 0, 0));
                float2 o = step(0.0, uv) * step(uv, 1.0);
                return c * (o.x * o.y);
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv + _Shift.xy;
                float2 d = _MainTex_TexelSize.xy;
                float4 c = S(uv);
                float4 l = S(uv - float2(d.x, 0)), r = S(uv + float2(d.x, 0));
                float4 b = S(uv - float2(0, d.y)), t = S(uv + float2(0, d.y));
                float h = c.r, hp = c.g;
                float lap = l.r + r.r + b.r + t.r - 4.0 * h;
                float hn = (2.0 * h - hp + _Shift.z * lap) * _Shift.w;
                float foam = lerp(c.b, (l.b + r.b + b.b + t.b) * 0.25, _Foam.y) * _Foam.x;
                foam += saturate((h - _Foam.z) * _Foam.w) * 0.06;           // whitecaps on high crests
                int n = (int)_SrcN;
                for (int k = 0; k < 20; k++)
                {
                    if (k >= n) break;
                    float4 A = _SrcA[k], B = _SrcB[k];
                    float dist = length(i.uv - A.xy);
                    float q = dist / max(A.z, 1e-5);
                    float g = exp(-q * q * 2.0);
                    if (B.x < 0.5) hn += A.w * g;
                    else hn = lerp(hn, A.w, saturate(g * B.w));
                    float rq = (dist - B.z) / max(A.z * 0.45, 1e-5);
                    float fr = B.z > 0.0 ? exp(-rq * rq) : g;
                    foam = max(foam, B.y * fr);
                }
                // absorbing border: waves fade out over the last ~6% of the window instead of bouncing back
                float2 e = min(i.uv, 1.0 - i.uv);
                float edge = saturate(min(e.x, e.y) * 16.0);
                hn *= lerp(0.86, 1.0, edge);
                hn = clamp(hn, -3.0, 3.0);
                return float4(hn, h * lerp(0.86, 1.0, edge), saturate(foam), 0);
            }
            ENDCG
        }
    }
    Fallback Off
}
