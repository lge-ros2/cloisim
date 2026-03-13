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
		Tags
		{
			"RenderType" = "Opaque"
			"Queue" = "Geometry"
			"ForceNoShadowCasting" = "True"
		}

		Pass
		{
			Cull Back
			ZWrite On
			ZTest LEqual
			Fog { Mode Off }

			CGPROGRAM

			#pragma multi_compile_instancing
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			#include "UnityCG.cginc"

			int _ReverseData;
			int _FlipX;

			uniform sampler2D _CameraDepthTexture;

			v2f_img vert(appdata_img v)
			{
				v2f_img o;
				UNITY_INITIALIZE_OUTPUT(v2f_img, o);

				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.texcoord;

				if (_FlipX > 0)
					o.uv.x = 1 - o.uv.x;

				return o;
			}

			float4 frag(v2f_img i) : COLOR
			{
				const float depth = UNITY_SAMPLE_DEPTH(tex2D(_CameraDepthTexture, i.uv.xy));
				const float linearDepth = Linear01Depth(depth);
	
				// if depth is near to far clip, return nothing
				if (linearDepth > 0.999999)
					return float4(0, 0, 0, 0);

				const float nearClip = _ProjectionParams.y;
				const float farClip  = _ProjectionParams.z;

				const float eyeSpaceDepth = linearDepth * (farClip - nearClip) + nearClip;
				const float normalizedDepth = saturate((eyeSpaceDepth - nearClip) / (farClip - nearClip));

				const float finalDepthRange = (_ReverseData > 0) ? (1.0 - normalizedDepth) : normalizedDepth;
				return float4(finalDepthRange, 0, 0, 0);
			}

			ENDCG
		}
	}
	FallBack "VertexLit"
}