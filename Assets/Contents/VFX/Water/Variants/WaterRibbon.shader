Shader "FlameBlame/Water/Ribbon"
{
    Properties
    {
        _FlowTex ("Flow (R streaks, G breakup)", 2D) = "white" {}
        _Color ("Body Color", Color) = (0.55, 0.8, 0.96, 1)
        _CoreColor ("Highlight Color", Color) = (1, 1, 1, 1)
        _EdgeColor ("Outline Color", Color) = (0.2, 0.47, 0.78, 1)
        _FlowSpeed ("Flow Speed", Float) = 14
        _Tiling ("Tiling (along, across)", Vector) = (0.22, 1, 0, 0)
        _Width ("Width share (start, end), spread dist", Vector) = (0.22, 1, 16, 0)
        _Ragged ("Ragged Edge", Range(0, 1)) = 0.5
        _Pulse ("Width Pulse", Range(0, 1)) = 0.55
        _PulseScale ("Pulse Scale", Float) = 0.05
        _PulseRamp ("Pulse Ramp Distance", Float) = 3
        _Surge ("Flow Surge", Range(0, 1)) = 0.5
        _Highlight ("Highlight Threshold", Range(0, 1)) = 0.5
        _Outline ("Outline Width", Range(0, 0.6)) = 0.22
        _Softness ("Band Softness", Range(0.005, 0.6)) = 0.05
        _Breakup ("Breakup dist (start, end)", Vector) = (9, 26, 0, 0)
        _EndFade ("End Fade Length", Float) = 1.2
        _Opacity ("Opacity", Range(0, 1)) = 0.95
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

            TEXTURE2D(_FlowTex);
            SAMPLER(sampler_FlowTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _CoreColor;
                half4 _EdgeColor;
                float _FlowSpeed;
                float4 _Tiling;
                float4 _Width;
                half _Ragged;
                half _Pulse;
                float _PulseScale;
                float _PulseRamp;
                half _Surge;
                half _Highlight;
                half _Outline;
                half _Softness;
                float4 _Breakup;
                float _EndFade;
                half _Opacity;
            CBUFFER_END

            // x = cut, y = reach, z = landed.
            float4 _StreamRange;

            struct Attributes
            {
                float4 positionOS : POSITION;
                // x = metres from the line start, y = across (0..1).
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
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float cut = _StreamRange.x;
                float reach = _StreamRange.y;
                float dist = input.uv.x + cut;
                float scroll = _Time.y * _FlowSpeed;

                // Slow blobs riding downstream: they swell the ribbon and push the streaks ahead in surges.
                half2 blob = SAMPLE_TEXTURE2D(_FlowTex, sampler_FlowTex, float2((dist - scroll * 0.8) * _PulseScale, 0.3)).rg;
                half gust = SAMPLE_TEXTURE2D(_FlowTex, sampler_FlowTex, float2((dist - scroll * 1.3) * _PulseScale * 2.3, 0.7)).g;
                half pulse = (blob.g * 0.65 + gust * 0.35 - 0.5) * 2.0;
                scroll += (blob.r - 0.5) * _Surge * 4.0;

                float2 flowUV = float2((dist - scroll) * _Tiling.x, input.uv.y * _Tiling.y);
                half4 near = SAMPLE_TEXTURE2D(_FlowTex, sampler_FlowTex, flowUV);
                half4 far = SAMPLE_TEXTURE2D(_FlowTex, sampler_FlowTex, float2((dist - scroll * 0.6) * _Tiling.x * 1.7 + 0.37, input.uv.y * _Tiling.y + 0.5));
                half streak = near.r * 0.6 + far.r * 0.4;

                float width = lerp(_Width.x, _Width.y, saturate(dist / _Width.z));
                width *= 1.0 + pulse * _Pulse * saturate(dist / _PulseRamp);
                half edge = abs(input.uv.y * 2.0 - 1.0) / width + (far.g - 0.5) * _Ragged;
                half shape = 1.0 - smoothstep(1.0 - _Softness, 1.0, edge);

                half breakup = smoothstep(_Breakup.x, _Breakup.y, dist);
                half holes = smoothstep(breakup - _Softness, breakup + _Softness, near.g * 0.8 + 0.2);

                half tail = cut > 0.0 ? saturate((dist - cut) / _EndFade) : 1.0;
                half head = _StreamRange.z > 0.5 ? 1.0 : saturate((reach - dist) / _EndFade);

                half outline = smoothstep(1.0 - _Outline - _Softness, 1.0 - _Outline, edge);
                half highlight = smoothstep(_Highlight, _Highlight + _Softness, streak * (1.0 - saturate(edge) * 0.5));
                half3 color = lerp(_Color.rgb, _CoreColor.rgb, highlight);
                color = lerp(color, _EdgeColor.rgb, outline * _EdgeColor.a);

                return half4(color, saturate(shape * holes * tail * head * _Opacity));
            }
            ENDHLSL
        }
    }
}
