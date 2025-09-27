Shader "URP/ScreenBlurSimple"
{
    Properties
    {
        _BlurRadius ("Blur Radius (px-ish)", Range(0, 6)) = 2
        _BlurSamples ("Samples per axis", Range(1, 8)) = 4
        _Tint ("Tint (RGB) & Strength (A)", Color) = (1,1,1,0.0)
        _Alpha ("Overall Opacity", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back

        Pass
        {
            Name "ScreenBlur"
            Tags{ "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Scene color captured after opaques (enable Opaque Texture)
            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            CBUFFER_START(UnityPerMaterial)
                float _BlurRadius;
                float _BlurSamples;
                float4 _Tint;
                float _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_Position;
                float4 screenPos   : TEXCOORD0; // for ComputeScreenPos
                float2 uv          : TEXCOORD1;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS);
                o.screenPos   = ComputeScreenPos(o.positionHCS);
                o.uv          = v.uv;
                return o;
            }

            float4 SampleScene(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv);
            }

            float4 frag (Varyings i) : SV_Target
            {
                // Screen-space UV
                float2 uv = i.screenPos.xy / i.screenPos.w;

                // Convert a pixel-ish radius to UV using screen size
                float2 texel = 1.0 / _ScreenParams.xy;
                float r = _BlurRadius;

                int n = (int)_BlurSamples;
                float2 stepU = float2(texel.x * r, 0);
                float2 stepV = float2(0, texel.y * r);

                float4 acc = 0;
                float w = 0;

                // Center tap
                acc += SampleScene(uv); w += 1;

                // Axes taps
                [unroll(8)] for (int k=1; k<=n; k++)
                {
                    float2 du = stepU * k;
                    float2 dv = stepV * k;

                    acc += SampleScene(uv + du); w += 1;
                    acc += SampleScene(uv - du); w += 1;
                    acc += SampleScene(uv + dv); w += 1;
                    acc += SampleScene(uv - dv); w += 1;
                }

                // Diagonal taps (comment out to make it cheaper)
                [unroll(8)] for (int k=1; k<=n; k++)
                {
                    float2 d = float2(stepU.x * k, stepV.y * k);
                    acc += SampleScene(uv + d);               w += 1;
                    acc += SampleScene(uv - d);               w += 1;
                    acc += SampleScene(uv + float2( d.x,-d.y)); w += 1;
                    acc += SampleScene(uv + float2(-d.x, d.y)); w += 1;
                }

                float4 col = acc / max(w, 1e-5);

                // Optional tint strength from _Tint.a, and overall opacity via _Alpha
                col.rgb = lerp(col.rgb, col.rgb * _Tint.rgb, saturate(_Tint.a));
                col.a = _Alpha;

                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
