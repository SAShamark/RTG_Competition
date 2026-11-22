Shader "FlameBlame/StylizedFire"
{
    Properties
    {
        _NoiseTex ("Noise", 2D) = "gray" {}
        [HDR] _ColorCore ("Core Color", Color) = (2.4, 2.0, 0.9, 1)
        [HDR] _ColorMid ("Mid Color", Color) = (2.2, 0.95, 0.2, 1)
        [HDR] _ColorEdge ("Edge Color", Color) = (1.5, 0.3, 0.06, 1)
        _NoiseScale ("Noise Scale", Float) = 1.3
        _NoiseSpeed ("Noise Speed", Float) = 1.1
        _Distort ("Distortion", Float) = 0.45
        _Width ("Base Width", Range(0.1, 0.5)) = 0.42
        _EdgeT ("Edge Threshold", Range(0, 1)) = 0.22
        _MidT ("Mid Threshold", Range(0, 1)) = 0.45
        _CoreT ("Core Threshold", Range(0, 1)) = 0.72
        _Soft ("Band Softness", Range(0.005, 0.3)) = 0.04
        _Intensity ("Intensity", Float) = 1
        _DepthFade ("Depth Fade", Float) = 0.4
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                float4 _ColorCore;
                float4 _ColorMid;
                float4 _ColorEdge;
                float _NoiseScale;
                float _NoiseSpeed;
                float _Distort;
                float _Width;
                float _EdgeT;
                float _MidT;
                float _CoreT;
                float _Soft;
                float _Intensity;
                float _DepthFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float4 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.color = input.color;
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(positions.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv.xy;
                float seed = input.uv.z;
                float time = _Time.y * _NoiseSpeed;

                float2 noiseUV = float2(uv.x * _NoiseScale + seed * 7.31, uv.y * _NoiseScale * 0.6 - time + seed * 3.7);
                float n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;
                float n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV * 1.9 + float2(0.37, -time * 0.7)).r;
                float noise = n1 * 0.65 + n2 * 0.35;

                // Tongue silhouette: wide at the base, narrowing to a tip, swayed by noise more at the top.
                float2 shapeUV = uv;
                shapeUV.x += (noise - 0.5) * _Distort * uv.y;
                float height = saturate(shapeUV.y);
                float halfWidth = lerp(_Width, 0.04, pow(height, 1.5));
                float side = saturate(1.0 - pow(abs(shapeUV.x - 0.5) / halfWidth, 1.5));
                float shape = side * (1.0 - height * 0.8) * saturate(height * 12.0 + 0.3);
                float flame = shape * lerp(0.3, 1.7, noise);

                float edge = lerp(0.97, _EdgeT, input.color.a);
                float alpha = smoothstep(edge, edge + _Soft, flame);
                float mid = smoothstep(_MidT, _MidT + _Soft, flame);
                float core = smoothstep(_CoreT, _CoreT + _Soft, flame);

                float3 color = lerp(_ColorEdge.rgb, _ColorMid.rgb, mid);
                color = lerp(color, _ColorCore.rgb, core);
                color *= input.color.rgb * _Intensity;

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                alpha *= saturate((sceneDepth - input.screenPos.w) / _DepthFade);

                clip(alpha - 0.001);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
