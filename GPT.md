# GPT-5.4 Working Context: CLOiSim

This file is a fresh workspace-derived context for GPT-5.4. It is not a copy of the other assistant context files. The goal is to capture what is actually visible in this repository right now, plus the implementation patterns that matter when making code changes.

## 1. High-level model of the project

CLOiSim is a Unity-based multi-robot simulator that loads SDF world and model descriptions, instantiates them into a Unity scene, then binds device and control plugins for simulation, visualization, and external communication.

From the current workspace, the simulator is best understood as five cooperating layers:

1. **Startup and orchestration**
     - `Assets/Scripts/Main.cs`
     - Initializes Unity-side services, resource paths, rendering/performance settings, parser state, bridge state, and the WebSocket control service.

2. **SDF parsing and import pipeline**
     - `Assets/Scripts/Tools/SDF/Parser/*`
     - `Assets/Scripts/Tools/SDF/Import/*`
     - `Assets/Scripts/Tools/SDF/Implement/*`
     - Converts `.world` / model SDF content into Unity objects, links, joints, visuals, collisions, sensors, actors, roads, and plugins.

3. **Simulation runtime modules**
     - `Assets/Scripts/Core/*`
     - Provides world state, reset behavior, object spawning, navmesh building, segmentation, plugin startup tracking, save support, and service modules.

4. **Device and plugin layer**
     - `Assets/Scripts/Devices/*`
     - `Assets/Scripts/CLOiSimPlugins/*`
     - Devices represent sensors and control endpoints; plugins bridge them to transport, control, ROS-related integration paths, and custom behavior.

5. **UI and external control**
     - `Assets/Scripts/UI/*`
     - `Assets/Scripts/Core/Modules/SimulationService.cs`
     - WebSocket services expose simulation control and marker visualization; UI components surface state and errors inside the simulator.

## 2. Current workspace facts that matter

### Unity version reality

The repository documentation still mentions Unity `2022.3.71f1`, but the checked-in project currently declares:

- `ProjectSettings/ProjectVersion.txt` → `6000.3.11f1`

So GPT-5.4 should treat this workspace as a **Unity 6-era project state** with legacy documentation still present. If behavior differs between docs and code, prefer the checked-in project files and package manifest.

### Package stack observed in this workspace

Key package signals from `Packages/manifest.json`:

- URP: `com.unity.render-pipelines.universal` `17.3.0`
- Input System: `com.unity.inputsystem` `1.19.0`
- AI Navigation: `com.unity.ai.navigation` `2.0.11`
- Mathematics: `com.unity.mathematics` `1.3.3`
- Splines, Terrain Tools, uGUI, and robotics VHACD support are included

This confirms the project is not a minimal Unity scene loader. It actively depends on modern URP, the new Input System, navmesh tooling, and convex decomposition tooling.

## 3. Startup flow from code, not just docs

The clearest runtime truth is in `Assets/Scripts/Main.cs`.

### What `Main` does during `Awake()`

- Replaces the legacy standalone input module with `InputSystemUIInputModule`
- Redirects `Console.Out` and `Console.Error` into Unity logging
- Reads resource roots from environment variables:
    - `CLOISIM_FILES_PATH`
    - `CLOISIM_MODEL_PATH`
    - `CLOISIM_WORLD_PATH`
- Loads the Assimp native library dynamically based on platform
- Forces AsyncIO/NetMQ initialization for transport compatibility
- Applies graphics and performance defaults:
    - `Application.targetFrameRate = 60`
    - texture streaming enabled
    - reduced shadow settings
    - camera culling distances customized per layer
- Locates root scene objects such as `Core`, `Props`, `World`, `Lights`, `Roads`, and `UI`
- Creates core runtime singletons/services:
    - `BridgeManager`
    - `SimulationService`
    - `SimulationWorld`
    - `ObjectSpawning`
    - `ModelImporter`
    - `Segmentation.Manager`
    - `MeshProcess.VHACD`

### What `Main` does during `Start()`

- Verifies support for `AsyncGPUReadback`
- Reads command-line arguments:
    - `-capture`
    - `-world`
    - `-worldFile`
- Builds an `SDF.Root`
- Pushes default file/model/world paths into the SDF root
- Updates the model resource table for UI/import usage
- Starts world loading if a world file was provided

