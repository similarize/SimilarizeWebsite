// lambo9 car paint: Standard metallic base coat + a clear-coat layer on top: Schlick fresnel reflection of the reflection
// probe (sky / track at grazing angles, deep colour face-on) and a tight sun glint on the lacquer. Gamma-space project.
Shader "LB/Paint"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0, 0, 1)
        _Glossiness ("Base smoothness", Range(0, 1)) = 0.7
        _Metallic ("Metal flake", Range(0, 1)) = 0.35
        _Clear ("Clear coat", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        fixed4 _Color;
        half _Glossiness, _Metallic, _Clear;
        struct Input { float3 worldNormal; float3 worldPos; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = _Color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            float3 N = normalize(IN.worldNormal), V = normalize(_WorldSpaceCameraPos - IN.worldPos);
            half nv = saturate(dot(N, V));
            half F = 0.04 + 0.96 * pow(1.0 - nv, 5.0);
            float3 R = reflect(-V, N);
            half4 env = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, R, 0.6);
            half3 envc = DecodeHDR(env, unity_SpecCube0_HDR);
            float3 L = normalize(_WorldSpaceLightPos0.xyz);
            half glint = pow(saturate(dot(N, normalize(L + V))), 700.0) * 2.5;
            o.Emission = (envc * F * 0.9 + _LightColor0.rgb * glint * 0.5) * _Clear;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
