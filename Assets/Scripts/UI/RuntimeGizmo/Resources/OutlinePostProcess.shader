Shader "Hidden/OutlinePostProcess"
{
    Properties
    {
        _MaskTex ("Mask", 2D) = "black" {}
        _OutlineColor ("Outline Color", Color) = (1, 0.5, 0, 1)
        _OutlineWidth ("Outline Width", Float) = 3.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            sampler2D _MaskTex;
            float4 _MaskTex_TexelSize;
            float4 _OutlineColor;
            float _OutlineWidth;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                
                #if UNITY_UV_STARTS_AT_TOP
                if (_MaskTex_TexelSize.y < 0)
                    output.uv.y = 1.0 - output.uv.y;
                #endif
                
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float mask = tex2D(_MaskTex, input.uv).r;
                if (mask > 0.5) return float4(0,0,0,0); // Inside the object, transparent

                float w = _OutlineWidth * _MaskTex_TexelSize.x;
                float h = _OutlineWidth * _MaskTex_TexelSize.y;

                // Sample around a circle instead of only 8 cardinal/diagonal taps so
                // outline ends and corners read as rounded rather than blocky.
                const int SampleCount = 20;
                float outlineAlpha = 0.0;

                [unroll]
                for (int index = 0; index < SampleCount; index++)
                {
                    float angle = TWO_PI * ((float)index / (float)SampleCount);
                    float2 offset = float2(cos(angle) * w, sin(angle) * h);
                    outlineAlpha = max(outlineAlpha, tex2D(_MaskTex, input.uv + offset).r);
                }

                if (outlineAlpha > 0.5)
                {
                    return float4(_OutlineColor.rgb, _OutlineColor.a * outlineAlpha);
                }
                
                return float4(0,0,0,0);
            }
            ENDHLSL
        }
    }
}
