Shader "UI/BurnFrame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Frame", 2D) = "black" {}
        _Fill ("Fill", Range(0, 1)) = 0
        _Depth ("Frame depth", Range(0.05, 1)) = 0.6
        _Softness ("Reveal softness", Range(0.01, 0.5)) = 0.18
        _Encroach ("Creep-in distance", Range(0, 0.4)) = 0.14
        _Distortion ("Flame distortion", Range(0, 0.1)) = 0.022
        _NoiseScale ("Noise scale", Float) = 7
        _FlowSpeed ("Flow speed", Float) = 0.9
        _Flicker ("Flicker", Range(0, 0.5)) = 0.12
        _Brightness ("Brightness", Float) = 1.15
        _AlphaGain ("Alpha gain", Float) = 1.6
        _FrameTime ("Time", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha

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
            float _Fill;
            float _Depth;
            float _Softness;
            float _Encroach;
            float _Distortion;
            float _NoiseScale;
            float _FlowSpeed;
            float _Flicker;
            float _Brightness;
            float _AlphaGain;
            float _FrameTime;

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);
                float bottom = lerp(hash(cell), hash(cell + float2(1, 0)), local.x);
                float top = lerp(hash(cell + float2(0, 1)), hash(cell + float2(1, 1)), local.x);
                return lerp(bottom, top, local.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 centered = i.uv - 0.5;
                // 0 on the screen border, 1 in the middle.
                float inward = 1.0 - 2.0 * max(abs(centered.x), abs(centered.y));

                float2 flow = float2(0.0, -_FrameTime * _FlowSpeed);
                float2 coarse = i.uv * _NoiseScale;
                float swirlX = noise(coarse + flow);
                float swirlY = noise(coarse * 1.9 + flow * 1.7 + 5.2);
                float tip = 0.3 + 0.7 * saturate(inward / _Depth);
                float2 wobble = (float2(swirlX, swirlY) - 0.5) * 2.0 * _Distortion * tip;

                // Low fill samples a zoomed-in frame, so the flames creep in from beyond the border; the margin keeps the wobble inside the texture.
                float zoom = lerp(1.0 - _Encroach, 1.0 - 2.0 * _Distortion, _Fill);
                float2 uv = 0.5 + centered * zoom + wobble;
                fixed3 flame = tex2D(_MainTex, saturate(uv)).rgb;

                float front = _Fill * (_Depth + _Softness);
                float reveal = 1.0 - smoothstep(front - _Softness, front, inward + (swirlX - 0.5) * 0.1);
                reveal *= saturate(_Fill * 25.0);

                float flicker = 1.0 + (noise(float2(_FrameTime * 9.0, i.uv.x * 3.0)) - 0.5) * 2.0 * _Flicker;
                flame *= _Brightness * flicker * reveal;
                float alpha = saturate(max(flame.r, max(flame.g, flame.b)) * _AlphaGain);
                return fixed4(flame, alpha) * i.color.a;
            }
            ENDCG
        }
    }
}
