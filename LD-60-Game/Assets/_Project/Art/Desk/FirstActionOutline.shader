Shader "LD60/First Action Outline"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _OutlineColor ("Outline color", Color) = (1, 0.84, 0.18, 1)
        _SpriteRect ("Sprite UV bounds", Vector) = (0, 0, 1, 1)
        _OutlineStep ("Outline UV width", Vector) = (0.01, 0.01, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float4 _SpriteRect;
                float4 _OutlineStep;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            half AlphaAt(float2 uv)
            {
                float2 inside = step(_SpriteRect.xy, uv) * step(uv, _SpriteRect.zw);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a * inside.x * inside.y;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half around = 0;
                [unroll] for (int i = 0; i < 16; i++)
                {
                    float angle = i * (6.2831853 / 16.0);
                    float2 offset = float2(cos(angle), sin(angle)) * _OutlineStep.xy;
                    around = max(around, AlphaAt(input.uv + offset));
                }
                return half4(_OutlineColor.rgb, around * (1 - AlphaAt(input.uv)) * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
