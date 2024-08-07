Shader "Sensor/Segmentation"
{
	Properties
	{
		_SegmentationColor ("Segmentation Color", Color) = (1, 1, 1, 1)
		_SegmentationClassId ("Segmentation Class ID Value in 16bits", Color) = (0, 0, 0, 1)
		_DisableColor ("Disable Color method", int) = 0
		_Hide ("Hide this label", int) = 0
	}

	SubShader
	{
		Tags
		{
			"RenderPipeline" = "UniversalPipeline"
			"RenderType" = "Opaque"
			"Queue" = "Geometry"
		}

		Cull Off // Disable backface culling
		Lighting Off

		HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

		CBUFFER_START(UnityPerMaterial)
		half4 _SegmentationColor;
		half4 _SegmentationClassId;
		int _DisableColor;
		int _Hide;
		CBUFFER_END

		ENDHLSL

		Pass
		{
			Name "PixelSegmentation"

			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			struct Attributes
			{
				float4 positionOS : POSITION;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
			};

			Varyings vert(Attributes i)
			{
				Varyings o;
				VertexPositionInputs vertexInput = GetVertexPositionInputs(i.positionOS.xyz);
				o.positionCS = vertexInput.positionCS;
				return o;
			}

			half4 frag(Varyings i) : SV_Target
			{
				half4 segColor = half4(0, 0, 0, 1);
				if (_Hide == 0)
				{
					segColor = (_DisableColor == 0)? _SegmentationColor : _SegmentationClassId;
				}
				return segColor;
			}
			ENDHLSL
		}
	}
}