// Simple 3-colour gradient sky (top / horizon / bottom) for URP.
// Used instead of Unity's procedural sky: its dark sunset colours turn into teal blocks
// after the 4-level PixelDither effect. These colours are chosen so they dither cleanly.
Shader "Game/Sky Gradient"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.33, 0.12, 0.1, 1)
        _HorizonColor ("Horizon", Color) = (0.95, 0.5, 0.25, 1)
        _BottomColor ("Below Horizon", Color) = (0.3, 0.2, 0.16, 1)
        _Exponent ("Horizon Sharpness", Range(0.2, 4)) = 0.8
        _SunColor ("Sun", Color) = (1, 0.85, 0.55, 1)
        _SunSize ("Sun Size", Range(0, 0.1)) = 0.03
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl" // has _MainLightPosition

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half _Exponent;
                half4 _SunColor;
                half _SunSize;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                half3 color = dir.y >= 0
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(dir.y), _Exponent))
                    : lerp(_HorizonColor.rgb, _BottomColor.rgb, saturate(-dir.y * 6));

                // Sun disc from the main directional light
                float3 sunDir = _MainLightPosition.xyz;
                half sun = step(1 - _SunSize, dot(dir, sunDir));
                color = lerp(color, _SunColor.rgb, sun);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
