Shader "Custom/ProceduralGrass"
{
	Properties
	{
		_BaseColor("Base Color", Color) = (0, 0, 0, 1)
		_TipColor("Tip Color", Color) = (1, 1, 1, 1)
		_BaseTex("Base Texture", 2D) = "white" {}
	}

	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque"
			"Queue" = "Geometry"
			"RenderPipeline" = "UniversalPipeline"
		}

		HLSLINCLUDE
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _SHADOWS_SOFT

			struct appdata
			{
				uint vertexID : SV_VertexID;
				uint instanceID : SV_InstanceID;
			};

			struct v2f
			{
				float4 positionCS : SV_Position;
				float4 positionWS : TEXCOORD0;
				float2 uv : TEXCOORD1;
			};

			StructuredBuffer<float3> _Positions;
			StructuredBuffer<float3> _Normals;
			StructuredBuffer<float2> _UVs;
			StructuredBuffer<float4x4> _TransformMatrices;

			CBUFFER_START(UnityPerMaterial)
				float4 _BaseColor;
				float4 _TipColor;

				sampler2D _BaseTex;
				float4 _BaseTex_ST;

				float _Cutoff;
			CBUFFER_END
		ENDHLSL

		Pass
		{
			Name "GrassPass"
			Tags { "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			v2f vert(appdata v)
			{
				v2f o;

				float4 positionOS = float4(_Positions[v.vertexID], 1.0f);
				float4x4 objectToWorld = _TransformMatrices[v.instanceID];

				o.positionWS = mul(objectToWorld, positionOS);
				o.positionCS = mul(UNITY_MATRIX_VP, o.positionWS);
				o.uv = _UVs[v.vertexID];

				return o;
			}

			float4 frag(v2f i) : SV_Target
			{
				float4 color = tex2D(_BaseTex, i.uv);

//#ifdef _MAIN_LIGHT_SHADOWS
				VertexPositionInputs vertexInput = (VertexPositionInputs)0;
				vertexInput.positionWS = i.positionWS;

				float4 shadowCoord = GetShadowCoord(vertexInput);
				float shadowAttenuation = saturate(MainLightRealtimeShadow(shadowCoord) + 0.25f);
				float4 shadowColor = lerp(0.0f, 1.0f, shadowAttenuation);
				color *= shadowColor;
//#endif
				return color * lerp(_BaseColor, _TipColor, i.uv.y);
			}

            ENDHLSL
        }

		Pass
		{
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }

			ZWrite On
			ZTest LEqual

			HLSLPROGRAM
			#pragma vertex shadowVert
			#pragma fragment shadowFrag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

			float3 _LightDirection;
			float3 _LightPosition;

			v2f shadowVert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
			{
				v2f o;

				float4 positionOS = float4(_Positions[vertexID], 1.0f);
				float3 normalOS = _Normals[vertexID];
				float4x4 objectToWorld = _TransformMatrices[instanceID];

				float4 positionWS = mul(objectToWorld, positionOS);
				o.positionCS = mul(UNITY_MATRIX_VP, positionWS);
				o.uv = _UVs[vertexID];

				float3 normalWS = TransformObjectToWorldNormal(normalOS);

				// Code required to account for shadow bias.
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
				float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
				float3 lightDirectionWS = _LightDirection;
#endif
				o.positionWS = float4(ApplyShadowBias(positionWS, normalWS, lightDirectionWS), 1.0f);

				return o;
			}

			float4 shadowFrag(v2f i) : SV_Target
			{
				//Alpha(SampleAlbedoAlpha(i.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
				return 0;
			}

			ENDHLSL
		}
    }
	Fallback Off
}
