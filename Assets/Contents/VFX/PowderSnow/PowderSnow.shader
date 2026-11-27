Shader "FlameAndBlame/PowderSnow"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.95, 0.96, 0.98, 1)
        _ShadowColor ("Shadow Color", Color) = (0.58, 0.64, 0.78, 1)
        _Wrap ("Light Wrap", Range(0, 1)) = 0.45
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.6
        _GrainScale ("Grain Scale", Float) = 38
        _GrainStrength ("Grain Strength", Range(0, 0.5)) = 0.1
        _SparkleScale ("Sparkle Scale", Float) = 160
        _SparkleThreshold ("Sparkle Threshold", Range(0.9, 1)) = 0.982
        _SparkleIntensity ("Sparkle Intensity", Range(0, 4)) = 1.6
        _CavityStrength ("Cavity Strength", Range(0, 1)) = 0.35
        _CavityRange ("Cavity Range", Range(0.01, 1)) = 0.35
        _EdgeFade ("Edge Fade", Range(0.001, 1)) = 0.2
        _EdgeBlobScale ("Edge Blob Scale", Float) = 9
        _EdgeDetailScale ("Edge Detail Scale", Float) = 34
        _EdgeSpeckScale ("Edge Speck Scale", Float) = 420
        _EdgeSpeck ("Edge Speck Amount", Range(0, 1)) = 0.22
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.25
        _PackedTint ("Packed Tint", Color) = (0.72, 0.76, 0.86, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadowColor;
            half _Wrap;
            half _AmbientStrength;
            float _GrainScale;
            half _GrainStrength;
            float _SparkleScale;
            half _SparkleThreshold;
            half _SparkleIntensity;
            half _CavityStrength;
            half _CavityRange;
            half _EdgeFade;
            float _EdgeBlobScale;
            float _EdgeDetailScale;
            float _EdgeSpeckScale;
            half _EdgeSpeck;
            half4 _RimColor;
            half _RimPower;
            half _RimStrength;
            half4 _PackedTint;
        CBUFFER_END

        float Hash13(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.zyx + 31.32);
            return frac((p.x + p.y) * p.z);
        }

        float ValueNoise(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);
            float3 u = f * f * (3.0 - 2.0 * f);
            float n000 = Hash13(i);
            float n100 = Hash13(i + float3(1, 0, 0));
            float n010 = Hash13(i + float3(0, 1, 0));
            float n110 = Hash13(i + float3(1, 1, 0));
            float n001 = Hash13(i + float3(0, 0, 1));
            float n101 = Hash13(i + float3(1, 0, 1));
            float n011 = Hash13(i + float3(0, 1, 1));
            float n111 = Hash13(i + float3(1, 1, 1));
            float nx00 = lerp(n000, n100, u.x);
            float nx10 = lerp(n010, n110, u.x);
            float nx01 = lerp(n001, n101, u.x);
            float nx11 = lerp(n011, n111, u.x);
            return lerp(lerp(nx00, nx10, u.y), lerp(nx01, nx11, u.y), u.z);
        }

        // Thin powder is eaten away by drifting blobs plus fine specks, so rims and wall films read as scattered dust.
        // The film factor keeps vertical surfaces from ever closing into a solid sheet.
        void ClipEdge(float coverage, float film, float3 positionWS)
        {
            float edge = saturate(coverage / _EdgeFade) * film;
            float blobs = ValueNoise(positionWS * _EdgeBlobScale) * 0.6 + ValueNoise(positionWS * _EdgeDetailScale) * 0.4;
            float specks = Hash13(floor(positionWS * _EdgeSpeckScale));
            clip(edge - lerp(blobs, specks, _EdgeSpeck));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float coverage : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                float film : TEXCOORD4;
                float packed : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.coverage = input.color.r;
                output.film = input.color.g;
                output.packed = input.color.b;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ClipEdge(input.coverage, input.film, input.positionWS);

                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                // Wrapped Lambert: (N·L + w) / (1 + w) keeps the powder soft and avoids a hard terminator.
                half wrapped = saturate((dot(normal, light.direction) + _Wrap) / (1.0 + _Wrap));
                half lit = wrapped * light.shadowAttenuation;

                half grain = 1.0 + (ValueNoise(input.positionWS * _GrainScale) - 0.5) * 2.0 * _GrainStrength;
                half cavity = lerp(1.0 - _CavityStrength, 1.0, saturate(input.coverage / _CavityRange));
                half3 albedo = _BaseColor.rgb * grain * cavity * lerp(half3(1, 1, 1), _PackedTint.rgb, saturate(input.packed));

                half3 ambient = SampleSH(normal) * _AmbientStrength;
                half3 color = lerp(_ShadowColor.rgb * albedo, albedo * light.color, lit) + albedo * ambient;

                float sparkleNoise = Hash13(floor(input.positionWS * _SparkleScale + viewDir * 2.0));
                half sparkle = step(_SparkleThreshold, sparkleNoise) * _SparkleIntensity * lit;

                // Fresnel rim: (1 - N·V)^p brightens the crest of the layer at grazing angles.
                half rim = pow(1.0 - saturate(dot(normal, viewDir)), _RimPower) * _RimStrength;

                color += (sparkle + rim) * _RimColor.rgb * light.color;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float coverage : TEXCOORD1;
                float film : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.coverage = input.color.r;
                output.film = input.color.g;
                return output;
            }

            half Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ClipEdge(input.coverage, input.film, input.positionWS);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
