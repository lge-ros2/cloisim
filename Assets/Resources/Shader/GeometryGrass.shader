Shader "Custom/GeometryGrass"
{
	Properties
	{
		// Albedo color properties.
		_BaseColor("Base Color", Color) = (1, 1, 1, 1)
		_TipColor("Tip Color", Color) = (1, 1, 1, 1)
		_BaseTex("Base Texture", 2D) = "white" {}

		// Blade size properties.
		_BladeWidthMin("Blade Width (Min)", Range(0, 0.1)) = 0.02
		_BladeWidthMax("Blade Width (Max)", Range(0, 0.1)) = 0.05
		_BladeHeightMin("Blade Height (Min)", Range(0, 2)) = 0.1
		_BladeHeightMax("Blade Height (Max)", Range(0, 2)) = 0.2

		// Blade shape properties.
		_BladeBendDistance("Blade Forward Amount", Range(0, 1)) = 0.38
		_BladeBendCurve("Blade Curvature Amount", Range(1, 4)) = 2
		_BladeBendDelta("Bend Variation", Range(0, 1)) = 0.2

		// Tessellation properties.
		_TessAmount("Tessellation Amount", Range(1, 64)) = 64
		_TessMinDistance("Min Tessellation Distance", Range(0, 1000)) = 100
		_TessMaxDistance("Max Tessellation Distance", Range(0, 1000)) = 400

		// Grass visibility properties.
		_GrassMap("Grass Visibility Map", 2D) = "white" {}
		_GrassThreshold("Grass Visibility Threshold", Range(-0.1, 1)) = 0.5
		_GrassFalloff("Grass Visibility Fade-In Falloff", Range(0, 1)) = 0.05

		// Dry Grass properties.
		_DryGrassMap("Dry Grass Map", 2D) = "black" {}
		_DryGrassColor("Dry Grass Color", Color) = (1, 0.7, 0, 1)

		// Wind properties.
		_WindMap("Wind Offset Map", 2D) = "bump" {}
		_WindVelocity("Wind Velocity", Vector) = (1, 0, 0, 0)
		_WindFrequency("Wind Pulse Frequency", Range(0, 1)) = 0.01

		_GrassOffset("Grass Offset, temporary property", Vector) = (0, 0, 0, 0)
	}

	SubShader
	{
		Tags
		{
			"RenderPipeline" = "UniversalPipeline"
			"RenderType" = "Opaque"
			"Queue" = "Geometry"
			"IgnoreProjector" = "True"
		}

		LOD 100
		Cull Off

		HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

		#pragma require geometry
		#pragma require tessellation tessHW

		#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
		#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
		#pragma multi_compile _ _SHADOWS_SOFT

		#pragma multi_compile_local DRY_GRASS_ON _
		#pragma multi_compile_local VISIBILITY_ON _
		#pragma multi_compile_local WIND_OFF _

		#pragma multi_compile_instancing
		#pragma instancing_options renderinglayer
		#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

		#define BLADE_SEGMENTS 4

		CBUFFER_START(UnityPerMaterial)
			float4 _BaseColor;
			float4 _TipColor;
			sampler2D _BaseTex;
			float4 _BaseTex_ST;

			float _BladeWidthMin;
			float _BladeWidthMax;
			float _BladeHeightMin;
			float _BladeHeightMax;

			float _BladeBendDistance;
			float _BladeBendCurve;
			float _BladeBendDelta;

			float _TessAmount;
			float _TessMinDistance;
			float _TessMaxDistance;

			sampler2D _GrassMap;
			float4 _GrassMap_ST;
			float  _GrassThreshold;
			float  _GrassFalloff;

			sampler2D _DryGrassMap;
			float4 _DryGrassMap_ST;
			float4 _DryGrassColor;

			sampler2D _WindMap;
			float4 _WindMap_ST;
			float4 _WindVelocity;
			float  _WindFrequency;

			float _Cutoff;

			float4 _GrassOffset;
		CBUFFER_END

		struct appdata
		{
			float4 positionOS: POSITION;
			float3 normalOS : NORMAL;
			float4 tangentOS : TANGENT;
			float2 uv : TEXCOORD0;
		};

		struct tessControlPoint
		{
			float4 positionWS : INTERNALTESSPOS;
			float3 normalWS : NORMAL;
			float4 tangentWS : TANGENT;
			float2 uv : TEXCOORD0;
		};

		struct v2g
		{
			float4 positionWS : SV_POSITION;
			float3 normalWS : NORMAL;
			float4 tangentWS : TANGENT;
			float2 uv : TEXCOORD0;
		};

		struct tessFactors
		{
			float edge[3] : SV_TessFactor;
			float inside  : SV_InsideTessFactor;
		};

		struct g2f
		{
			float4 positionCS : SV_POSITION;
			float3 positionWS : TEXCOORD0;
			float2 uv : TEXCOORD1;
			float4 dryRate : BLENDWEIGHT;
		};

		// Following functions from Roystan's code:
		// (https://github.com/IronWarrior/UnityGrassGeometryShader)

		// Simple noise function, sourced from http://answers.unity.com/answers/624136/view.html
		// Extended discussion on this function can be found at the following link:
		// https://forum.unity.com/threads/am-i-over-complicating-this-random-function.454887/#post-2949326
		// Returns a number in the 0...1 range.
		float rand(float3 co)
		{
			return frac(sin(dot(co.xyz, float3(12.9898, 78.233, 53.539))) * 43758.5453);
		}

		// Construct a rotation matrix that rotates around the provided axis, sourced from:
		// https://gist.github.com/keijiro/ee439d5e7388f3aafc5296005c8c3f33
		float3x3 angleAxis3x3(float angle, float3 axis)
		{
			float c, s;
			sincos(angle, s, c);

			float t = 1 - c;
			float x = axis.x;
			float y = axis.y;
			float z = axis.z;

			return float3x3
			(
				t * x * x + c, t * x * y - s * z, t * x * z + s * y,
				t * x * y + s * z, t * y * y + c, t * y * z - s * x,
				t * x * z - s * y, t * y * z + s * x, t * z * z + c
			);
		}

		float3x3 identity3x3()
		{
			return float3x3
			(
				1, 0, 0,
				0, 1, 0,
				0, 0, 1
			);
		}

		// Vertex shader which transforms from object to world space.
		tessControlPoint vert(appdata v)
		{
			tessControlPoint o;

			o.positionWS = float4(TransformObjectToWorld(v.positionOS), 1.0f);
			o.normalWS = TransformObjectToWorldNormal(v.normalOS);
			o.tangentWS = v.tangentOS;
			o.uv = TRANSFORM_TEX(v.uv, _GrassMap);

			return o;
		}

		// Tessellation hull and domain shaders derived in part from Catlike Coding's tutorial:
		// https://catlikecoding.com/unity/tutorials/advanced-rendering/tessellation/

		// The patch constant function generates additional vertices along the edge
		// and in the center of the triangle. This function adds fewer vertices as the
		// camera gets further from the triangle.
		tessFactors patchConstantFunc(InputPatch<tessControlPoint, 3> patch)
		{
			tessFactors f;

			float3 triPos0 = patch[0].positionWS.xyz;
			float3 triPos1 = patch[1].positionWS.xyz;
			float3 triPos2 = patch[2].positionWS.xyz;

			float3 edgePos0 = 0.5f * (triPos1 + triPos2);
			float3 edgePos1 = 0.5f * (triPos0 + triPos2);
			float3 edgePos2 = 0.5f * (triPos0 + triPos1);

			float3 camPos = _WorldSpaceCameraPos;

			float dist0 = distance(edgePos0, camPos);
			float dist1 = distance(edgePos1, camPos);
			float dist2 = distance(edgePos2, camPos);

			float fadeDist = _TessMaxDistance - _TessMinDistance;

			float edgeFactor0 = saturate(1.0f - (dist0 - _TessMinDistance) / fadeDist);
			float edgeFactor1 = saturate(1.0f - (dist1 - _TessMinDistance) / fadeDist);
			float edgeFactor2 = saturate(1.0f - (dist2 - _TessMinDistance) / fadeDist);

			f.edge[0] = max(pow(edgeFactor0, 2) * _TessAmount, 1);
			f.edge[1] = max(pow(edgeFactor1, 2) * _TessAmount, 1);
			f.edge[2] = max(pow(edgeFactor2, 2) * _TessAmount, 1);

			f.inside = (f.edge[0] + f.edge[1] + f.edge[2]) / 3.0f;

			return f;
		}

		// The hull function is the first half of the tessellation shader.
		// It operates on each patch (in our case, a patch is a triangle),
		// and outputs new control points for the other tessellation stages.
		//
		// The patch constant function is where we create new control points
		// (which are kind of like new vertices).
		[domain("tri")]
		[outputcontrolpoints(3)]
		[outputtopology("triangle_cw")]
		[partitioning("integer")]
		[patchconstantfunc("patchConstantFunc")]
		tessControlPoint hull(InputPatch<tessControlPoint, 3> patch, uint id : SV_OutputControlPointID)
		{
			return patch[id];
		}

		// In between the hull shader stage and the domain shader stage, the
		// tessellation stage takes place. This is where, under the hood,
		// the graphics pipeline actually generates the new vertices.

		// The domain function is the second half of the tessellation shader.
		// It interpolates the properties of the vertices (position, normal, etc.)
		// to create new vertices.
		[domain("tri")]
		v2g domain(tessFactors factors, OutputPatch<tessControlPoint, 3> patch, float3 barycentricCoordinates : SV_DomainLocation)
		{
			v2g i;

			#define INTERPOLATE(fieldname) i.fieldname = \
				patch[0].fieldname * barycentricCoordinates.x + \
				patch[1].fieldname * barycentricCoordinates.y + \
				patch[2].fieldname * barycentricCoordinates.z;

			INTERPOLATE(positionWS)
			INTERPOLATE(normalWS)
			INTERPOLATE(tangentWS)
			INTERPOLATE(uv)

			return i;
		}

		// Geometry functions derived from Roystan's tutorial:
		// https://roystan.net/articles/grass-shader.html

		// This function applies a transformation (during the geometry shader),
		// converting to clip space in the process.
		g2f worldToClip(float3 pos, float3 offset, float3x3 transformationMatrix, float2 uv, float dryRate = 0)
		{
			g2f o;
			o.positionCS = TransformObjectToHClip(pos + mul(transformationMatrix, offset));
			o.positionWS = TransformObjectToWorld(pos + mul(transformationMatrix, offset));
			o.uv = TRANSFORM_TEX(uv, _BaseTex);
			o.dryRate = dryRate;
			return o;
		}

		// This is the geometry shader. For each vertex on the mesh, a leaf
		// blade is created by generating additional vertices.
		[maxvertexcount(BLADE_SEGMENTS * 2 + 1)]
		void geom(triangle v2g input[3], inout TriangleStream<g2f> triStream)
		{
#if VISIBILITY_ON
			float grassVisibility = tex2Dlod(_GrassMap, float4(input[0].uv, 0, 0)).r;
#else
			float grassVisibility = 1.0f;
#endif
			if (grassVisibility >= _GrassThreshold)
			{
				float3 pos = (input[0].positionWS + input[1].positionWS + input[2].positionWS) / 3.0f;
				float3 normal = (input[0].normalWS + input[1].normalWS + input[2].normalWS) / 3.0f;
				float4 tangent = (input[0].tangentWS + input[1].tangentWS + input[2].tangentWS) / 3.0f;
				float3 bitangent = cross(normal, tangent.xyz) * tangent.w;

				pos -= _GrassOffset.xyz;

				float3x3 tangentToLocal = float3x3
				(
					tangent.x, bitangent.x, normal.x,
					tangent.y, bitangent.y, normal.y,
					tangent.z, bitangent.z, normal.z
				);

				// Rotate around the y-axis a random amount.
				float3x3 randRotMatrix = angleAxis3x3(rand(pos) * TWO_PI, float3(0, 0, 1.0f));

				// Create a matrix that rotates the base of the blade.
				float3x3 baseTransformationMatrix = mul(tangentToLocal, randRotMatrix);

				// The rest of the grass blade rotates slightly around the base.
				float3x3 randBendMatrix = angleAxis3x3(rand(pos.zzx) * _BladeBendDelta * HALF_PI, float3(-1.0f, 0, 0));

#if WIND_ON
				float2 windUV = pos.xz * _WindMap_ST.xy + _WindMap_ST.zw + normalize(_WindVelocity.xz) * _WindFrequency * _Time.y;
				float2 windSample = (tex2Dlod(_WindMap, float4(windUV, 0, 0)).xy * 2.0f - 0.5f) * length(_WindVelocity);

				float3 windAxis = normalize(float3(windSample.x, windSample.y, 0));
				float3x3 windMatrix = angleAxis3x3(PI * windSample, windAxis);

				// Create a matrix for the non-base vertices of the grass blade, incorporating wind.
				float3x3 tipTransformationMatrix = mul(mul(mul(tangentToLocal, windMatrix), randBendMatrix), randRotMatrix);
#else
				// Create a matrix for the non-base vertices of the grass blade.
				float3x3 tipTransformationMatrix = mul(mul(tangentToLocal, randBendMatrix), randRotMatrix);
#endif

#if VISIBILITY_ON
				float falloff = smoothstep(_GrassThreshold, _GrassThreshold + _GrassFalloff, grassVisibility);
#else
				float falloff = 1.0f;
#endif

				float width  = lerp(_BladeWidthMin, _BladeWidthMax, rand(pos.xzy) * falloff);
				float height = lerp(_BladeHeightMin, _BladeHeightMax, rand(pos.zyx) * falloff);
				float forward = rand(pos.yyz) * _BladeBendDistance;

#ifdef DRY_GRASS_ON
				float dryRate = tex2Dlod(_DryGrassMap, float4(-input[0].uv, 0, 0)).r;
#else
				float dryRate = 0;
#endif

				// Create blade segments by adding two vertices at once.
				for (int i = 0; i < BLADE_SEGMENTS; ++i)
				{
					float t = i / (float)BLADE_SEGMENTS;
					float3 offset = float3(width * (1 - t), pow(t, _BladeBendCurve) * forward, height * t);

					float3x3 transformationMatrix = (i == 0) ? baseTransformationMatrix : tipTransformationMatrix;

					triStream.Append(worldToClip(pos, float3( offset.x, offset.y, offset.z), transformationMatrix, float2(0, t), dryRate));
					triStream.Append(worldToClip(pos, float3(-offset.x, offset.y, offset.z), transformationMatrix, float2(1, t), dryRate));
				}

				// Add the final vertex at the tip of the grass blade.
				triStream.Append(worldToClip(pos, float3(0, forward, height), tipTransformationMatrix, float2(0.5, 1), dryRate));

				triStream.RestartStrip();
			}
		}
		ENDHLSL

		// This pass draws the grass blades generated by the geometry shader.
		Pass
		{
			Name "GrassPass"
			Tags
			{
				"RenderType" = "Opaque"
				"Queue" = "Geometry"
				"LightMode" = "UniversalForward"
			}

			HLSLPROGRAM
			#pragma vertex vert
			#pragma hull hull
			#pragma domain domain
			#pragma geometry geom
			#pragma fragment frag

			// The lighting sections of the frag shader taken from this helpful post by Ben Golus:
			// https://forum.unity.com/threads/water-shader-graph-transparency-and-shadows-universal-render-pipeline-order.748142/#post-5518747

			float4 frag(g2f i) : SV_Target
			{
				float4 color = tex2D(_BaseTex, i.uv);

// #ifdef _MAIN_LIGHT_SHADOWS
				VertexPositionInputs vertexInput = (VertexPositionInputs)0;
				vertexInput.positionWS = i.positionWS;

				float4 shadowCoord = GetShadowCoord(vertexInput);
				float shadowAttenuation = saturate(MainLightRealtimeShadow(shadowCoord) + 0.25f);
				float4 shadowColor = lerp(0.0f, 1.0f, shadowAttenuation);
				color *= shadowColor;
// #endif

#ifdef DRY_GRASS_ON
				float4 tipColor = lerp(_TipColor, _DryGrassColor, i.dryRate);
#else
				float4 tipColor = _TipColor;
#endif
				return color * lerp(_BaseColor, tipColor, i.uv.y);
			}
			ENDHLSL
		}

		Pass
		{
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }

			HLSLPROGRAM
			#pragma vertex shadowVert
			#pragma hull hull
			#pragma domain domain
			#pragma geometry geom
			#pragma fragment shadowFrag

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

			float3 _LightDirection;
			float3 _LightPosition;

			// Custom vertex shader to apply shadow bias.
			tessControlPoint shadowVert(appdata v)
			{
				tessControlPoint o;

				o.normalWS = TransformObjectToWorldNormal(v.normalOS);
				o.tangentWS = v.tangentOS;
				o.uv = TRANSFORM_TEX(v.uv, _GrassMap);

				float3 positionWS = TransformObjectToWorld(v.positionOS);

				// Code required to account for shadow bias.
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
				float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
				float3 lightDirectionWS = _LightDirection;
#endif
				o.positionWS = float4(ApplyShadowBias(positionWS, o.normalWS, lightDirectionWS), 1.0f);

				return o;
			}

			float4 shadowFrag(g2f i) : SV_Target
			{
				Alpha(SampleAlbedoAlpha(i.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
				return 0;
			}
			ENDHLSL
		}
	}
	CustomEditor "GeometryGrassGUI"
}
