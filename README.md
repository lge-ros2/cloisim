# CLOiSim : Multi-Robot Simulator

Happy to announce CLOiSim. It is a new multi-robot simulator to bring-up SDF(www.sdformat.org) file which contains 3d world environemnt and multi-robot descriptions. 

It may looks similar to Gazebo simulator, but we had a problems to load multi-robot which equipped multi-sensors in Gazebo.
So that is why we initiated to utilize a 'Unity 3D'.
I believe 'Unity' can provide a powerful resource handling and can see many possiblity in industrial area by Unity.

So, this project consist of 
- [SDF](http://sdformat.org/spec?ver=1.7) Parser for C#
- [SDF](http://sdformat.org/spec?ver=1.7) Implementation for Unity -> **Visual / Collision / Sensor / Physics for joints**
- [SDF](http://sdformat.org/spec?ver=1.7) Plugins for Unity
- UI modules -> On screen information
- Network modules -> transporting sensor data or control data
- Web service -> control and manipulate simulation


## Features
Unfortunately, all sensor models are not fully developed yet.
Because first target for simulation was 2-wheeled mobile robot with 2D lidar sensor.

Here are sensor models that already implemented or ongoing.
- [X] 2D Lidar Sensor
- [X] 2-Wheeled Motor
- [X] Sonar sensor
- [X] IMU
- [ ] Camera
- [ ] Multi Camera
- [ ] Depth Camera
- [ ] 3D Lidar Sensor
- [ ] Sensor noise model

Plus, [SDF](http://sdformat.org/spec?ver=1.7) implementation only works on essenstial elements like `<model>`, `<link>`, `<visual>`, `<collision>`, `<joint>`,  etc.
For example, kind of optional elmenets `<lights>`, `<audio>`, `<actor>`, `<state>` and so on are not implemented yet.


## How it works
It's simple. Refer to core codes in 'Assets/Scripts'.
* Load SDF file -> Parse SDF(simulation description) -> Implement and realize description

Shaders are also used to get depth buffer information in a few sensor model.

Default physics engine 'Nvidia PhysX' is used for physics. and retrieve physics parameters from `<ode>` in sdf.
And 'SDFPlugins' help physics tricky handling for jointing `<link>` ojbects by `<joint>` element. Because there are quite big constraints in terms of 'mass' relationship between rigidbody in PhysX engine.

- You could find details about these struggling issue with PhysX in unity forums. -> [this thread](https://forum.unity.com/threads/ape-deepmotion-avatar-physics-engine-for-robust-joints-and-powerful-motors.259889/)
    - Mass ratio between two joined rigid bodies is limited to less than 1:10 in order to maintain joint stability
    - Motors are soft and cannot deliver enough power to drive multi-level articulated robotics
    - Wheels wobble around their joint axis under heavy load
    - Simulation step size (time interval) has to be reduced to too small to provide the needed accuracy which kills the performance

Inertia factors which retrieved from SDF are NOT USED for rigidbody in Unity. Because it cause unexpected behavior with physX engine.


## Build and Run

#### Tested environement:
  - Linux - Ubuntu 18.04
  - Current editor version is *'2019.3.11f1'*.
    - We are heading to *Unity Editor 2019 LTS* as a final version.

#### Release version
If you don't want to build a project, just USE a release binary([Download linux version](https://github.com/lge-ros2/multi-robot-simulator/releases)). And go to 'Usage'


#### Build guide

1. First, You need a Unity Editor to build a project. Download and install [Unity Editor](https://unity3d.com/get-unity/download)

1. Open the project folder where you clone the git repository.

1. You will see popup window if you open it at first time.
    - ***Don't forget to import 'Import TMP Essentials'  when TMP Importer windows popuped.***

1. Select menu, **[File]** -> **[Build Settings]**

1. Choose *'PC, Mac & Linux Standalone'* in Platform list
    - Target Platform: Linux
    - Architeture: x86_64
    - Uncheck all options below
    - (Optional) Compression Method: LZ4HC
    
1. Press the **'Build'** button.

1. **Last important things!** Due to 'protobuf-net' plugin issue after build, we need a copy plugin into build output.
   (We are investigating the issue)
   * Change directory to root project path
      * cd multi-robot-simulator;
      
   * Copy 'protobuf-net' library to output directory. Assume '/cloisim_release'.
      * cp Assets/Plugins/protobuf-net.2.4.6/lib/net40/protobuf-net.dll /cloisim_release/CLOiSim_Data/Managed


## Usage

#### Run 'CLOiSim'

Set environment path like below.

> *export CLOISIM_MODEL_PATH="/home/Unity/cloisim/sample-resources/models"*          
> *export CLOISIM_WORLD_PATH="/home/Unity/cloisim/sample-resources/worlds"*

* ***./CLOiSim.x86_64 -worldFile cloisim.world***

or you can execute '***./run.sh***' script in release [binary](https://github.com/lge-ros2/multi-robot-simulator/releases) version.

* ***./run.sh cloisim.world***


#### After run 'CLOiSim'

  * *'[simdevice](https://github.com/lge-ros2/sim-device)' ros2 packages for transporting sensor data are required.*
  
  * *Run bringup node in '[simdevice](https://github.com/lge-ros2/sim-device)' ros2 packages*

  * *And have fun!!!*


## Future Plan
New features or functions shall be developed on demand.

- Fully support to keep up with 'SDF specifiaction version 1.7'

- Add new sensor models and enhance sensor performance

- Noise models for sensor model

- Performance optimization for sensor(Use DOTS by unity?)

- Upgrade quality of graphical elements

- Change physics engine (havok or something else...) to find a stable one.

- **If you have any troubles or issues, please don't hesitate to create a new issue on 'Issues'.**
  https://github.com/lge-ros2/multi-robot-simulator/issues


#### 감사합니다. Thank you.
