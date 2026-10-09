// Lambo Blast sea: animated normals from two scrolling noise layers, shallow -> deep colour from a baked sea map,
// sky reflection from the reflection probe (fresnel), sun glint, and shoreline foam bands rolling in to the beach.
// Sea map (baked from the island height field by Track.cs): R = depth 0..1, G = shore foam mask.
Shader "LB/Water"
{
    Properties
    {
        _Shallow ("Shallow", Color) = (0.22, 0.86, 0.82, 0.5)
        _Deep ("Deep", Color) = (0.02, 0.36, 0.56, 0.92)
        _FoamColor ("Foam", Color) = (1, 1, 1, 1)
        _SeaMap ("Sea map", 2D) = "black" {}
        _Noise ("Noise", 2D) = "gray" {}
        _SeaRect ("Sea map rect (x0, z0, 1/w, 1/h)", Vector) = (0, 0, 0.002, 0.002)
        _WaveScale ("Wave tiling", Float) = 0.045
        _Speed ("Speed", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _SeaMap, _Noise;
            float4 _Shallow, _Deep, _FoamColor, _SeaRect;
            float _WaveScale, _Speed;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wp : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                float4 wp = mul(unity_ObjectToWorld, v.vertex);
                o.wp = wp.xyz;
                o.pos = UnityWorldToClipPos(wp.xyz);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 w = i.wp.xz * _WaveScale;
                half3 n1 = tex2D(_Noise, w + float2(t * 0.021, t * 0.013)).rgb * 2.0 - 1.0;
                half3 n2 = tex2D(_Noise, w * 2.3 + float2(-t * 0.017, t * 0.027)).rgb * 2.0 - 1.0;
                half2 nn = (n1.xy + n2.yx * 0.7) * 0.32;
                float3 N = normalize(float3(nn.x, 1.0, nn.y));
                float3 V = normalize(_WorldSpaceCameraPos - i.wp);

                float2 suv = (i.wp.xz - _SeaRect.xy) * _SeaRect.zw;
                half4 sm = tex2D(_SeaMap, saturate(suv) + nn * 0.002);
                bool inside = suv.x > 0.0 && suv.y > 0.0 && suv.x < 1.0 && suv.y < 1.0;
                half depth = inside ? sm.r : 1.0;
                half shore = inside ? sm.g : 0.0;

                half ndv = saturate(dot(N, V));
                half fres = pow(1.0 - ndv, 4.0);
                half3 col = lerp(_Shallow.rgb, _Deep.rgb, depth);
                float3 R = reflect(-V, N);
                half4 env = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, R);
                half3 sky = DecodeHDR(env, unity_SpecCube0_HDR);
                col = lerp(col, sky, saturate(0.1 + fres * 0.8));
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 H = normalize(L + V);
                col += _LightColor0.rgb * pow(saturate(dot(N, H)), 180.0) * 1.4;

                // foam: bands rolling towards the beach + broken up by noise
                half wave = sin(depth * 46.0 - t * 2.1 + n1.z * 2.5) * 0.5 + 0.5;
                half brk = tex2D(_Noise, i.wp.xz * 0.21 + float2(t * 0.03, -t * 0.02)).b;
                half foam = shore * (0.55 + wave * 0.6) - brk * 0.5;
                foam = smoothstep(0.18, 0.5, foam);
                col = lerp(col, _FoamColor.rgb, foam * 0.9);

                half a = lerp(_Shallow.a, _Deep.a, depth);
                a = saturate(max(a + fres * 0.25, foam));
                UNITY_APPLY_FOG(i.fogCoord, col);
                return half4(col, a);
            }
            ENDCG
        }
    }
    Fallback Off
}
