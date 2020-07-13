# CLOiSim : Multi-Robot Simulator

![Multi-robot](https://user-images.githubusercontent.com/21001946/82773215-75572480-9e7c-11ea-85a2-a3838fa1e190.png)

Happy to announce CLOiSim. It is a new multi-robot simulator that uses an SDF(www.sdformat.org) file containing 3d world environemnts and robot descriptions.

The simulator is based on Unity 3D. It may look similar to Gazebo, where, unfortunately, we encountered performance problems while loading multiple robots equipped with multiple sensors.

This project consists of 
- [SDF](http://sdformat.org/spec?ver=1.7) Parser for C#
- [SDF](http://sdformat.org/spec?ver=1.7) Robot Implementation for Unity -> **Visual / Collision / Sensor / Physics for joints**
- [SDF](http://sdformat.org/spec?ver=1.7) Plugins for Unity
- UI modules -> Module for on-screen information
- Network modules -> Module for transporting sensor data or control signal
- Web service -> Module for controling simulation through a web interface


## Features
The current release includes the features only for a 2-wheeled mobile robot with 2D LiDAR sensor.
Other sensor models are work in progress.
Here is the full list of models that is implemented or planned to be implemented.
- [X] 2D LiDAR Sensor
- [X] 2-Wheeled Motor
- [X] Sonar sensor
- [X] IMU
- [X] Camera
    - [ ] Camera intrinsic parameter
- [X] Multi-camera
- [X] GPS sensor
- [ ] Depth Camera
    - [ ] Point Cloud message
- [ ] 3D Lidar Sensor
- [ ] Sensor noise model

Plus, [SDF](http://sdformat.org/spec?ver=1.7) works on the essential elements such as `<model>`, `<link>`, `<visual>`, `<collision>`, `<joint>`,  etc.
It does not support optional elmenets like `<lights>`, `<audio>`, `<actor>`, `<state>`.


## How it works
Refer to core codes in 'Assets/Scripts'.
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
  - Current editor version is *'2019.4.1f1 (LTS)'*.
  
#### Release version
If you don't want to build a project, just USE a release binary([Download linux version](https://github.com/lge-ros2/multi-robot-simulator/releases)). And go to 'Usage'


#### Build guide

1. First, You need a Unity Editor to build a project. Download and install [Unity Editor](https://unity3d.com/get-unity/download)

1. Install prerequisite libraries
    - $ sudo apt-get install libvulkan1

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


#### Control service 

CLOiSim supports web-based simulation control service through websocket as an external interface.

websocket service path: ***ws://127.0.0.1:8080/{service-name}***

Just send a request data as a JSON format.

##### Supporting Service Name
+ control  => *url: ws://127.0.0.1:8080/control*
    + Reset simulation
        +   `{"command": "reset"}`
    
+ markers  => *url: ws://127.0.0.1:8080/markers*
    + Markers are consist of 'group', 'id', 'type', 'color'.
        + 'id' must be unique in 'group'.
        + Check supporing color type in below
            + Red, Green, Blue, Gray, Orange, Lime, Pink, Purple, Navy, Aqua, Cyan, Magenta, Yellow, Black
        + There are four type of 'type'. Must describe type properties for each type.
            + line
                + `"line":{"size":0.03, "point":{"x":1.1, "y":1.1, "z":3.3}, "endpoint":{"x":1.1, "y":1.1, "z":10.0}}}`
            + text
                + `"text":{"size": 5, "align": "center", "following": null, "text": "Hello!!!!\nCLOiSim!!", "point": {"x": 1.0, "y": 1.0, "z": 4.0}}`
            + sphere
                + ` "sphere":{"size":0.1, "point":{"x": 1.1, "y": 2.2, "z": 4.1}}`
            + box
                + `"box":{"size": 0.2, "point":{"x": 2.1, "y": 3.2, "z": 4.1}}`
    
    + Add markers
        + `{"command":"add", "markers":[{"group":"sample","id":4,"type":"line","color":"red", (type_properties_here)},]}`
        + `{"command":"add", "markers":[{"group":"sample","id":4,"type":"line","color":"red", "line":{"size":0.03, "point":{"x":1.1, "y":1.1, "z":3.3}, "endpoint":{"x":1.1, "y":1.1, "z":10.0}}},]}`
    + Modify markers
        + `{"command":"modify", "markers":[{"group":"sample", "id":4, "type":"line", "color":"blue", "line":{"size":0.05, "point":{"x":1.1, "y":1.1, "z":3.3}, "endpoint":{"x":1.1,"y":1.1,"z":5.0}}},]}`
    + Remove markers
        + `{"command": "remove", "markers":[{"group":"sample","id":5,"type":"line"},]}`
    + Get markers list
        + `{"command": "list"}`
        + `{"command": "list", "filter":{"group": "sample"}}`

###### Following object feature in 'Text' marker
+ Enable following function
    + Input target object name in simulation on "following" field.
    + `"text":{"size": 5, "align": "center", "following": "CLOI_Porter_Robot", "text": "Hello, CLOiSim!!", "point": {"x": 1.0, "y": 3.0, "z": 4.0}}`
+ Disable following function 
    + `"text":{"size": 5, "align": "center", "following": "", "text": "Hello, CLOiSim", "point": {"x": 1.0, "y": 3.0, "z": 4.0}}`
    + `"text":{"size": 5, "align": "center", "following": null, "text": "Hello, CLOiSim", "point": {"x": 1.0, "y": 3.0, "z": 4.0}}`



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
