Shader "FlameBlame/Water/Puddle"
{
    Properties
    {
        [HideInInspector] _WaterMap ("Water Map", 2D) = "black" {}
        [HideInInspector] _Impact ("Impact Point", Vector) = (0, 0, 0, 0)

        [Header(Wet ground)]
        _WetTint ("Wet Tint", Color) = (0.03, 0.035, 0.04, 1)
        _WetDarkness ("Wet Darkness", Range(0, 1)) = 0.45
        _WetEdge ("Wet Edge", Range(0, 1)) = 0.25

        [Header(Standing water)]
        _PoolDarkness ("Pool Darkness", Range(0, 1)) = 0.6
        _PoolEdge ("Pool Edge Depth", Range(0, 1)) = 0.3
        _EdgeSoftness ("Edge Softness", Range(0.005, 0.3)) = 0.04
        _EdgeNoise ("Edge Noise", Range(0, 1)) = 0.3
        _NoiseScale ("Edge Noise Scale", Float) = 6

        [Header(Reflection)]
        _Reflection ("Reflection", Range(0, 2)) = 1
        _FacingReflectance ("Facing Reflectance", Range(0, 0.2)) = 0.03
        _GlintSharpness ("Sun Glint Sharpness", Float) = 500
        _GlintStrength ("Sun Glint Strength", Float) = 5

        [Header(Ripples)]
        _Shimmer ("Still Shimmer", Range(0, 0.1)) = 0.006
        _ShimmerScale ("Shimmer Scale", Float) = 9
        _ShimmerSpeed ("Shimmer Speed", Float) = 1.5
        _Chop ("Stirred Chop", Range(0, 0.5)) = 0.08
        _RippleStrength ("Ring Strength", Range(0, 1)) = 0.3
        _RippleFrequency ("Ring Frequency", Float) = 20
        _RippleSpeed ("Ring Speed", Float) = 9
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Offset -1, -1

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_WaterMap);
            SAMPLER(sampler_WaterMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _Impact;
                half4 _WetTint;
                half _WetDarkness;
                half _WetEdge;
                half _PoolDarkness;
                half _PoolEdge;
                half _EdgeSoftness;
                half _EdgeNoise;
                float _NoiseScale;
                half _Reflection;
                half _FacingReflectance;
                float _GlintSharpness;
                half _GlintStrength;
                half _Shimmer;
                float _ShimmerScale;
                float _ShimmerSpeed;
                half _Chop;
                half _RippleStrength;
                float _RippleFrequency;
                float _RippleSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            float Hash(float2 cell)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 position)
            {
                float2 cell = floor(position);
                float2 local = frac(position);
                float2 blend = local * local * (3.0 - 2.0 * local);
                float low = lerp(Hash(cell), Hash(cell + float2(1.0, 0.0)), blend.x);
                float high = lerp(Hash(cell + float2(0.0, 1.0)), Hash(cell + float2(1.0, 1.0)), blend.x);
                return lerp(low, high, blend.y);
            }

            float EdgeNoise(float2 position)
            {
                return ValueNoise(position) * 0.65 + ValueNoise(position * 2.7 + 13.1) * 0.35 - 0.5;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 water = SAMPLE_TEXTURE2D(_WaterMap, sampler_WaterMap, input.uv);
                half depth = water.r;
                half stir = water.g;
                half stain = water.b;

                float2 ground = input.positionWS.xz;
                half grain = EdgeNoise(ground * _NoiseScale) * _EdgeNoise;
                half pool = smoothstep(_PoolEdge - _EdgeSoftness, _PoolEdge + _EdgeSoftness, depth + grain);
                half wet = smoothstep(_WetEdge - _EdgeSoftness, _WetEdge + _EdgeSoftness, stain + grain);

                float2 fromImpact = ground - _Impact.xz;
                float range = length(fromImpact);
                float2 slope = fromImpact / max(range, 0.0001) * cos(range * _RippleFrequency - _Time.y * _RippleSpeed) * stir * _RippleStrength;

                float2 wave = ground * _ShimmerScale;
                float time = _Time.y * _ShimmerSpeed;
                float2 shimmer = float2(
                    cos(wave.x + wave.y * 0.6 + time) + cos(wave.y * 1.3 - wave.x * 0.7 - time * 1.3),
                    cos(wave.y * 1.1 - wave.x * 0.5 + time * 0.8) + cos(wave.x * 1.2 - wave.y * 0.9 + time * 1.1));
                slope += shimmer * (_Shimmer + stir * _Chop);

                half3 normal = normalize(half3(-slope.x, 1.0, -slope.y));
                half3 view = normalize(GetWorldSpaceViewDir(input.positionWS));

                // Schlick: F = F₀ + (1 − F₀)·(1 − N·V)⁵
                half fresnel = _FacingReflectance + (1.0 - _FacingReflectance) * pow(1.0 - saturate(dot(normal, view)), 5.0);
                half3 reflection = GlossyEnvironmentReflection(reflect(-view, normal), 0.0, 1.0) * _Reflection;

                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 halfway = normalize(sun.direction + view);
                half3 glint = sun.color * (pow(saturate(dot(normal, halfway)), _GlintSharpness) * _GlintStrength * sun.shadowAttenuation);

                half alpha = max(wet * _WetDarkness, pool * max(_PoolDarkness, fresnel));
                half3 color = _WetTint.rgb * alpha + pool * (reflection * fresnel + glint);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
