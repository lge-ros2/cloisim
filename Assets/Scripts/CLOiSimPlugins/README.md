# CLOiSimPlugin

These plugin scripts are for sensor connection.

Each class name is important to load plugin in SDF. Class name should be exact to filename which means if the class name is 'SensorPlugin' filename would be a 'libSensorPlugin.so'. But 'lib' or '.so' words will be discarded.

For example, if it describes a name with 'RobotControl' in `<plugin>` attributes, SDF Parser will start to find a filename in plugin element as 'RobotControl' in Unity project.

## List of plugins

Just take one of below plugins list and write it inside of `filename` attirbute.
It is optional but recommend to append 'lib' and '.so' to the name as a prefix and postfix.

### Model Specific

- `LaserPlugin`: help to publish 2D or 3D lidar data
- `CameraPlugin`: help to publish 2D color image data or depth image data
- `SegmentationCameraPlugin`: help to publish semantic segmentation image data and label info
- `MultiCameraPlugin`: help to publish multiple color image data
- `RealSensePlugin`: can handle ir1(left), ir2(right), depth, color
- `MicomPlugin`: control micom(differential drive) input/output(sensor)
- `GpsPlugin`: gps position in world
- `JointControlPlugin`: can control joints and help to publish joints status.
- `ActorPlugin`: add actor control functionality using AI(Unity) components
- `ImuPlugin`: help to publish IMU sensor data
- `SonarPlugin`: help to publish Sonar range data (ultrasound)
- `RangePlugin`: base plugin for range sensors (ultrasound / infrared)
- `IRPlugin`: help to publish infrared range sensor data (extends RangePlugin)
- `ContactPlugin`: help to publish contact sensor data
- `LogicalCameraPlugin`: help to publish logical camera data (detects models within FOV volume)
- `ClothPlugin`: simulates cloth physics on a target link mesh using a Burst-based Position-Based Dynamics (PBD) solver
- `ClothGrabberPlugin`: enables robot fingertip links to grab and release cloth vertices via configurable gripper groups

### World Specific

- `ElevatorSystemPlugin`: control(lifting, cal) elevators
- `GroundTruthPlugin`: retrieve all information(position, size, velocity) for objects
- `ActorControlPlugin`: controls actor using AI(Unity) components(actor which loaded `ActorPlugin`)
- `MowingPlugin`: simulates grass growth and mowing by tracking a rotating blade link over a target plane
- `ParticleSystemPlugin`: configures and drives a Unity ParticleSystem from SDF parameters (emission, shape, noise, collision, renderer)

## Example of RobotControl

```xml
<model>
  ...
  ...
  <plugin name='RobotControl' filename='libRobotControl.so'>
    <PID>
      <kp>3.0</kp>
      <ki>0.2</ki>
      <kd>0.0</kd>
    </PID>
    <wheel>
      <tread>449</tread>
      <radius>95.5</radius>
      <location type="left">LeftWheel</location>
      <location type="right">RightWheel</location>
      <friction>
        <motor>0.06</motor>
        <brake>13.0</brake>
      </friction>
    </wheel>
    <update_rate>20</update_rate>
  </plugin>
</model>
```

## Support 4-Wheel Drive

```xml
<model>
  ...
  ...
  <plugin name='RobotControl' filename='libRobotControl.so'>
    ...
    ...
    <wheel>
      ...
      ...
      <location type="left">LeftWheel</location>
      <location type="right">RightWheel</location>
      <location type="rear_left">RearLeftWheel</location>
      <location type="rear_right">RearRightWheel</location>
      ...
      ...
    </wheel>
    ...
    ...
  </plugin>
</model>
```

## Realsense Camera

Specify depth scale for depth in millimeter and which sensor to activate.

