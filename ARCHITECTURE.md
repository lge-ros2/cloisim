# CLOiSim Architecture Reference

Single source of truth for AI coding assistants (Claude Code, Gemini CLI, GPT/Codex, etc.) working in this repo. When project documentation and checked-in code disagree, this file follows the code. Tool-specific files (`CLAUDE.md`, `GEMINI.md`, `GPT.md`) are thin wrappers that point here and add only tool-specific behavior.

## 1. What this project is

CLOiSim is a Unity-based multi-robot simulator. It reads SDF (Simulation Description Format) world and model files, dynamically builds a Unity scene from them, and exposes simulated sensor data and control endpoints over a NetMQ-based transport layer. An external ROS 2 bridge package (`cloisim_ros`) connects these endpoints to ROS topics.

## 2. Verified Unity version and package state

Source of truth: `ProjectSettings/ProjectVersion.txt` and `Packages/manifest.json` (re-verify these two files if this section looks stale — this project upgrades Unity versions frequently, see git log).

```
m_EditorVersion: 6000.6.2f1
```

This is a **Unity 6** project. Key packages from `Packages/manifest.json`:

| Package | Version |
|---------|---------|
| `com.unity.render-pipelines.universal` | 17.6.0 |
| `com.unity.inputsystem` | 1.20.0 |
| `com.unity.ai.navigation` | 2.0.14 |
| `com.unity.mathematics` | 1.4.0 |
| `com.unity.splines` | 2.9.1 |
| `com.unity.terrain-tools` | 5.3.3 |
| `com.unity.nuget.newtonsoft-json` | 3.2.2 |
| `com.unity.robotics.vhacd` | GitHub (Unity-Technologies/VHACD) |
| `com.lge-ros2.sdformat` | GitHub (lge-ros2/sdformat-sharp) — external SDF parser package |

Third-party libraries in `Assets/Plugins/` (versioned in the directory name — check there directly, this drifts):

| Library | Purpose |
|---------|---------|
| AssimpNetter 6.0.5 | Mesh import (OBJ, DAE, STL) via native Assimp |
| NetMQ 4.0.4.3 + AsyncIO | ZeroMQ transport for sensor/control data |
| protobuf-net 3.2.56 | Protocol Buffers serialization for messages |
| WebSocketSharp 1.0.3-rc11 | WebSocket server for external simulation control |

## 3. Architectural layers

The simulator is organized into five cooperating layers, all rooted under `Assets/Scripts/`:

### Layer 1: Startup and orchestration — `Main.cs`

`Main` is the singleton `MonoBehaviour` (`[DefaultExecutionOrder(30)]`) that bootstraps everything:

**`Awake()` does:**
- Replaces legacy `StandaloneInputModule` with `InputSystemUIInputModule`
- Redirects `Console.Out`/`Console.Error` to Unity log via `DebugLogWriter`
- Reads resource paths from environment variables (`CLOISIM_FILES_PATH`, `CLOISIM_MODEL_PATH`, `CLOISIM_WORLD_PATH`) — colon-separated, multiple paths supported
- Loads Assimp native library dynamically (platform-conditional paths)
- Forces `AsyncIO.ForceDotNet.Force()` for NetMQ Windows compatibility
- Sets `Application.targetFrameRate = 60`, enables texture streaming (`512MB` budget), reduces shadow distances
- Configures per-layer camera culling distances (Default layer gets 50% of far clip)
- Reads `CLOISIM_QUALITY` env var (0-4, default 3 = "Very High")
- Locates scene root objects by name: `Core`, `Props`, `World`, `Lights`, `Roads`, `UI`
- Creates singletons: `BridgeManager`, `SimulationService`, `SimulationWorld`, `ObjectSpawning`, `ModelImporter`, `Segmentation.Manager`, `MeshProcess.VHACD`

**`Start()` does:**
- Hard-fails if `AsyncGPUReadback` is not supported
- Parses command-line args: `-world` / `-worldFile`, `-capture`
- Builds `SDF.Root`, populates resource paths, calls `UpdateResourceModelTable()`
- Launches `LoadWorld()` coroutine if a world file was specified

