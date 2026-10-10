// ffu23 REAL ROOM frog eyeball (third-person body): unit sphere, +z = gaze. Gold-bronze iris with dark radial veins and
// a horizontal pupil, black sclera (frogs show almost no white), wet cornea: SH (moving-object probe) + lamp glint +
// box-projected room reflection.
Shader "FF/RREye"
{
    Properties { _LampPos ("Lamp position", Vector) = (0,2,0,0) }
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
            float4 _LampPos;
            struct v2f { float4 pos : SV_POSITION; float3 o : TEXCOORD0; float3 wpos : TEXCOORD1; float3 nrm : TEXCOORD2; };
            v2f vert (appdata_base v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.o = v.vertex.xyz;
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz; o.nrm = UnityObjectToWorldNormal(v.normal); return o;
            }
            float H(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }
            float4 frag (v2f i) : SV_Target
            {
                float3 p = normalize(i.o);
                float r = length(p.xy);                       // 0 at the centre of the gaze
                float front = step(0.0, p.z);
                float ang = atan2(p.y, p.x);
                float vein = H(float2(floor(ang * 22.0), 1.0)) * 0.5 + H(float2(floor(ang * 57.0), 2.0)) * 0.5;
                float3 iris = lerp(float3(0.55, 0.36, 0.07), float3(0.85, 0.66, 0.22), smoothstep(0.75, 0.25, r)) * (0.55 + 0.6 * vein);
                iris = lerp(iris, float3(0.06, 0.05, 0.02), smoothstep(0.62, 0.8, r));
                float pupil = length(float2(p.x / 0.44, p.y / 0.2));
                float3 alb = lerp(float3(0.01, 0.01, 0.008), iris, front * smoothstep(1.02, 0.96, pupil) * smoothstep(0.95, 0.8, r));
                alb = lerp(alb, float3(0.004, 0.004, 0.004), front * smoothstep(1.0, 0.92, pupil));
                alb = SrgbToLin(alb);
                float3 N = normalize(i.nrm), V = normalize(_WorldSpaceCameraPos - i.wpos);
                float3 diff = alb * SHIrrD(N) * 1.6;
                float fres = 0.04 + 0.96 * pow(1.0 - saturate(dot(N, V)), 5.0);
                float3 Rr = reflect(-V, N);
                float3 spec = Pano(BoxProject(Rr, i.wpos), 0.02) * fres * 1.2;
                float3 L = normalize(_LampPos.xyz - i.wpos);
                spec += pow(saturate(dot(Rr, L)), 600.0) * _RRLampCol.rgb * 30.0;
                return RROut(diff + spec);
            }
            ENDCG
        }
    }
    Fallback Off
}
