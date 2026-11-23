// Full-screen ordered dithering (Bayer 4x4) for the low-res pixel camera.
// Used by a Full Screen Pass Renderer Feature on Pixel_Renderer.
// The effect runs at the camera's render-texture resolution, so one dither cell = one game pixel.
Shader "Game/PixelDither"
{
    Properties
    {
        _Levels ("Color Levels (per channel, or palette size)", Range(2, 16)) = 4
        _Spread ("Dither Strength", Range(0, 1)) = 0.3
        [Toggle(_USE_PALETTE)] _UsePalette ("Use Palette Ramp", Float) = 0
        [NoScaleOffset] _PaletteTex ("Palette Ramp (left = dark, right = light, width = Levels)", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "PixelDither"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _USE_PALETTE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_PaletteTex);
            SAMPLER(sampler_PaletteTex);

            float _Levels;
            float _Spread;

            static const float BAYER4[16] =
            {
                 0,  8,  2, 10,
                12,  4, 14,  6,
                 3, 11,  1,  9,
                15,  7, 13,  5
            };

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Exact pixel of the low-res target: no filtering, no blur.
                uint2 pixel = uint2(input.positionCS.xy);
                float3 color = LOAD_TEXTURE2D_X(_BlitTexture, pixel).rgb;

                // Threshold in [-0.5, 0.5) from the Bayer matrix.
                float threshold = BAYER4[(pixel.y % 4) * 4 + (pixel.x % 4)] / 16.0 - 0.5;
                float steps = max(_Levels - 1.0, 1.0);

            #if defined(_USE_PALETTE)
                // Brightness -> one of N palette colors.
                float lum = LinearToSRGB(saturate(dot(color, float3(0.2126, 0.7152, 0.0722))));
                lum = saturate(lum + threshold * _Spread);
                float index = floor(lum * steps + 0.5);
                float u = (index + 0.5) / max(_Levels, 1.0);
                float3 result = SAMPLE_TEXTURE2D(_PaletteTex, sampler_PaletteTex, float2(u, 0.5)).rgb;
            #else
                // Quantize each channel in sRGB space so dark tones get as many steps as bright ones.
                float3 c = LinearToSRGB(saturate(color));
                c = saturate(c + threshold * _Spread);
                c = floor(c * steps + 0.5) / steps;
                float3 result = SRGBToLinear(c);
            #endif

                return half4(result, 1.0);
            }
            ENDHLSL
        }
    }
}
