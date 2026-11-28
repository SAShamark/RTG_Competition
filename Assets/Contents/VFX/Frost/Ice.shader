Shader "FlameAndBlame/Ice"
{
    Properties
    {
        _LightColor ("Top Color", Color) = (0.88, 0.96, 1, 1)
        _DeepColor ("Side Color", Color) = (0.34, 0.56, 0.95, 1)
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.2
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.75
        _Alpha ("Opacity", Range(0, 1)) = 0.9
        _Glow ("Glow", Range(0, 2)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _LightColor;
                half4 _DeepColor;
                half4 _RimColor;
                half _RimPower;
                half _RimStrength;
                half _Alpha;
                half _Glow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light light = GetMainLight();

                // Faces that look up read as snow-bright tops, the rest as deep blue sides.
                half top = smoothstep(0.35, 0.85, normal.y * 0.5 + 0.5);
                half3 color = lerp(_DeepColor.rgb, _LightColor.rgb, top);
                color *= 0.78 + 0.32 * saturate(dot(normal, light.direction));

                half rim = pow(1.0 - saturate(dot(normal, view)), _RimPower);
                color = lerp(color, _RimColor.rgb, rim * _RimStrength);
                color += color * _Glow;

                half alpha = saturate(_Alpha + rim * 0.3) * input.color.a;
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
