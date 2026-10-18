// Lambo Blast post: soft-threshold bloom (box down/up chain) + warm, saturated "beach racer" grade,
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

    half4 fragFinal(v2f_img i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb;
        c += tex2D(_Bloom, i.uv).rgb * _Params.z;
        c *= _Grade.w;
        c *= half3(1.0 + _Grade.z, 1.0 + _Grade.z * 0.3, 1.0 - _Grade.z * 0.7);
        half l = dot(c, half3(0.299, 0.587, 0.114));
        c = lerp(half3(l, l, l), c, _Grade.x);
        half3 hc = max(c - 0.78, 0.0);
        c = c - hc + hc / (1.0 + hc * 2.6);
        c = saturate((c - 0.5) * _Grade.y + 0.5);
        float2 d = i.uv - 0.5;
        c *= 1.0 - dot(d, d) * _Params.w;
        return half4(c, 1.0);
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