```xml
<model name="intel_d435">
  <link name="link">
    ...
    ...
    <sensor name='color' type='camera'>
      <pose>0 0.032 0.0 0 0 0</pose>
      <camera name="__default__">
        <horizontal_fov>1.2113</horizontal_fov>
        <image>
          <width>640</width>
          <height>480</height>
          <format>R8G8B8</format>
        </image>
        <clip>
          <near>0.1</near>
          <far>100</far>
        </clip>
      </camera>
    </sensor>

    <sensor name="aligned_depth_to_color" type="depth">
      <pose>0 0.032 0.0 0 0 0</pose>
      <camera>
        <horizontal_fov>1.2113</horizontal_fov>
        <image>
          <width>640</width>
          <height>480</height>
          <format>L16</format>
        </image>
        <clip>
          <near>0.105</near>
          <far>10</far>
        </clip>
      </camera>
    </sensor>

    <sensor name='ir1' type='camera'>
      <pose>0 0.017 0 0 0 0</pose>
      <camera>
        <horizontal_fov>1.047</horizontal_fov>
        <image>
          <width>640</width>
          <height>480</height>
          <format>L8</format>
        </image>
        <clip>
          <near>0.1</near>
          <far>100</far>
        </clip>
      </camera>
    </sensor>

    <sensor name='ir2' type='camera'>
      <pose>0 -0.032 0 0 0 0</pose>
      <camera>
        <horizontal_fov>1.047</horizontal_fov>
        <image>
          <width>640</width>
          <height>480</height>
          <format>L8</format>
        </image>
        <clip>
          <near>0.1</near>
          <far>100</far>
        </clip>
      </camera>
    </sensor>
    <sensor name="depth" type="depth">
      <pose>0 -0.011 0 0 0 0</pose>
      <camera>
        <horizontal_fov>1.2113</horizontal_fov>
        <image>
          <width>640</width>
          <height>480</height>
          <format>L16</format>
        </image>
        <clip>
          <near>0.105</near>
          <far>10</far>
        </clip>
      </camera>
    </sensor>
  </link>

  <plugin name="D435Plugin" filename="libRealSensePlugin.so">
    <configuration>
      <depth_scale>1000</depth_scale>
    </configuration>
    <activate>
      <module name='color'>color</module>
      <module name='aligned_depth_to_color'>aligned_depth_to_color</module>
      <!-- <module name='left_imager'>ir1</module>
      <module name='right_imager'>ir2</module> -->
      <!-- <module name='depth'>depth</module> -->
    </activate>
  </plugin>
</model>
```

## Depth camera

Specify depth scale for depth in millimeter and which sensor to activate.

```xml
<sensor name='depth_camera' type='depth'>
  <camera>
    <horizontal_fov>0.98</horizontal_fov>
    <image>
      <width>640</width>
      <height>480</height>
      <format>L16</format>
    </image>
    <clip>
      <near>0.02</near>
      <far>2.1</far>
    </clip>
  </camera>

  <plugin name='DepthCameraPlugin' filename='libCameraPlugin.so'>
    <configuration>
      <depth_scale>1000</depth_scale>
    </configuration>
    <ros2>
      <topic_name>depth</topic_name>
      <frame_id>depthcam_link</frame_id>
    </ros2>
  </plugin>
</sensor>
```

## Semantic Segmentation Camera

Describe which object to label it for segmentation camera.

```xml
<sensor name='camera' type='segmentation_camera'>
  <camera>
    <segmentation_type>semantic</segmentation_type>
  </camera>
  <plugin name='SegmentationCameraPlugin' filename='libSegmentationCameraPlugin.so'>
    <ros2>
      <topic_name>segmentation</topic_name>
      <frame_id>camera_link</frame_id>
    </ros2>
    <segmentation>
      <label>BOX</label>
      <label>CYLINDER</label>
      <!-- <label>SPHERE</label> -->
      <label>ground</label>
    </segmentation>
  </plugin>
</sensor>
```

## Contact Sensor

Publishes contact sensor data from a collision surface.

```xml
<sensor name='contact_sensor' type='contact'>
  <contact>
    <collision>link_collision</collision>
  </contact>
  <plugin name='ContactPlugin' filename='libContactPlugin.so'/>
</sensor>
```

## Infrared / Range Sensor

`RangePlugin` is the base for range sensors. Use `IRPlugin` for infrared and `SonarPlugin` for ultrasound.

```xml
<sensor name='ir_sensor' type='ray'>
  <ray>
    <scan>
      <horizontal>
        <samples>1</samples>
        <resolution>1</resolution>
        <min_angle>0</min_angle>
        <max_angle>0</max_angle>
      </horizontal>
    </scan>
    <range>
      <min>0.02</min>
      <max>0.3</max>
    </range>
  </ray>
  <plugin name='IRSensor' filename='libIRPlugin.so'>
    <ros2>
      <topic_name>ir</topic_name>
      <frame_id>ir_link</frame_id>
    </ros2>
  </plugin>
</sensor>
```

## Logical Camera

Detects models that enter a frustum volume and publishes their names and relative poses.

```xml
<sensor name='logical_camera' type='logical_camera'>
  <logical_camera>
    <near>0.5</near>
    <far>5.0</far>
    <horizontal_fov>1.047</horizontal_fov>
    <aspect_ratio>1.333</aspect_ratio>
  </logical_camera>
  <plugin name='LogicalCameraPlugin' filename='libLogicalCameraPlugin.so'>
    <ros2>
      <topic_name>logical_camera</topic_name>
      <frame_id>camera_link</frame_id>
    </ros2>
  </plugin>
</sensor>
```

