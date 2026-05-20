# CLOiSim Project Guidelines

## Project Overview

CLOiSim is a Unity 6-based multi-robot simulator. It parses SDF (Simulation Description Format) files, builds Unity scenes dynamically, and exposes sensor/control data over NetMQ transport for ROS 2 bridging.

- **Unity version**: 6000.x (Unity 6)
- **Render pipeline**: URP (Universal Render Pipeline)
- **Transport**: NetMQ (ZeroMQ) with protobuf-net serialization
- **Coordinate conversion**: SDF right-hand → Unity left-hand (X→Z, Y→-X, Z→Y)

## Architecture

Changes should flow through the SDF parse → import → implement pipeline (`Tools/SDF/`). Do not bypass with scene-only logic.

Plugin lifecycle: `OnAwake()` → `OnStart()` → `Started` event → `OnReset()` / `OnDestroy()`. Every `CLOiSimPlugin` must register device ports via `BridgeManager` and clean up in `OnDestroy()`.

Do not rename these scene root objects: `Core`, `Props`, `World`, `Lights`, `Roads`, `UI`.

## General Rules

- License header on all new source files:
  ```
  /*
   * Copyright (c) <YEAR> LG Electronics Inc.
   *
   * SPDX-License-Identifier: MIT
   */
  ```
- Thread safety: use `lock` for shared dictionaries, `ConcurrentQueue<>` for message passing, background threads via `CLOiSimPluginThread`
- Avoid `GameObject.Find()` at runtime except in `Main.cs` bootstrap
- `AsyncGPUReadback` is mandatory — never replace with synchronous GPU reads

## Change Scope And Validation

- Prefer the smallest viable edit in the nearest code that directly decides the behavior; avoid widening into adjacent systems without evidence.
- Do not mix localized fixes with unrelated cleanup, renames, or refactors.
- Validate with the narrowest existing command that covers the touched behavior before running broader checks.
- For deterministic Unity unit coverage, prefer `./scripts/run-editmode-tests.sh "<namespace-or-fixture>"` from the repository root, targeting the nearest fixture under `Assets/Editor/Tests/`.
- If there is no reliable narrow filter for the touched slice, run `./scripts/run-editmode-tests.sh`.
- If Unity test execution is blocked by a project lock or compiler errors, report that explicitly and point to `Logs/EditModeTests.log`.

---

## C# (`**/*.cs`)

### Formatting
- **Indentation**: Tabs (not spaces)
- **Braces**: Allman style (opening brace on new line) for type and method declarations; K&R for short property accessors
- **Line length**: Prefer under 120 characters

### Naming
| Element | Convention | Example |
|---------|-----------|---------|
| Private fields | `_camelCase` with underscore prefix | `_grabRadius`, `_laserScan` |
| Public properties | `PascalCase` | `ModelName`, `IsStarted` |
| Public methods | `PascalCase` | `Activate()`, `Release()` |
| Private methods | `PascalCase` | `TryGrabNearestVertex()` |
| Constants | `PascalCase` | `MinPortRange`, `MaxPoolSize` |
| Local variables | `camelCase` | `bestIndex`, `worldPos` |
| Enums | `PascalCase` type, `UPPER_SNAKE` or `PascalCase` values | `ModeType.TX_THREAD`, `ICLOiSimPlugin.Type.LASER` |
| Interfaces | `I` prefix | `ICLOiSimPlugin`, `ISensorRenderable` |
| Namespaces | `PascalCase` | `SensorDevices`, `CLOiSim.Cloth` |

### Patterns
- Use expression-bodied properties for simple getters/setters:
  ```csharp
  public float GrabRadius
  {
      get => _grabRadius;
      set => _grabRadius = value;
  }
  ```
- Use `in` modifier for readonly reference parameters: `Position(in double x, in double y, in double z)`
- Use `partial class` to split large classes across files (e.g., `CLOiSimPlugin.cs`, `CLOiSimPlugin.Transport.cs`, `CLOiSimPlugin.Thread.cs`)
- Alias message namespaces: `using messages = cloisim.msgs;`
- Use `[SerializeField]` for inspector-exposed private fields; prefer `[field: SerializeField]` for auto-properties
- Use `[Header("...")]` to group serialized fields in the inspector
- Guard editor-only code with `#if UNITY_EDITOR` / `#endif`
- Prefer `Coroutine` for Unity-thread async work, `Thread` for CPU-bound background work
- Use `ConcurrentQueue<>` or `ConcurrentBag<>` for cross-thread data, never bare `Queue<>` or `List<>`
- Dispose patterns: implement `IDisposable` with `GC.SuppressFinalize(this)` in `Dispose()`

