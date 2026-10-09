// Four Froggies underwater surfaces: Lambert + albedo texture, with animated world-space caustics (two scrolling
// layers of a Worley-edge pattern, min-combined so the web shimmers) added on up-facing surfaces and fading with depth.
Shader "FF/Underwater"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Caustics ("Caustics", 2D) = "black" {}
        _CausticColor ("Caustic colour", Color) = (0.75, 0.95, 1.0, 1)
        _CausticStrength ("Caustic strength", Range(0, 2)) = 0.55
        _CausticScale ("Caustic tiling (per metre)", Float) = 0.11
        _SurfaceY ("Water surface height", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Lambert
        #pragma target 3.0
        sampler2D _MainTex, _Caustics;
        fixed4 _Color, _CausticColor;
        half _CausticStrength, _CausticScale, _SurfaceY;
        struct Input { float2 uv_MainTex; float3 worldPos; float3 worldNormal; };

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            float t = _Time.y;
            float2 w = IN.worldPos.xz * _CausticScale;
            half a = tex2D(_Caustics, w + float2(t * 0.031, t * 0.017)).r;
            half b = tex2D(_Caustics, w * 1.37 + float2(-t * 0.023, t * 0.029)).r;
            half caus = min(a, b) * 1.6;
            half up = saturate(IN.worldNormal.y * 0.8 + 0.2);
            half depthFade = saturate(1.0 - (_SurfaceY - IN.worldPos.y) / 45.0);
            o.Emission = _CausticColor.rgb * caus * _CausticStrength * up * (0.35 + 0.65 * depthFade) * (0.5 + 0.5 * c.rgb);
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
