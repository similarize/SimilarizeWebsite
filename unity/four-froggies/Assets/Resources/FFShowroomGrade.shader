// ffu29 lobby showroom grade (ShowroomGrade.cs): exposure, ACES filmic tonemap (Narkowicz fit, applied in linear via a
// gamma-2 approximation - the project renders in Gamma space), mild contrast, saturation, soft vignette.
Shader "Hidden/FFShowroomGrade"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Grade;   // x exposure, y contrast, z saturation, w vignette

            half3 ACES(half3 x)
            {
                x *= 0.6;
                return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
            }

            half4 frag(v2f_img i) : SV_Target
            {
                half4 src = tex2D(_MainTex, i.uv);
                half3 c = max(src.rgb, 0.0);
                c = c * c;                          // gamma -> ~linear
                c = ACES(c * _Grade.x * 1.55);      // 1.55: keeps mid-grey where it was (ACES fit darkens mids)
                c = sqrt(c);                        // back to gamma
                c = saturate((c - 0.42) * _Grade.y + 0.42);
                half l = dot(c, half3(0.299, 0.587, 0.114));
                c = lerp(half3(l, l, l), c, _Grade.z);
                float2 d = i.uv - 0.5;
                d.x *= 1.15;
                half v = 1.0 - _Grade.w * smoothstep(0.08, 0.62, dot(d, d) * 1.9);
                c *= v;
                return half4(saturate(c), src.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