### Unity-Specific
- Sensor devices inherit from `Device` (in `Devices/Modules/Base/Device.cs`)
- Plugins inherit from `CLOiSimPlugin` (in `CLOiSimPlugins/Modules/Base/CLOiSimPlugin.cs`)
- SDF helpers inherit from `SDF.Helper.Base`
- Always call `Release()` / cleanup in both `OnDisable()` and `OnDestroy()`
- Use `Physics.SyncTransforms()` after manual transform changes during scene construction
- Prefer `AsyncGPUReadback` for all GPU data reads

### Prohibited
- Do not use `UnityWebRequest` or synchronous file I/O on the main thread
- Do not add `[ExecuteAlways]` without justification
- Do not use `FindObjectOfType<>()` in hot paths — cache references
- Do not allocate in `Update()` / `FixedUpdate()` — use object pooling (see `DeviceMessage` pool pattern)

---

## HLSL / Shaders (`**/*.shader`, `**/*.compute`, `**/*.hlsl`)

### Formatting
- **Indentation**: Tabs
- **Braces**: K&R style (opening brace on same line for functions)

### Naming
| Element | Convention | Example |
|---------|-----------|---------|
| Shader properties | `_PascalCase` with underscore prefix | `_DepthMax`, `_BaseColor` |
| Kernel names | `PascalCase` with `CS` prefix for compute | `CSScaleDepthBuffer` |
| Defines / macros | `UPPER_SNAKE_CASE` | `THREADS`, `MAX_RANGE_16BITS` |
| Local variables | `camelCase` | `packWidth`, `depthValue` |
| Inline helpers | `PascalCase` | `GetPackWidth()`, `Pack4Bytes()` |

### Patterns
- Compute shaders: declare `#define THREADS` and `#define GROUPS` at top
- Use `cbuffer` for uniform parameters
- Mark small utility functions `inline`
- All shaders must be URP compatible: `Tags { "RenderPipeline" = "UniversalPipeline" }`
- Place shaders in `Assets/Resources/Shader/`
- Use `HLSLINCLUDE` / `HLSLPROGRAM` blocks (not `CGINCLUDE` / `CGPROGRAM`)

---

## Shell / Bash (`**/*.sh`)

### Formatting
- **Indentation**: 2 spaces
- **Shebang**: `#!/bin/bash` on first line

### Naming
| Element | Convention | Example |
|---------|-----------|---------|
| Environment variables | `UPPER_SNAKE_CASE` | `CLOISIM_FILES_PATH`, `TARGET_PATH` |
| Local variables | `UPPER_SNAKE_CASE` | `PROTO_MSGS_PATH` |
| Section headers | `##` double-hash comments | `## 1. check and edit here` |

### Patterns
- Quote all variable expansions: `"${VARIABLE}"`
- Use `set -e` for scripts that should fail fast
- Clean up temporary files
- Use `$()` for command substitution, not backticks

---

## Dockerfile (`**/Dockerfile*`)

### Patterns
- Base image: Ubuntu LTS (currently 24.04)
- Set `DEBIAN_FRONTEND=noninteractive`
- Use `apt-get` with `--no-install-recommends`
- Clean up after install: `rm -rf /var/lib/apt/lists/* && apt-get clean`
- Chain related `RUN` commands with `&&` to reduce layers
- Use `ENV` for configuration variables
- Expose Vulkan/GPU configuration for headless rendering
- Do not store secrets or credentials in the image

---

## YAML (`**/*.yml`, `**/*.yaml`)

### Formatting
- **Indentation**: 2 spaces (never tabs)
- Use explicit key quoting only when values contain special characters

### Patterns
- GitHub Actions workflows go in `.github/workflows/`
- Use specific action versions (pinned tags, not `@main`)
- Prefer `ubuntu-latest` for CI runners unless a specific version is needed
