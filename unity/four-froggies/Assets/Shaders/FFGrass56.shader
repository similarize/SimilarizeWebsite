// ffu56 ranch grass (Terrain56 / Grass56): GPU-instanced (Graphics.DrawMeshInstanced) 3-card clumps, alpha-cut, double-sided,
// half-Lambert like FF/Foliage. Vertex: wind sway (two frequencies + slow gusts, phase from the clump root), BEND away from up to
// 8 movers (_FFBend xyz = foot / wheel / body point, w = radius; frogs, robots, animals, vehicles) and flatten under them,
// distance fade (clumps shrink into the ground between _FadeNear and _FadeFar). Root darkening in the vertex colour.
Shader "FF/Grass56"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Grass card (RGBA)", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.42
        _Wind ("Wind", Range(0, 0.5)) = 0.12
        _FadeNear ("Fade start (m)", Float) = 40
        _FadeFar ("Fade end (m)", Float) = 55
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        LOD 200
        Cull Off
        CGPROGRAM
        #pragma surface surf Folia vertex:vert alphatest:_Cutoff noforwardadd nolightmap nodynlightmap nodirlightmap nometa
        #pragma multi_compile_instancing
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        half _Wind, _FadeNear, _FadeFar;
        float4 _FFBend[8];
        struct Input { float2 uv_MainTex; float4 color : COLOR; };

        void vert(inout appdata_full v)
        {
            float3 root = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float h = saturate(v.texcoord.y);
            float hy = wp.y - root.y;
            float t = _Time.y;
            float gust = 0.55 + 0.45 * sin(t * 0.37 + root.x * 0.021 + root.z * 0.017);
            float s = sin(t * 1.7 + root.x * 0.35 + root.z * 0.27) * 0.65 + sin(t * 3.3 + root.x * 0.9 - root.z * 0.6) * 0.25;
            float3 off = float3(s, 0, s * 0.6) * _Wind * gust * h * h;
            float flat = 0;
            for (int i = 0; i < 8; i++)
            {
                float4 b = _FFBend[i];
                if (b.w > 0.001)
                {
                    float3 d = root - b.xyz; d.y = 0;
                    float dist = length(d);
                    float k = saturate(1.0 - dist / b.w);
                    k *= saturate(1.0 - abs(root.y - b.y) * 0.5);     // only near the ground the mover is on
                    float3 dir = dist > 0.001 ? d / dist : float3(1, 0, 0);
                    off += dir * k * hy * 0.85;
                    flat = max(flat, k);
                }
            }
            float dc = distance(root, _WorldSpaceCameraPos);
            float fade = saturate((_FadeFar - dc) / max(0.01, _FadeFar - _FadeNear));
            wp.y = root.y + hy * fade * (1.0 - flat * 0.72);
            wp += off * fade;
            v.vertex.xyz = mul(unity_WorldToObject, float4(wp, 1)).xyz;
            float n = frac(sin(dot(root.xz, float2(12.9898, 78.233))) * 43758.5453);
            v.color = float4(lerp(float3(0.92, 0.95, 0.82), float3(1.08, 1.04, 0.9), n) * lerp(0.55, 1.0, h), 1);
            v.normal = float3(0, 1, 0);
        }

        half4 LightingFolia(SurfaceOutput s, half3 lightDir, half atten)
        {
            half d = abs(dot(s.Normal, lightDir)) * 0.5 + 0.5;
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * d * atten;
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb * IN.color.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    Fallback "Legacy Shaders/Transparent/Cutout/VertexLit"
}
