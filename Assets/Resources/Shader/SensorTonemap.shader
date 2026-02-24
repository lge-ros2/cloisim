Shader "Sensor/Tonemap"
{
	Properties
	{
		_MainTex ("Main Texture", 2D) = "white" {}
		_Exposure ("Exposure", Float) = 1.0
	}

	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque"
			"Queue" = "Geometry"
			"ForceNoShadowCasting" = "True"
		}

		Pass
		{
			Cull Off
			ZWrite Off
			ZTest Always

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			sampler2D _MainTex;
			float _Exposure;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			// ACES filmic tone mapping curve (Narkowicz 2015)
			float3 ACESFilm(float3 x)
			{
				float a = 2.51;
				float b = 0.03;
				float c = 2.43;
				float d = 0.59;
				float e = 0.14;
				return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
			}

			// Linear to sRGB gamma conversion
			float3 LinearToSRGB(float3 c)
			{
				float3 lo = c * 12.92;
				float3 hi = 1.055 * pow(max(c, 0.0), 1.0 / 2.4) - 0.055;
				return lerp(lo, hi, step(0.0031308, c));
			}

			float4 frag(v2f i) : SV_Target
			{
				float4 hdr = tex2D(_MainTex, i.uv);
				float3 color = max(hdr.rgb * _Exposure, 0.0);

				// Apply ACES tonemapping
				color = ACESFilm(color);

				// Output linear color — the R8G8B8A8_SRGB render target
				// applies hardware linear-to-sRGB conversion automatically.
				return float4(color, 1.0);
			}

			ENDCG
		}
	}
}
