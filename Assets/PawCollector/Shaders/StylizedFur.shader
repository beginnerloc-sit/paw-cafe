// Paw Collector — stylized fur / toy-gloss shader for URP.
// Vertex-colour albedo, wrapped diffuse with a soft ramp, warm sheen rim,
// GGX highlight for glossy parts (eyes, nose), main-light shadows + SH ambient.
Shader "PawCollector/StylizedFur"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _Wrap ("Light Wrap", Range(0,1)) = 0.45
        _ShadowTint ("Shadow Tint", Color) = (0.78,0.62,0.66,1)
        _RimColor ("Sheen / Rim Color", Color) = (1,0.93,0.82,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 2.6
        _RimStrength ("Rim Strength", Range(0,2)) = 0.55
        _Smoothness ("Smoothness", Range(0,1)) = 0.25
        _SpecStrength ("Spec Strength", Range(0,4)) = 0.25
        _AmbientStrength ("Ambient Strength", Range(0,2)) = 1.0
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint; half _Wrap; half4 _ShadowTint; half4 _RimColor; half _RimPower; half _RimStrength;
                half _Smoothness; half _SpecStrength; half _AmbientStrength; half4 _EmissionColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1;
                half4 color : COLOR; half fog : TEXCOORD2;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color; o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half3 Shade(Light l, half3 n, half3 vdir, half3 albedo)
            {
                half ndl = dot(n, l.direction);
                half wrapped = saturate((ndl + _Wrap) / (1.0 + _Wrap));
                half ramp = smoothstep(0.0, 1.0, wrapped);
                half atten = l.shadowAttenuation * l.distanceAttenuation;
                half lit = ramp * atten;
                half3 diffuse = albedo * lerp(_ShadowTint.rgb, 1.0.xxx, lit);
                half3 h = SafeNormalize(l.direction + vdir);
                half r = max(1.0 - _Smoothness, 0.04); half a2 = r * r * r * r;
                half nh = saturate(dot(n, h)); half d = nh * nh * (a2 - 1.0) + 1.0;
                half spec = a2 / (PI * d * d) * _SpecStrength * saturate(ndl) * atten;
                return (diffuse * lit + spec) * l.color;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 vdir = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                #if defined(UNITY_COLORSPACE_GAMMA)
                half3 albedo = i.color.rgb * _Tint.rgb;
                #else
                half3 albedo = SRGBToLinear(i.color.rgb) * _Tint.rgb;   // vertex colours are authored in sRGB
                #endif
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light ml = GetMainLight(sc);
                half3 col = Shade(ml, n, vdir, albedo);
                #if defined(_ADDITIONAL_LIGHTS)
                uint cnt = GetAdditionalLightsCount();
                for (uint li = 0u; li < cnt; ++li) col += Shade(GetAdditionalLight(li, i.positionWS), n, vdir, albedo) * 0.8;
                #endif
                half3 amb = SampleSH(n) * _AmbientStrength;
                col += albedo * amb * lerp(_ShadowTint.rgb, 1.0.xxx, 0.6);
                half rim = pow(1.0 - saturate(dot(n, vdir)), _RimPower) * _RimStrength;
                col += _RimColor.rgb * rim * (0.35 + 0.65 * ml.shadowAttenuation) * (albedo * 0.6 + 0.4);
                col += albedo * _EmissionColor.rgb;
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection;
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            float4 vert (A v) : SV_POSITION
            {
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float3 nws = TransformObjectToWorldNormal(v.normalOS);
                float4 cs = TransformWorldToHClip(ApplyShadowBias(ws, nws, _LightDirection));
                #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return cs;
            }
            half4 frag () : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 vert (float4 p : POSITION) : SV_POSITION { return TransformObjectToHClip(p.xyz); }
            half frag () : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 p : POSITION; float3 n : NORMAL; };
            struct V { float4 cs : SV_POSITION; half3 n : TEXCOORD0; };
            V vert (A a) { V o; o.cs = TransformObjectToHClip(a.p.xyz); o.n = TransformObjectToWorldNormal(a.n); return o; }
            half4 frag (V i) : SV_Target { return half4(NormalizeNormalPerPixel(i.n), 0); }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