### Layer 2: SDF parsing and import pipeline — `Tools/SDF/`

This layer has three sub-stages:

1. **Parser** (`Tools/SDF/Parser/`, plus the external `com.lge-ros2.sdformat` package) — C# classes mirroring SDF XML elements: `Root`, `World`, `Model`, `Link`, `Joint`, `Visual`, `Collision`, `Sensor`, `Plugin`, `Actor`, `Light`, `Material`, `Physics`, etc. `Root.cs` (via `RootLoader`) handles XML loading, `<include>` model resolution, and path-to-absolute conversion. Supports SDF versions 1.0–1.9. Model include resolution uses `_resourceModelTable` populated from `model.config` files across `CLOISIM_MODEL_PATH` directories. `<include merge="true">` flattens children into parent scope; non-merge adds `is_nested="true"` attribute.

2. **Importer** (`Tools/SDF/Import/`) — Partial class `Loader` with files per element type (`Import.World.cs`, `Import.Model.cs`, `Import.Link.cs`, `Import.Joint.cs`, `Import.Sensor.cs`, `Import.Plugin.cs`, etc.). Converts parsed SDF into Unity GameObjects/components. Sensor import is a type-switch dispatching to extension methods like `AddLidar()`, `AddCamera()`, `AddImu()`, etc. Plugin import uses `Type.GetType(pluginLibraryName)` + `AddComponent` — plugins are resolved by class name derived from the SDF `filename` attribute. **Deferred processing**: joints, grippers, and plugins are stored during model import but applied later (joints need both links to exist; plugins need full articulation hierarchy). Orchestration order in `Base.Start()`: ImportWorld → ImportModels → deferred joints → deferred grippers → ImportActors → SpecifyPose → deferred plugins.

3. **Implementer** (`Tools/SDF/Implement/`) — Static extension methods that create the actual Unity components. `Implement.Sensor`: `Add*()` methods create child GameObjects with `SensorDevices.*` components. `Implement.Joint`: `MakeJoint()` dispatches by `JointType` to `MakeRevoluteJoint()`, `MakePrismaticJoint()`, `MakeFixedJoint()`, `MakeBallJoint()`, `MakeContinuousJoint()`. `Implement.Geometry`: dispatches to `MeshLoader` (Assimp) for external meshes, `ProceduralMesh` for primitives. `Implement.Collision`: VHACD convex decomposition for dynamic, merged mesh colliders for static; prefers native primitive colliders when possible.

**Helper classes** (`Tools/SDF/Helper/`) — `Base`, `Model`, `Link`, `Visual`, `Collision`, `Actor` are MonoBehaviours attached to imported objects, holding SDF pose data and enabling reset. `Helper.Base` stores SDF `Pose`, `PoseRelativeTo`, manages `PoseControl` frame list, provides `ResetPose()`. `Helper.Model` stores `modelNameInPath`, `isStatic`, `isNested`; creates `ArticulationBody`/`Rigidbody` on root. `Helper.Link` stores joint metadata, axis limits, mimic constraints; manages self-collision ignoring.

**Utility** (`Tools/SDF/Util/`) — `SDF2Unity` provides coordinate system conversion, material creation, and mesh utilities. `Unity2SDF` provides the reverse for world saving.

### Layer 3: Simulation runtime — `Core/`

