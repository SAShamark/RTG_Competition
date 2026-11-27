Shader "FlameBlame/PowderPuff"
{
    Properties
    {
        _NoiseTex ("Noise", 2D) = "gray" {}
        _LightColor ("Lit Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Color", Color) = (0.62, 0.6, 0.7, 1)
        _Wrap ("Light Wrap", Range(0, 1)) = 0.45
        _RampSharpness ("Ramp Sharpness", Range(0.05, 1)) = 0.55
        _Ambient ("Ambient", Range(0, 2)) = 0.6
        _Rim ("Back Light", Range(0, 2)) = 0.4
        _Lit ("Lighting Amount", Range(0, 1)) = 1
        _LightTint ("Light Color Influence", Range(0, 1)) = 0.35
        _NoiseScale ("Noise Scale", Float) = 0.6
        _NoiseScroll ("Noise Scroll", Float) = 0.15
        _EdgeNoise ("Edge Noise", Range(0, 1)) = 0.45
        _Softness ("Edge Softness", Range(0.01, 1)) = 0.25
        _Erode ("Age Erosion", Range(0, 1.5)) = 0.9
        _Density ("Density", Range(0, 3)) = 1.2
        _SoftDistance ("Soft Particle Distance", Float) = 0.4
        _NearFade ("Camera Near Fade", Float) = 0.3
        _NearFadeRange ("Camera Fade Range", Float) = 0.8
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _LightColor;
                half4 _ShadowColor;
                half _Wrap;
                half _RampSharpness;
                half _Ambient;
                half _Rim;
                half _Lit;
                half _LightTint;
                float _NoiseScale;
                float _NoiseScroll;
                half _EdgeNoise;
                half _Softness;
                half _Erode;
                half _Density;
                float _SoftDistance;
                float _NearFade;
                float _NearFadeRange;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                // xy = quad UV, z = age (0..1), w = stable per-particle random.
                float4 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.screenPos = ComputeScreenPos(positions.positionCS);
                output.eyeDepth = -positions.positionVS.z;
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 centered = input.uv.xy * 2.0 - 1.0;
                float age = input.uv.z;
                float seed = input.uv.w;

                float2 noiseUV = input.uv.xy * _NoiseScale + seed * 17.31 + _Time.y * _NoiseScroll * float2(0.3, 0.7);
                half3 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).rgb;
                half billow = noise.r * 0.65 + noise.g * 0.35;

                float radius = length(centered) + (billow - 0.5) * _EdgeNoise;
                half density = saturate(1.0 - radius);
                half threshold = age * _Erode * 0.5;
                half alpha = smoothstep(threshold, threshold + _Softness, density * (0.55 + billow * 0.9)) * _Density;

                // Sphere normal reconstructed from the quad, bumped by the noise so the puff reads as billows.
                float2 bump = (noise.gb - 0.5) * 0.6;
                float2 n2 = centered * 0.9 + bump;
                float3 normalVS = normalize(float3(n2, sqrt(saturate(1.0 - dot(n2, n2))) + 0.2));
                float3 normalWS = normalize(UNITY_MATRIX_I_V[0].xyz * normalVS.x + UNITY_MATRIX_I_V[1].xyz * normalVS.y + UNITY_MATRIX_I_V[2].xyz * normalVS.z);

                Light mainLight = GetMainLight();
                half wrapped = saturate((dot(normalWS, mainLight.direction) + _Wrap) / (1.0 + _Wrap));
                half ramp = smoothstep(0.5 - _RampSharpness * 0.5, 0.5 + _RampSharpness * 0.5, wrapped);
                half3 lightTint = lerp(half3(1, 1, 1), mainLight.color, _LightTint);
                half3 direct = lerp(_ShadowColor.rgb, _LightColor.rgb, ramp) * lightTint;
                half3 viewDir = UNITY_MATRIX_I_V[2].xyz;
                half backLight = pow(saturate(dot(-viewDir, mainLight.direction)), 3.0) * (1.0 - density) * _Rim;
                half3 lit = direct + SampleSH(normalWS) * _Ambient * 0.5 + lightTint * backLight;
                half3 color = lerp(_LightColor.rgb, lit, _Lit) * input.color.rgb;

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                half soft = saturate((sceneDepth - input.eyeDepth) / _SoftDistance);
                half cameraFade = saturate((input.eyeDepth - _NearFade) / _NearFadeRange);

                return half4(color, saturate(alpha * input.color.a * soft * cameraFade));
            }
            ENDHLSL
        }
    }
}
