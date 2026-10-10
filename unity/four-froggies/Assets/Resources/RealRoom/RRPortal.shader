// REAL ROOM: what is behind the door - the bright cartoon house glow (seen only while the door is open).
Shader "FF/RRPortal"
{
    Properties { _Glow ("Glow", Float) = 1 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="Always" }
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "RRCommon.cginc"
            float _Glow;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }
            float4 frag (v2f i) : SV_Target
            {
                float3 wall = float3(0.93, 0.86, 0.66), floorC = float3(0.62, 0.42, 0.25);
                float3 c = lerp(floorC, wall, smoothstep(0.05, 0.25, i.uv.y)) * _Glow;
                return RROut(c);
            }
            ENDCG
        }
    }
    Fallback Off
}