| File | Role |
|------|------|
| `SimulationWorld.cs` | `CLOiSimPlugin`-derived world clock + reset signal broadcaster. Registers a Clock TX and Control client channel |
| `PluginStartTracker.cs` | Collects all `CLOiSimPlugin` instances under a loaded root, monitors their `Started` events, emits progress, fires `AllStartedEvent` when all are ready |
| `ObjectSpawning.cs` | Runtime spawning of primitive props (box, cylinder, sphere) with physics. Supports Ctrl+click placement via Input System |
| `WorldNavMeshBuilder.cs` | NavMesh baking for actor AI navigation |
| `WorldSaver.cs` | Serializes current world state back to the original loaded SDF XML document |
| `SegmentationManager.cs` | Manages semantic segmentation tags (max 256 labels), attaches `Segmentation.Tag` components |
| `Modules/BridgeManager.cs` | Dynamic port allocator for device transport. Range: 49152–65535. Maintains a nested map: `ModelName → PluginType → PartsName → TopicName → Port`. Thread-safe |
| `Modules/SimulationService.cs` | WebSocket server (default port 8080, override via `CLOISIM_SERVICE_PORT`). Registers `/control` and `/markers` services |
| `Services/SimulationControlService.cs` | Handles JSON commands: `reset`, `device_list`, `port_list`, `fps`, `start_record`, `stop_record`, `teleport`, `get_model_info` |
| `Services/MarkerVisualizerService.cs` | Runtime marker creation (line, text, box, sphere) via WebSocket |
| `Modules/SphericalCoordinates.cs` | GPS coordinate frame for world positioning |
| `Modules/UltraFastWebMRecorder.cs` | Screen capture to WebM |

### Layer 4: Devices and plugins — `Devices/` + `CLOiSimPlugins/`

These are two distinct but tightly coupled subsystems:

**Devices** (`Devices/`) are sensor/actuator implementations as `Device`-derived MonoBehaviours. `Device` (base class in `Devices/Modules/Base/Device.cs`) provides:
- Update rate control, coroutine-based or thread-based TX/RX modes
- `DeviceMessageQueue` for thread-safe data passing
- Synthetic monotonic timestamps for jitter-free publishing
- Event-driven TX wake via `AutoResetEvent`
- Object pooling for `DeviceMessage` to reduce GC
- Diagnostic profiling (Hz measurement, bandwidth in editor)

Concrete devices: `Lidar` (+ `Lidar.Livox`, `Lidar.Visualize`), `Camera`, `DepthCamera`, `MultiCamera`, `SegmentationCamera`, `LogicalCamera`, `GPS`, `IMU`, `Sonar`, `Contact`, `Clock`, `JointCommand`, `JointState`, `MicomCommand`, `MicomSensor`.

**Plugins** (`CLOiSimPlugins/`) are the transport/control bridge layer. `CLOiSimPlugin` (base in `CLOiSimPlugins/Modules/Base/CLOiSimPlugin.cs`) is an abstract `MonoBehaviour` that:
- Has a typed category via `ICLOiSimPlugin.Type` enum (NONE, WORLD, GROUNDTRUTH, ELEVATOR, ACTOR, MICOM, JOINTCONTROL, SENSOR, GPS, IMU, IR, SONAR, CONTACT, LASER, CAMERA, DEPTHCAMERA, MULTICAMERA, REALSENSE, SEGMENTCAMERA, LOGICALCAMERA)
- Manages a `Transporter` (NetMQ socket collection) and a `CLOiSimPluginThread` (background thread pool)
- Registers TX/RX/Service/Client device ports via `BridgeManager`
- Uses a delayed startup pattern: `Awake()` → `OnAwake()` abstract, then coroutine-based `OnStart()`
- Raises `Started` event for `PluginStartTracker` coordination
- Properly tears down threads, transport, and deallocates ports in `OnDestroy()`

Concrete plugins include `LaserPlugin`, `CameraPlugin`, `RealSensePlugin`, `SegmentationCameraPlugin`, `MultiCameraPlugin`, `LogicalCameraPlugin`, `ImuPlugin`, `GpsPlugin`, `SonarPlugin`, `IRPlugin`, `RangePlugin`, `ContactPlugins`, `JointControlPlugin`, `MicomPlugin`, `ActorPlugin`, `ActorControlPlugin`, `GroundTruthPlugin`, `ElevatorSystem`, `MowingPlugin`, `ParticleSystemPlugin`, `ClothPlugin`, `ClothGrabberPlugin`.

