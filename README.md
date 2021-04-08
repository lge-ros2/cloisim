# CLOiSim : Multi-Robot Simulator

![Multi-robot](https://user-images.githubusercontent.com/21001946/82773215-75572480-9e7c-11ea-85a2-a3838fa1e190.png)

Happy to announce CLOiSim. It is a new multi-robot simulator that uses an [SDF](www.sdformat.org) file containing 3d world environemnts and robot descriptions.

The simulator is based on Unity 3D. It may look similar to Gazebo, where, unfortunately, we encountered performance problems while loading multiple robots equipped with multiple sensors. Hence, CLOiSim.

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
- [X] Actors
  - [ ] interpolate_x in `<animation>`

Plus, [SDF](http://sdformat.org/spec?ver=1.7) works on the essential elements such as `<model>`, `<link>`, `<visual>`, `<collision>`, `<joint>`,  etc.
It does not support optional elmenets like `<lights>`, `<audio>`, `<state>`.

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

So there is NO more constaints for rigidbodies by PGS(Projected Gauss Seidel) solver type since latest version([CLOiSim-1.11.0](https://github.com/lge-ros2/cloisim/releases/tag/1.11.0)).

But inertia factors which retrieved from SDF are still NOT USED for rigidbody in Unity. Because it could cause unexpected behavior with physX engine.

## Getting Started

### Minimum requirement

- Processor: testing and looking for the minimum
- Memory: testing and looking for the minimum
- Graphics: testing and looking for the minimum

### Tested environement

- Latest Unity Editor Version: *'2020.3.0f1 (LTS)'*
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

Set environment path like below. You can find the sample resources [here](https://github.com/lge-ros2/sample_resources)

```shell
export CLOISIM_FILES_PATH="/home/Unity/cloisim/sample_resources/media"
export CLOISIM_MODEL_PATH="/home/Unity/cloisim/sample_resources/models"
export CLOISIM_WORLD_PATH="/home/Unity/cloisim/sample_resources/worlds"
```
Run CLOiSim

```shell
./CLOiSim.x86_64 -world lg_seocho.world
```

or you can execute '***./run.sh***' script in release [binary](https://github.com/lge-ros2/cloisim/releases) version.

- ***./run.sh cloisim.world***

#### Run 'cloisim_ros' after running CLOiSim

- *You need to run this package in order to publish sensor data in ROS2.*

- *Run bringup node in '[cloisim_ros](https://github.com/lge-ros2/cloisim_ros)' ros2 packages*

- That's it. *Have fun!!!*

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

- introduce programmable c++ plugin

- Noise models for sensor model

- Performance optimization for sensors (Use DOTS by unity?)

- Upgrade quality of graphical elements

- **If you have any troubles or issues, please don't hesitate to create a new issue on 'Issues'.**
  <https://github.com/lge-ros2/cloisim/issues>

감사합니다. Thank you
