// Four Froggies soft stylised skin: wrapped (half-soft) diffuse with a warm sub-surface tint at the terminator,
// a broad soft highlight and a gentle rim, on top of Unity's ambient / SH. Cheap: one forward pass + shadows.
Shader "FF/Skin"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 0.75, 0.3, 1)
        _Gloss ("Gloss", Range(0, 1)) = 0.45
        _Wrap ("Wrap", Range(0, 1)) = 0.5
        _Rim ("Rim", Range(0, 1)) = 0.35
        _SSS ("Subsurface tint", Color) = (1, 0.55, 0.35, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Skin fullforwardshadows
        #pragma target 3.0
        fixed4 _Color, _SSS;
        half _Gloss, _Wrap, _Rim;
        struct Input { float3 viewDir; };

        half4 LightingSkin(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
        {
            half nl = dot(s.Normal, lightDir);
            half diff = saturate((nl + _Wrap) / (1.0 + _Wrap));
            half term = saturate(diff - saturate(nl)) * 0.7;
            half3 h = normalize(lightDir + viewDir);
            half spec = pow(saturate(dot(s.Normal, h)), 12.0 + 120.0 * _Gloss) * _Gloss * 0.55;
            half4 c;
            c.rgb = (s.Albedo * (diff + _SSS.rgb * term) + spec) * _LightColor0.rgb * atten;
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            o.Albedo = _Color.rgb;
            half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Emission = (_Color.rgb * 0.6 + 0.25) * pow(rim, 3.0) * _Rim * 0.55;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
