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
