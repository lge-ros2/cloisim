# CLOiSim: Multi-Robot Simulator

[![Unity](https://img.shields.io/badge/Unity-6-black.svg?style=flat&logo=unity)](https://unity.com/)
[![ROS 2 Humble](https://img.shields.io/badge/ROS%202-Humble-blue.svg?style=flat&logo=ros)](https://docs.ros.org/en/humble/)
[![ROS 2 Jazzy](https://img.shields.io/badge/ROS%202-Jazzy-green.svg?style=flat&logo=ros)](https://docs.ros.org/en/jazzy/)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Release](https://img.shields.io/github/v/release/lge-ros2/cloisim)](https://github.com/lge-ros2/cloisim/releases)

CLOiSim is a high-performance multi-robot simulator based on **Unity 6**. It dynamically builds simulated 3D environments and robots directly from [SDFormat (SDF)](http://sdformat.org/) description files.

![multi-type-of-robots](https://github.com/lge-ros2/cloisim/assets/21001946/499fc995-0a29-454b-902f-3df77d00c7de)

## 🚀 Overview

CLOiSim was developed to address performance bottlenecks encountered with other simulators when loading multiple robots with complex sensor suites. By leveraging Unity's efficient rendering and physics pipelines, CLOiSim provides a scalable solution for large-scale robot simulation.

### Key Components
- **SDF Parser**: Utilizes [sdformat-sharp](https://github.com/lge-ros2/sdformat-sharp) as a Unity package for robust and comprehensive SDF 1.6+ specification parsing.
- **Unity Implementer**: Automated mapping of SDF elements to Unity's Visual, Collision, and Physics (ArticulationBody) components.
- **Transport Layer**: High-performance sensor data and control signal transport via ZeroMQ (NetMQ).
- **Web Service**: JSON-based simulation control and monitoring through a web interface.

![cloisim_multirobot](https://user-images.githubusercontent.com/21001946/107105748-3a124f80-686b-11eb-8ac8-74377696e641.gif)
*[Full Video Demo](https://user-images.githubusercontent.com/21001946/104274159-96d84f80-54e3-11eb-9975-9d4bbbbdd586.mp4)*

---

## 📢 Notices

> [!IMPORTANT]
> CLOiSim has been upgraded to **Unity 6** (6000.4.0f1). Legacy versions based on Unity 2022.3 LTS are no longer maintained.

### Version History
| Branch | CLOiSim Version | Unity Version | Status |
| :--- | :--- | :--- | :--- |
| `main` | **5.x.x (Latest)** | **Unity 6** | Active |
| `release-4.14.6` | 4.x.x | Unity 2022.3 LTS | Maintenance |
| `release-3.2.0` | 3.x.x | Unity 2021 | Legacy |
| `release-2.7.7` | 2.x.x | Unity 2020 | Legacy |

---

## ✨ Features

### 🛠 Sensors & Actuators
| Category | Feature | Status | Notes |
| :--- | :--- | :---: | :--- |
| **Joints** | Joint Control / Pose | ✅ | ArticulationBody based |
| **LiDAR** | 2D / 3D (URT ray tracing) | ✅ | Pattern-based (e.g., Livox) supported |
| **Camera** | Color / Multi / Segmentation | ✅ | Rasterization-based |
| **Depth Camera** | Depth / RealSense (IR1, IR2, VCSEL dot pattern) | ✅ | Rasterization-based |
| **Inertial** | IMU / GPS | ✅ | Gaussian noise models included |
| **Other** | Sonar / IR / Contact | ✅ | |
| **Noise** | Gaussian / Custom | 🚧 | Gaussian fully supported |

### 🌍 World & Physics
- **Physics Engine**: NVIDIA PhysX with **Temporal Gauss Seidel (TGS)** solver for enhanced stability.
- **World Elements**: Actors (animated characters), Lights, Heightmaps (DEM), Roads.
- **Coordinates**: Support for Spherical Coordinates.
- **Rendering**: URP-based high-quality visuals with specialized shaders for sensors.

![cloisim_lidar_ros](https://user-images.githubusercontent.com/21001946/107105540-42b65600-686a-11eb-8797-7d937b108c11.gif)

---

## 🔌 Plugin System

CLOiSim uses a flexible plugin architecture to extend robot and world functionalities via SDF `<plugin>` tags. Plugin names are case-sensitive and should be specified in the `filename` attribute (e.g., `<plugin name="actor_plugin" filename="libActorPlugin.so" />`).

### Model Plugins
- `LaserPlugin`: Publishes 2D or 3D LiDAR data.
- `CameraPlugin`: Publishes 2D color or depth image data.
- `SegmentationCameraPlugin`: Publishes semantic segmentation images and label info.
- `MultiCameraPlugin`: Publishes data from multiple color cameras.
- `RealSensePlugin`: Handles IR1, IR2, depth, and color data for RealSense sensors.
- `MicomPlugin`: Controls differential drive (2/4-wheeled) and self-balancing robots.
- `JointControlPlugin`: Controls joints and publishes joint status.
- `GpsPlugin`: Provides GPS position data in the world.
- `ImuPlugin`: Publishes IMU sensor data.
- `SonarPlugin` / `IRPlugin`: Publishes range data from Sonar or IR sensors.
- `LogicalCameraPlugin`: Publishes object detection and info within the camera view.
- `ContactPlugin`: Publishes contact sensor data.
- `ActorPlugin`: Adds character control functionality using Unity AI components.
- `ParticleSystemPlugin`: Enables Unity's particle systems.
- `ClothPlugin` / `ClothGrabberPlugin`: Handles cloth simulation and fingertip grabbers.

### World Plugins
- `ElevatorSystemPlugin`: Controls lifting and calling logic for elevator systems.
- `GroundTruthPlugin`: Retrieves precise information (position, size, velocity) for all objects.
- `ActorControlPlugin`: Centrally controls actors that have the `ActorPlugin` attached.
- `MowingPlugin`: Handles grass and mowing simulation.

---

## 🏁 Getting Started

### Prerequisites
- **OS**: Ubuntu 22.04+ (Recommended) or Windows 10+
- **Graphics**: Vulkan-capable GPU (NVIDIA RTX 20-series recommended)
- **Unity**: Unity Editor 6000.5.3f1 (if building from source)

### Installation
1. **Release Binary**: [Download the latest Linux version](https://github.com/lge-ros2/cloisim/releases).
2. **From Source**: Refer to the [Build Guide](https://github.com/lge-ros2/cloisim/wiki/Build-Guide) for detailed instructions.

---

## 📖 Usage

### 1. Environment Setup
Set the paths to your resources (models, worlds, media):
```bash
export CLOISIM_FILES_PATH="/path/to/sample_resources/media"
export CLOISIM_MODEL_PATH="/path/to/sample_resources/models"
export CLOISIM_WORLD_PATH="/path/to/sample_resources/worlds"
```

### 2. Running the Simulator
```bash
# Standard mode
./run.sh cloisim.world

# Headless mode (Linux only)
./run.sh --headless --world cloisim.world
```

### 3. ROS 2 Integration
 To bridge simulation data to ROS 2, use the [cloisim_ros](https://github.com/lge-ros2/cloisim_ros) package:
- Supports **ROS 2 Humble & Jazzy**.
- Launch the bringup node to start publishing sensor topics.

### 4. Running EditMode Unit Tests
See [scripts/README.md](scripts/README.md) for detailed EditMode test runner usage and configuration.

---

## 🛠 Advanced Features

### External UI & Control
CLOiSim provides a WebSocket interface for runtime interaction:
- **Path**: `ws://127.0.0.1:8080/{service-name}`
- **Capabilities**: Marker placement (lines, boxes, text), simulation reset, and more.
- [Detailed Guide](https://github.com/lge-ros2/cloisim/wiki/Usage#control-service)

![cloisim_nav2_ros2](https://user-images.githubusercontent.com/21001946/107105530-37fbc100-686a-11eb-9ff8-f3cf45012d9b.gif)

---

## 🗺 Roadmap
- [ ] Full SDF specification compatibility.
- [ ] Programmable C++ plugin interface.
- [ ] Advanced sensor performance optimizations.
- [ ] Enhanced graphical fidelity and material support.

## 🤝 Support
If you encounter any issues or have feature requests, please open an [Issue](https://github.com/lge-ros2/cloisim/issues).

---
감사합니다. Thank you!
