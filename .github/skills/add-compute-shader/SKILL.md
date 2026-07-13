---
name: add-compute-shader
description: "Add a new compute or surface shader for sensor data processing or visualization. Use when: implementing GPU-side sensor processing, adding a new visual effect, creating a ray tracing shader for a sensor."
---

# Add a New Shader

Procedure for adding a compute shader (for GPU-side sensor processing) or a surface/fragment shader (for visualization) to CLOiSim.

## When to Use

- Processing sensor data on the GPU (depth scaling, noise, point cloud generation)
- Adding a new ray tracing shader for a sensor (lidar)
- Creating a visual effect (segmentation overlay, grass rendering, video texture)
- Adding a post-processing pass for a camera sensor

## Shader Types in CLOiSim

| Type | Pattern | Examples |
|------|---------|----------|
| **Traditional Compute** | `StructuredBuffer` I/O, `cbuffer` params | `DepthBufferScaling.compute`, `VCSELPrepass.compute` |
| **URT Ray Tracing** | Unified Ray Tracing with `_AccelStruct` | `LidarRayTrace.compute`, `LivoxLidarRayTrace.compute` |
| **Surface/Fragment** | URP vertex/fragment shader | `AddGaussianNoise.shader`, `Segmentation.shader`, `DepthRange.shader` (depth camera, rasterization-based, Blitter-driven) |
| **Geometry** | URP with geometry stage | `GeometryGrass.shader` |

## Procedure

### Option A: Traditional Compute Shader

Create `Assets/Resources/Shader/MyCompute.compute`:

```hlsl
/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

#pragma kernel CSMyKernel

#define THREADS 16
#define GROUPS 16

// Input/output buffers
StructuredBuffer<float> _Input;
RWStructuredBuffer<float> _Output;

// Parameters in a cbuffer for efficient batching
cbuffer Params {
	uint _Width;
	uint _Height;
	float _ScaleFactor;
};

// Mark small utility functions inline
inline float MyHelper(const float value) {
	return value * _ScaleFactor;
}

[numthreads(THREADS, GROUPS, 1)]
void CSMyKernel(uint3 id : SV_DispatchThreadID) {
	if (id.x >= _Width || id.y >= _Height)
		return;

	const uint index = id.y * _Width + id.x;
	_Output[index] = MyHelper(_Input[index]);
}
```

**C# dispatch side** (in a Device or Manager class):

```csharp
private ComputeShader _computeShader;
private int _kernelIndex;
private ComputeBuffer _inputBuffer;
private ComputeBuffer _outputBuffer;

private void InitializeShader()
{
	_computeShader = Resources.Load<ComputeShader>("Shader/MyCompute");
	_kernelIndex = _computeShader.FindKernel("CSMyKernel");

	_inputBuffer = new ComputeBuffer(width * height, sizeof(float));
	_outputBuffer = new ComputeBuffer(width * height, sizeof(float));

	_computeShader.SetBuffer(_kernelIndex, "_Input", _inputBuffer);
	_computeShader.SetBuffer(_kernelIndex, "_Output", _outputBuffer);
	_computeShader.SetInt("_Width", width);
	_computeShader.SetInt("_Height", height);
	_computeShader.SetFloat("_ScaleFactor", scaleFactor);
}

private void Dispatch()
{
	var threadGroupsX = Mathf.CeilToInt((float)_width / 16);
	var threadGroupsY = Mathf.CeilToInt((float)_height / 16);
	_computeShader.Dispatch(_kernelIndex, threadGroupsX, threadGroupsY, 1);
}
```

### Option B: URT Ray Tracing Compute Shader

Create `Assets/Resources/Shader/MyRayTrace.compute`:

```hlsl
/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

#pragma only_renderers d3d11 vulkan metal
#pragma target 4.5

#pragma kernel MainRayGenShader
#pragma kernel ComputeIndirectDispatchDims

// URT Compute backend setup
#define UNIFIED_RT_BACKEND_COMPUTE
#define UNIFIED_RT_GROUP_SIZE_X 16
#define UNIFIED_RT_GROUP_SIZE_Y 8

#include "Packages/com.unity.render-pipelines.core/Runtime/UnifiedRayTracing/Bindings.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/UnifiedRayTracing/TraceRayAndQueryHit.hlsl"

// Acceleration structure
UNIFIED_RT_DECLARE_ACCEL_STRUCT(_AccelStruct);

// Output buffer
RWStructuredBuffer<float> _Output;

// Sensor parameters
uint _SamplesH;
uint _SamplesV;
float _RangeMin;
float _RangeMax;
float3 _SensorPosition;
float3 _SensorForward;
float3 _SensorRight;
float3 _SensorUp;

// Custom parameters for your sensor
float _MyParam;

void RayGenExecute(UnifiedRT::DispatchInfo dispatchInfo)
{
	const uint2 pixel = dispatchInfo.dispatchThreadID.xy;
	if (pixel.x >= _SamplesH || pixel.y >= _SamplesV)
		return;

	// Build ray direction from sensor parameters
	float3 dirWorld = normalize(/* compute from angles/pixel */);

	UnifiedRT::Ray ray;
	ray.origin    = _SensorPosition;
	ray.direction = dirWorld;
	ray.tMin      = _RangeMin;
	ray.tMax      = _RangeMax;

	UnifiedRT::RayTracingAccelStruct accelStruct = UNIFIED_RT_GET_ACCEL_STRUCT(_AccelStruct);
	UnifiedRT::Hit hit = UnifiedRT::TraceRayClosestHit(
		dispatchInfo, accelStruct, 0xFF, ray, UnifiedRT::kRayFlagNone);

	float dist = asfloat(0x7FC00000); // NaN = no hit
	if (hit.IsValid())
	{
		dist = hit.hitDistance;
	}

	_Output[pixel.y * _SamplesH + pixel.x] = dist;
}

// Include the shared dispatch infrastructure
#include "ComputeRaygenShaderLocal.hlsl"
```

