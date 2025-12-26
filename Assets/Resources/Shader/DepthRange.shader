// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'
Shader "Sensor/DepthRange"
{
	Properties
	{
		[Toggle] _ReverseData ("reverse depth data", int) = 0
		[Toggle] _FlipX ("horizontal flip", int) = 1
	}

	SubShader
	{
		Pass
		{
			Cull Back
			ZWrite On
			ZTest LEqual
			ColorMask 0
		}

		Tags
		{
			"RenderType" = "Opaque"
			"Queue" = "Geometry"
			"ForceNoShadowCasting" = "True"
		}

		Pass
		{
			Fog { Mode Off }

			CGPROGRAM

			int _ReverseData;
			int _FlipX;

			#pragma multi_compile_instancing
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			#include "UnityCG.cginc"

			uniform sampler2D _CameraDepthTexture;
			uniform half4 _MainTex_TexelSize;

			v2f_img vert(appdata_img v)
			{
				v2f_img o;
				UNITY_INITIALIZE_OUTPUT(v2f_img, o);

				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = MultiplyUV(UNITY_MATRIX_TEXTURE0, v.texcoord);

				// why do we need this? cause sometimes the image I get is flipped. see: http://docs.unity3d.com/Manual/SL-PlatformDifferences.html
				#if UNITY_UV_STARTS_AT_TOP
				if (_MainTex_TexelSize.y < 0)
					o.uv.y = 1 - o.uv.y;
				#endif

				if (_FlipX > 0)
					o.uv.x = 1 - o.uv.x;

				return o;
			}

			float4 frag(v2f_img i) : COLOR
			{
				float depth = UNITY_SAMPLE_DEPTH(tex2D(_CameraDepthTexture, i.uv.xy));
				float linearDepth = Linear01Depth(depth);

				float nearClip = _ProjectionParams.y;
				float farClip  = _ProjectionParams.z;

				float eyeSpaceDepth = linearDepth * (farClip - nearClip) + nearClip;
					
				// if depth is near farclip, return nothing
				if (linearDepth > 0.9999)
					return float4(0, 0, 0, 0);
				
				float normalizedDepth = saturate((eyeSpaceDepth - nearClip) / (farClip - nearClip));

				float finalDepthRange = (_ReverseData > 0) ? (1.0 - normalizedDepth) : normalizedDepth;
				
				return float4(finalDepthRange, 0, 0, 0);
			}

			ENDCG
		}
	}
	FallBack "VertexLit"
}