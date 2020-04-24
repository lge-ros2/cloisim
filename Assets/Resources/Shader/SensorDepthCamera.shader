Shader "Sensor/DepthCamera"
{
	SubShader
	{
		Cull Back
		ZWrite Off
		ZTest LEqual

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

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#include "UnityCG.cginc"

			uniform sampler2D _CameraDepthTexture;
			// uniform float4x4 _ViewProjInv;
			uniform half4 _MainTex_TexelSize;

			v2f_img vert(appdata_img v)
			{
				v2f_img o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = MultiplyUV(UNITY_MATRIX_TEXTURE0, v.texcoord);

				// why do we need this? cause sometimes the image I get is flipped. see: http://docs.unity3d.com/Manual/SL-PlatformDifferences.html
			#if UNITY_UV_STARTS_AT_TOP
				if (_MainTex_TexelSize.y < 0)
					o.uv.y = 1 - o.uv.y;
			#endif

				return o;
			}

			float frag(v2f_img i) : SV_Target
			{
				float depth = UNITY_SAMPLE_DEPTH(tex2D(_CameraDepthTexture, i.uv.xy));
				return Linear01Depth(depth);
			}

			ENDCG
		}
	}
	FallBack "VertexLit"
}