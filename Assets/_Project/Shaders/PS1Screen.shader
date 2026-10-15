// Final screen pass for the low-res render texture: reduces color depth with ordered (Bayer) dithering,
// like the PS1's 15-bit color output.
Shader "Semblance/PS1 Screen"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ColorLevels ("Color Levels Per Channel", Float) = 32
        _DitherStrength ("Dither Strength", Range(0, 1)) = 1
        _Distress ("Distress", Range(0, 1)) = 0
        _Brightness ("Brightness", Range(0.5, 2)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _ColorLevels;
            float _DitherStrength;
            float _Distress;
            float _Brightness;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            static const float bayer4x4[16] =
            {
                 0,  8,  2, 10,
                12,  4, 14,  6,
                 3, 11,  1,  9,
                15,  7, 13,  5
            };

            fixed4 frag(v2f i) : SV_Target
            {
                float3 color = tex2D(_MainTex, i.uv).rgb * _Brightness;
                // Fear: drain colour, add grain, close in on the edges.
                float lum = dot(color, float3(0.3, 0.59, 0.11));
                color = lerp(color, lum.xxx * float3(1.05, 0.95, 0.92), _Distress * 0.75);
                float2 px = floor(i.uv * _MainTex_TexelSize.zw);
                float grain = frac(sin(dot(px + floor(_Time.y * 24.0), float2(12.9898, 78.233))) * 43758.5453) - 0.5;
                color += grain * 0.05 * _Distress;
                float2 c = i.uv * 2.0 - 1.0;
                color *= 1.0 - saturate(dot(c, c) - 0.35) * _Distress * 0.9;
                uint2 pixel = uint2(floor(i.uv * _MainTex_TexelSize.zw)) % 4u;
                float threshold = (bayer4x4[pixel.y * 4 + pixel.x] + 0.5) / 16.0 - 0.5;
                float levels = max(_ColorLevels - 1, 1);
                // Quantize in gamma space so dark tones don't band too harshly.
                #ifndef UNITY_COLORSPACE_GAMMA
                color = LinearToGammaSpace(saturate(color));
                #endif
                color += threshold * _DitherStrength / levels;
                color = saturate(floor(color * levels + 0.5) / levels);
                #ifndef UNITY_COLORSPACE_GAMMA
                color = GammaToLinearSpace(color);
                #endif
                return fixed4(color, 1);
            }
            ENDHLSL
        }
    }
}
