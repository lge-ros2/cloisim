// Sensor/DepthRange
// Rasterization-based depth acquisition for DepthCamera (URP).
// Output contract MUST match the previous URT DepthCameraRayTrace.compute so the
// downstream compute shaders (VCSELPrepass, DepthBufferScaling) are reused unchanged:
//
//     _computeBufferSrc[i] = planarZ / farClip   (in [0,1]),  miss/background = 0
//
// Uses Linear01Depth for the miss check (robust against float precision near the
// farClip-scaled threshold) and LinearEyeDepth for the actual physical distance in
// meters (matches how a real depth sensor reports distance). Both already account for
// reversed-Z (Vulkan/Unity 6) via _ZBufferParams, so no manual reverse-Z handling is
// needed here.
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
			Cull Off
			ZWrite Off
			ZTest Always

			HLSLPROGRAM

			#pragma multi_compile_instancing
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

			CBUFFER_START(UnityPerMaterial)
				int _ReverseData;
				int _FlipX;
			CBUFFER_END

			// Blitter.BlitTexture() draws a procedural full-screen triangle with no bound
			// vertex buffer, so a mesh-style Attributes/TransformObjectToHClip vertex shader
			// reads garbage POSITION/UV data and produces a degenerate triangle that covers
			// no pixels. Use Core RP's SV_VertexID-based full-screen-triangle helpers instead
			// (same struct names/semantics as Blit.hlsl's Attributes/Varyings).
			Varyings vert(Attributes IN)
			{
				Varyings OUT;
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

				OUT.positionCS = GetFullScreenTriangleVertexPosition(IN.vertexID);
				OUT.texcoord = GetFullScreenTriangleTexCoord(IN.vertexID);

				if (_FlipX > 0)
					OUT.texcoord.x = 1 - OUT.texcoord.x;

				return OUT;
			}

			float4 frag(Varyings IN) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(IN);

				const float depth = SampleSceneDepth(IN.texcoord.xy);

				// Miss check on the normalized 0..1 value: comparing against a farClip-scaled
				// eye-space value here caused float precision false-negatives (e.g. a background
				// pixel landing at 0.999996 * farClip, just under a farClip-scaled threshold).
				const float linear01Depth = Linear01Depth(depth, _ZBufferParams);
				if (linear01Depth > 0.999999)
					return float4(0, 0, 0, 0);

				const float farClip = _ProjectionParams.z;

				// Physical eye-space distance in meters. Mathematically equal to
				// linear01Depth * (farClip - nearClip) + nearClip, but computed directly for
				// better precision (matches how a real depth sensor reports distance).
				const float eyeSpaceDepth = LinearEyeDepth(depth, _ZBufferParams);

				// Contract: planarZ / farClip in [0,1] (matches DepthBufferScaling.compute's
				// depth_ranged(d) = d * _DepthMax, where _DepthMax is set to farClip).
				const float normalizedDepth = saturate(eyeSpaceDepth / farClip);

				const float finalDepthRange = (_ReverseData > 0) ? (1.0 - normalizedDepth) : normalizedDepth;
				return float4(finalDepthRange, 0, 0, 0);
			}

			ENDHLSL
		}
	}
}
