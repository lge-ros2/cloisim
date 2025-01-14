# Running CLOiSim in Docker

This folder contains Dockerfile and instructions to run pre-built CLOiSim in container.
Dockerfile creates minimal image with Vulkan capabilities, and downloads latest release version of CLOiSim.

## Prerequisite

Make sure you if NVIDIA Container Toolkit and Docker are already installed on your machine.

Installation docker using snap does not recommended.

### docker

Please refer to [here](https://docs.docker.com/engine/install/ubuntu/#install-using-the-repository) for latest guideline

### nvidia-docker

Refer here for install guide [NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html).

```shell
### test if it installed successfully
$ sudo docker run --rm --gpus all nvidia/cuda:11.0.3-base-ubuntu20.04 nvidia-smi
```

## Run Docker

### build docker image

Run following command to build a image:

```shell
docker build -t cloisim .
```

### run docker container

You can change paths for resources in docker run command.

refer to [samples_resource](https://github.com/lge-ros2/sample_resources) more details about resource

For example,

```shell
# change to
-v ${CLOISIM_RESOURCES_PATH}/assets/models:/opt/resources/models/

# instead of
-v ${CLOISIM_RESOURCES_PATH}/models:/opt/resources/models/
```

Default resource paths in container is like below.

```dockerfile
ENV CLOISIM_FILES_PATH=/opt/resources/media \
    CLOISIM_MODEL_PATH=/opt/resources/models \
    CLOISIM_WORLD_PATH=/opt/resources/worlds
```

It is possible to select GPU device through `--gpus '"device=0"'` instead of `--gpus all`.

for example,

```shell
docker run -it --rm --net=host --gpus '"device=0"'  ...
```

#### Option A (non-headless)

Use following command to run CLOiSim docker container:

```shell
export CLOISIM_RESOURCES_PATH=/home/closim/SimulatorInstance/sample_resources/

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
    cloisim lg_seocho.world
```

You can input other style of optional arguments.

```shell
docker run -ti --rm --gpus all --net=host
    ...
    cloisim --world lg_seocho.world
```

#### Option B (headless)

```shell
docker run -it --rm --net=host --gpus '"device=0"' \
    -v /tmp/cloisim/unity3d:/root/.config/unity3d \
    -v /usr/share/fonts/:/usr/share/fonts/ \
    -v ${CLOISIM_RESOURCES_PATH}/materials:/opt/resources/materials/ \
    -v ${CLOISIM_RESOURCES_PATH}/models:/opt/resources/models/ \
    -v ${CLOISIM_RESOURCES_PATH}/worlds:/opt/resources/worlds/ \
    cloisim --headless --world lg_seocho.world
```

#### Option C (Predefined script)

just run with target world file name in 'worlds'

```shell
export CLOISIM_RESOURCES_PATH=/home/closim/SimulatorInstance/sample_resources/
./start.sh lg_seocho.world
./start.sh --world lg_seocho.world

## headless mode
./start.sh --headless --world lg_seocho.world
./start-headless.sh --headless --world lg_seocho.world
```

-------------------------------

This docker image has been tested on __Ubuntu 20.04__.
