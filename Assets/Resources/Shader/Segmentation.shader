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
			"RenderPipeline" = "UniversalPipeline"
			"RenderType" = "Opaque"
			"Queue" = "Geometry"
		}

		Cull Off // Disable backface culling
		Lighting Off

		HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

		CBUFFER_START(UnityPerMaterial)
		int _SegmentationValue;
		int _Hide;
		CBUFFER_END

		ENDHLSL

		Pass
		{
			Name "PixelSegmentation"

			HLSLPROGRAM

			#pragma target 4.6
			#pragma vertex vert
			#pragma fragment frag

			struct Attributes
			{
				half4 positionOS : POSITION;
			};

			struct Varyings
			{
				half4 positionCS : SV_POSITION;
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
				half4 segColor = half4(0, 0, 0, 0);
				if (_Hide == 0)
				{
					// Encode 16Bits To RG
					float R = ((_SegmentationValue >> 8) & 0xFF) / 255.0;
					float G = (_SegmentationValue & 0xFF) / 255.0;
					
					// Due to little-endian data, SWAP
					segColor.r = G;
					segColor.g = R;
				}
				return segColor;
			}
			ENDHLSL
		}
	}
}