// REAL ROOM window view: the moonlit field of the Poly Haven HDRI (crop) looked up by view direction (infinitely far,
// so it parallaxes correctly from anywhere in the room). Same mapping and brightness as the light it casts in the bake.
Shader "FF/RRWindow"
{
    Properties { _MainTex ("HDRI crop", 2D) = "black" {} _Crop ("u0 u1 v0 v1", Vector) = (0.29, 0.71, 0.28, 0.75) _K ("Decode", Float) = 1 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" }
        Pass
        {
            Tags { "LightMode"="Always" }
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "RRCommon.cginc"
            sampler2D _MainTex; float4 _Crop; float _K;
            struct v2f { float4 pos : SV_POSITION; float3 wpos : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz; return o; }
            float4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.wpos - _WorldSpaceCameraPos);
                float u = atan2(d.z, -d.x) * 0.15915494 + 0.5;    // Blender world mapping of the HDRI
                float v = asin(clamp(d.y, -1.0, 1.0)) * 0.31830989 + 0.5;
                float2 uv = float2((u - _Crop.x) / (_Crop.y - _Crop.x), (v - _Crop.z) / (_Crop.w - _Crop.z));
                // ffu23: mirror past the crop edges (clamping smeared the edge column into a bright vertical strip at
                // the right side of the sash when looking across the window)
                uv = 1.0 - abs(1.0 - 2.0 * frac(uv * 0.5));
                float3 c = DecodeRR(tex2D(_MainTex, uv).rgb, _K) * _RREnvCol.rgb;
                // ffu23: the eye only partly adapts to the dark room - keep the night outside night-dark
                c *= sqrt(saturate(4.0 / max(_RRExposure, 1.0)));
                float w = Realness(i.wpos);
                float3 st = lerp(float3(0.05, 0.08, 0.16), float3(0.1, 0.16, 0.06), step(uv.y, 0.55)) * Lum(_RREnvCol.rgb) * 1.5;
                return RROut(lerp(st, c, w));
            }
            ENDCG
        }
    }
    Fallback Off
}
