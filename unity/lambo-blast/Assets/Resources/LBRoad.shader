// lambo8 road: hi-res procedural markings / curbs map (uv: u across the road, v = metres / 32) times the CC0 asphalt grain at
// two scales (second scale rotated + fades in with distance), asphalt normal map, and "wet-ish" patches from a macro noise:
// darker, glossier, flatter -> the sky / reflection probe shows in them like a damp racing surface. Paint (map alpha) is
// slightly glossier than the asphalt.
Shader "LB/Road"
{
    Properties
    {
        _MainTex ("Markings (a = paint)", 2D) = "white" {}
        _Grain ("Asphalt grain (mean 0.5)", 2D) = "grey" {}
        _GrainBump ("Asphalt normal", 2D) = "bump" {}
        _Macro ("Macro noise", 2D) = "grey" {}
        _GrainRep ("Grain repeats (u, v)", Vector) = (6, 12.8, 0, 0)
        _Glossiness ("Smoothness", Range(0,1)) = 0.3
        _Wet ("Wet patches", Range(0,1)) = 0.5
        _BumpScale ("Bump", Float) = 0.8
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows nolightmap nodynlightmap nodirlightmap exclude_path:deferred exclude_path:prepass
        #pragma multi_compile __ LB_LITE
        #pragma target 3.0
        sampler2D _MainTex, _Grain, _GrainBump, _Macro;
        float4 _GrainRep;
        half _Glossiness, _Wet, _BumpScale;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
            INTERNAL_DATA
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 T = WorldNormalVector(IN, float3(1, 0, 0));
            float3 B = WorldNormalVector(IN, float3(0, 1, 0));
            float3 N = WorldNormalVector(IN, float3(0, 0, 1));
            float2 uv = IN.uv_MainTex;
            fixed4 mk = tex2D(_MainTex, uv);
            float dist = distance(IN.worldPos, _WorldSpaceCameraPos);
            half far = saturate((dist - 30.0) / 160.0);
            float2 g = uv * _GrainRep.xy;
            fixed3 d1 = tex2D(_Grain, g).rgb;
            half m = tex2D(_Macro, uv * float2(0.6, 0.45)).r;
            half m2 = tex2D(_Macro, uv * float2(0.25, 0.11) + 0.4).g;
        #ifdef LB_LITE
            fixed3 d = lerp(d1, fixed3(0.5, 0.5, 0.5), far * 0.6);
        #else
            fixed3 d2 = tex2D(_Grain, float2(g.y, -g.x) * 0.31 + 0.5).rgb;
            fixed3 d = lerp(d1, d2, 0.35);
            d = lerp(d, lerp(d2, fixed3(0.5, 0.5, 0.5), 0.5), far);
        #endif
            half wet = saturate((m - 0.56) * 5.0) * _Wet;
            fixed3 alb = mk.rgb * d * 2.0 * (0.93 + 0.14 * m2) * (1.0 - 0.28 * wet);
            half gloss = lerp(_Glossiness * (0.8 + 0.4 * d.g), 0.8, wet);
            gloss = lerp(gloss, max(gloss, 0.45), mk.a);
            half3 nt = UnpackNormal(tex2D(_GrainBump, g));
            nt.xy *= _BumpScale * (1.0 - far * 0.8) * (1.0 - wet * 0.85);
            o.Albedo = alb;
            o.Metallic = 0;
            o.Smoothness = gloss;
            o.Occlusion = 1.0;
            o.Normal = dot(T, T) > 0.01 ? normalize(nt) : float3(0, 0, 1);
        }
        ENDCG
    }
    Fallback "Diffuse"
}
