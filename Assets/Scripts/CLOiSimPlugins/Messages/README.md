# CLOiSimPlugins Messages

## Generate Sensor Message Interface (Protobuf 3)

Generate C# protobuf message classes from `.proto` definitions using `protogen`.

If you want custom proto messages, follow all steps below.

### Prerequisites

Install the .NET SDK (6.0 or later):

```bash
sudo apt-get update && sudo apt-get install -y dotnet-sdk-6.0
```

If it fails to install, follow the [official .NET install guide for Ubuntu](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu).

You can also install manually:

```bash
wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb

sudo apt-get update
sudo apt-get install -y apt-transport-https
sudo apt-get install -y dotnet-sdk-8.0
```

### Install Protogen

This tool requires .NET 6.0.

```bash
dotnet tool install --global protobuf-net.Protogen --version 3.2.52
```

Verify the installation:

```bash
$ protogen
Missing input file.
```

If `protogen` command is not found, check the path: `~/.dotnet/tools/protogen`

### Generate Messages

#### Manual Example

```bash
protogen *.proto --proto_path=<path_to_proto_msgs> --csharp_out=./
```

#### Using the Script

1. Open `.gen_proto_code.sh`
2. Set `PROTO_MSGS_PATH` to the absolute or relative path of `cloisim_ros_protobuf_msgs/msgs/`
3. Run:

```bash
cd Assets/Scripts/CLOiSimPlugins/Messages
bash .gen_proto_code.sh
```

##### Script Reference

```bash
#!/bin/bash

TARGET_PATH=$1

if [ -z ${TARGET_PATH} ]; then
  TARGET_PATH="./"
fi

## 1. check and edit here
##    set the location of protogen in absolute path
PROTOGEN="protogen"

## 2. check and edit here
##    set the location of protobuf messages in absolute path
PROTO_MSGS_PATH="../../../../../../cloi_ws/src/simulator/cloisim_ros/cloisim_ros_protobuf_msgs/msgs/"

## 3. target protobuf message
TARGET_PROTO_MSGS=""

MSG="header any param param_v color empty "
MSG+="time vector2d vector3d quaternion pose pose_v twist "
MSG+="image images camerasensor distortion double float_v camera_lens lens sensor_noise "
MSG+="laserscan raysensor pointcloud "
MSG+="segmentation vision_class "
MSG+="micom battery "
MSG+="contact contacts contactsensor entity "
MSG+="gps gps_sensor "
MSG+="imu imu_sensor "
MSG+="sonar "
MSG+="world_stats log_playback_stats "
MSG+="request response "
MSG+="perception perception_v "
MSG+="pid wrench joint_wrench "
MSG+="joystick "
MSG+="joint_state joint_state_v "
MSG+="joint_cmd joint_cmd_v "

for i in $MSG
do
  TARGET_PROTO_MSGS+="$i.proto "
done

command="$PROTOGEN -I$PROTO_MSGS_PATH --csharp_out=$TARGET_PATH $TARGET_PROTO_MSGS"
eval $command
```

This generates C# classes for all required proto3 messages into the current directory.