**Transport** (`CLOiSimPlugins/Modules/Connection/`): NetMQ wrappers — `Publisher`, `Subscriber`, `Requestor`, `Responsor`, `Transporter`. All use TCP binding on localhost with hash-based message tagging.

**Messages** (`CLOiSimPlugins/Messages/`): protobuf-net generated C# classes in the `cloisim.msgs` namespace (laser scans, images, IMU, GPS, joint states, poses, etc.). Generated from `.proto` definitions via `.gen_proto_code.sh`.

### Layer 5: UI and visualization — `UI/`

- `UIController.cs` — Unity UI Toolkit-based HUD. Manages camera view switching (perspective/orthographic), prop spawning controls, scale factor, model import UI, and status/error messages
- `InfoDisplay.cs` — On-screen info overlay with FPS counter
- `Camera/CameraControl.cs` — Abstract base for fly-cam controls (WASD + mouse). Subclasses: `PerspectiveCameraControl`, `OrthographicCameraControl`
- `FollowingTargetList.cs` — UI for following/tracking specific models
- `ModelImporter.cs` — Runtime model import from file browser
- `RuntimeGizmo/TransformGizmo.cs` — In-scene transform manipulation gizmos
- `MarkerVisualizer/` — Runtime marker rendering (add, modify, remove, list)

## 4. World/model loading lifecycle

Both `LoadWorld()` and `LoadModel()` in `Main.cs` follow the same pattern:

1. Parse SDF via `SDF.Root.DoParse()` — resolves `<include>` references, converts paths to absolute
2. Switch physics to `SimulationMode.Script` (prevents physics during construction)
3. Run `SDF.Import.Loader.Start()` coroutine — creates GameObjects for all parsed elements
4. `PluginStartTracker.Bind()` — discovers all CLOiSimPlugin components, subscribes to their `Started` events
5. `Physics.SyncTransforms()` then restore `SimulationMode.FixedUpdate`
6. Wait until `PluginStartTracker.AllStarted` is true
7. `BridgeManager.PrintAllocatedHistory()` — logs all port assignments
8. UI status updated

Reset flow: `Ctrl+R` triggers `ResetSimulation()` coroutine (resets all `SDF.Helper.Base`, `Device`, and `CLOiSimPlugin` components). `Ctrl+Shift+R` triggers full scene reload.

## 5. Physics configuration

From `ProjectSettings/DynamicsManager.asset`:
- Gravity: (0, -9.807, 0)
- Solver type: 1 (TGS — Temporal Gauss Seidel)
- Enhanced determinism: enabled
- Default contact offset: 0.001
- Solver iterations: 10 / velocity iterations: 5
- Auto sync transforms: disabled (manual `Physics.SyncTransforms()` calls)
- Improved patch friction: enabled
- Broadphase: default (sweep-and-prune)
- World bounds: 250m extent

Custom Unity tags used: `Model`, `Link`, `Visual`, `Collision`, `Sensor`, `Light`, `Actor`, `Marker`, `Props`, `Geometry`, `Road`

Custom layers: `Plane` (3), `Visualization` (6), `Cloth` (7)

## 6. GPU/rendering pipeline details

- URP (Universal Render Pipeline), see current version in section 2
- Custom shaders in `Assets/Resources/Shader/`:
  - `DepthBufferScaling.compute` — GPU-side depth processing (rasterization-based; DepthCamera uses `DepthRangeRendererFeature` + `DepthRange.shader` to encode scene depth, not URT ray tracing)
  - `LidarRayTrace.compute`, `LivoxLidarRayTrace.compute` — GPU-side lidar ray tracing (URT)
  - `ComputeRaygenShaderLocal.hlsl` — Shared ray generation helper (Lidar URT only)
  - `VCSELPrepass.compute` — IR depth camera emulation
  - `AddGaussianNoise.shader` — GPU noise injection for cameras
  - `Segmentation.shader` — Semantic segmentation rendering
  - `GeometryGrass.shader` — Grass rendering for mowing simulation
  - `URPSimpleLit.shader` — Custom simplified URP lit shader
  - `UnlitVideoTexture.shader` — Unlit video texture display
  - `Rotate180.shader` — Sensor utility
