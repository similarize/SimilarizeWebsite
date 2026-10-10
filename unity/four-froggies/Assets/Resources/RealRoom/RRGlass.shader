// REAL ROOM glass (window pane, picture glass, lamp glass): box-projected room reflection with Fresnel, faint tint.
Shader "FF/RRGlass"
{
    Properties { _Tint ("Transmission tint", Color) = (0.9, 0.95, 0.93, 0.06) _Rough ("Roughness", Float) = 0.02 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="Always" }
            Blend One OneMinusSrcAlpha ZWrite Off Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "RRCommon.cginc"
            float4 _Tint; float _Rough;
            struct v2f { float4 pos : SV_POSITION; float3 wpos : TEXCOORD0; float3 nrm : TEXCOORD1; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz; o.nrm = UnityObjectToWorldNormal(v.normal); return o; }
            float4 frag (v2f i, float face : VFACE) : SV_Target
            {
                float3 N = normalize(i.nrm) * (face > 0 ? 1.0 : -1.0);
                float3 V = normalize(_WorldSpaceCameraPos - i.wpos);
                float nv = saturate(dot(N, V));
                float f = 0.04 + 0.96 * pow(1.0 - nv, 5.0);
                float3 refl = Pano(BoxProject(reflect(-V, N), i.wpos), _Rough) * f;
                float a = saturate(f + _Tint.a);
                float3 col = refl + (1.0 - _Tint.rgb) * 0.0;
                float w = Realness(i.wpos);
                col *= w; a *= lerp(0.3, 1.0, w);
                if (_RRDirect > 0.5) return float4(LinToSrgb(ACES(col * _RRExposure)), a);
                return float4(col, a);
            }
            ENDCG
        }
    }
    Fallback Off
}
