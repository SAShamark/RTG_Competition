Shader "FlameAndBlame/FrostZone"
{
    Properties
    {
        _FrostColor ("Frost Color", Color) = (0.72, 0.86, 1, 1)
        _DeepColor ("Deep Color", Color) = (0.36, 0.55, 0.95, 1)
        _CrackColor ("Crack Color", Color) = (0.9, 0.97, 1, 1)
        _CellScale ("Cell Scale", Float) = 0.9
        _CrackWidth ("Crack Width", Range(0.001, 0.2)) = 0.035
        _Opacity ("Opacity", Range(0, 1)) = 0.82
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "FrostZone"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            #define MAX_ZONES 8

            half4 _FrostColor;
            half4 _DeepColor;
            half4 _CrackColor;
            float _CellScale;
            half _CrackWidth;
            half _Opacity;
            half _EdgeSoftness;

            float4 _FrostZones[MAX_ZONES];
            float _FrostStrengths[MAX_ZONES];
            float _FrostZoneCount;

            float2 Hash2(float2 p)
            {
                float3 q = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                q += dot(q, q.yzx + 33.33);
                return frac((q.xx + q.yz) * q.zy);
            }

            // x: distance to the nearest cell border, y: random tone of the cell
            float2 Cells(float2 p)
            {
                float2 base = floor(p);
                float2 local = frac(p);
                float nearest = 8.0;
                float second = 8.0;
                float tone = 0.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 cell = float2(x, y);
                        float2 random = Hash2(base + cell);
                        float distanceToSeed = length(cell + random - local);
                        if (distanceToSeed < nearest)
                        {
                            second = nearest;
                            nearest = distanceToSeed;
                            tone = random.x;
                        }
                        else if (distanceToSeed < second)
                        {
                            second = distanceToSeed;
                        }
                    }
                }

                return float2(second - nearest, tone);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.0)
                    {
                        return half4(0, 0, 0, 0);
                    }
                    float depth = rawDepth;
                #else
                    if (rawDepth >= 1.0)
                    {
                        return half4(0, 0, 0, 0);
                    }
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                float3 positionWS = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float mask = 0.0;
                int count = (int)_FrostZoneCount;
                for (int i = 0; i < MAX_ZONES; i++)
                {
                    if (i >= count)
                    {
                        break;
                    }

                    float4 zone = _FrostZones[i];
                    float reach = (zone.w - distance(positionWS, zone.xyz)) / max(zone.w * _EdgeSoftness, 0.001);
                    mask = max(mask, saturate(reach) * _FrostStrengths[i]);
                }

                if (mask <= 0.001)
                {
                    return half4(0, 0, 0, 0);
                }

                float3 normal = SampleSceneNormals(uv);
                float3 weight = pow(abs(normal), 6.0);
                weight /= max(weight.x + weight.y + weight.z, 0.0001);
                float3 scaled = positionWS * _CellScale;
                float2 cells = Cells(scaled.zy) * weight.x + Cells(scaled.xz) * weight.y + Cells(scaled.xy) * weight.z;

                // The frost front is ragged: whole cells freeze a little earlier or later than their neighbours.
                mask = saturate(mask * 1.6 - cells.y * 0.6);
                half crack = 1.0 - smoothstep(0.0, _CrackWidth, cells.x);
                half3 color = lerp(_FrostColor.rgb, _DeepColor.rgb, cells.y * 0.55);
                color *= 0.86 + 0.14 * saturate(normal.y);
                color = lerp(color, _CrackColor.rgb, crack);
                return half4(color, mask * _Opacity);
            }
            ENDHLSL
        }
    }
}
