Shader "FlameBlame/BacklitSmoke"
{
    Properties
    {
        _BaseMap ("Flipbook", 2D) = "white" {}
        _BumpMap ("Flipbook Normals", 2D) = "bump" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _BumpScale ("Normal Strength", Range(0, 2)) = 1
        _Wrap ("Light Wrap", Range(0, 1)) = 0.6
        _Ambient ("Ambient", Range(0, 2)) = 0.7
        _ScatterStrength ("Back Light Strength", Range(0, 8)) = 2.2
        _ScatterPower ("Back Light Focus", Range(1, 16)) = 4
        _ScatterEdge ("Back Light On Thin Edges", Range(0, 1)) = 0.75
        _CoreShadow ("Dense Core Shadow", Range(0, 1)) = 0.35
        _PointLights ("Point Light Strength", Range(0, 4)) = 1
        _Density ("Density", Range(0, 2)) = 1
        _SoftParticlesNearFadeDistance ("Soft Particles Near", Float) = 0
        _SoftParticlesFarFadeDistance ("Soft Particles Far", Float) = 1.5
        _CameraNearFadeDistance ("Camera Fade Near", Float) = 0.5
        _CameraFarFadeDistance ("Camera Fade Far", Float) = 2.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale;
                half _Wrap;
                half _Ambient;
                half _ScatterStrength;
                half _ScatterPower;
                half _ScatterEdge;
                half _CoreShadow;
                half _PointLights;
                half _Density;
                float _SoftParticlesNearFadeDistance;
                float _SoftParticlesFarFadeDistance;
                float _CameraNearFadeDistance;
                float _CameraFarFadeDistance;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                // xy = current flipbook frame, zw = next frame.
                float4 uv : TEXCOORD0;
                float blend : TEXCOORD1;
                float4 tangentOS : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                // xyz = world position, w = eye depth.
                float4 positionWS : TEXCOORD2;
                // w = flipbook blend.
                float4 normalWS : TEXCOORD3;
                // w = fog factor.
                float4 tangentWS : TEXCOORD4;
                float3 bitangentWS : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = positions.positionCS;
                output.screenPos = ComputeScreenPos(positions.positionCS);
                output.positionWS = float4(positions.positionWS, -positions.positionVS.z);
                output.normalWS = float4(normals.normalWS, input.blend);
                output.tangentWS = float4(normals.tangentWS, ComputeFogFactor(positions.positionCS.z));
                output.bitangentWS = normals.bitangentWS;
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half WrapDiffuse(half3 normal, half3 lightDirection, half wrap)
            {
                return saturate((dot(normal, lightDirection) + wrap) / (1.0 + wrap));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float blend = input.normalWS.w;
                half4 current = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy);
                half4 next = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.zw);
                half4 smoke = lerp(current, next, blend);

                half3 normalCurrent = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv.xy), _BumpScale);
                half3 normalNext = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv.zw), _BumpScale);
                half3 normalTS = lerp(normalCurrent, normalNext, blend);
                half3 normalWS = normalize(input.tangentWS.xyz * normalTS.x + input.bitangentWS * normalTS.y + input.normalWS.xyz * normalTS.z);

                float3 positionWS = input.positionWS.xyz;
                float eyeDepth = input.positionWS.w;
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                half3 viewDirection = GetWorldSpaceNormalizeViewDir(positionWS);

                // Thin smoke lets the light through, the dense core blocks it.
                half thin = 1.0 - smoke.a;
                half edge = lerp(1.0, thin, _ScatterEdge);
                half core = lerp(1.0, thin, _CoreShadow);

                Light mainLight = GetMainLight();
                half towardLight = saturate(dot(-viewDirection, mainLight.direction));
                half3 lighting = mainLight.color * WrapDiffuse(normalWS, mainLight.direction, _Wrap) * core;
                lighting += mainLight.color * pow(towardLight, _ScatterPower) * _ScatterStrength * edge;
                lighting += SampleSH(normalWS) * _Ambient;

                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.normalizedScreenSpaceUV = screenUV;
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, positionWS);
                    // Smoke scatters a nearby lamp in every direction, so it is lit on the far side too.
                    half around = WrapDiffuse(normalWS, light.direction, 1.0);
                    lighting += light.color * light.distanceAttenuation * around * _PointLights;
                LIGHT_LOOP_END
                #endif

                half3 color = smoke.rgb * _BaseColor.rgb * input.color.rgb * lighting;
                color = MixFog(color, input.tangentWS.w);

                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float softRange = max(_SoftParticlesFarFadeDistance - _SoftParticlesNearFadeDistance, 0.0001);
                half soft = saturate((sceneDepth - eyeDepth - _SoftParticlesNearFadeDistance) / softRange);
                float cameraRange = max(_CameraFarFadeDistance - _CameraNearFadeDistance, 0.0001);
                half cameraFade = saturate((eyeDepth - _CameraNearFadeDistance) / cameraRange);

                half alpha = saturate(smoke.a * _BaseColor.a * input.color.a * _Density * soft * cameraFade);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