### World/model loading behavior

`LoadWorld()` and `LoadModel()` both follow the same general pattern:

1. Parse SDF
2. Temporarily switch physics to `SimulationMode.Script`
3. Import objects through the SDF loader
4. Bind plugin startup tracking
5. Sync transforms
6. Return physics to `SimulationMode.FixedUpdate`
7. Wait until all plugins report started
8. Print port allocation history via `BridgeManager`

This means plugin startup sequencing is an explicit part of the runtime design, not an incidental detail.

## 4. Plugin system: important implementation pattern

The plugin system is not just a folder of feature scripts; it has lifecycle and transport conventions.

### Base plugin contract

`Assets/Scripts/CLOiSimPlugins/Modules/Base/CLOiSimPlugin.cs` shows that every plugin:

- Is a `MonoBehaviour`
- Implements `ICLOiSimPlugin`
- Has a typed plugin category through `ICLOiSimPlugin.Type`
- Stores `modelName`, `partsName`, optional parent link metadata, and startup summary text
- Follows a delayed startup model via `DelayedOnStart()`
- Raises a `Started` event when initialization is complete
- Resets through `Reset()` → `OnReset()`
- Disposes transport/thread resources and deregisters allocated device ports in `OnDestroy()`

### Plugin startup tracking

`Assets/Scripts/Core/PluginStartTracker.cs` is a newer runtime coordination utility that:

- Collects all `CLOiSimPlugin` instances under a loaded root
- Watches their `Started` event
- Emits progress counts
- Fires a single `AllStartedEvent`
- Aggregates startup summaries

This means any new plugin should integrate cleanly with the existing startup lifecycle instead of bypassing it.

### Practical implication for changes

When modifying or adding plugins, preserve:

- delayed startup sequencing
- proper `Started` signaling
- reset behavior
- device port deregistration
- summary/progress visibility

## 5. External communication architecture

Two separate communication concepts exist in the visible code.

### A. Device/transport port allocation

`Assets/Scripts/Core/Modules/BridgeManager.cs` manages dynamic device ports.

Observed behavior:

- allocates ports in the dynamic/private range starting at `49152`
- tracks both a flat hash-key-to-port table and a nested device map
- supports lookup/filtering for external queries
- removes mappings during plugin destruction or reset flows

This is the internal registry for simulated devices and their exposed endpoints.

### B. WebSocket service for simulator control

`Assets/Scripts/Core/Modules/SimulationService.cs` exposes a separate WebSocket server.

Observed behavior:

- default service port: `8080`
- override environment variable: `CLOISIM_SERVICE_PORT`
- registers two services:
    - `/control`
    - `/markers`
- implemented with `WebSocketSharp.Server`

This service is simulator control infrastructure, not the same thing as sensor/device transport allocation.

## 6. Directory map with recommended interpretation

### `Assets/Scripts/Main.cs`
Single highest-value file for understanding boot, parsing, reset, command-line arguments, and service wiring.

### `Assets/Scripts/Core/`
Runtime coordination layer. Notable components visible from the workspace:

- `SimulationWorld.cs` — world clock/reset integration
- `WorldNavMeshBuilder.cs` — navmesh generation support
- `ObjectSpawning.cs` — runtime object/model creation helpers
- `WorldSaver.cs` — persistence back to SDF-derived world content
- `PluginStartTracker.cs` — plugin startup synchronization
- `Modules/BridgeManager.cs` — transport/device port registry
- `Modules/SimulationService.cs` — WebSocket host
- `Services/SimulationControlService.cs` — control endpoint behavior
- `Services/MarkerVisualizerService.cs` — marker endpoint behavior

### `Assets/Scripts/Devices/`
Sensor/control abstractions such as:

- `Lidar.cs`
- `Camera.cs`
- `DepthCamera.cs`
- `GPS.cs`
- `IMU.cs`
- `Contact.cs`
- `JointCommand.cs`
- `JointState.cs`
- `MicomCommand.cs`
- `MicomSensor.cs`
- `SegmentationCamera.cs`

This layer appears to model device semantics independently from plugin transport wrappers.

### `Assets/Scripts/CLOiSimPlugins/`
Feature and transport bridge layer. Concrete plugins include:

