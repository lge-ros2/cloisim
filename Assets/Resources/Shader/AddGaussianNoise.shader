Shader "Sensor/Camera/GaussianNoise"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Mean ("Mean", Range(-1, 1)) = 0.0
        _StdDev ("Standard Deviation", Range(0, 1)) = 0.1
    }
    SubShader
    {
        // Use URP-compatible tags
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }

        Pass
        {
            // HLSL shader program
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Include the URP core library
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Declare properties in a CBuffer for URP
            CBUFFER_START(UnityPerMaterial)
                float _Mean;
                float _StdDev;
            CBUFFER_END

            // Declare texture and sampler the URP way
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // Input to vertex shader
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            // Output from vertex shader, input to fragment shader
            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                // Use modern URP function to transform vertex position
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // Random function (no changes needed)
            float rand(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
            }

            // Box–Muller Method for Gaussian noise (no changes needed)
            float gaussianNoise(float2 uv)
            {
                // Use the URP-provided _Time variable
                float2 seed = uv * _Time.y;
                float u1 = rand(seed);
                float u2 = rand(seed + 0.37);
                float z = sqrt(-2.0 * log(max(u1, 1e-6))) * cos(6.283185 * u2);
                return z;
            }

            // Approximate Gaussian Noise (no changes needed)
            float fastGaussianNoise(float2 uv)
            {
                float n = 0.0;
                float2 seed = uv * _Time.y;

                n += rand(seed);
                n += rand(seed + 1.23);
                n += rand(seed + 2.54);
                n += rand(seed + 3.91);
                n += rand(seed + 4.77);
                n += rand(seed + 5.62);

                n = (n / 6.0 - 0.5) * 2.0;
                return n;
            }

            // Fragment shader
            half4 frag (Varyings IN) : SV_Target
            {
                // Sample texture the URP way
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                if (col.r > 0.001 || col.g > 0.001 || col.b > 0.001)
                {
                    float noise = gaussianNoise(IN.uv) * _StdDev + _Mean;
                    // float noise = fastGaussianNoise(IN.uv) * _StdDev + _Mean;
                    col.rgb += noise;
                }

                return saturate(col);
            }
            ENDHLSL
        }
    }
}