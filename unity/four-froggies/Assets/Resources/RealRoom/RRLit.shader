// REAL ROOM surfaces: scanned PBR (albedo / normal / AO-rough-metal) lit by the baked lightmap groups (static) or the
// SH probe (moving things: the duck, the switch rocker, door hardware). Box-projected panorama reflections with
// lightmap-based specular occlusion. See RRCommon.cginc.
Shader "FF/RRLit"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal (GL)", 2D) = "bump" {}
        _ArmTex ("AO / Rough / Metal", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _UVScale ("UV scale", Float) = 1
        _Rough ("Roughness", Float) = 0.6
        _Metal ("Metallic", Float) = 0
        _HasNrm ("Has normal", Float) = 0
        _HasArm ("Has ARM (1) / rough only (2)", Float) = 0
        _Dynamic ("Dynamic (SH)", Float) = 0
        _NrmK ("Normal strength", Float) = 1
        _SpecK ("Spec scale", Float) = 1
        _Emis ("Emission (rgb x a)", Color) = (0,0,0,0)
        _EmisTex ("Emission uses albedo", Float) = 0
        _TexK ("Texture contrast (albedo + AO vs their mean)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="Always" }
            Cull Back ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "RRCommon.cginc"
            sampler2D _MainTex, _BumpMap, _ArmTex;
            float4 _Tint, _Emis;
            float _UVScale, _Rough, _Metal, _HasNrm, _HasArm, _Dynamic, _NrmK, _SpecK, _EmisTex, _TexK;
            struct a2v { float4 vertex : POSITION; float3 normal : NORMAL; float4 tangent : TANGENT; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; float3 wpos : TEXCOORD2; float3 nrm : TEXCOORD3; float4 tan : TEXCOORD4; };
            v2f vert (a2v v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.nrm = UnityObjectToWorldNormal(v.normal);
                o.tan = float4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w * unity_WorldTransformParams.w);
                o.uv0 = v.uv0 * _UVScale; o.uv1 = v.uv1;
                return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                float3 alb = SrgbToLin(tex2D(_MainTex, i.uv0).rgb);
                // ffu18c: _TexK < 1 pulls the scan towards its own mean colour (plaster blotches read as dirt)
                if (_TexK < 0.999) alb = lerp(SrgbToLin(tex2Dlod(_MainTex, float4(i.uv0, 0, 11)).rgb), alb, _TexK);
                alb *= _Tint.rgb;
                float ao = 1.0, rough = _Rough, metal = _Metal;
                if (_HasArm > 1.5) { rough = tex2D(_ArmTex, i.uv0).g * _Rough; }
                else if (_HasArm > 0.5) { float3 arm = tex2D(_ArmTex, i.uv0).rgb; ao = lerp(1.0, arm.r, _TexK); rough = arm.g * _Rough; metal = arm.b * _Metal; }
                rough = clamp(rough, 0.03, 1.0);
                float3 Ng = normalize(i.nrm);
                float3 N = Ng;
                if (_HasNrm > 0.5)
                {
                    float3 tn = tex2D(_BumpMap, i.uv0).xyz * 2.0 - 1.0;
                    tn.xy *= _NrmK;
                    float3 T = normalize(i.tan.xyz - Ng * dot(i.tan.xyz, Ng));
                    float3 B = cross(Ng, T) * (i.tan.w < 0.0 ? -1.0 : 1.0);
                    N = normalize(T * tn.x + B * tn.y + Ng * max(tn.z, 0.05));
                }
                float3 V = normalize(_WorldSpaceCameraPos - i.wpos);
                float3 shN = SHIrr(N);
                float3 irr;
                if (_Dynamic > 0.5) irr = SHIrrD(N);   // ffu18c: dimmer probe for moving things (the duck glowed)
                else
                {
                    irr = DecodeRR(tex2D(_RRLMEnv, i.uv1).rgb, _RRK.x) * _RREnvCol.rgb
                        + DecodeRR(tex2D(_RRLMLamp, i.uv1).rgb, _RRK.y) * _RRLampCol.rgb
                        + DecodeRR(tex2D(_RRLMTv, i.uv1).rgb, _RRK.z) * _RRTvCol.rgb;
                    // normal-map detail on top of the (flat) baked irradiance
                    irr *= clamp(shN / max(SHIrr(Ng), 1e-5), 0.6, 1.5);
                }
                float3 diff = alb * (1.0 - metal) * irr * ao;
                float nv = saturate(dot(N, V)) + 1e-4;
                float3 R = reflect(-V, N);
                float3 pre = Pano(BoxProject(R, i.wpos), rough);
                float2 ab = EnvBRDF(rough, nv);
                float3 F0 = lerp(float3(0.04, 0.04, 0.04), alb, metal);
                float occ = saturate(Lum(irr) / max(Lum(shN), 1e-5));
                float3 spec = pre * (F0 * ab.x + ab.y) * lerp(1.0, occ, 0.85) * ao * _SpecK;
                float3 col = diff + spec + _Emis.rgb * _Emis.a * (_EmisTex > 0.5 ? alb * 3.0 : float3(1, 1, 1));
                float w = Realness(i.wpos);
                if (w < 0.999)
                {
                    float3 flat = SrgbToLin(tex2Dlod(_MainTex, float4(i.uv0, 0, 7)).rgb) * _Tint.rgb;
                    float3 irrS = _Dynamic > 0.5 ? SHIrrD(Ng) : irr / clamp(shN / max(SHIrr(Ng), 1e-5), 0.6, 1.5);
                    float3 st = Stylise(flat, irrS) + _Emis.rgb * _Emis.a;
                    col = lerp(st, col, w);
                }
                col += _RRWaveCol.rgb * WaveEdge(i.wpos) / max(_RRExposure, 0.05);
                return RROut(col);
            }
            ENDCG
        }
    }
    Fallback Off
}
