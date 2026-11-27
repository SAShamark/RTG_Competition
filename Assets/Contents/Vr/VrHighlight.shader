Shader "Vr/Highlight"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.55, 0.15, 1)
        _Power ("Rim Power", Range(0.5, 8)) = 2.5
        _PulseSpeed ("Pulse Speed", Float) = 4
        _PulseDepth ("Pulse Depth", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Highlight"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Back
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Power;
                half _PulseSpeed;
                half _PulseDepth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 view = normalize(GetCameraPositionWS() - input.positionWS);
                half rim = pow(1.0 - saturate(abs(dot(normalize(input.normalWS), view))), _Power);
                // Slow breathing between (1 - depth) and 1 so the glow reads as alive without flashing.
                half pulse = 1.0 - _PulseDepth * (0.5 + 0.5 * sin(_Time.y * _PulseSpeed));
                return half4(_Color.rgb * rim * pulse, 1.0);
            }
            ENDHLSL
        }
    }
}
