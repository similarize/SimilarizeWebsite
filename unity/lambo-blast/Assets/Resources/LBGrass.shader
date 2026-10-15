// lambo8 grass / scrub tufts: geometry blades (no alpha test, so nothing shimmers under MSAA), vertex-coloured, darker roots,
// swaying with the wind. Distance LOD without popping: every blade shrinks into its root between _Fade.x and _Fade.y metres
// from the camera (root = uv0.y height + uv1 xz). Chunk meshes sit at the world origin (object space = world space).
Shader "LB/Grass"
{
    Properties
    {
        _Fade ("Fade start / end (m)", Vector) = (40, 70, 0, 0)
        _Wind ("Wind", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull Off
        CGPROGRAM
        #pragma surface surf Lambert vertex:vert noforwardadd nolightmap nodynlightmap nodirlightmap exclude_path:deferred exclude_path:prepass
        #pragma target 3.0
        float4 _Fade;
        half _Wind;

        struct Input
        {
            fixed4 color : COLOR;
            half h;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float h = v.texcoord.x;
            float3 root = float3(v.texcoord1.x, v.texcoord.y, v.texcoord1.y);
            float d = distance(_WorldSpaceCameraPos.xz, root.xz);
            float s = saturate((_Fade.y - d) / max(_Fade.y - _Fade.x, 1.0));
            float3 wp = root + (v.vertex.xyz - root) * s;
            float ph = _Time.y * 1.9 + root.x * 0.23 + root.z * 0.19;
            float sway = (sin(ph) * 0.6 + sin(ph * 2.3 + 1.7) * 0.25) * _Wind * h * h * s;
            wp.xz += float2(0.16, 0.1) * sway;
            v.vertex.xyz = wp;
            v.normal = float3(0, 1, 0);
            o.h = h;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            o.Albedo = IN.color.rgb * (0.45 + 0.55 * IN.h);
        }
        ENDCG
    }
    Fallback Off
}