- `LaserPlugin`
- `CameraPlugin`
- `RealSensePlugin`
- `SegmentationCameraPlugin`
- `MultiCameraPlugin`
- `ImuPlugin`
- `GpsPlugin`
- `SonarPlugin`
- `IRPlugin`
- `JointControlPlugin`
- `MicomPlugin`
- `ActorPlugin`
- `ActorControlPlugin`
- `GroundTruthPlugin`
- `ElevatorSystem`
- `MowingPlugin`
- `ParticleSystemPlugin`

### `Assets/Scripts/Tools/SDF/`
Core parser/importer implementation. This is where to work when the request involves SDF compatibility, parser bugs, or model/world import behavior.

### `Assets/Scripts/UI/`
User-facing simulator UI plus marker visualization and control feedback. Important for changes that need visible status, error reporting, or runtime manipulation tools.

## 7. Sensor and simulation capabilities inferred from both docs and code layout

The repository structure and README consistently indicate support for:

- 2D and 3D LiDAR
- RGB camera
- depth camera
- multi-camera
- RealSense-style grouped camera setup
- semantic segmentation camera
- IMU
- GPS
- sonar / IR range sensors
- contact sensors
- joint control
- actor control
- world-level plugins such as elevators, mowing, and ground truth

The code also strongly suggests GPU-assisted rendering paths matter for sensor output, especially because startup hard-fails when `AsyncGPUReadback` is unsupported.

## 8. Operational constraints GPT-5.4 should respect

### Prefer code over stale prose when they conflict

Example: docs say Unity 2022 LTS, but project metadata says Unity 6. Prefer the checked-in project files.

### Preserve serialized fields and scene object names

Many systems depend on Unity serialization and `GameObject.Find(...)` names such as `Core`, `World`, `Lights`, `Roads`, and `UI`. Renaming or refactoring these casually is risky.

### Treat plugin teardown as important

`CLOiSimPlugin` performs thread shutdown, transport disposal, and port deregistration in `OnDestroy()`. Changes should not leak threads, sockets, or allocated ports.

### Maintain startup visibility

The project already reports progress and errors through:

- Unity logs
- `UIController` messages
- plugin startup summaries

New runtime behavior should continue to surface failures and progress instead of failing silently.

### Avoid bypassing the SDF pipeline

The codebase is organized around SDF parse → import → implement. Feature work that changes world/model semantics should usually be expressed through that path rather than through one-off scene-only logic.

## 9. Run and environment notes

Resource discovery is environment-driven outside the editor:

- `CLOISIM_FILES_PATH`
- `CLOISIM_MODEL_PATH`
- `CLOISIM_WORLD_PATH`
- `CLOISIM_QUALITY`
- `CLOISIM_SERVICE_PORT`

Command-line arguments used by `Main`:

- `-world`
- `-worldFile`
- `-capture`

Documented execution scripts still reference `run.sh` and release binaries, but code analysis indicates the runtime contract above is what must remain intact.

## 10. Suggested reasoning strategy for GPT-5.4 when editing this repo

When asked to change behavior, first classify the request into one of these buckets:

1. **Boot/runtime orchestration** → inspect `Main.cs`, `Core/Modules`, `Core/Services`
2. **SDF compatibility/import bug** → inspect `Tools/SDF/Parser`, `Import`, `Implement`
3. **Sensor behavior or transport** → inspect `Devices/*`, matching `CLOiSimPlugins/*`, and `BridgeManager`
4. **Control/UI behavior** → inspect `UI/*` and WebSocket services
5. **World state/reset/save** → inspect `SimulationWorld`, `WorldSaver`, reset paths in `Main`

Then preserve existing patterns:

- Unity serialization first
- explicit reset support
- plugin lifecycle compliance
- UI/log feedback
- minimal API breakage

## 11. Short project summary for future sessions

CLOiSim is a Unity-based SDF simulator whose true center of gravity is `Main.cs` plus the SDF importer and plugin lifecycle. The most important architectural ideas in the current workspace are:

- SDF-driven scene generation
- plugin-based device/world behavior
- dynamic transport port allocation via `BridgeManager`
- WebSocket simulator control via `SimulationService`
- explicit plugin startup synchronization
- GPU-sensitive sensor/render paths
- a likely in-progress migration from older documented Unity versions to a current Unity 6 project state
