// ffu57 Four Froggies animal + frog skin (Core/Animals57.cs). One shader for every animal and every froggy so the same
// animal looks the same in every world and view:
//  * FUR (_Fur 1): object-space triplanar strand texture (_MainTex, made in code, no files) tints the albedo along the
//    hair direction, a Kajiya-Kay style anisotropic sheen runs across the strands, the silhouette gets a soft fuzzy rim
//    and a little darkening where the hair parts (cats / dogs fluffy, horses sleek + glossy, cows short).
//  * WET SKIN (_Wet 1): frogs - a broad soft lobe plus a tight wet highlight, fresnel reflection of the sky probe
//    (unity_SpecCube0) and a warm sub-surface tint at the terminator, faint mottling from the same texture.
// Vertex colours multiply the albedo (Quaternius farm packs keep their painted patches). Cheap: one forward pass + shadows.
Shader "FF/Skin57"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Strands / mottle (grey)", 2D) = "white" {}
        _Scale ("Texture metres", Float) = 0.25
        _Gloss ("Gloss", Range(0, 1)) = 0.3
        _Wrap ("Wrap", Range(0, 1)) = 0.5
        _Rim ("Rim", Range(0, 1)) = 0.3
        _SSS ("Subsurface tint", Color) = (1, 0.55, 0.35, 1)
        _Fur ("Fur", Range(0, 1)) = 0
        _Wet ("Wet", Range(0, 1)) = 0
        _VC ("Use vertex colour", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Skin57 fullforwardshadows vertex:vert
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color, _SSS;
        half _Gloss, _Wrap, _Rim, _Fur, _Wet, _Scale, _VC;
        struct Input { float3 viewDir; float3 lp; float3 ln; float4 color : COLOR; };   // no worldRefl: keeps target 3.0 under 10 interpolators

        struct SurfaceOutputSkin57
        {
            fixed3 Albedo; fixed3 Normal; fixed3 Emission; half Specular; fixed Gloss; fixed Alpha;
            half3 Tang;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float s = length(mul((float3x3)unity_ObjectToWorld, float3(0, 1, 0)));
            o.lp = v.vertex.xyz * s;      // metres, so prints / strands keep their size on scaled primitives
            o.ln = v.normal;
        }

        half4 LightingSkin57(SurfaceOutputSkin57 s, half3 lightDir, half3 viewDir, half atten)
        {
            half nl = dot(s.Normal, lightDir);
            half diff = saturate((nl + _Wrap) / (1.0 + _Wrap));
            half term = saturate(diff - saturate(nl)) * (0.7 + _Wet * 0.5);
            half3 h = normalize(lightDir + viewDir);
            half nh = saturate(dot(s.Normal, h));
            // broad soft lobe (both)
            half spec = pow(nh, 10.0 + 90.0 * _Gloss) * _Gloss * 0.45 * (1.0 - _Fur * 0.7);
            // wet: tight highlight
            spec += pow(nh, 260.0) * _Wet * 1.6;
            // fur: anisotropic sheen across the strands (Kajiya-Kay)
            half th = dot(normalize(s.Tang), h);
            half sinTH = sqrt(saturate(1.0 - th * th));
            spec += pow(sinTH, 30.0 + 60.0 * _Gloss) * _Fur * (0.08 + 0.22 * _Gloss) * saturate(nl * 2.0 + 0.3);
            half4 c;
            c.rgb = (s.Albedo * (diff + _SSS.rgb * term) + spec * lerp(half3(1, 1, 1), s.Albedo * 1.4 + 0.2, _Fur * 0.6)) * _LightColor0.rgb * atten;
            c.a = s.Alpha;
            return c;
        }

        half Tri(float3 p, float3 n)
        {
            float3 w = abs(n); w = w / (w.x + w.y + w.z + 1e-4);
            float k = 1.0 / max(_Scale, 0.01);
            half a = tex2D(_MainTex, p.yz * k).r, b = tex2D(_MainTex, p.zx * k).r, d = tex2D(_MainTex, p.xy * k).r;
            return a * w.x + b * w.y + d * w.z;
        }

        void surf(Input IN, inout SurfaceOutputSkin57 o)
        {
            half3 base = _Color.rgb * lerp(half3(1, 1, 1), IN.color.rgb, _VC);
            half t = Tri(IN.lp, IN.ln);
            // fur: strands vary the albedo +-25 %, partings darker; wet skin: faint mottle +-8 %
            half k = lerp(1.0 + (t - 0.5) * 0.16, 0.78 + t * 0.44, _Fur);
            o.Albedo = saturate(base * k);
            // hair runs from head to tail (object +z) and drapes down: tangent in object space
            half3 tang = normalize(cross(IN.ln, float3(1, 0, 0)) + float3(0, -0.3, 0) + 1e-3);
            o.Tang = mul((float3x3)unity_ObjectToWorld, tang);
            half3 vd = normalize(IN.viewDir);
            half rim = 1.0 - saturate(dot(vd, o.Normal));
            half3 sky = 0;
            #if defined(UNITY_PASS_FORWARDBASE)
            half3 r = reflect(-vd, o.Normal);   // o.Normal = world normal here (surf never writes a normal map)
            sky = DecodeHDR(UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, r, 2.0), unity_SpecCube0_HDR);
            #endif
            half fres = pow(rim, 4.0);
            // wet: fresnel sky reflection; fur: soft fuzzy rim that picks up the sky colour; plain: the FF/Skin rim
            o.Emission = sky * fres * _Wet * 0.55
                       + (sky * 0.5 + base * 0.4) * pow(rim, 2.2) * _Fur * 0.28
                       + (_Color.rgb * 0.6 + 0.25) * pow(rim, 3.0) * _Rim * 0.4 * (1.0 - _Fur);
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
