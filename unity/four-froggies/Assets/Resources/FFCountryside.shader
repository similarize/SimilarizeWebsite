// ffu14: cheap countryside around the ranch: Lambert, texture x vertex colour x tint, fog. No extra passes.
Shader "FF/Countryside"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FarTex ("Far texture (blended in by vertex alpha, UV2)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 150
        CGPROGRAM
        #pragma surface surf Lambert noforwardadd nolightmap nodynlightmap nodirlightmap nometa
        #pragma target 3.0
        sampler2D _MainTex;
        sampler2D _FarTex;
        fixed4 _Color;
        struct Input
        {
            float2 uv_MainTex;
            float2 uv2_FarTex;
            float4 color : COLOR;
        };
        void surf (Input IN, inout SurfaceOutput o)
        {
            // ffu15: the near ring fades into the far texture over its last rings (the seam showed from ~2 km up)
            fixed4 t = lerp(tex2D(_MainTex, IN.uv_MainTex), tex2D(_FarTex, IN.uv2_FarTex), IN.color.a);
            fixed4 c = t * fixed4(IN.color.rgb, 1) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
