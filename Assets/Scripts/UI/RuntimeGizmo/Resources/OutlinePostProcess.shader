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
                
                float maxMask = 0.0;
                // 8-neighbor sample for a smooth outline
                maxMask = max(maxMask, tex2D(_MaskTex, input.uv + float2(w, 0)).r);
                maxMask = max(maxMask, tex2D(_MaskTex, input.uv + float2(-w, 0)).r);
                maxMask = max(maxMask, tex2D(_MaskTex, input.uv + float2(0, h)).r);
                maxMask = max(maxMask, tex2D(_MaskTex, input.uv + float2(0, -h)).r);
                
                maxMask = max(maxMask, tex2D(_MaskTex, input.uv + float2(w, h)).r);
                maxMask = max(maxMask, tex2D(_MaskTex, input.uv + float2(-w, -h)).r);
                maxMask = max(maxMask, tex2D(_MaskTex, input.uv + float2(w, -h)).r);
                maxMask = max(maxMask, tex2D(_MaskTex, input.uv + float2(-w, h)).r);

                if (maxMask > 0.5)
                {
                    return _OutlineColor;
                }
                
                return float4(0,0,0,0);
            }
            ENDHLSL
        }
    }
}