**Key URT rules:**
- `#pragma kernel` declarations must be in the root `.compute` file, not in `.hlsl` includes
- Group sizes: `16×8` for 2D patterns (cameras, standard lidar), `64×1` for 1D patterns (Livox)
- `ComputeRaygenShaderLocal.hlsl` provides `MainRayGenShader` and `ComputeIndirectDispatchDims` implementations
- Only supported on `d3d11`, `vulkan`, `metal` renderers

### Option C: Surface/Fragment Shader

Create `Assets/Resources/Shader/MySensorShader.shader`:

```hlsl
/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

Shader "Sensor/MySensorShader"
{
	Properties
	{
		_MainTex ("Main Texture", 2D) = "white" {}
		_MyParam ("My Parameter", Float) = 1.0
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			CBUFFER_START(UnityPerMaterial)
				float _MyParam;
			CBUFFER_END

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv         : TEXCOORD0;
			};

			struct Varyings
			{
				float2 uv         : TEXCOORD0;
				float4 positionCS : SV_POSITION;
			};

			Varyings vert(Attributes IN)
			{
				Varyings OUT;
				OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
				OUT.uv = IN.uv;
				return OUT;
			}

			half4 frag(Varyings IN) : SV_Target
			{
				half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
				color.rgb *= _MyParam;
				return color;
			}
			ENDHLSL
		}
	}
}
```

### Using Shaders in Camera-Based Sensors

For post-processing on camera sensors, use `CommandBuffer` blits:

```csharp
// In a Camera sensor device:
private Material _myMaterial;
private CommandBuffer _cmdBuffer;

private void SetupShader()
{
	var shader = Shader.Find("Sensor/MySensorShader");
	_myMaterial = new Material(shader);
	_myMaterial.SetFloat("_MyParam", 1.0f);

	_cmdBuffer = new CommandBuffer { name = "MySensorEffect" };
	_cmdBuffer.Blit(BuiltinRenderTextureType.CurrentActive, targetRT, _myMaterial);
	_camera.AddCommandBuffer(CameraEvent.AfterEverything, _cmdBuffer);
}
```

### AsyncGPUReadback Integration

All GPU data reads must use `AsyncGPUReadback` — never synchronous reads:

```csharp
AsyncGPUReadback.Request(_outputBuffer, (request) =>
{
	if (request.hasError)
		return;

	var data = request.GetData<float>();
	// Process data, enqueue message
	EnqueueMessage(data);
});
```

## Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Shader properties | `_PascalCase` with underscore prefix | `_DepthMax`, `_BaseColor` |
| Kernel names | `PascalCase` with `CS` prefix for compute | `CSScaleDepthBuffer` |
| Defines / macros | `UPPER_SNAKE_CASE` | `THREADS`, `MAX_RANGE_16BITS` |
| Local variables | `camelCase` | `packWidth`, `depthValue` |
| Inline helpers | `PascalCase` | `GetPackWidth()`, `Pack4Bytes()` |

## Checklist

- [ ] Shader placed in `Assets/Resources/Shader/`
- [ ] URP compatible: `Tags { "RenderPipeline" = "UniversalPipeline" }` (surface shaders)
- [ ] Uses `HLSLPROGRAM`/`ENDHLSL` (not `CGPROGRAM`/`ENDCG`)
- [ ] Uses `CBUFFER_START`/`CBUFFER_END` for uniforms (surface shaders)
- [ ] Uses `cbuffer` for compute shader parameters
- [ ] `#define THREADS` and `#define GROUPS` at top of compute shaders
- [ ] `inline` on small utility functions
- [ ] All GPU readbacks use `AsyncGPUReadback`
- [ ] Tab indentation, K&R braces
- [ ] License header on file
