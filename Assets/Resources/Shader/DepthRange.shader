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
			"RenderPipeline" = "UniversalPipeline"
			"RenderType" = "Opaque"
			"Queue" = "Geometry"
			"ForceNoShadowCasting" = "True"
		}

		Pass
		{
			Cull Back
			ZWrite On
			ZTest LEqual

			HLSLPROGRAM

			#pragma multi_compile_instancing
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

			CBUFFER_START(UnityPerMaterial)
				int _ReverseData;
				int _FlipX;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv         : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float2 uv         : TEXCOORD0;
				float4 positionCS : SV_POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			Varyings vert(Attributes IN)
			{
				Varyings OUT;
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

				OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
				OUT.uv = IN.uv;

				if (_FlipX > 0)
					OUT.uv.x = 1 - OUT.uv.x;

				return OUT;
			}

			float4 frag(Varyings IN) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(IN);

				const float depth = SampleSceneDepth(IN.uv.xy);
				const float linearDepth = Linear01Depth(depth, _ZBufferParams);

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

			ENDHLSL
		}
	}
}