## Cloth Simulation

Simulates PBD-based cloth physics on a target link mesh. Supports anchor pins, stretch/bending constraints, and periodic collider refresh.

```xml
<model name='cloth_model'>
  <link name='cloth_link'>
    <!-- mesh visual here -->
  </link>
  <plugin name='ClothPlugin' filename='libClothPlugin.so'>
    <target>cloth_model::cloth_link</target>
    <cloth>
      <simulation>
        <mass>0.5</mass>
        <damping>0.9</damping>
        <friction>0.8</friction>
        <sleep_threshold>0.05</sleep_threshold>
        <gravity>
          <x>0</x>
          <y>-9.81</y>
          <z>0</z>
        </gravity>
      </simulation>
      <solver>
        <iterations>10</iterations>
        <sub_steps>4</sub_steps>
      </solver>
      <constraints>
        <stretching_stiffness>1.0</stretching_stiffness>
        <bending_stiffness>0.8</bending_stiffness>
      </constraints>
      <anchor>
        <axis>y</axis>
        <side>max</side>
        <threshold>0.01</threshold>
      </anchor>
      <collider>
        <particle_radius>0.01</particle_radius>
        <search_margin>0.1</search_margin>
        <update_interval>1.0</update_interval>
      </collider>
    </cloth>
  </plugin>
</model>
```

## Cloth Grabber

Allows robot fingertip links to grab cloth vertices. Grabbers are grouped into activation groups; all grippers in a group must be within `contact_distance_threshold` of the cloth to activate.

```xml
<model name='robot'>
  <!-- links ... -->
  <plugin name='ClothGrabberPlugin' filename='libClothGrabberPlugin.so'>
    <grab_radius>0.005</grab_radius>
    <grippers>
      <gripper name='thumb'>
        <link>thumb_distal</link>
        <collision>thumb_distal_collision</collision>
      </gripper>
      <gripper name='index'>
        <link>index_distal</link>
        <collision>index_distal_collision</collision>
      </gripper>
    </grippers>
    <activation>
      <group contact_distance_threshold='0.005'>
        <gripper>thumb</gripper>
        <gripper>index</gripper>
      </group>
    </activation>
  </plugin>
</model>
```

## Mowing Plugin (World)

Renders procedural grass on a target plane and tracks a rotating blade link to progressively mow it.

```xml
<world>
  <!-- models ... -->
  <plugin name='MowingPlugin' filename='libMowingPlugin.so'>
    <mowing>
      <blade>
        <target>lawn_mower::blade_link</target>
      </blade>
    </mowing>
    <grass>
      <target>lawn::grass_plane_link</target>
      <color>
        <base>0.2 0.6 0.1 1</base>
        <tip>0.5 0.8 0.2 1</tip>
      </color>
      <blade>
        <width><min>0.002</min><max>0.005</max></width>
        <height><min>0.05</min><max>0.12</max></height>
      </blade>
      <bend>
        <blade_amount>
          <forward>0.38</forward>
          <curvature>2.0</curvature>
        </blade_amount>
        <variation>0.2</variation>
      </bend>
      <tessellation>
        <amount>5</amount>
        <distance><min>5</min><max>20</max></distance>
      </tessellation>
      <visibility>
        <threshold>0.5</threshold>
        <falloff>0.05</falloff>
      </visibility>
      <map><resolution>0.05</resolution></map>
    </grass>
  </plugin>
</world>
```

## Particle System Plugin (World)

Configures a Unity `ParticleSystem` component entirely from SDF plugin parameters.

```xml
<world>
  <plugin name='ParticleSystemPlugin' filename='libParticleSystemPlugin.so'>
    <main>
      <start>
        <lifetime>5</lifetime>
        <speed>2</speed>
        <size>0.05</size>
      </start>
      <gravity_modifier>0</gravity_modifier>
      <simulation_speed>1</simulation_speed>
      <max_particles>500</max_particles>
    </main>
    <emission>
      <rate><over_time>20</over_time></rate>
    </emission>
    <shape>
      <angle>15</angle>
      <radius>0.1</radius>
      <rotation>0 0 0</rotation>
    </shape>
    <noise>
      <strength>0.5</strength>
      <frequency>0.5</frequency>
      <scroll_speed>0.1</scroll_speed>
    </noise>
    <collision>
      <bounce>0.1</bounce>
    </collision>
    <renderer>
      <billboard>
        <particle_size><min>0.001</min><max>0.05</max></particle_size>
      </billboard>
      <cast_shadows>false</cast_shadows>
      <material>
        <color>0.8 0.8 1.0 0.6</color>
      </material>
    </renderer>
  </plugin>
</world>
```
