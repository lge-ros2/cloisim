Shader "Sensor/Segmentation"
{
	Properties
	{
		_SegmentationIdColor ("Id Color", Color) = (0.3, 0.5, 1, 1)
		_SegmentationNameColor ("Name Color", Color) = (0.4, 0.7, 1, 1)
		_SegmentationLayerColor ("Layer Color", Color) = (0.5, 0.9, 1, 1)
		_UnsupportedColor ("Unsupported Color", Color) = (0.1, 0.1, 0.1, 1)
		_OutputMode ("Output Mode", int) = -1
	}

	SubShader
	{
		Tags {
			"RenderType" = "Opaque"
		}

		Pass
		{
			Name "Segmentation"

			HLSLPROGRAM
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			#pragma vertex vert
			#pragma fragment frag

			CBUFFER_START(UnityPerMaterial)
			half4 _SegmentationIdColor;
			half4 _SegmentationNameColor;
			half4 _SegmentationLayerColor;
			half4 _UnsupportedColor;
			int _OutputMode;
			CBUFFER_END

			half4 replacement_output()
			{
				/*
				public enum ReplacementMode
				{
					ObjectId = 0,
					ObjectName = 1,
					ObjectLayer = 2
				};
				*/
				switch (_OutputMode)
				{
				case 0:
					return _SegmentationIdColor;
				case 1:
					return _SegmentationNameColor;
				case 2:
					return _SegmentationLayerColor;
				}
				return _UnsupportedColor;
			}

			struct Attributes
			{
				float4 positionOS : POSITION;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				half4 color : COLOR;
			};

			Varyings vert(Attributes input)
			{
				Varyings output = (Varyings)0;
				VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
				output.positionCS = vertexInput.positionCS;
				half4 color =replacement_output();
				output.color = color;
				return output;
			}

			half4 frag(Varyings i) : SV_Target
			{
				half4 color =replacement_output();
				return color;
			}
			ENDHLSL
		}
	}
}