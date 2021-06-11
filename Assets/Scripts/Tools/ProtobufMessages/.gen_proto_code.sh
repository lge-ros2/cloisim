#!/bin/bash

##
## For more details, go to here(https://github.com/lge-ros2/cloisim/wiki/(New)-Generate-Sensor-Message-Interface)
##
TARGET_PATH=$1

if [ -z ${TARGET_PATH} ]; then
  TARGET_PATH="./"
fi

## 1. check and edit here
##    set the location of protogen in absolute path
PROTOGEN="protogen"

## 2. check and edit here
##    set the location of protobuf messages in absolute path
# PROTO_MSGS_PATH="../../../../../cloi3_ws/src/cloisim_ros/cloisim_ros_protobuf_msgs/msgs/"
PROTO_MSGS_PATH="../../../../../../../cloi3/src/cloisim_ros/cloisim_ros_protobuf_msgs/msgs/"

## 3. target protobuf message
##
# TARGET_PROTO_MSGS="*.proto"
TARGET_PROTO_MSGS=""

MSG="header any param param_v color empty "
MSG+="time vector2d vector3d quaternion pose pose_v poses_stamped "
MSG+="image images_stamped image_stamped camerasensor distortion camera_lens "
MSG+="laserscan laserscan_stamped raysensor pointcloud "
MSG+="micom battery twist "
MSG+="contact contacts contactsensor "
MSG+="gps gps_sensor "
MSG+="imu imu_sensor "
MSG+="sonar sonar_stamped "
MSG+="sensor_noise "
MSG+="world_stats log_playback_stats "
MSG+="request response "
MSG+="perception perception_v "
MSG+="pid joint_cmd wrench joint_wrench joint_state joint_state_v "
# MSG+=" "

for i in $MSG
do
  TARGET_PROTO_MSGS+="$i.proto "
done
# echo $TARGET_PROTO_MSGS


command="$PROTOGEN -I$PROTO_MSGS_PATH --csharp_out=$TARGET_PATH $TARGET_PROTO_MSGS"
#echo $command
eval $command