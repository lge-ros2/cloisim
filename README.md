# CLOiSim : Multi-Robot Simulator

![Multi-robot](https://user-images.githubusercontent.com/21001946/82773215-75572480-9e7c-11ea-85a2-a3838fa1e190.png)

Happy to announce CLOiSim. It is a new multi-robot simulator that uses an [SDF](www.sdformat.org) file containing 3d world environemnts and robot descriptions.

The simulator is based on Unity 3D. It may look similar to Gazebo, where, unfortunately, we encountered performance problems while loading multiple robots equipped with multiple sensors. Hence, CLOiSim.

This project consists of

- [SDF](http://sdformat.org/spec?ver=1.7) Parser for C#
- [SDF](http://sdformat.org/spec?ver=1.7) Robot Implementation for Unity -> **Visual / Collision / Sensor / Physics for joints**
- [SDF](http://sdformat.org/spec?ver=1.7) Plugins for Unity3D
- UI modules -> Module for on-screen information
- Network modules -> Module for transporting sensor data or control signal
- Web service -> Module for controling simulation through a web interface

![cloisim_multirobot](https://user-images.githubusercontent.com/21001946/107105748-3a124f80-686b-11eb-8ac8-74377696e641.gif)
[video link](https://user-images.githubusercontent.com/21001946/104274159-96d84f80-54e3-11eb-9975-9d4bbbbdd586.mp4)

## Features

### Sensors

The current release includes the features only for marked items in the list below.
Other sensor models are work in progress.
Here are the list of items that is implemented(marked) or planned to be implemented.

- [X] Joint models
  - [X] 2-Wheeled Motor driving
  - [X] Joint control
- [X] Sensor models
  - [X] LiDAR Sensor
    - [X] 2D
    - [X] 3D
  - [X] Sonar sensor
  - [X] IMU
  - [X] Contact
  - [X] Camera
    - [ ] Camera intrinsic parameter
    - [X] Depth Camera
    - [X] Multi-camera
    - [X] RealSense (RGB + IR1 + IR2 + Depth)
  - [X] GPS sensor
  - [ ] Sensor noise models
    - [X] Gaussian
      - [X] GPS, IMU
      - [X] Lidar
      - [ ] Camera
    - [ ] Custom
- [ ] Physics
  - [ ] Support all physics parameters in SDF specification
  - [X] Support `<Joint type="revolute2">`
- [ ] Worlds
  - [X] Actors
    - [ ] interpolate_x in `<animation>`
  - [X] Lights
    - [ ] supporting `<specular>`, `<attenuation/linear>`, `<attenuation/contant>`, `<attenuation/quadratic>`, `<spot/falloff>`
  - [X] Spherical Coordinates

Plus, [SDF](http://sdformat.org/spec?ver=1.7) works on the essential elements such as `<model>`, `<link>`, `<visual>`, `<collision>`, `<joint>`,  etc.
It does not support optional elmenets like `<wind>`, `<audio>`, `<state>`, `<atmosphere>`, `<magnetic_field>`, `<scene>`, `<road>`, `<population>`.

Currently, geometry mesh type is supporting only 'Wavefront(.obj) with material', 'Collada(.dae) including animation' and 'STL(.stl)'.
`<ambient>` elements in `<materal>` and ambient properies in mesh files are not support in CLOiSim.

![cloisim_lidar_ros](https://user-images.githubusercontent.com/21001946/107105540-42b65600-686a-11eb-8797-7d937b108c11.gif)
[video link](https://user-images.githubusercontent.com/21001946/103972179-d0415000-51af-11eb-824b-3d77051664d5.mp4)

### Sensor Plugins

It called 'CLOiSimPlugin'. And below plugins could be utilized through write an element on SDF.

Plugin name should be written in filename attribute and it's case sensitive.

For example,

```xml
<plugin name="actor_plugin" filename="libActorPlugin.so" />
```

more details in [here](https://github.com/lge-ros2/cloisim/tree/main/Assets/Scripts/CLOiSimPlugins)).

#### Model Specific

- `LaserPlugin`: help to publish 2D or 3D lidar data
- `CameraPlugin`: help to publish 2D color image data or depth image data
- `MultiCameraPlugin`: help to publish multiple color image data
- `RealSensePlugin`: can handle ir1(left), ir2(right), depth, color
- `MicomPlugin`: control micom input/output(sensor)
- `GpsPlugin`: gps position in world
- `ActorPlugin`: add actor control functionality using AI(Unity) components

#### World Specific

- `ElevatorSystemPlugin`: control(lifting, cal) elevators
- `GroundTruthPlugin`: retrieve all information(position, size, velocity) for objects
- `ActorControlPlugin`: controls actor using AI(Unity) components(actor which loaded `ActorPlugin`)

## How it works

Refer to core codes in 'Assets/Scripts'.

- Load SDF file -> Parse SDF(simulation description) -> Implement and realize description

Shaders are also used to get depth buffer information in a few sensor model.

Default physics engine 'Nvidia PhysX' is used for physics. And it retrieves some of physics parameters from `<ode>` in sdf.
'SDFPlugins' help physics tricky handling for jointing `<link>` ojbects by `<joint>` element.

We've deceided to change a solver type of physics engine since new solver "TGS(Temporal Gauss Seidel)" is intorduced recently(PhysX 4.1).

So there is NO more constaints for rigidbodies by PGS(Projected Gauss Seidel) solver type since latest version([CLOiSim-1.11.0](https://github.com/lge-ros2/cloisim/releases/tag/1.11.0)).

For the performance in terms of collision handling, designing collision geometry properly may important.

## Getting Started

### Minimum requirement

- Processor: testing and looking for the minimum
- Memory: testing and looking for the minimum
- Graphics: testing and looking for the minimum

### Tested environement

- Latest Unity Editor Version: *'2020.3.15f1 (LTS)'*.

- Linux Machine
  - OS: Ubuntu 20.04.2 LTS
  - Processor: AMD® Ryzen 9 3900x 12-core processor × 24
  - Memory: 32GB
  - Graphics: NVIDIA Corporation TU102 [GeForce RTX 2080 Ti]

- Windows Machine
  - OS: Windows 10 20H2
  - Processor: AMD® Ryzen 9 5900HS 8-core processor x 16
  - Memory: 32GB
  - Graphics: NVIDIA GeForce RTX3060 Laptop GPU

### Release version

If you don't want to build a project, just USE a release binary([Download linux version](https://github.com/lge-ros2/cloisim/releases)). And just refer to '[Usage](https://github.com/lge-ros2/cloisim#usage)' section.

In terms of branch, 'main' is release(stable) version, and 'develop' is used for development(on-going).

### If you want to build a project

Please visit here [build guide](https://github.com/lge-ros2/cloisim/wiki/Build-Guide).

## Usage

### Run 'CLOiSim'

Set environment path like below. You can find the sample resources [here](https://github.com/lge-ros2/sample_resources)

Multiple path can be set by :(colon).

```shell
export CLOISIM_FILES_PATH="/home/Unity/cloisim/sample_resources/media"
export CLOISIM_MODEL_PATH="/home/Unity/cloisim/sample_resources/models:/home/Unity/cloisim/another_resources/models"
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
tail -f  ~/.config/unity3d/LGElectronics.AdvancedRoboticsLab/CLOiSim/Player.log
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

- Performance optimization for sensors (Use DOT by unity?)

- Upgrade quality of graphical elements

- **If you have any troubles or issues, please don't hesitate to create a new issue on 'Issues'.**
  <https://github.com/lge-ros2/cloisim/issues>

감사합니다. Thank you
