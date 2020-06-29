#!/bin/bash
TARGET_PATH=$1

if [ -z ${TARGET_PATH} ]; then
  TARGET_PATH="./ProtobufMessages"
fi

## 1. check and edit here
##    set the location of protogen in absolute path
PROTOGEN="../../../../protobuf-net/src/protogen/bin/Release/netcoreapp3.0/protogen"

## 2. check and edit here
##    set the location of protobuf messages in absolute path
PROTO_MSGS_PATH="../../../../sim-device/driver_sim/msgs/"

command="$PROTOGEN -I$PROTO_MSGS_PATH --csharp_out=$TARGET_PATH *.proto"
echo $command
eval $command