- `AsyncGPUReadback` is mandatory — startup fails without it
- `SensorRenderManager` singleton centrally schedules render timing for all `ISensorRenderable` sensors

## 7. Noise model subsystem

`Devices/Modules/Noise.cs` + `Devices/Modules/NoiseModel/`:

- `GaussianNoiseModel` — Standard Gaussian with optional quantization
- `CustomNoiseModel` — Extensible noise from XML parameters
- Noise is applied in parallel via `Parallel.For` with adaptive thread count (`ProcessorCount / 4`)
- Supports per-element clamping (min/max)
- Applied to LiDAR ranges, camera images, IMU, GPS

## 8. Coordinate system conversion

SDF uses right-handed (X-forward, Y-left, Z-up). Unity uses left-handed (X-right, Y-up, Z-forward).

The `SDF2Unity` class in `Tools/SDF/Util/SDF2Unity.cs` applies the mapping:
```
Unity.X = -SDF.Y
Unity.Y =  SDF.Z
Unity.Z =  SDF.X
```

Quaternion conversion follows a corresponding transformation. `Unity2SDF` provides the inverse for world saving. Never manually swap axes outside these utilities.

## 9. Environment variables and command-line arguments

| Variable | Purpose | Notes |
|----------|---------|-------|
| `CLOISIM_FILES_PATH` | Media/texture resource paths | Colon-separated |
| `CLOISIM_MODEL_PATH` | SDF model search paths | Colon-separated, multiple supported |
| `CLOISIM_WORLD_PATH` | SDF world file search paths | Colon-separated |
| `CLOISIM_QUALITY` | Graphics quality preset (0-4) | Default: 3 (Very High) |
| `CLOISIM_SERVICE_PORT` | WebSocket service port | Default: 8080 |

Command-line args: `-world <file>`, `-worldFile <file>`, `-capture <filename>`

## 10. Key patterns to preserve when making changes

### Plugin lifecycle
Every `CLOiSimPlugin` must:
- Set `_type` in `OnAwake()`
- Register device ports and add threads in `OnStart()` coroutine
- Signal completion (automatic via base class `Started` event)
- Clean up in `OnDestroy()` (thread join, transport dispose, port deregistration)
- Support `Reset()` → `OnReset()`

### Startup coordination
`PluginStartTracker` watches all plugins under a loaded root. New plugins automatically participate if they follow the base class contract. The world is not considered "loaded" until all plugins report started.

### Transport registration
Plugins call `RegisterTxDevice()`, `RegisterRxDevice()`, `RegisterServiceDevice()`, or `RegisterClientDevice()`. These allocate ports via `BridgeManager`, create NetMQ sockets, and return port numbers for thread binding. Port deregistration is automatic on destroy.

### Scene object naming
`Main.cs` uses `GameObject.Find()` for: `Core`, `Props`, `World`, `Lights`, `Roads`, `UI`. `UI` contains a child `Main Canvas`. Renaming these breaks initialization.

### SDF pipeline
Changes to world/model semantics should flow through the SDF parse → import → implement pipeline rather than bypassing it with scene-only logic.

### Thread safety
`BridgeManager` uses `lock(_deviceMapTable)` and `lock(_haskKeyPortMapTable)`. Device message passing uses `ConcurrentQueue<>`. Plugin threads are background threads managed by `CLOiSimPluginThread`.

## 11. Directory reference

