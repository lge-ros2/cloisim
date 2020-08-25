#!/bin/bash
TARGET_PATH=$1

if [ -z ${TARGET_PATH} ]; then
  TARGET_PATH="./ProtobufMessages"
fi

## 1. check and edit here
##    set the location of protogen in absolute path
PROTOGEN="protogen"

## 2. check and edit here
##    set the location of protobuf messages in absolute path
PROTO_MSGS_PATH="../../../../sim-device/driver_sim/msgs/"


## 3. target protobuf message
##
# TARGET_PROTO_MSGS="*.proto"
TARGET_PROTO_MSGS=""

MSG="header any param param_v color "
MSG+="time vector2d vector3d quaternion pose pose_v poses_stamped "
MSG+="image images_stamped image_stamped camerasensor distortion camera_lens "
MSG+="laserscan laserscan_stamped raysensor "
MSG+="pointcloud "
MSG+="micom battery "
MSG+="contact contacts contactsensor wrench wrench_stamped joint_wrench "
MSG+="gps gps_sensor "
MSG+="imu imu_sensor "
MSG+="sonar sonar_stamped "
MSG+="sensor_noise "
# MSG+=" "

for i in $MSG
do
  TARGET_PROTO_MSGS+="$i.proto "
done
# echo $TARGET_PROTO_MSGS


command="$PROTOGEN -I$PROTO_MSGS_PATH --csharp_out=$TARGET_PATH $TARGET_PROTO_MSGS"
#echo $command
eval $command