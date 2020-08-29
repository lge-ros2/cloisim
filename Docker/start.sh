#!/bin/sh

docker run -ti --rm --gpus all --net=host \
     -e DISPLAY \
     -v ${XAUTHORITY}:/tmp/.Xauthority \
     -e XAUTHORITY=/tmp/.Xauthority \
     -v /tmp/cloisim/unity3d:/root/.config/unity3d \
     -v /tmp/.X11-unix:/tmp/.X11-unix  \
     -v ${CLOISIM_RESOURCES_PATH}/materials:/opt/resources/materials/ \
     -v ${CLOISIM_RESOURCES_PATH}/models:/opt/resources/models/ \
     -v ${CLOISIM_RESOURCES_PATH}/worlds:/opt/resources/worlds/ \
     cloisim $1