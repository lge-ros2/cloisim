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
        Tags { "RenderType"="Opaque" }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Mean;
            float _StdDev;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float rand(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898,78.233))) * 43758.5453);
            }

            // Box–Muller Method
            float gaussianNoise(float2 uv)
            {
                float2 seed = uv * _Time.y;
                float u1 = rand(seed);
                float u2 = rand(seed + 0.37);
                float z = sqrt(-2.0 * log(max(u1, 1e-6))) * cos(6.283185 * u2);
                return z;
            }

            // Approximate Gaussian Noise (Central Limit Theorem based)
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

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                if (col.r > 0.001 || col.g > 0.001 || col.b > 0.001)
                {
                    float noise = gaussianNoise(i.uv) * _StdDev + _Mean;
                    // float noise = fastGaussianNoise(i.uv) * _StdDev + _Mean;
                    col.rgb += noise;
                }
                return saturate(col);
            }
            ENDCG
        }
    }
}