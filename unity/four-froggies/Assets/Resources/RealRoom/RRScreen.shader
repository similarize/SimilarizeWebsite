// REAL ROOM TV screen: the video (or a fallback test pattern) as emission + the glossy black glass reflecting the room.
Shader "FF/RRScreen"
{
    Properties { _MainTex ("Video", 2D) = "black" {} _Bright ("Brightness", Float) = 1.6 _On ("On", Float) = 1 _Fallback ("Fallback pattern", Float) = 0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="Always" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "RRCommon.cginc"
            sampler2D _MainTex; float _Bright, _On, _Fallback;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wpos : TEXCOORD1; float3 nrm : TEXCOORD2; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz; o.nrm = UnityObjectToWorldNormal(v.normal); return o; }
            float3 Pattern(float2 uv)
            {
                // slow-drifting nature-documentary colours when no video is available
                float t = _RRTime * 0.15;
                float3 a = float3(0.25, 0.55, 0.9), b = float3(0.35, 0.7, 0.25), c = float3(0.95, 0.75, 0.35);
                float s = sin(uv.x * 3.0 + t) * 0.5 + 0.5, s2 = sin(uv.y * 2.0 - t * 1.3 + uv.x) * 0.5 + 0.5;
                return lerp(lerp(a, b, smoothstep(0.35, 0.65, uv.y + (s - 0.5) * 0.2)), c, s2 * 0.25);
            }
            float4 frag (v2f i) : SV_Target
            {
                float3 vid = _Fallback > 0.5 ? Pattern(i.uv) : SrgbToLin(tex2D(_MainTex, i.uv).rgb);
                // faint pixel grid up close
                float2 px = frac(i.uv * float2(960.0, 540.0));
                float grid = lerp(0.82, 1.0, smoothstep(0.0, 0.18, min(px.x, px.y)));
                float3 emit = vid * _Bright * _On * grid;
                float3 N = normalize(i.nrm), V = normalize(_WorldSpaceCameraPos - i.wpos);
                float nv = saturate(dot(N, V));
                float f = 0.04 + 0.96 * pow(1.0 - nv, 5.0);
                float3 refl = Pano(BoxProject(reflect(-V, N), i.wpos), 0.05) * f * 0.8;
                float3 col = emit + refl + float3(0.002, 0.002, 0.0025) * Lum(_RRLampCol.rgb);
                float w = Realness(i.wpos);
                col = lerp(emit * 0.9 + float3(0.01, 0.01, 0.012), col, w);
                return RROut(col);
            }
            ENDCG
        }
    }
    Fallback Off
}
