Shader "Vr/ComfortVignette"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 1)
        _Aperture ("Aperture (deg)", Float) = 80
        _Feather ("Feather (deg)", Float) = 14
        _HeadForward ("Head Forward", Vector) = (0, 0, 1, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ComfortVignette"
            Tags { "LightMode" = "UniversalForward" }
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Aperture;
                float _Feather;
                float4 _HeadForward;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 direction = normalize(input.positionWS - GetCameraPositionWS());
                // Angle from the head forward in degrees; the ring fades in over _Feather degrees past _Aperture.
                float angle = degrees(acos(clamp(dot(direction, normalize(_HeadForward.xyz)), -1.0, 1.0)));
                half alpha = saturate((angle - _Aperture) / max(_Feather, 0.001)) * _Color.a;
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
