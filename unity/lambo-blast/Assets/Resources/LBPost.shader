// Lambo Blast post (lambo7: + ACES filmic tonemap normalised for LDR, per-theme grade, softer vignette): soft-threshold bloom (box down/up chain) + warm, saturated "beach racer" grade,
// soft highlight shoulder, contrast and vignette. Gamma-space LDR (the project renders in Gamma).
Shader "Hidden/LBPost"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Bloom ("Bloom", 2D) = "black" {}
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _Bloom;
    float4 _Params;   // x threshold, y knee, z bloom intensity, w vignette
    float4 _Grade;    // x saturation, y contrast, z warmth, w exposure

    half3 Box4(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d);
        return (tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb +
                tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb) * 0.25;
    }

    half4 fragPre(v2f_img i) : SV_Target
    {
        half3 c = Box4(i.uv, 1.0);
        half br = max(c.r, max(c.g, c.b));
        half soft = clamp(br - _Params.x + _Params.y, 0.0, 2.0 * _Params.y);
        soft = soft * soft / (4.0 * _Params.y + 0.0001);
        half contrib = max(soft, br - _Params.x) / max(br, 0.0001);
        return half4(c * contrib, 1.0);
    }

    half4 fragDown(v2f_img i) : SV_Target { return half4(Box4(i.uv, 1.0), 1.0); }
    half4 fragUp(v2f_img i) : SV_Target { return half4(Box4(i.uv, 0.5), 1.0); }

    float4 _Look;     // lambo7: x filmic exposure (0 = old grade), y vignette inner, z bloom tint mix, w grain
    half3 ACESn(half3 x)
    {
        return (x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14);
    }

    half4 fragFinal(v2f_img i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb;
        c += tex2D(_Bloom, i.uv).rgb * _Params.z;
        if (_Look.x > 0.0)
        {
            // lambo7 filmic: gamma -> ~linear, ACES (Narkowicz) normalised so LDR white stays white (soft shoulder, S-curve)
            half3 l = c * c * _Look.x * 0.6;
            half wl = _Look.x * 0.6;
            l = ACESn(l) / ACESn(half3(wl, wl, wl));
            c = sqrt(max(l, 0.0));
        }
        else c *= _Grade.w;
        c *= half3(1.0 + _Grade.z, 1.0 + _Grade.z * 0.3, 1.0 - _Grade.z * 0.7);
        half lum = dot(c, half3(0.299, 0.587, 0.114));
        c = lerp(half3(lum, lum, lum), c, _Grade.x);
        if (_Look.x <= 0.0) { half3 hc = max(c - 0.78, 0.0); c = c - hc + hc / (1.0 + hc * 2.6); }
        c = saturate((c - 0.45) * _Grade.y + 0.45);
        float2 d = i.uv - 0.5;
        d.x *= 1.2;
        c *= 1.0 - _Params.w * smoothstep(_Look.y, 0.9, dot(d, d) * 2.2);
        return half4(saturate(c), 1.0);
    }
    ENDCG

    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass { CGPROGRAM
               #pragma vertex vert_img
               #pragma fragment fragPre
               ENDCG }
        Pass { CGPROGRAM
               #pragma vertex vert_img
               #pragma fragment fragDown
               ENDCG }
        Pass { Blend One One
               CGPROGRAM
               #pragma vertex vert_img
               #pragma fragment fragUp
               ENDCG }
        Pass { CGPROGRAM
               #pragma vertex vert_img
               #pragma fragment fragFinal
               ENDCG }
    }
    Fallback Off
}
