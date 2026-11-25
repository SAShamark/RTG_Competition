Shader "FlameBlame/Water/GpuSpray"
{
    Properties
    {
        _Color ("Spray Color", Color) = (0.7, 0.88, 1, 1)
        _CoreColor ("Core Color", Color) = (1, 1, 1, 1)
        _Radius ("Stream Radius (start, end)", Vector) = (0.02, 0.45, 0, 0)
        _SpreadDistance ("Spread Distance", Float) = 22
        _Size ("Droplet Size (min, max)", Vector) = (0.035, 0.08, 0, 0)
        _Stretch ("Stretch (near, far)", Vector) = (7, 2, 0, 0)
        _Turbulence ("Turbulence", Float) = 0.25
        _Droop ("Heavy Drop Sag", Float) = 1.2
        _EndFade ("End Fade Length", Float) = 1.5
        _FarOpacity ("Far Opacity", Range(0, 1)) = 0.45
        _Opacity ("Opacity", Range(0, 1)) = 0.9
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

            // Must match CurvePoints in GpuSprayStreamVisual.cs.
            #define CURVE_POINTS 24

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _CoreColor;
                float4 _Radius;
                float _SpreadDistance;
                float4 _Size;
                float4 _Stretch;
                float _Turbulence;
                float _Droop;
                float _EndFade;
                half _FarOpacity;
                half _Opacity;
            CBUFFER_END

            float4 _Curve[CURVE_POINTS];
            float _CurveLength;
            float _Phase;
            float _Span;
            // x = cut, y = reach, z = landed.
            float4 _StreamRange;

            struct Attributes
            {
                float4 positionOS : POSITION;
                // Quad corner, 0..1.
                float2 uv : TEXCOORD0;
                // Four random numbers shared by the quad.
                float4 seed : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half alpha : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float4 seed = input.seed;
                float cut = _StreamRange.x;
                float reach = _StreamRange.y;
                float dist = frac(seed.x + _Phase) * _Span;

                float along = saturate(dist / max(_CurveLength, 0.001)) * (CURVE_POINTS - 1);
                int index = min((int)floor(along), CURVE_POINTS - 2);
                float3 from = _Curve[index].xyz;
                float3 to = _Curve[index + 1].xyz;
                float3 centre = lerp(from, to, along - index);
                float3 tangent = normalize(to - from + float3(0.0, 0.0, 0.00001));
                float3 side = normalize(cross(float3(0.0, 1.0, 0.0), tangent) + float3(0.00001, 0.0, 0.0));
                float3 up = cross(tangent, side);

                float spread = saturate(dist / _SpreadDistance);
                float radius = lerp(_Radius.x, _Radius.y, spread * sqrt(spread)) * seed.z;
                float angle = seed.y * TWO_PI;
                float wander = dist * 1.7 + seed.w * 40.0 - _Time.y * 3.0;
                float3 offset = side * (cos(angle) * radius + sin(wander) * _Turbulence * spread)
                              + up * (sin(angle) * radius + cos(wander * 1.3) * _Turbulence * spread);
                offset.y -= seed.w * seed.w * _Droop * spread * spread;
                float3 positionWS = centre + offset;

                float size = lerp(_Size.x, _Size.y, seed.w) * (0.6 + spread);
                float stretch = lerp(_Stretch.x, _Stretch.y, spread);
                float3 toCamera = normalize(_WorldSpaceCameraPos - positionWS);
                float3 right = normalize(cross(tangent, toCamera) + float3(0.0, 0.00001, 0.0));
                float2 corner = input.uv - 0.5;
                positionWS += right * (corner.x * size) + tangent * (corner.y * size * stretch);

                half tail = cut > 0.0 ? saturate((dist - cut) / _EndFade) : 1.0;
                half head = _StreamRange.z > 0.5 ? step(dist, reach) : saturate((reach - dist) / _EndFade);
                half visible = step(cut, dist) * step(dist, reach);

                output.positionCS = TransformWorldToHClip(positionWS) * visible;
                output.uv = corner * 2.0;
                output.alpha = tail * head * lerp(1.0, _FarOpacity, spread) * _Opacity;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half falloff = saturate(1.0 - dot(input.uv, input.uv));
                half3 color = lerp(_Color.rgb, _CoreColor.rgb, falloff * falloff);
                return half4(color, falloff * input.alpha);
            }
            ENDHLSL
        }
    }
}
