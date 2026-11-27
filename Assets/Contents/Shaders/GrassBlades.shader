Shader "FlameBlame/GrassBlades"
{
    Properties
    {
        [Header(Colour)]
        _BaseColor ("Root Color", Color) = (0.36, 0.56, 0.22, 1)
        _TipColor ("Tip Color", Color) = (0.56, 0.76, 0.3, 1)
        _GustColor ("Gust Sheen", Color) = (0.16, 0.16, 0.05, 1)
        _ShadeVariation ("Blade Shade Variation", Range(0, 0.6)) = 0.22
        _PatchVariation ("Patch Tint Variation", Range(0, 0.6)) = 0.18
        _PatchScale ("Patch Tint Scale", Float) = 0.35

        [Header(Wind)]
        _WindDirection ("Wind Direction (x, z)", Vector) = (1, 0.35, 0, 0)
        _WindBase ("Steady Lean", Range(0, 1)) = 0.08
        _GustStrength ("Gust Lean", Range(0, 1.5)) = 0.5
        _GustScale ("Gust Scale", Float) = 0.11
        _GustSpeed ("Gust Speed", Float) = 0.5
        _FlutterStrength ("Flutter Lean", Range(0, 0.5)) = 0.09
        _FlutterSpeed ("Flutter Speed", Float) = 3.2

        [Header(Player)]
        _PushStrength ("Push Lean", Range(0, 1)) = 0.92
        _PushHeight ("Push Height Range", Float) = 1.2

        [Header(Distance)]
        _FadeDistance ("Fade Distance", Float) = 38
        _FadeRange ("Fade Range", Float) = 6
        _ThinningStart ("Thinning Start (fraction)", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                half4 _GustColor;
                half _ShadeVariation;
                half _PatchVariation;
                float _PatchScale;
                float4 _WindDirection;
                float _WindBase;
                float _GustStrength;
                float _GustScale;
                float _GustSpeed;
                float _FlutterStrength;
                float _FlutterSpeed;
                float _PushStrength;
                float _PushHeight;
                float _FadeDistance;
                float _FadeRange;
                float _ThinningStart;
            CBUFFER_END

            // xyz = feet position of whoever parts the grass, w = radius.
            float4 _GrassPusher;

            static const float MaxLean = 0.96;
            static const float TwoPi = 6.2831853;

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                // x = height along the blade, y = shade, z = gust, w = fog.
                half4 data : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash(float2 cell)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(cell);
                float b = Hash(cell + float2(1.0, 0.0));
                float c = Hash(cell + float2(0.0, 1.0));
                float d = Hash(cell + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                UNITY_SETUP_INSTANCE_ID(input);

                float3 origin = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float seed = Hash(origin.xz);

                // Each tuft gets its own reach, so the lawn thins out before it ends.
                float reach = lerp(_FadeDistance * _ThinningStart, _FadeDistance, seed);
                float visible = saturate((reach - distance(origin, _WorldSpaceCameraPos)) / _FadeRange);
                positionWS = lerp(origin, positionWS, visible);

                float2 windDir = normalize(_WindDirection.xy);
                float2 windSide = float2(-windDir.y, windDir.x);
                float2 gustUV = origin.xz * _GustScale - windDir * (_Time.y * _GustSpeed);
                // Two octaves drifting at different speeds: gusts roll through instead of pulsing in place.
                float gustNoise = ValueNoise(gustUV) * 0.65 + ValueNoise(gustUV * 2.9 + windSide * (_Time.y * _GustSpeed * 0.6) + 17.0) * 0.35;
                float gust = smoothstep(0.3, 0.8, gustNoise);

                float phase = seed * TwoPi + dot(input.positionOS.xz, float2(9.0, 7.0));
                float flutterRate = _Time.y * _FlutterSpeed * (0.75 + 0.5 * seed);
                float flutterGain = _FlutterStrength * (0.4 + gust);
                float2 lean = windDir * (_WindBase + _GustStrength * gust + sin(flutterRate + phase) * flutterGain)
                    + windSide * (sin(flutterRate * 1.31 + phase * 1.7) * flutterGain);

                float2 fromPusher = positionWS.xz - _GrassPusher.xz;
                float pusherDistance = length(fromPusher);
                float push = saturate(1.0 - pusherDistance / max(_GrassPusher.w, 0.001));
                push *= push * saturate(1.0 - abs(origin.y - _GrassPusher.y) / _PushHeight);
                lean = lerp(lean, fromPusher / max(pusherDistance, 0.001) * _PushStrength, push);

                // The blade rotates about its root: a lean of r keeps its length by dropping to sqrt(1 - r^2).
                float height = positionWS.y - origin.y;
                float bend = input.uv.y;
                float2 tilt = lean * bend;
                float tiltLength = length(tilt);
                tilt *= min(tiltLength, MaxLean) / max(tiltLength, 0.0001);
                positionWS.xz += tilt * height;
                positionWS.y = origin.y + height * sqrt(1.0 - dot(tilt, tilt));

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                half patch = ValueNoise(origin.xz * _PatchScale + 31.0);
                half shade = (1.0 - _ShadeVariation * input.color.r) * (1.0 - _PatchVariation * patch);
                output.data = half4(input.uv.y, shade, gust * bend, ComputeFogFactor(output.positionCS.z));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 albedo = lerp(_BaseColor.rgb, _TipColor.rgb, input.data.x) * input.data.y + _GustColor.rgb * input.data.z;

                // Blades are lit as the lawn they grow from, so they melt into it instead of sparkling.
                half3 normalWS = half3(0.0, 1.0, 0.0);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 lighting = SampleSH(normalWS)
                    + mainLight.color * (saturate(mainLight.direction.y) * mainLight.shadowAttenuation * mainLight.distanceAttenuation);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    lighting += light.color * (saturate(light.direction.y) * light.distanceAttenuation);
                LIGHT_LOOP_END

                return half4(MixFog(albedo * lighting, input.data.w), 1.0);
            }
            ENDHLSL
        }
    }
}
