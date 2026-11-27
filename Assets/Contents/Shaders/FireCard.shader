Shader "FlameBlame/FireCard"
{
    Properties
    {
        _MainTex ("Card", 2D) = "white" {}
        _NoiseTex ("Noise", 2D) = "gray" {}
        _Intensity ("Intensity", Float) = 1.2
        _Distort ("Sway", Range(0, 0.1)) = 0.01
        _SwayHeight ("Sway Fade Height", Range(0.05, 1)) = 0.7
        _NoiseScale ("Noise Scale", Float) = 2.0
        _NoiseSpeed ("Noise Speed", Float) = 0.6
        _Flicker ("Flicker", Range(0, 1)) = 0.15
        _FlickerSpeed ("Flicker Speed", Float) = 3.0
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Intensity;
                float _Distort;
                float _SwayHeight;
                float _NoiseScale;
                float _NoiseSpeed;
                float _Flicker;
                float _FlickerSpeed;
            CBUFFER_END

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
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv;
                // World position seeds the noise so neighbouring windows never sway in sync.
                float seed = dot(input.positionWS, float3(0.37, 0.11, 0.53));
                float time = _Time.y * _NoiseSpeed;

                float2 noiseUV = float2(uv.x * _NoiseScale + seed, uv.y * _NoiseScale * 0.6 - time);
                float nx = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r - 0.5;
                float ny = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV * 1.6 + 0.43).r - 0.5;
                float sway = 1.0 - smoothstep(0.0, _SwayHeight, uv.y) * 0.6;
                float2 cardUV = clamp(uv + float2(nx, ny * 0.5) * _Distort * sway, 0.002, 0.998);

                half4 card = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, cardUV);
                float flicker = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(seed * 0.13, _Time.y * _FlickerSpeed * 0.05)).r - 0.5;
                float3 color = card.rgb * input.color.rgb * _Intensity * (1.0 + flicker * 2.0 * _Flicker);
                return half4(color, card.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
