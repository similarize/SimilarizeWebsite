// Balloon Blast latex balloon: bright translucent body (more opaque towards the rim, like real latex), a sharp sun
// glint plus a broad sheen that stay opaque, soft wrapped diffuse, a coloured rim glow and a little self-glow so the
// balloons read bright and candy-like at any distance. One forward base pass (no per-pixel extra lights).
Shader "BB/Balloon"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.3, 0.3, 1)
        _Opacity ("Centre opacity", Range(0, 1)) = 0.78
        _Glow ("Self glow", Range(0, 1)) = 0.22
        _Rim ("Rim glow", Range(0, 2)) = 0.7
        _Spec ("Glint", Range(0, 4)) = 2.2
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200
        ZWrite Off
        CGPROGRAM
        #pragma surface surf Balloon alpha:fade noforwardadd
        #pragma target 3.0
        fixed4 _Color;
        half _Opacity, _Glow, _Rim, _Spec;
        struct Input { float3 viewDir; };

        half4 LightingBalloon(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
        {
            half nl = saturate(dot(s.Normal, lightDir) * 0.6 + 0.4);
            half3 h = normalize(lightDir + viewDir);
            half nh = saturate(dot(s.Normal, h));
            half spec = (pow(nh, 220.0) * _Spec + pow(nh, 22.0) * 0.28) * atten;
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * nl * (0.55 + 0.45 * atten) + spec * _LightColor0.rgb;
            c.a = saturate(s.Alpha + spec);
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            half fres = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Albedo = _Color.rgb;
            o.Emission = _Color.rgb * (_Glow + fres * fres * _Rim * 0.5);
            o.Alpha = lerp(_Opacity, 1.0, fres * fres);
        }
        ENDCG
    }
    Fallback "Legacy Shaders/VertexLit"
}
