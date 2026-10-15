// lambo9 contact shadow / AO pad under each car: multiply blend (darkens whatever ground is below), fogged towards white
// so it vanishes into the distance with the ground. _Strength per car (MaterialPropertyBlock, fades while airborne).
Shader "LB/Blob"
{
    Properties
    {
        _MainTex ("Shape", 2D) = "white" {}
        _Strength ("Strength", Range(0, 1)) = 0.78
        _Shade ("Shade", Color) = (0.18, 0.19, 0.22, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-20" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off
        Offset -2, -2
        Blend DstColor Zero
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Strength;
            fixed4 _Shade;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) };
            v2f vert(appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; UNITY_TRANSFER_FOG(o, o.pos); return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed a = tex2D(_MainTex, i.uv).a * _Strength;
                fixed4 c = fixed4(lerp(fixed3(1, 1, 1), _Shade.rgb, a), 1);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(1, 1, 1, 1));
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
