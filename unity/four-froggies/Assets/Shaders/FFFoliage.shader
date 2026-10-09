// Four Froggies foliage (Quaternius leaf cards): alpha-cut, double-sided, half-Lambert so back faces stay soft,
// and a small two-frequency wind sway in the vertex shader (world-space phase, so merged meshes sway unevenly).
Shader "FF/Foliage"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Leaves (RGBA)", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.45
        _Wind ("Wind", Range(0, 0.3)) = 0.06
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        LOD 200
        Cull Off
        CGPROGRAM
        #pragma surface surf Folia vertex:vert alphatest:_Cutoff addshadow
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        half _Wind;
        struct Input { float2 uv_MainTex; };

        void vert(inout appdata_full v)
        {
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float t = _Time.y;
            float s = sin(t * 1.3 + wp.x * 0.21 + wp.z * 0.17) + 0.4 * sin(t * 3.1 + wp.y * 0.9 + wp.x * 0.5);
            float3 off = float3(s, 0.15 * s, s * 0.6) * _Wind;
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, off);
        }

        half4 LightingFolia(SurfaceOutput s, half3 lightDir, half atten)
        {
            half d = abs(dot(s.Normal, lightDir)) * 0.6 + 0.4;
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * d * atten;
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    Fallback "Legacy Shaders/Transparent/Cutout/VertexLit"
}
