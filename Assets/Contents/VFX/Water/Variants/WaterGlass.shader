Shader "FlameBlame/Water/Glass"
{
    Properties
    {
        _Tint ("Water Tint", Color) = (0.78, 0.92, 1, 1)
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _RimColor ("Rim Color", Color) = (0.85, 0.95, 1, 1)
        [Toggle] _UseRefraction ("Refraction (needs URP Opaque Texture)", Float) = 1
        _Refraction ("Refraction Strength", Range(0, 0.2)) = 0.045
        _BodyOpacity ("Opacity Without Refraction", Range(0, 1)) = 0.45
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.5
        _Gloss ("Specular Gloss", Range(4, 256)) = 64
        _Specular ("Specular Strength", Range(0, 4)) = 1.2
        _FlowSpeed ("Flow Speed", Float) = 12
        _StreakScale ("Streak Scale (along, around)", Vector) = (0.9, 5, 0, 0)
        _Foam ("Foam Amount", Range(0, 1)) = 0.3
        _ErosionSoftness ("Erosion Softness", Range(0.01, 0.5)) = 0.15
        _Wobble ("Wobble Amplitude", Range(0, 0.1)) = 0.018
        _WobbleScale ("Wobble Scale", Float) = 1.4
        _WobbleRamp ("Wobble Ramp Distance", Float) = 5
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half4 _FoamColor;
                half4 _RimColor;
                half _UseRefraction;
                half _Refraction;
                half _BodyOpacity;
                half _FresnelPower;
                half _Gloss;
                half _Specular;
                float _FlowSpeed;
                float4 _StreakScale;
                half _Foam;
                half _ErosionSoftness;
                float _Wobble;
                float _WobbleScale;
                float _WobbleRamp;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                // r = how solid the water still is, a = end fades.
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
                float4 screenPos : TEXCOORD3;
            };

            float Hash(float2 cell)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
            }

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
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                float travel = (input.uv.x - _Time.y * _FlowSpeed) * _WobbleScale;
                float turn = input.uv.y * TWO_PI;
                float wobble = sin(travel + turn) * 0.6 + sin(travel * 2.3 - turn * 2.0) * 0.4;
                positionWS += normalWS * (wobble * _Wobble * saturate(input.uv.x / _WobbleRamp));

                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = input.color;
                output.uv = input.uv;
                output.normalWS = normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 view = normalize(input.viewDirWS);
                half side = dot(normal, view);
                normal *= side < 0.0 ? -1.0 : 1.0;
                half facing = abs(side);
                half fresnel = pow(saturate(1.0 - facing), _FresnelPower);
                half solid = input.color.r;

                float period = max(1.0, round(_StreakScale.y));
                float2 flow = float2((input.uv.x - _Time.y * _FlowSpeed) * _StreakScale.x, input.uv.y * period);
                float streak = WrappedNoise(flow, period) * 0.65 + WrappedNoise(flow * 2.0 + 17.0, period * 2.0) * 0.35;

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float3 normalVS = TransformWorldToViewDir(normal);
                half3 behind = SampleSceneColor(screenUV + normalVS.xy * (_Refraction * (streak + 0.5)));
                half3 body = lerp(_Tint.rgb, behind * _Tint.rgb, _UseRefraction);

                half foam = smoothstep(1.0 - _Foam, 1.0, streak + fresnel * 0.3 + (1.0 - solid) * 0.5);

                Light light = GetMainLight();
                half3 halfway = normalize(light.direction + view);
                half3 specular = light.color * (pow(saturate(dot(normal, halfway)), _Gloss) * _Specular);

                half3 color = lerp(body, _FoamColor.rgb, foam) + _RimColor.rgb * fresnel + specular;

                half bodyAlpha = lerp(saturate(_BodyOpacity + fresnel + foam), 1.0, _UseRefraction);
                half erode = 1.0 - solid;
                half strands = smoothstep(erode - _ErosionSoftness, erode + _ErosionSoftness, streak * 0.7 + 0.3 + solid * _ErosionSoftness);

                return half4(color, saturate(bodyAlpha * strands * input.color.a));
            }
            ENDHLSL
        }
    }
}
