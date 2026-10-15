// lambo8: world-space triplanar PBR for scenery props (rocks, mesas, hoodoos, ruins, bark, foliage, concrete, facades).
// The detail map (_MainTex, CC0 ambientCG, mean 0.5 grey) is projected on world X / Y / Z, so stretched primitives and
// merged meshes never show stretched or seamed UVs; albedo = _Color * detail * 2 keeps the game's palette.
// Normal map: same three projections blended into a world normal, then rotated into the mesh's tangent frame
// (meshes without tangents fall back to the plain vertex normal). _Snow > 0 = snow settles on up-facing surfaces.
// _AO = use vertex colour red as ambient occlusion (procedural trees). _Raw = 1: _MainTex is a full-colour map (facades).
Shader "LB/Tri"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Detail (mean 0.5)", 2D) = "grey" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _Scale ("Tiles per metre", Float) = 0.25
        _BumpScale ("Bump", Float) = 1
        _Glossiness ("Smoothness", Range(0,1)) = 0.2
        _Metallic ("Metallic", Range(0,1)) = 0
        _Snow ("Snow cover", Range(0,1)) = 0
        _AO ("Vertex AO", Range(0,1)) = 0
        _Raw ("Full-colour map", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows nolightmap nodynlightmap nodirlightmap exclude_path:deferred exclude_path:prepass
        #pragma target 3.0
        sampler2D _MainTex;
        sampler2D _BumpMap;
        fixed4 _Color;
        half _Scale, _BumpScale, _Glossiness, _Metallic, _Snow, _AO, _Raw;

        // worldPos is packed into the tangent-frame interpolators (INTERNAL_DATA), so this stays within target 3.0 limits
        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            fixed4 color : COLOR;
            INTERNAL_DATA
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 T = WorldNormalVector(IN, float3(1, 0, 0));
            float3 B = WorldNormalVector(IN, float3(0, 1, 0));
            float3 N = WorldNormalVector(IN, float3(0, 0, 1));
            float3 n = normalize(N);
            float3 w = n * n; w *= w; w /= (w.x + w.y + w.z + 1e-4);
            float3 p = IN.worldPos * _Scale;
            float2 ux = p.zy, uy = p.xz + 0.37, uz = p.xy + 0.71;
            fixed3 d = tex2D(_MainTex, ux).rgb * w.x + tex2D(_MainTex, uy).rgb * w.y + tex2D(_MainTex, uz).rgb * w.z;
            half3 tx = UnpackNormal(tex2D(_BumpMap, ux));
            half3 ty = UnpackNormal(tex2D(_BumpMap, uy));
            half3 tz = UnpackNormal(tex2D(_BumpMap, uz));
            float3 pert = float3(0, tx.y, tx.x) * w.x + float3(ty.x, 0, ty.y) * w.y + float3(tz.x, tz.y, 0) * w.z;
            float3 nw = normalize(n + pert * _BumpScale);

            fixed3 alb = _Color.rgb * d * 2.0;
            half gloss = _Glossiness * (0.75 + 0.5 * d.g);
            if (_Snow > 0.001)
            {
                half s = saturate((nw.y - (1.0 - _Snow * 0.75)) * 5.0) * saturate(0.6 + (d.r - 0.5) * 3.0);
                alb = lerp(alb, half3(0.86, 0.9, 0.96), s);
                gloss = lerp(gloss, 0.45, s);
            }
            if (_AO > 0.001) alb *= lerp(1.0, IN.color.r, _AO);
            o.Albedo = alb;
            o.Metallic = _Metallic;
            o.Smoothness = gloss;
            // world normal -> this mesh's tangent frame (zero tangents = plain vertex normal)
            float3 tn = float3(dot(nw, T), dot(nw, B), dot(nw, N));
            o.Normal = dot(T, T) > 0.01 ? normalize(tn) : float3(0, 0, 1);
            o.Occlusion = 1.0;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
