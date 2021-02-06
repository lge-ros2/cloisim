# CLOiSim : Multi-Robot Simulator

![Multi-robot](https://user-images.githubusercontent.com/21001946/82773215-75572480-9e7c-11ea-85a2-a3838fa1e190.png)

Happy to announce CLOiSim. It is a new multi-robot simulator that uses an [SDF](www.sdformat.org) file containing 3d world environemnts and robot descriptions.

The simulator is based on Unity 3D. It may look similar to Gazebo, where, unfortunately, we encountered performance problems while loading multiple robots equipped with multiple sensors.

This project consists of

- [SDF](http://sdformat.org/spec?ver=1.7) Parser for C#
- [SDF](http://sdformat.org/spec?ver=1.7) Robot Implementation for Unity -> **Visual / Collision / Sensor / Physics for joints**
- [SDF](http://sdformat.org/spec?ver=1.7) Plugins for Unity
- UI modules -> Module for on-screen information
- Network modules -> Module for transporting sensor data or control signal
- Web service -> Module for controling simulation through a web interface

![cloisim_multirobot](https://user-images.githubusercontent.com/21001946/107105748-3a124f80-686b-11eb-8ac8-74377696e641.gif)
[video link](https://user-images.githubusercontent.com/21001946/104274159-96d84f80-54e3-11eb-9975-9d4bbbbdd586.mp4)

## Features

The current release includes the features only for a 2-wheeled mobile robot with 2D LiDAR sensor.
Other sensor models are work in progress.
Here is the full list of models that is implemented or planned to be implemented.

- [X] 2D LiDAR Sensor
- [X] 2-Wheeled Motor
- [X] Sonar sensor
- [X] IMU
- [X] Contact
- [X] Camera
  - [ ] Camera intrinsic parameter
- [X] Multi-camera
- [X] GPS sensor
- [X] Depth Camera
  - [ ] Point Cloud message
- [X] RealSense (RGB + IR1 + IR2 + Depth)
- [ ] 3D Lidar Sensor
- [ ] Sensor noise models
- [ ] Physics
  - [ ] Support all physics parameters in SDF specification
  - [X] Support `<Joint type="revolute2">`

Plus, [SDF](http://sdformat.org/spec?ver=1.7) works on the essential elements such as `<model>`, `<link>`, `<visual>`, `<collision>`, `<joint>`,  etc.
It does not support optional elmenets like `<lights>`, `<audio>`, `<actor>`, `<state>`.

Currently, geometry mesh type is supporting only 'Wavefront(.obj) with material' and 'STL(.stl)'.

![cloisim_lidar_ros](https://user-images.githubusercontent.com/21001946/107105540-42b65600-686a-11eb-8797-7d937b108c11.gif)
[video link](https://user-images.githubusercontent.com/21001946/103972179-d0415000-51af-11eb-824b-3d77051664d5.mp4)

## How it works

Refer to core codes in 'Assets/Scripts'.

- Load SDF file -> Parse SDF(simulation description) -> Implement and realize description

Shaders are also used to get depth buffer information in a few sensor model.

Default physics engine 'Nvidia PhysX' is used for physics. And it retrieves some of physics parameters from `<ode>` in sdf.
'SDFPlugins' help physics tricky handling for jointing `<link>` ojbects by `<joint>` element.

We've deceided to change a solver type of physics engine since new solver "TGS(Temporal Gauss Seidel)" is intorduced recently(PhysX 4.1).

So from latest version([CLOiSim-1.11.0](https://github.com/lge-ros2/cloisim/releases/tag/1.11.0)), there is NO more constaints for rigidbodies by PGS(Projected Gauss Seidel) solver type.

In previous with PGS solver has big constraints in terms of 'mass' relationship between rigidbodies.

- ~~You could find details about these struggling issue with PhysX in unity forums. -> [this thread](https://forum.unity.com/threads/ape-deepmotion-avatar-physics-engine-for-robust-joints-and-powerful-motors.259889/)~~
  - ~~Mass ratio between two joined rigid bodies is limited to less than 1:10 in order to maintain joint stability~~
  - ~~Motors are soft and cannot deliver enough power to drive multi-level articulated robotics~~
  - ~~Wheels wobble around their joint axis under heavy load~~
  - ~~Simulation step size (time interval) has to be reduced to too small to provide the needed accuracy which kills the performance~~

But inertia factors which retrieved from SDF are still NOT USED for rigidbody in Unity. Because it could cause unexpected behavior with physX engine.

## Getting Started

### Tested environement

- Latest Unity Editor Version: *'2019.4.18f1 (LTS)'*.
- Linux: Ubuntu 20.04.1
- Processor: AMD® Ryzen 9 3900x 12-core processor × 24
- Memory: 32GB
- Graphics: NVIDIA Corporation TU102 [GeForce RTX 2080 Ti]

### Release version

If you don't want to build a project, just USE a release binary([Download linux version](https://github.com/lge-ros2/cloisim/releases)). And just refer to 'Usage'

### If you want to build a project

Please visit here [build guide](https://github.com/lge-ros2/multi-robot-simulator/wiki/Build-Guide).

## Usage

### Run 'CLOiSim'

Set environment path like below.

```shell
export CLOISIM_MODEL_PATH="/home/Unity/cloisim/sample-resources/models"
export CLOISIM_WORLD_PATH="/home/Unity/cloisim/sample-resources/worlds"
```

- ***export LD_LIBRARY_PATH=./CLOiSim_Data/Plugins/:$LD_LIBRARY_PATH***
- ***./CLOiSim.x86_64 -worldFile cloisim.world***

or you can execute '***./run.sh***' script in release [binary](https://github.com/lge-ros2/cloisim/releases) version.

- ***./run.sh cloisim.world***

#### After run 'CLOiSim'

- *'[sim_device](https://github.com/lge-ros2/sim_device)' ros2 packages for transporting sensor data are required.*

- *Run bringup node in '[sim_device](https://github.com/lge-ros2/sim_device)' ros2 packages*

- And *have fun!!!*

#### Debugging log

```shell
tail -f  ~/.config/unity3d/LG\ Electronics/CLOiSim/Player.log
```

#### Control service

CLOiSim supports web-based simulation control service through websocket as an external interface.

websocket service path: ***ws://127.0.0.1:8080/{service-name}***

Just send a request data as a JSON format.

Read [detail guide](https://github.com/lge-ros2/cloisim/wiki/Usage#control-service)

![cloisim_nav2_ros2](https://user-images.githubusercontent.com/21001946/107105530-37fbc100-686a-11eb-9ff8-f3cf45012d9b.gif)
[video link](https://user-images.githubusercontent.com/21001946/103973626-2f549400-51b3-11eb-8d1f-0945d40c700b.mp4)

## Future Plan

New features or functions shall be developed on demand.

- Fully support to keep up with 'SDF specifiaction version 1.7'

- Add new sensor models and enhance sensor performance

- Noise models for sensor model

- Performance optimization for sensors (Use DOTS by unity?)

- Upgrade quality of graphical elements

- Change physics engine (havok or something else...) to find a stable one.

- Change Unity Editor version to 2020.3 (LTS) to utilize 'articulation body'.

- **If you have any troubles or issues, please don't hesitate to create a new issue on 'Issues'.**
  <https://github.com/lge-ros2/cloisim/issues>

감사합니다. Thank you
