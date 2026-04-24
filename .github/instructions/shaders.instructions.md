---
applyTo: "Assets/Resources/Shader/**"
---

# Shader Development Instructions

All shaders are in `Assets/Resources/Shader/` and must be URP-compatible.

## Two Compute Shader Patterns

### Pattern A: Traditional Compute

```hlsl
#pragma kernel CSMyKernel
#define THREADS 16
#define GROUPS 16

cbuffer Params {
    uint _Width;
    float _DepthMax;
};

StructuredBuffer<float> _Input;
RWStructuredBuffer<float> _Output;

inline float MyHelper(float value) { ... }

[numthreads(THREADS, GROUPS, 1)]
void CSMyKernel(uint3 id : SV_DispatchThreadID) { ... }
```

Used by: `DepthBufferScaling.compute`, `VCSELPrepass.compute`

### Pattern B: Unified Ray Tracing (URT)

```hlsl
#pragma only_renderers d3d11 vulkan metal
#pragma target 4.5
#pragma kernel MainRayGenShader
#pragma kernel ComputeIndirectDispatchDims

#define UNIFIED_RT_BACKEND_COMPUTE
#define UNIFIED_RT_GROUP_SIZE_X 16
#define UNIFIED_RT_GROUP_SIZE_Y 8

#include "Packages/com.unity.rendering.light-transport/Runtime/UnifiedRayTracing/Bindings.hlsl"
#include "Packages/com.unity.rendering.light-transport/Runtime/UnifiedRayTracing/TraceRayAndQueryHit.hlsl"

void RayGenExecute(UnifiedRT::DispatchInfo dispatchInfo) { ... }
#include "ComputeRaygenShaderLocal.hlsl"
```

Used by: `LidarRayTrace.compute`, `DepthCameraRayTrace.compute`, `LivoxLidarRayTrace.compute`

- Group sizes: 16×8 for 2D (cameras, standard lidar), 64×1 for 1D (Livox pattern-based)
- `ComputeRaygenShaderLocal.hlsl` provides `MainRayGenShader` and `ComputeIndirectDispatchDims` kernel implementations
- `#pragma kernel` declarations must be in the root `.compute` file, not the `.hlsl` include

## Surface/Fragment Shader Structure

```hlsl
Shader "Sensor/MyShader"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _DepthMax ("Depth Max", Float) = 20.0
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _DepthMax;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
            }
            ENDHLSL
        }
    }
}
```

## Shader Naming Convention

| Category | Pattern | Example |
|----------|---------|---------|
| Sensor shaders | `"Sensor/..."` | `"Sensor/Camera/GaussianNoise"`, `"Sensor/Segmentation"` |
| Custom materials | `"Custom/..."` | `"Custom/GeometryGrass"`, `"Custom/URP/Simple Lit"` |
| Utility/hidden | `"Hidden/..."` | `"Hidden/Rotate180"` |

## URP Requirements Checklist

- `Tags { "RenderPipeline" = "UniversalPipeline" }` on all SubShaders
- `HLSLPROGRAM` / `ENDHLSL` blocks (never `CGPROGRAM` / `ENDCG`)
- `TEXTURE2D()` + `SAMPLER()` macros (not legacy `sampler2D`)
- `SAMPLE_TEXTURE2D()` (not `tex2D`)
- `CBUFFER_START(UnityPerMaterial)` / `CBUFFER_END` for SRP Batcher
- `TransformObjectToHClip()` (not `UnityObjectToClipPos`)
- Struct naming: `Attributes` (input), `Varyings` (output)
- Compute shaders: `#pragma target 4.5`, restrict renderers: `#pragma only_renderers d3d11 vulkan metal`

## Naming Rules

| Element | Convention | Example |
|---------|-----------|---------|
| Shader properties | `_PascalCase` with underscore prefix | `_DepthMax`, `_BaseColor` |
| Kernel names | `CS` prefix (traditional) or `MainRayGenShader` (URT) | `CSScaleDepthBuffer` |
| Defines/macros | `UPPER_SNAKE_CASE` | `THREADS`, `MAX_RANGE_16BITS` |
| Local variables | `camelCase` | `packWidth`, `depthValue` |
| Inline helpers | `PascalCase` | `GetPackWidth()`, `Pack4Bytes()` |

## Key Rules

- Indentation: tabs
- Braces: K&R style (opening brace on same line)
- Mark small utility functions `inline`
- Use `cbuffer` for uniform parameters in compute shaders
