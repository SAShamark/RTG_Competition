Shader "FlameBlame/Water/ImpactRing"
{
    Properties
    {
        _Color ("Foam Color", Color) = (1, 1, 1, 1)
        _Rings ("Ring Count", Float) = 3
        _Speed ("Spread Speed", Float) = 2.2
        _Thickness ("Ring Thickness", Range(0.02, 0.5)) = 0.22
        _Lobes ("Lobes", Float) = 9
        _LobeDepth ("Lobe Depth", Range(0, 0.5)) = 0.12
        _Centre ("Centre Foam Radius", Range(0, 1)) = 0.3
        _Softness ("Softness", Range(0.005, 0.3)) = 0.04
        _Opacity ("Opacity", Range(0, 1)) = 0.9
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1, -1

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
                float _Rings;
                float _Speed;
                half _Thickness;
                float _Lobes;
                half _LobeDepth;
                half _Centre;
                half _Softness;
                half _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * 2.0 - 1.0;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float angle = atan2(input.uv.y, input.uv.x);
                float time = _Time.y;
                float radius = length(input.uv);
                radius += (sin(angle * _Lobes + time * 3.0) + sin(angle * (_Lobes * 0.5 + 1.0) - time * 2.0)) * 0.5 * _LobeDepth * radius;

                float wave = frac(radius * _Rings - time * _Speed);
                half ring = smoothstep(_Thickness + _Softness, _Thickness, abs(wave - 0.5));
                half fade = saturate(1.0 - radius);
                half centre = smoothstep(_Centre + _Softness, _Centre, radius);
                half alpha = max(ring * fade * fade, centre) * step(radius, 1.0);

                return half4(_Color.rgb, saturate(alpha * _Opacity));
            }
            ENDHLSL
        }
    }
}
