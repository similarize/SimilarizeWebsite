// lambo9 distant horizon ring (mountains / mesas / islands): vertex colour, soft sun lambert, aerial-perspective haze
// towards the scene fog colour (stronger at the base, like real distant ranges), no Unity fog (it would erase it at 1 km).
Shader "LB/Far"
{
    Properties { _Haze ("Haze", Range(0, 1)) = 0.5 }
    SubShader
    {
        Tags { "Queue" = "Geometry+10" "RenderType" = "Opaque" }
        Cull Off
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            float _Haze;
            struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; float lit : TEXCOORD0; };
            v2f vert(appdata_full v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = UnityObjectToWorldNormal(v.normal);
                o.lit = 0.62 + 0.38 * abs(dot(normalize(n), normalize(_WorldSpaceLightPos0.xyz)));
                o.col = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 c = i.col.rgb * i.lit * _LightColor0.rgb * 1.05;
                float h = saturate(_Haze * (1.25 - 0.55 * i.col.a));
                c = lerp(c, unity_FogColor.rgb, h);
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
