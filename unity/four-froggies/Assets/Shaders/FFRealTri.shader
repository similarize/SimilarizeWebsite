// ?realism=1 test: world-space triplanar PBR (Standard lighting) for scanned rock / bark on meshes without good UVs
// or tangents (procedural boulders, Quaternius trunks). The normal map is blended in WORLD space (whiteout blend) and fed
// to the Standard BRDF through a custom lighting wrapper, so no mesh tangents are needed.
Shader "FF/RealTri"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _Scale ("Metres per tile", Float) = 2
        _Gloss ("Smoothness", Range(0, 1)) = 0.2
        _BumpScale ("Normal strength", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf RealTri fullforwardshadows addshadow
        #pragma target 3.0
        #include "UnityPBSLighting.cginc"
        sampler2D _MainTex, _BumpMap;
        fixed4 _Color;
        half _Scale, _Gloss, _BumpScale;

        struct SurfaceOutputRT
        {
            fixed3 Albedo;
            float3 Normal;     // left untouched -> Unity fills it with the WORLD normal
            half3 Emission;
            half Metallic;
            half Smoothness;
            half Occlusion;
            fixed Alpha;
            float3 WN;         // perturbed world normal used for lighting
        };

        struct Input { float3 worldPos; float3 worldNormal; };

        SurfaceOutputStandard ToStd(SurfaceOutputRT s)
        {
            SurfaceOutputStandard o;
            o.Albedo = s.Albedo; o.Normal = s.WN; o.Emission = s.Emission; o.Metallic = s.Metallic;
            o.Smoothness = s.Smoothness; o.Occlusion = s.Occlusion; o.Alpha = s.Alpha;
            return o;
        }
        half4 LightingRealTri(SurfaceOutputRT s, half3 viewDir, UnityGI gi) { return LightingStandard(ToStd(s), viewDir, gi); }
        void LightingRealTri_GI(SurfaceOutputRT s, UnityGIInput data, inout UnityGI gi)
        {
            SurfaceOutputStandard o = ToStd(s);
            LightingStandard_GI(o, data, gi);
        }

        half3 Unpack(float2 uv)
        {
            half4 p = tex2D(_BumpMap, uv);
            half3 n; n.xy = (p.xy * 2.0 - 1.0) * _BumpScale; n.z = sqrt(saturate(1.0 - dot(n.xy, n.xy)));
            return n;
        }

        void surf(Input IN, inout SurfaceOutputRT o)
        {
            float3 wn = normalize(IN.worldNormal);
            float3 bl = pow(abs(wn), 4.0); bl /= dot(bl, 1.0);
            float3 p = IN.worldPos / _Scale;
            float2 ux = p.zy, uy = p.xz, uz = p.xy;
            fixed4 c = tex2D(_MainTex, ux) * bl.x + tex2D(_MainTex, uy) * bl.y + tex2D(_MainTex, uz) * bl.z;
            o.Albedo = c.rgb * _Color.rgb;
            half3 nx = Unpack(ux), ny = Unpack(uy), nz = Unpack(uz);
            nx = half3(nx.xy + wn.zy, abs(nx.z) * wn.x);
            ny = half3(ny.xy + wn.xz, abs(ny.z) * wn.y);
            nz = half3(nz.xy + wn.xy, abs(nz.z) * wn.z);
            o.WN = normalize(nx.zyx * bl.x + ny.xzy * bl.y + nz.xyz * bl.z);
            o.Metallic = 0;
            o.Smoothness = _Gloss * (0.6 + 0.4 * c.g);
            o.Occlusion = 1;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
