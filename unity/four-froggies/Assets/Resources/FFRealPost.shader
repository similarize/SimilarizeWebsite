// ?realism=1 test post (RealPost.cs): bloom chain (threshold on HDR highlights), depth-only SSAO with normals rebuilt from
// depth (half res + blur), ACES filmic tonemap (Narkowicz fit) on the gamma-space HDR buffer, light grade, soft vignette.
Shader "Hidden/FFRealPost"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Bloom ("Bloom", 2D) = "black" {}
        _AO ("AO", 2D) = "white" {}
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _Bloom, _AO;
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
    float4 _Params;   // x threshold, y knee, z bloom, w vignette
    float4 _Grade;    // x saturation, y exposure, z hdr (1) / ldr (0)
    float4 _ProjInfo; // x 1/P00, y 1/P11, z AO radius (m), w AO strength
    float _AOOn;

    half3 Box4(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d);
        return (tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb +
                tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb) * 0.25;
    }

    half4 fragPre(v2f_img i) : SV_Target
    {
        half3 c = min(Box4(i.uv, 1.0), 12.0);
        half br = max(c.r, max(c.g, c.b));
        half soft = clamp(br - _Params.x + _Params.y, 0.0, 2.0 * _Params.y);
        soft = soft * soft / (4.0 * _Params.y + 0.0001);
        half contrib = max(soft, br - _Params.x) / max(br, 0.0001);
        return half4(c * contrib, 1.0);
    }
    half4 fragDown(v2f_img i) : SV_Target { return half4(Box4(i.uv, 1.0), 1.0); }
    half4 fragUp(v2f_img i) : SV_Target { return half4(Box4(i.uv, 0.5), 1.0); }

    float EyeDepth(float2 uv) { return LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv)); }
    float3 ViewPos(float2 uv, float d) { return float3((uv * 2.0 - 1.0) * _ProjInfo.xy * d, d); }
    static const float3 K[10] = {
        float3(0.53, 0.18, 0.31), float3(-0.41, 0.42, 0.22), float3(0.08, -0.61, 0.45), float3(-0.22, -0.18, 0.12),
        float3(0.71, -0.33, 0.58), float3(-0.66, -0.52, 0.36), float3(0.19, 0.83, 0.41), float3(-0.09, 0.27, 0.76),
        float3(0.36, 0.05, 0.11), float3(-0.85, 0.12, 0.47) };
    float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

    half4 fragAO(v2f_img i) : SV_Target
    {
        float d = EyeDepth(i.uv);
        if (d > 120.0) return half4(1, 1, 1, 1);
        float3 P = ViewPos(i.uv, d);
        float3 N = normalize(cross(ddy(P), ddx(P)));
        if (dot(N, P) > 0) N = -N;
        float a = Hash(i.uv * _ScreenParams.xy) * 6.2832;
        float3 rv = float3(cos(a), sin(a), 0.37);
        float3 T = normalize(rv - N * dot(rv, N));
        float3 B = cross(N, T);
        float R = _ProjInfo.z * (1.0 + d * 0.02);
        float occ = 0.0;
        [unroll] for (int k = 0; k < 10; k++)
        {
            float3 s = P + (T * K[k].x + B * K[k].y + N * K[k].z) * R;
            float2 suv = (s.xy / s.z) / _ProjInfo.xy * 0.5 + 0.5;
            float sd = EyeDepth(suv);
            float range = saturate(R / max(abs(d - sd), 0.001));
            occ += (sd < s.z - 0.03 * (1.0 + d * 0.05) ? 1.0 : 0.0) * range;
        }
        float ao = 1.0 - occ / 10.0;
        ao = lerp(1.0, ao, saturate((120.0 - d) / 40.0));
        return half4(ao, ao, ao, 1);
    }

    half4 fragBlur(v2f_img i) : SV_Target
    {
        float2 o = _MainTex_TexelSize.xy;
        half a = tex2D(_MainTex, i.uv).r * 0.4;
        a += tex2D(_MainTex, i.uv + float2(o.x * 1.5, o.y * 0.5)).r * 0.15;
        a += tex2D(_MainTex, i.uv - float2(o.x * 1.5, o.y * 0.5)).r * 0.15;
        a += tex2D(_MainTex, i.uv + float2(-o.x * 0.5, o.y * 1.5)).r * 0.15;
        a += tex2D(_MainTex, i.uv - float2(-o.x * 0.5, o.y * 1.5)).r * 0.15;
        return half4(a, a, a, 1);
    }

    half3 ACES(half3 x)
    {
        return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
    }

    half4 fragFinal(v2f_img i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb;
        if (_AOOn > 0.5)
        {
            half ao = tex2D(_AO, i.uv).r;
            c *= lerp(1.0, ao, _ProjInfo.w);
        }
        c += tex2D(_Bloom, i.uv).rgb * _Params.z;
        if (_Grade.z > 0.5)
        {
            half3 lin = pow(max(c, 0.0), 2.2) * _Grade.y;
            c = pow(ACES(lin), 1.0 / 2.2);
        }
        else
        {
            c = saturate(c * (_Grade.y + 0.06));
            c = lerp(c, c * c * (3.0 - 2.0 * c), 0.22);   // gentle filmic S on the LDR tier
        }
        half l = dot(c, half3(0.2126, 0.7152, 0.0722));
        c = lerp(half3(l, l, l), c, _Grade.x);
        float2 d = i.uv - 0.5;
        c *= 1.0 - dot(d, d) * _Params.w;
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
               #pragma fragment fragAO
               #pragma target 3.0
               ENDCG }
        Pass { CGPROGRAM
               #pragma vertex vert_img
               #pragma fragment fragBlur
               ENDCG }
        Pass { CGPROGRAM
               #pragma vertex vert_img
               #pragma fragment fragFinal
               ENDCG }
    }
    Fallback Off
}
