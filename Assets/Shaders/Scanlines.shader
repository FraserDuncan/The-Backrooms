Shader "Hidden/Backrooms/Scanlines"
{
    Properties
    {
        _LineCount      ("Line Count", Float) = 600
        _LineIntensity  ("Line Intensity", Range(0,1)) = 0.35
        _ScrollSpeed    ("Scroll Speed", Float) = -6
        _Flicker        ("Flicker", Range(0,1)) = 0.05
        _Aberration     ("Chromatic Aberration", Float) = 1.5
        _Vignette       ("Vignette", Float) = 0.5
        _Desaturate     ("Desaturate", Range(0,1)) = 0.15
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off
        Pass
        {
            Name "Scanlines"
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            float _LineCount;
            float _LineIntensity;
            float _ScrollSpeed;
            float _Flicker;
            float _Aberration;
            float _Vignette;
            float _Desaturate;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                // RGB split (chromatic aberration)
                float2 off = float2(_Aberration * 0.001, 0.0);
                half3 col;
                col.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + off).r;
                col.g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).g;
                col.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - off).b;

                // Scanlines (scrolling)
                float phase = (uv.y * _LineCount) + (_Time.y * _ScrollSpeed);
                float scan = sin(phase * 3.14159265) * 0.5 + 0.5;   // 0..1
                col *= lerp(1.0, scan, _LineIntensity);

                // Flicker
                col *= 1.0 - _Flicker * (0.5 + 0.5 * sin(_Time.y * 50.0));

                // Slight desaturation (worn tape)
                float grey = dot(col, half3(0.299, 0.587, 0.114));
                col = lerp(col, grey.xxx, _Desaturate);

                // Vignette
                float2 d = uv - 0.5;
                col *= 1.0 - dot(d, d) * _Vignette;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}