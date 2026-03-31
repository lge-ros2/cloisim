Shader "Custom/Unlit/VideoTexture"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv         : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);

                // rotating UV by 90 degrees
                const float Deg2Rad = (PI * 2.0) / 360.0;
                const float Rotation = 90.0;

                float rotationRadians = Rotation * Deg2Rad;
                float s = sin(rotationRadians);
                float c = cos(rotationRadians);

                float2x2 rotationMatrix = float2x2(c, -s, s, c);
                float2 uv = IN.uv - 0.5;
                uv = mul(rotationMatrix, uv);
                uv += 0.5;

                OUT.uv = TRANSFORM_TEX(uv, _MainTex);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                return col;
            }
            ENDHLSL
        }
    }
}