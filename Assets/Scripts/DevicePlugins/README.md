# DevicePlugins

These plugin scripts are for sensor connection.

Each class name is important to load plugin in SDF. Class name should be exact to filename which means if the class name is 'SensorPlugin' filename would be a  'libSensorPlugin.so'.

For example, if it describes a name with 'RobotControl' in `<plugin>` attributesm, SDF Parser will start to find a filename in plugin element as 'RobotControl' in Unity project.

```
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
      <base>449</base>
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
