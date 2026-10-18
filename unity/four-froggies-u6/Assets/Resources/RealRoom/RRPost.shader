// REAL ROOM post (desktop / Xbox tier): HDR bloom (prefilter + down / up chain), exposure, ACES filmic, vignette,
// fade, sRGB encode with dither. The phone tier skips this (shaders output display-referred colour directly).
Shader "Hidden/FF/RRPost"
{
    Properties { _MainTex ("", 2D) = "black" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex, _Bloom; float4 _MainTex_TexelSize;
    float _Threshold, _BloomK, _Exposure, _Vignette, _Grain; float4 _Fade;
    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert (appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
    float3 Box4(float2 uv, float d) { float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d); return (tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb) * 0.25; }
    float3 LinToSrgbP(float3 c) { c = max(c, 0.0); float3 s1 = sqrt(c), s2 = sqrt(s1), s3 = sqrt(s2); return saturate(0.662002687 * s1 + 0.684122060 * s2 - 0.323583601 * s3 - 0.0225411470 * c); }
    float3 ACESP(float3 x) { return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14)); }
    ENDCG
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass // 0 prefilter (exposure-relative threshold) + 4-tap down
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment f
            float4 f (v2f i) : SV_Target { float3 c = Box4(i.uv, 1.0) * _Exposure; float br = max(c.r, max(c.g, c.b)); float k = max(br - _Threshold, 0.0); k = k * k / (br + 1e-4) ; return float4(min(c * k / max(br, 1e-4), 40.0), 1); }
            ENDCG
        }
        Pass // 1 down
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment f
            float4 f (v2f i) : SV_Target { return float4(Box4(i.uv, 1.0), 1); }
            ENDCG
        }
        Pass // 2 up (additive)
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment f
            float4 f (v2f i) : SV_Target { return float4(Box4(i.uv, 0.5) * 0.7, 1); }
            ENDCG
        }
        Pass // 3 final
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment f
            float4 f (v2f i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb * _Exposure + tex2D(_Bloom, i.uv).rgb * _BloomK;
                c = ACESP(c);
                float2 q = i.uv - 0.5; c *= 1.0 - _Vignette * dot(q, q) * 1.6;
                c = LinToSrgbP(c);
                float n = frac(sin(dot(i.uv * _ScreenParams.xy + _Time.y, float2(12.9898, 78.233))) * 43758.5453);
                c += (n - 0.5) * (1.0 / 255.0 + _Grain);
                c = lerp(c, _Fade.rgb, _Fade.a);
                return float4(c, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
