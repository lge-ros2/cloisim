Shader "Sensor/Segmentation"
{
	Properties
	{
		_SegmentationColor ("Segmentation Color", Color) = (1, 1, 1, 1)
		_SegmentationClassId ("Segmentation Class ID Value in 16bits", Color) = (0, 0, 0, 1)
		_DisableColor ("Disable Color output", int) = 0
		_Hide ("Hide this label", int) = 0
	}

	SubShader
	{
		Tags {
			"RenderType" = "Opaque"
		}

		Pass
		{
			Name "Segmentation"

			Cull Off // Disable backface culling

			HLSLPROGRAM
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			#pragma vertex vert
			#pragma fragment frag

			CBUFFER_START(UnityPerMaterial)
			half4 _SegmentationColor;
			half4 _SegmentationClassId;
			int _DisableColor;
			int _Hide;
			CBUFFER_END

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

				half4 output_color = half4(0, 0, 0, 1);
				if (_Hide == 0)
				{
					output_color = (_DisableColor == 0)? _SegmentationColor : _SegmentationClassId;
				}
				output.color = output_color;
				return output;
			}

			half4 frag(Varyings i) : SV_Target
			{
				half4 output_color = half4(0, 0, 0, 1);
				if (_Hide == 0)
				{
					output_color = (_DisableColor == 0)? _SegmentationColor : _SegmentationClassId;
				}
				return output_color;
			}
			ENDHLSL
		}
	}
}