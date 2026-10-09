// ffu14: cheap countryside around the ranch: Lambert, texture x vertex colour x tint, fog. No extra passes.
Shader "FF/Countryside"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 150
        CGPROGRAM
        #pragma surface surf Lambert noforwardadd nolightmap nodynlightmap nodirlightmap nometa
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };
        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * IN.color * _Color;
            o.Albedo = c.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
