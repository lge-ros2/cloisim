# Gemini Code Assistant Context: CLOiSim

This document provides definitive, up-to-date context for the Gemini Code Assistant (currently using Gemini 3.1 Pro/Preview) to understand and assist with the CLOiSim project. It reflects the deeply analyzed, *actual* state of the repository code, overriding stale project documentation.

## Project Overview

CLOiSim is a multi-robot simulator based on the Unity engine. It dynamically builds a simulated 3D world (models, links, joints, sensors, actors) directly from [SDFormat (SDF)](http://sdformat.org/) description files. CLOiSim provides a ZeroMQ (NetMQ) transport layer that exposes sensor data and control endpoints which are then bridged to ROS 2 via an external package (`cloisim_ros`).

### Key Technologies & State

*   **Simulation Engine:** Unity 6 (`6000.4.5f1`), *not* Unity 2022 LTS as older docs might imply.
*   **Rendering Pipeline:** Universal Render Pipeline (URP) version 17.4.0.
*   **Input & Navigation:** Input System `1.19.0`, AI Navigation `2.0.12`.
*   **Plugin Dependencies:** AssimpNetter 6.0.4 (native mesh import), NetMQ 4.0.4 (ZeroMQ transport), protobuf-net 3.2.56 (messaging), WebSocketSharp 1.0.3-rc11 (control API), Newtonsoft.Json 13.0.4 (JSON).
*   **Core Logic:** C# scripts located under `Assets/Scripts/`.

## Architecture Layers

The simulator architecture is divided into five main layers heavily coordinated through a singleton `Main` MonoBehaviour:

### 1. Orchestration (`Assets/Scripts/Main.cs`)
*   Bootstraps the simulator, initializes environment paths (`CLOISIM_FILES_PATH`, `CLOISIM_MODEL_PATH`, `CLOISIM_WORLD_PATH`), manages singletons, and executes CLI arguments (`-world`).
*   Configures physics engine dynamically before bringing components fully online.

### 2. SDF Parsing & Implementer Pipeline (`Assets/Scripts/Tools/SDF/`)
*   **Parser:** Deserializes SDF (1.0 - 1.9) XML into C# class objects (`Root`, `World`, `Model`, `Sensor`, etc.).
*   **Importer:** Processes parsed elements and maps them functionally.
*   **Implementer:** Calls actual Unity APIs (creating primitive Colliders, URP Renderers, ArticulationBodies for joints) and correctly attaches them.
*   Coordinate system translation happens here (SDF Right-Handed to Unity Left-Handed space).

### 3. Simulation Runtime (`Assets/Scripts/Core/`)
*   **SimulationWorld:** Drives the internal simulation clock and reset logic.
*   **BridgeManager:** Allocates UDP/TCP ports dynamically for devices (range `49152 - 65535`).
*   **PluginStartTracker:** Monitors all loaded plugins iteratively until all report a `Started` state before proceeding with the physics tick.
*   **Services:** WebSocket servers enabling JSON-based external requests (reset, fps, marker placement).

### 4. Devices & Plugins (`Assets/Scripts/Devices/` & `Assets/Scripts/CLOiSimPlugins/`)
*   **Devices:** High-level abstractions for sensors (Lidar, Camera, GPS, IMU, etc.) and actuators handling noise application (`NoiseModel`), synthetic monotonic timestamping, and threaded event dispatch to offload the Unity main thread.
*   **CLOiSimPlugins:** Bridges instances of Devices to NetMQ sockets. Contains publisher, subscriber, requestor, and transponder logic, transmitting `cloisim.msgs` Protobuf payloads.

### 5. UI & Camera Controls (`Assets/Scripts/UI/`)
*   UI Toolkit-centric heads-up display. Handles perspective switching, object targeting/following (`FollowingTargetList`), primitive spawning (`ObjectSpawning`), and runtime model loading from URIs.

## Physics & Simulation Constraints
*   **Physics Settings:** Temporal Gauss Seidel (TGS), 10 solver iterations, 5 velocity iterations. Auto-sync transforms is disabled (transforms are explicitly synchronized by the physics scripts iteratively).
*   **Materials:** Ground friction and custom patches are explicitly built to mitigate physics jitter.

## Key Development Rules

*   **Plugin Threading:** Devices offload transmission/serialization to `CLOiSimPluginThread` worker threads to maintain 60 FPS. Ensure data passed out of Unity Engine threads uses concurrent structures (like `DeviceMessageQueue` buffers).
*   **SDF Import Loop:** Do not manually instantiate complex models in code for permanent features. Implement parsing hooks inside `Import.Element.cs` or `Implement.Element.cs` layers.
*   **Custom Tags and Layers:** Components depend on explicitly named tags: `Model`, `Link`, `Visual`, `Collision`, `Sensor`, `Light`, `Actor`, `Marker`, `Props`, `Geometry`, `Road`. Custom layers: `Plane` (3), `Visualization` (6), `Cloth` (7). URP culls standard cameras with explicit bitmask matrices—be aware of this when editing `SensorRenderManager` render loops.
