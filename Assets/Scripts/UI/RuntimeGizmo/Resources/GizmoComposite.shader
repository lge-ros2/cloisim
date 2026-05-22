/*
 * Copyright (c) 2025 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

Shader "Hidden/GizmoComposite"
{
	Properties
	{
		_GizmoTex ("Gizmo Texture", 2D) = "black" {}
		_ClipRect ("Clip Rect", Vector) = (0, 0, 0, 0)
	}
	SubShader
	{
		Tags { "RenderPipeline" = "UniversalPipeline" }

		Pass
		{
			ZTest Always
			ZWrite Off
			Cull Off
			Blend One OneMinusSrcAlpha

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			#define MAX_CLIP_RECTS 64

			sampler2D _GizmoTex;
			int _ClipRectCount;
			float4 _ClipRects[MAX_CLIP_RECTS]; // (xMin, yMin, xMax, yMax) in normalized coords

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			Varyings vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.uv = input.uv;
				return output;
			}

			half4 frag(Varyings input) : SV_Target
			{
				[loop]
				for (int index = 0; index < _ClipRectCount; index++)
				{
					float4 clipRect = _ClipRects[index];
					if (input.uv.x >= clipRect.x && input.uv.x <= clipRect.z &&
						input.uv.y >= clipRect.y && input.uv.y <= clipRect.w)
					{
						discard;
					}
				}

				half4 col = tex2D(_GizmoTex, input.uv);
				// RT content is already premultiplied (SrcAlpha blend onto clear background)
				// Output as-is for Blend One OneMinusSrcAlpha
				return col;
			}
			ENDHLSL
		}
	}
}
