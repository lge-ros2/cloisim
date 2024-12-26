#!/bin/sh

xhost +

docker run -it --rm --net=host --gpus '"device=0"' \
    -e DISPLAY=$DISPLAY \
    -v ${HOME}/.Xauthority:/root/.Xauthority:rw \
    -v /tmp/cloisim/unity3d:/root/.config/unity3d \
    -v /tmp/.X11-unix:/tmp/.X11-unix \
    -v /usr/share/fonts/:/usr/share/fonts/ \
    -v ${CLOISIM_RESOURCES_PATH}/materials:/opt/resources/materials/ \
    -v ${CLOISIM_RESOURCES_PATH}/models:/opt/resources/models/ \
    -v ${CLOISIM_RESOURCES_PATH}/worlds:/opt/resources/worlds/ \
    cloisim $@
