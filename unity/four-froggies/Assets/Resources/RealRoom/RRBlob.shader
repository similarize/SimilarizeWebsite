// REAL ROOM soft contact shadow under thrown things (multiply).
Shader "FF/RRBlob"
{
    Properties { _Str ("Strength", Float) = 0.55 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-1" }
        Pass
        {
            Tags { "LightMode"="Always" }
            Blend DstColor Zero ZWrite Off Offset -1, -1
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Str;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy * 2.0 - 1.0; return o; }
            float4 frag (v2f i) : SV_Target { float r = length(i.uv); float s = 1.0 - _Str * pow(saturate(1.0 - r), 1.6); return float4(s, s, s, 1); }
            ENDCG
        }
    }
    Fallback Off
}