```
Assets/Scripts/
├── Main.cs                          # Singleton bootstrap, world loading, reset, CLI args
├── Core/
│   ├── SimulationWorld.cs           # World clock + reset signal (CLOiSimPlugin)
│   ├── PluginStartTracker.cs        # Plugin readiness coordination
│   ├── ObjectSpawning.cs            # Runtime prop creation
│   ├── WorldNavMeshBuilder.cs       # NavMesh baking
│   ├── WorldSaver.cs               # SDF world persistence
│   ├── SegmentationManager.cs       # Semantic label management
│   ├── Modules/
│   │   ├── BridgeManager.cs         # Device port registry (49152–65535)
│   │   ├── SimulationService.cs     # WebSocket server host
│   │   ├── SphericalCoordinates.cs  # GPS coordinate frame
│   │   └── UltraFastWebMRecorder.cs # Screen recording
│   └── Services/
│       ├── SimulationControlService.cs  # /control endpoint
│       └── MarkerVisualizerService.cs   # /markers endpoint
├── Devices/
│   ├── Lidar.cs, Camera.cs, DepthCamera.cs, LogicalCamera.cs, GPS.cs, IMU.cs, ...
│   └── Modules/
│       ├── Base/Device.cs           # Abstract sensor base class
│       ├── Noise.cs                 # Noise application
│       ├── Motor/                   # Differential drive, PID, self-balance
│       └── NoiseModel/              # Gaussian, custom noise models
├── CLOiSimPlugins/
│   ├── LaserPlugin.cs, CameraPlugin.cs, MicomPlugin.cs, LogicalCameraPlugin.cs, ...
│   ├── Modules/
│   │   ├── Base/
│   │   │   ├── CLOiSimPlugin.cs            # Abstract plugin base
│   │   │   ├── CLOiSimPlugin.Transport.cs  # Port registration
│   │   │   ├── CLOiSimPlugin.Thread.cs     # Thread management
│   │   │   ├── CLOiSimPluginThread.cs      # Thread pool + sender/receiver/service loops
│   │   │   └── CLOiSimMultiPlugin.cs       # Multi-device variant
│   │   └── Connection/
│   │       ├── Publisher.cs, Subscriber.cs  # NetMQ pub/sub
│   │       ├── Requestor.cs, Responsor.cs   # NetMQ req/rep
│   │       └── Transporter.cs               # Socket collection manager
│   └── Messages/                    # protobuf-net generated message types
├── Tools/
│   ├── SDF/
│   │   ├── Parser/                  # SDF XML → C# object model
│   │   ├── Import/                  # C# objects → Unity GameObjects
│   │   ├── Implement/               # Extension methods for component creation
│   │   ├── Helper/                  # MonoBehaviours for SDF pose/reset
│   │   └── Util/                    # SDF↔Unity conversion (coordinates, materials)
│   ├── Mesh/                        # Assimp wrappers, procedural mesh, heightmap, VHACD
│   └── StdHash/                     # Hashing utilities
└── UI/
    ├── UIController.cs              # Main HUD (UI Toolkit)
    ├── InfoDisplay.cs               # FPS + info overlay
    ├── ModelImporter.cs             # Runtime model import UI
    ├── Camera/CameraControl.cs      # Fly-cam base (perspective/ortho)
    ├── RuntimeGizmo/                # In-scene transform gizmos
    └── MarkerVisualizer/            # Runtime marker management
```

## 12. CI/CD

GitHub Actions runs CodeQL analysis on `main`, `develop`, `develop-2` branches for C# and C++ code (`.github/workflows/codeql-analysis.yml`).

## 13. Docker

`Docker/Dockerfile` builds an Ubuntu 24.04 image with Vulkan support. It auto-downloads the latest CLOiSim release binary. Entrypoint is `run.sh`. Resource paths are set via environment variables inside the container.

## 14. Related documents

- `.github/copilot-instructions.md` — GitHub Copilot's own convention file (separate loading mechanism from this file). Covers coding style/naming/formatting per file type, license headers, commit message rules, and test validation commands. Not duplicated here; consult it directly for style questions.
- `.github/instructions/*.instructions.md` — deeper per-subsystem instructions (core, plugins, devices, SDF pipeline, shaders, UI, scripts-validation). Referenced from `CLAUDE.md` via `@`-includes.
