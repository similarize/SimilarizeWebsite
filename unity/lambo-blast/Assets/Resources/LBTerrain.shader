// lambo8 ground: the generated colour map (rock / sand / snow / grass by height + slope, uv over the whole terrain) times the
// theme's CC0 grain at two scales (rotated second layer + macro noise = no visible tiling), fading to the coarse layer with
// distance (no moire / shimmer at grazing angles); steep faces get a world-space triplanar CC0 rock (no stretched texels on
// cliffs and mesa walls). LB_LITE (phones under load / governor level 2) = one grain layer, no rock projection.
Shader "LB/Terrain"
{
    Properties
    {
        _ColorMap ("Colour map", 2D) = "grey" {}
        _MainTex ("Grain (mean 0.5)", 2D) = "grey" {}
        _BumpMap ("Grain normal", 2D) = "bump" {}
        _Macro ("Macro noise", 2D) = "grey" {}
        _RockTex ("Rock (mean 0.5)", 2D) = "grey" {}
        _RockBump ("Rock normal", 2D) = "bump" {}
        _RockTint ("Rock tint", Color) = (0.5,0.5,0.5,1)
        _Rep ("Grain repeats", Float) = 100
        _RockScale ("Rock tiles per metre", Float) = 0.08
        _RockAmt ("Rock amount", Range(0,1)) = 1
        _RockSlope ("Rock slope start", Range(0,1)) = 0.35
        _BumpScale ("Bump", Float) = 1
        _Glossiness ("Smoothness", Range(0,1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows nolightmap nodynlightmap nodirlightmap exclude_path:deferred exclude_path:prepass
        #pragma multi_compile __ LB_LITE
        #pragma target 3.0
        sampler2D _ColorMap, _MainTex, _BumpMap, _Macro, _RockTex, _RockBump;
        fixed4 _RockTint;
        half _Rep, _RockScale, _RockAmt, _RockSlope, _BumpScale, _Glossiness;

        struct Input
        {
            float2 uv_ColorMap;
            float3 worldPos;
            float3 worldNormal;
            INTERNAL_DATA
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 T = WorldNormalVector(IN, float3(1, 0, 0));
            float3 B = WorldNormalVector(IN, float3(0, 1, 0));
            float3 N = WorldNormalVector(IN, float3(0, 0, 1));
            float3 n = normalize(N);
            float2 uv = IN.uv_ColorMap;
            fixed3 col = tex2D(_ColorMap, uv).rgb;
            float dist = distance(IN.worldPos, _WorldSpaceCameraPos);
            half far = saturate((dist - 45.0) / 230.0);
            float2 g1 = uv * _Rep;
            half m = tex2D(_Macro, uv * (_Rep * 0.045)).r;
            half m2 = tex2D(_Macro, uv * (_Rep * 0.012) + 0.31).g;
            fixed3 d1 = tex2D(_MainTex, g1).rgb;
        #ifdef LB_LITE
            fixed3 d = lerp(d1, fixed3(0.5, 0.5, 0.5), far * 0.5);
        #else
            float2 g2 = float2(uv.x * 0.866 - uv.y * 0.5, uv.x * 0.5 + uv.y * 0.866) * (_Rep * 0.29) + 0.19;
            fixed3 d2 = tex2D(_MainTex, g2).rgb;
            fixed3 d = lerp(d1, d2, 0.3 + 0.35 * m);
            d = lerp(d, lerp(d2, fixed3(0.5, 0.5, 0.5), 0.45), far);
        #endif
            fixed3 alb = col * d * 2.0 * (0.88 + 0.24 * m2);
            half3 nt = UnpackNormal(tex2D(_BumpMap, g1));
            nt.xy *= _BumpScale * (1.0 - far * 0.75);
            float3 nw = normalize(T * nt.x + B * nt.y + n * nt.z);
            if (dot(T, T) < 0.01) nw = n;
        #ifndef LB_LITE
            half rk = saturate((1.0 - n.y - _RockSlope) * 5.0) * _RockAmt;
            float3 w = n * n; w *= w; w /= (w.x + w.y + w.z + 1e-4);
            float3 p = IN.worldPos * _RockScale;
            float2 ux = p.zy, uy = p.xz + 0.37, uz = p.xy + 0.71;
            fixed3 rd = tex2D(_RockTex, ux).rgb * w.x + tex2D(_RockTex, uy).rgb * w.y + tex2D(_RockTex, uz).rgb * w.z;
            half3 tx = UnpackNormal(tex2D(_RockBump, ux));
            half3 ty = UnpackNormal(tex2D(_RockBump, uy));
            half3 tz = UnpackNormal(tex2D(_RockBump, uz));
            float3 pert = float3(0, tx.y, tx.x) * w.x + float3(ty.x, 0, ty.y) * w.y + float3(tz.x, tz.y, 0) * w.z;
            float3 rn = normalize(n + pert * 1.3);
            fixed3 ralb = lerp(col, _RockTint.rgb, 0.5) * rd * 2.0;
            alb = lerp(alb, ralb, rk);
            nw = normalize(lerp(nw, rn, rk));
        #endif
            o.Albedo = alb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness * (0.7 + 0.6 * d.g);
            o.Occlusion = 1.0;
            float3 tn = float3(dot(nw, T), dot(nw, B), dot(nw, N));
            o.Normal = dot(T, T) > 0.01 ? normalize(tn) : float3(0, 0, 1);
        }
        ENDCG
    }
    Fallback "Diffuse"
}
