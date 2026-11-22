// Full-screen PS1 + old CRT TV look, in ONE pass at the screen's resolution.
// Used by the Full Screen Pass Renderer Feature on Pixel_Renderer (the Player camera uses that renderer).
//
//   1. Low resolution:  the image is cut into big "game pixels" (Virtual Height = 240 rows, like the PS1).
//   2. 15-bit color:    each channel gets only `Levels` steps, with 4x4 Bayer dithering between them.
//   3. Composite bleed: colors smear sideways a little (cheap TV cable), brightness stays sharp.
//   4. CRT:             dark lines between rows, RGB phosphor stripes, curved glass, dark corners.
//
// Every effect has its own slider on the material (Art/Shaders/M_PS1Screen) — 0 turns it off.
// A level can switch the whole CRT part (4) off from code, keeping the PS1 look (1-3):
//   Shader.SetGlobalFloat("_PS1CrtOff", 1);   // see Game.Core.ScreenLook.CrtOff
// and lift the dark parts of the picture for a dark level:
//   Shader.SetGlobalFloat("_PS1Lift", 0.4);   // see Game.Core.ScreenLook.Lift (0 = as is)
Shader "Game/PS1 Screen"
{
    Properties
    {
        [Header(Low Resolution)]
        _VirtualHeight ("Virtual Height (rows of game pixels)", Range(120, 720)) = 240

        [Header(Color Depth)]
        _Levels ("Color Levels per channel (PS1 = 32)", Range(2, 64)) = 24
        _Spread ("Dither Strength", Range(0, 1.5)) = 1

        [Header(Old TV)]
        _Bleed ("Color Bleed (sideways smear)", Range(0, 1)) = 0.5
        _Scanlines ("Scanlines", Range(0, 1)) = 0.35
        _Mask ("RGB Phosphor Mask", Range(0, 1)) = 0.15
        _Curvature ("Screen Curvature", Range(0, 0.3)) = 0.06
        _Vignette ("Dark Corners", Range(0, 1)) = 0.35
        _Brightness ("Brightness (makes up for the dark lines)", Range(0.5, 2)) = 1.15
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "PS1Screen"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _VirtualHeight;
            float _Levels;
            float _Spread;
            float _Bleed;
            float _Scanlines;
            float _Mask;
            float _Curvature;
            float _Vignette;
            float _Brightness;
            float _PS1CrtOff; // global (not on the material): 1 = no CRT in this level
            float _PS1Lift;   // global: brightens the shadows of a dark level (0 = as is)

            static const float BAYER4[16] =
            {
                 0,  8,  2, 10,
                12,  4, 14,  6,
                 3, 11,  1,  9,
                15,  7, 13,  5
            };

            // Camera color at the center of one game pixel (point sampled = hard square pixels).
            float3 GamePixel(float2 cell, float2 res)
            {
                float2 uv = (clamp(cell, 0, res - 1) + 0.5) / res;
                return LinearToSRGB(saturate(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).rgb));
            }

            // Brightness (Y) and color (IQ) like an NTSC signal: the cable blurs only the color.
            float3 ToYIQ(float3 c)
            {
                return float3(dot(c, float3(0.299, 0.587, 0.114)),
                              dot(c, float3(0.596, -0.274, -0.322)),
                              dot(c, float3(0.211, -0.523, 0.312)));
            }

            float3 FromYIQ(float3 c)
            {
                return float3(dot(c, float3(1.0, 0.956, 0.621)),
                              dot(c, float3(1.0, -0.272, -0.647)),
                              dot(c, float3(1.0, -1.106, 1.703)));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float crt = 1.0 - saturate(_PS1CrtOff);

                // ---- Curved glass: push the picture outwards from the center ----
                float2 centered = input.texcoord * 2.0 - 1.0;
                centered *= 1.0 + _Curvature * crt * dot(centered, centered);
                float2 uv = centered * 0.5 + 0.5;
                if (any(uv < 0.0) || any(uv > 1.0)) return half4(0, 0, 0, 1); // TV bezel

                // ---- Low resolution grid ----
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 res = float2(round(_VirtualHeight * aspect), _VirtualHeight);
                float2 pos = uv * res;
                float2 cell = floor(pos);

                // ---- Composite color bleed: average the color of the neighbours, keep the brightness ----
                float3 center = GamePixel(cell, res);
                float3 color = center;
                if (_Bleed > 0.0)
                {
                    float3 yiq = ToYIQ(center);
                    float2 chroma = yiq.yz * 0.4
                                  + ToYIQ(GamePixel(cell + float2(-1, 0), res)).yz * 0.3
                                  + ToYIQ(GamePixel(cell + float2(-2, 0), res)).yz * 0.1
                                  + ToYIQ(GamePixel(cell + float2( 1, 0), res)).yz * 0.2;
                    yiq.yz = lerp(yiq.yz, chroma, _Bleed);
                    color = saturate(FromYIQ(yiq));
                }

                // ---- Lift: gamma curve that brightens the dark parts, keeps white white ----
                color = pow(saturate(color), 1.0 / (1.0 + max(_PS1Lift, 0.0)));

                // ---- 15-bit color + ordered dither (one dither cell = one game pixel) ----
                uint2 p = (uint2)cell;
                float threshold = BAYER4[(p.y % 4) * 4 + (p.x % 4)] / 16.0 - 0.5;
                float steps = max(_Levels - 1.0, 1.0);
                color = saturate(color + threshold * _Spread / steps);
                color = floor(color * steps + 0.5) / steps;

                // ---- CRT: dark gap between rows ----
                float row = frac(pos.y);
                float scan = sin(row * PI);                 // 0 at the row edges, 1 in the middle
                color *= lerp(1.0, scan * scan, _Scanlines * crt);

                // ---- CRT: RGB phosphor stripes (by real screen pixel) ----
                uint stripe = (uint)input.positionCS.x % 3;
                float3 mask = stripe == 0 ? float3(1, 0.7, 0.7) : stripe == 1 ? float3(0.7, 1, 0.7) : float3(0.7, 0.7, 1);
                color *= lerp(1.0, mask, _Mask * crt);

                // ---- Dark corners ----
                float2 v = uv * (1.0 - uv);
                float vig = saturate(pow(abs(v.x * v.y * 16.0), 0.25));
                color *= lerp(1.0, vig, _Vignette * crt);

                color *= lerp(1.0, _Brightness, crt); // the boost only makes up for the CRT's dark lines
                return half4(SRGBToLinear(saturate(color)), 1.0);
            }
            ENDHLSL
        }
    }
}
