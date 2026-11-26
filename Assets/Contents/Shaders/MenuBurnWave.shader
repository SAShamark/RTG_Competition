Shader "UI/MenuBurnWave"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BurntTex ("Burnt Texture", 2D) = "black" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _Center ("Wave Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Wave Radius", Float) = 0
        _Aspect ("Aspect", Float) = 1.777
        _Phase ("Phase", Float) = 0

        _EdgeWidth ("Edge Width", Float) = 0.05
        _TrailWidth ("Ember Trail Width", Float) = 0.3
        _NoiseScale ("Noise Scale", Float) = 9
        _NoiseAmount ("Noise Amount", Float) = 0.09
        _Distortion ("Distortion", Float) = 0.012
        _Scorch ("Scorch Ahead", Range(0, 1)) = 0.6
        [HDR] _EdgeColor ("Edge Color", Color) = (1, 0.42, 0.05, 1)
        [HDR] _CoreColor ("Core Color", Color) = (1, 0.9, 0.55, 1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "False"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _BurntTex;
            fixed4 _Color;
            float4 _Center;
            float _Radius;
            float _Aspect;
            float _Phase;
            float _EdgeWidth;
            float _TrailWidth;
            float _NoiseScale;
            float _NoiseAmount;
            float _Distortion;
            float _Scorch;
            fixed4 _EdgeColor;
            fixed4 _CoreColor;

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x),
                            lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }

            float fbm(float2 p)
            {
                return valueNoise(p) * 0.6 + valueNoise(p * 2.3 + 17.0) * 0.3 + valueNoise(p * 5.1 + 41.0) * 0.1;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 scale = float2(_Aspect, 1.0);
                float2 p = (i.uv - _Center.xy) * scale;
                float dist = length(p);
                float2 dir = p / max(dist, 1e-4);

                float isActive = step(1e-4, _Radius);
                float noise = fbm(p * _NoiseScale + float2(0.0, -_Phase * 0.7));
                float flicker = valueNoise(p * _NoiseScale * 9.0 + float2(_Phase * 1.3, _Phase * 0.4));
                float front = _Radius - dist - (noise - 0.5) * _NoiseAmount * saturate(_Radius * 6.0);

                float ring = saturate(1.0 - abs(front) / _EdgeWidth) * isActive;
                float trail = saturate(1.0 - front / _TrailWidth) * step(0.0, front) * isActive;
                float burned = smoothstep(0.0, _EdgeWidth * 0.5, front) * isActive;
                float heat = saturate(1.0 + front / (_EdgeWidth * 3.0)) * (1.0 - burned) * isActive;

                float2 uv = i.uv - dir / scale * ring * _Distortion;
                fixed4 clean = tex2D(_MainTex, uv);
                fixed4 burnt = tex2D(_BurntTex, uv);

                fixed4 col = lerp(clean, burnt, burned);
                col.rgb = lerp(col.rgb, col.rgb * float3(1.3, 0.75, 0.5), heat * _Scorch);

                float ember = trail * trail * smoothstep(0.7, 0.86, flicker);
                float glow = saturate(ring * ring + ember);
                col.rgb = lerp(col.rgb, _EdgeColor.rgb, glow * 0.85) + _EdgeColor.rgb * glow * 0.3;
                col.rgb = lerp(col.rgb, _CoreColor.rgb, pow(ring, 6.0));

                col.a = clean.a;
                return col * i.color;
            }
            ENDCG
        }
    }
}
