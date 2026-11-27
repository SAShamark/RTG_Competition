Shader "FlameBlame/PaintedFire"
{
    Properties
    {
        _ShapeTex ("Shape Atlas (R = heat)", 2D) = "black" {}
        _Tiles ("Atlas Tiles (X, Y)", Vector) = (2, 2, 0, 0)
        _RampTex ("Colour Ramp", 2D) = "white" {}
        _NoiseTex ("Noise", 2D) = "gray" {}
        _NoiseScale ("Noise Scale", Float) = 1.4
        _NoiseSpeed ("Noise Speed", Float) = 0.9
        _Distort ("Distortion", Range(0, 0.5)) = 0.12
        _HeatGain ("Heat Gain", Float) = 1.15
        _EdgeCut ("Edge Cut", Range(0, 0.5)) = 0.06
        _EdgeSoft ("Edge Softness", Range(0.005, 0.5)) = 0.08
        _Erode ("Lifetime Erosion", Range(0, 1.5)) = 0.9
        _Steps ("Colour Steps (0 = smooth)", Float) = 0
        _Intensity ("Intensity", Float) = 1.6
        _CoreBoost ("Core Boost", Float) = 1.5
        _Additive ("Additive Amount", Range(0, 1)) = 0.25
        _DepthFade ("Depth Fade", Float) = 0.35
        _BaseFade ("Base Fade Height", Range(0.01, 0.5)) = 0.16
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend One OneMinusSrcAlpha
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_ShapeTex);
            SAMPLER(sampler_ShapeTex);
            TEXTURE2D(_RampTex);
            SAMPLER(sampler_RampTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _ShapeTex_ST;
                float4 _Tiles;
                float _NoiseScale;
                float _NoiseSpeed;
                float _Distort;
                float _HeatGain;
                float _EdgeCut;
                float _EdgeSoft;
                float _Erode;
                float _Steps;
                float _Intensity;
                float _CoreBoost;
                float _Additive;
                float _DepthFade;
                float _BaseFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float4 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.color = input.color;
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(positions.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv.xy;
                float seed = input.uv.z;
                float time = _Time.y * _NoiseSpeed;

                // Per-particle atlas cell and horizontal mirror from the stable random seed.
                float tileCount = _Tiles.x * _Tiles.y;
                float tile = min(floor(seed * tileCount), tileCount - 1.0);
                float2 cell = float2(fmod(tile, _Tiles.x), floor(tile / _Tiles.x));
                uv.x = frac(seed * 7.13) > 0.5 ? 1.0 - uv.x : uv.x;

                // Panning noise sways the painted shape; the base stays anchored (amount grows with height).
                float2 noiseUV = float2(uv.x * _NoiseScale + seed * 5.3, uv.y * _NoiseScale * 0.7 - time);
                float nx = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r - 0.5;
                float ny = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV * 1.7 + float2(0.31, -time * 0.6)).r - 0.5;
                float2 shapeUV = saturate(uv + float2(nx, ny * 0.6) * _Distort * pow(uv.y, 1.2));
                shapeUV = clamp(shapeUV, 0.004, 0.996);
                float2 atlasUV = (shapeUV + float2(cell.x, _Tiles.y - 1.0 - cell.y)) / _Tiles.xy;

                float heat = SAMPLE_TEXTURE2D(_ShapeTex, sampler_ShapeTex, atlasUV).r * _HeatGain;

                // Lifetime erosion: as vertex alpha drops, the cooler outer parts and tips burn away first.
                float erodeNoise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV * 2.3).r;
                heat -= (1.0 - input.color.a) * _Erode * (0.6 + erodeNoise * 0.8);

                float alpha = smoothstep(_EdgeCut, _EdgeCut + _EdgeSoft, heat) * smoothstep(0.0, _BaseFade, input.uv.y);
                float rampU = saturate(heat);
                rampU = _Steps > 0.5 ? floor(rampU * _Steps + 0.5) / _Steps : rampU;
                float3 color = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(rampU, 0.5)).rgb;
                color *= input.color.rgb * _Intensity * (1.0 + saturate(heat) * saturate(heat) * _CoreBoost);

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                alpha *= saturate((sceneDepth - input.screenPos.w) / _DepthFade);

                clip(alpha - 0.002);
                return half4(color * alpha, alpha * (1.0 - _Additive));
            }
            ENDHLSL
        }
    }
}
