Shader "FlameBlame/WaterJet"
{
    Properties
    {
        _Color ("Water Color", Color) = (0.6, 0.82, 0.96, 1)
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FlowSpeed ("Flow Speed", Float) = 9
        _StreakScale ("Streak Scale (along, around)", Vector) = (0.7, 5, 0, 0)
        _Foam ("Foam Amount", Range(0, 1)) = 0.55
        _Erosion ("Erosion", Range(0, 1)) = 0.35
        _ErosionSoftness ("Erosion Softness", Range(0.01, 0.5)) = 0.15
        _EdgeFade ("Edge Fade", Range(0.1, 4)) = 1.2
        _Opacity ("Opacity", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _FoamColor;
                float _FlowSpeed;
                float4 _StreakScale;
                half _Foam;
                half _Erosion;
                half _ErosionSoftness;
                half _EdgeFade;
                half _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                // x = metres along the jet, y = turn around it (0..1).
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            float Hash(float2 cell)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
            }

            // Value noise whose lattice wraps around the jet, so the streaks have no seam.
            float WrappedNoise(float2 p, float period)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float y0 = fmod(cell.y, period);
                float y1 = fmod(cell.y + 1.0, period);
                float a = Hash(float2(cell.x, y0));
                float b = Hash(float2(cell.x + 1.0, y0));
                float c = Hash(float2(cell.x, y1));
                float d = Hash(float2(cell.x + 1.0, y1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.color = input.color;
                output.uv = input.uv;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positions.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float period = max(1.0, round(_StreakScale.y));
                float2 flow = float2((input.uv.x - _Time.y * _FlowSpeed) * _StreakScale.x, input.uv.y * period);
                float streak = WrappedNoise(flow, period) * 0.65 + WrappedNoise(flow * 2.0 + 17.0, period * 2.0) * 0.35;

                half facing = abs(dot(normalize(input.normalWS), normalize(input.viewDirWS)));
                half body = pow(facing, _EdgeFade);
                half foam = smoothstep(1.0 - _Foam, 1.0, streak + (1.0 - facing) * 0.3);
                half3 color = lerp(_Color.rgb, _FoamColor.rgb, foam);

                // The ends of the jet break up into strands: erosion rises as the vertex alpha falls.
                half erode = lerp(_Erosion, 1.0, 1.0 - input.color.a);
                half strands = smoothstep(erode - _ErosionSoftness, erode + _ErosionSoftness, streak * 0.7 + 0.3);

                return half4(color, saturate(strands * body * input.color.a * _Opacity));
            }
            ENDHLSL
        }
    }
}
