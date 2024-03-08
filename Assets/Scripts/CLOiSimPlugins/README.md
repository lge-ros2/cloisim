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
- `SonarPlugin`: help to publish Sonar range data

### World Specific

- `ElevatorSystemPlugin`: control(lifting, cal) elevators
- `GroundTruthPlugin`: retrieve all information(position, size, velocity) for objects
- `ActorControlPlugin`: controls actor using AI(Unity) components(actor which loaded `ActorPlugin`)

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
