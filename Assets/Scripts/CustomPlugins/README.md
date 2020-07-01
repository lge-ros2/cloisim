# CustomPlugins

These plugin scripts are utilized for sensor connection.

For example, if you define a `<plugin>` element with name 'RobotControl' attribute in SDF,
C# SDF Parser shall start to find a plugin from filename 'libRobotControl.so'.

libRobotControl.so -> RobotControl


Matching Each class name and filename is IMPORTANT to load plugin in SDF.

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
