Shader "Sensor/Segmentation"
{
	Properties
	{
		_SegmentationValue  ("Segmentation Value", int) = 0
		[Toggle] _Hide ("Hide this label", int) = 0
	}

	SubShader
	{
		Tags
		{
			"RenderPipeline" = "HDRenderPipeline"
			"RenderType" = "Opaque"
			"Queue" = "Geometry"
		}

		Cull Back
		ZWrite On
		ZTest LEqual

		HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

		// Per-draw segmentation parameters set as globals by SegmentationPass.
		// Using a unique name (_SegParams) to avoid collision with the
		// Properties block's _SegmentationValue material binding.
		// x = segmentation class ID (uint16), y = reserved
		float4 _SegParams;

		// Custom VP matrix set by SegmentationPass via SetGlobalMatrix.
		// Bypasses HDRP's _ViewProjMatrix in ShaderVariablesGlobal CBUFFER
		// which contains stale data when rendering to a dedicated RT.
		float4x4 _SegViewProjMatrix;

		ENDHLSL

		Pass
		{
			Name "PixelSegmentation"
			Tags { "LightMode" = "ForwardOnly" }

			HLSLPROGRAM

			#pragma target 4.5
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing

			struct Attributes
			{
				float4 positionOS : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			Varyings vert(Attributes i)
			{
				Varyings o;
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_TRANSFER_INSTANCE_ID(i, o);

				// Use HDRP's per-draw model matrix (set by cmd.DrawRenderer)
				// to get world position, then our custom VP matrix to project.
				float3 worldPos = TransformObjectToWorld(i.positionOS.xyz);
				o.positionCS = mul(_SegViewProjMatrix, float4(worldPos, 1.0));
				return o;
			}

			half4 frag(Varyings i) : SV_Target
			{
				int segVal = (int)_SegParams.x;
				// Encode uint16 class ID into R (low byte) and G (high byte)
				float lo = (segVal & 0xFF) / 255.0;
				float hi = ((segVal >> 8) & 0xFF) / 255.0;
				return half4(lo, hi, 0, 1);
			}
			ENDHLSL
		}
	}
	FallBack Off
}