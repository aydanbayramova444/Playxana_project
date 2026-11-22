// Old-console FMV look for the cutscene video (CutscenePlayer blits the video through this into a tiny image):
// soft downscale, MJPEG-like colour blocks, 15-bit colour with dithering, faint interlace lines, cold blue tint.
Shader "Hidden/Game/OldFmv"
{
    Properties
    {
        _MainTex ("Video", 2D) = "black" {}
        _Out ("Output size", Vector) = (384, 216, 0, 0)
        _BlueTint ("Blue tint", Range(0, 1)) = 0.35
        _Blocks ("Compression blocks", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Out;
            float _BlueTint;
            float _Blocks;

            static const float BAYER[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            float3 ReadVideo(float2 uv)
            {
                float3 c = saturate(tex2Dlod(_MainTex, float4(saturate(uv), 0, 0)).rgb);
            #ifndef UNITY_COLORSPACE_GAMMA
                c = LinearToGammaSpace(c);
            #endif
                return c;
            }

            // 16 taps over 1.5 output pixels: downscales without shimmer and a little blurry, like old video
            float3 Soft(float2 uv, float size)
            {
                float2 stepUv = size / _Out.xy / 4.0;
                float3 sum = 0;
                for (int x = 0; x < 4; x++)
                    for (int y = 0; y < 4; y++)
                        sum += ReadVideo(uv + (float2(x, y) - 1.5) * stepUv);
                return sum / 16.0;
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float3 luma = float3(0.299, 0.587, 0.114);
                float2 px = floor(i.uv * _Out.xy);

                float3 c = Soft(i.uv, 1.5);

                // Video codecs of the 90s kept colour per block and squashed the detail inside it
                float2 blockUv = (floor(px / 8.0) + 0.5) * 8.0 / _Out.xy;
                float3 block = Soft(blockUv, 8.0);
                float y = dot(c, luma);
                float by = dot(block, luma);
                float3 coded = lerp(y, by, 0.18) + (block - by);
                c = lerp(c, coded, _Blocks);

                // Cold, slightly washed-out tube TV
                float grey = dot(c, luma);
                float3 cold = lerp(c, grey, 0.3) * float3(0.72, 0.92, 1.22) + float3(0.0, 0.012, 0.05);
                c = lerp(c, cold, _BlueTint);

                // Interlace, then 15-bit colour with the console's ordered dither
                c *= fmod(px.y, 2.0) < 1.0 ? 0.94 : 1.0;
                uint2 p = (uint2)px % 4;
                float threshold = (BAYER[p.y * 4 + p.x] + 0.5) / 16.0;
                c = floor(saturate(c) * 31.0 + threshold) / 31.0;

            #ifndef UNITY_COLORSPACE_GAMMA
                c = GammaToLinearSpace(c);
            #endif
                return float4(c, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
