// Paw Collector — soft toon shader matching the Blender look (no outlines).
// Vertex-colour albedo (sRGB), 2-tone eased ramp with lavender shadow tint, toon shadows, light ambient.
// _Unlit = 1 renders flat vertex colour (eyes, nose, mouth, whiskers).
Shader "PawCollector/ToonCat"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _ShadowTint ("Shadow Tint", Color) = (0.6,0.58,0.72,1)
        _RampMin ("Ramp Min", Range(0,1)) = 0.42
        _RampMax ("Ramp Max", Range(0,1)) = 0.62
        _LightInfluence ("Light Colour Influence", Range(0,1)) = 0.35
        _AmbientStrength ("Ambient Strength", Range(0,1)) = 0.15
        _Unlit ("Unlit (face parts)", Float) = 0
        _GlowStrength ("Glow (vertex alpha 0 = glow)", Range(0,4)) = 1.6
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
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint; half4 _ShadowTint; half _RampMin; half _RampMax; half _LightInfluence; half _AmbientStrength; half _Unlit; half4 _EmissionColor; half _GlowStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half4 color : COLOR; half fog : TEXCOORD2; };
            Varyings vert (Attributes v)
            {
                Varyings o; VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS; o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color; o.fog = ComputeFogFactor(p.positionCS.z); return o;
            }
            half4 frag (Varyings i) : SV_Target
            {
                #if defined(UNITY_COLORSPACE_GAMMA)
                half3 albedo = i.color.rgb * _Tint.rgb;
                #else
                half3 albedo = SRGBToLinear(i.color.rgb) * _Tint.rgb;
                #endif
                if (_Unlit > 0.5) return half4(MixFog(albedo + _EmissionColor.rgb, i.fog), 1);
                half3 n = normalize(i.normalWS);
                Light ml = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half h = dot(n, ml.direction) * 0.5 + 0.5;
                half lit = smoothstep(_RampMin, _RampMax, h);
                lit = min(lit, smoothstep(0.25, 0.75, ml.shadowAttenuation));
                half3 col = albedo * lerp(_ShadowTint.rgb, 1.0.xxx, lit);
                col *= lerp(1.0.xxx, ml.color, _LightInfluence);
                col += albedo * SampleSH(n) * _AmbientStrength;
                col += _EmissionColor.rgb * albedo;
                col += albedo * (1.0 - i.color.a) * _GlowStrength * (0.6 + 0.4 * sin(_Time.y * 1.7 + i.positionWS.x * 3.0));
                return half4(MixFog(col, i.fog), 1);
            }
            ENDHLSL
        }
        UsePass "PawCollector/StylizedFur/SHADOWCASTER"
        UsePass "PawCollector/StylizedFur/DEPTHONLY"
        UsePass "PawCollector/StylizedFur/DEPTHNORMALS"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
