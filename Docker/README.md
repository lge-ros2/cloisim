# Running CLOiSim in Docker

This folder contains Dockerfile and instructions to run pre-built CLOiSim in container.
Dockerfile creates minimal image with Vulkan capabilities, and downloads latest release version of CLOiSim.

## Prerequisite

Make sure you if NVIDIA Container Toolkit and Docker are already installed on your machine.

### docker

Please refer to [here](https://docs.docker.com/engine/install/ubuntu/#install-using-the-repository) for latest guideline

```shell
$ sudo apt-get update

$ sudo apt-get install \
    apt-transport-https \
    ca-certificates \
    curl \
    gnupg-agent \
    software-properties-common

$ curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo apt-key add -

$ sudo add-apt-repository \
   "deb [arch=amd64] https://download.docker.com/linux/ubuntu \
   $(lsb_release -cs) \
   stable"

$ sudo apt-get update
$ sudo apt-get install docker-ce docker-ce-cli containerd.io
```

### nvidia-docker

Refer here for install guide [nvidia-docker2](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/install-guide.html#docker).
It may differ from latest guide.

```shell
$ distribution=$(. /etc/os-release;echo $ID$VERSION_ID) \
   && curl -s -L https://nvidia.github.io/nvidia-docker/gpgkey | sudo apt-key add - \
   && curl -s -L https://nvidia.github.io/nvidia-docker/$distribution/nvidia-docker.list | sudo tee /etc/apt/sources.list.d/nvidia-docker.list

$ sudo apt-get update
$ sudo apt-get install -y nvidia-docker2
$ sudo systemctl restart docker

### test if it installed successfully
$ sudo docker run --rm --gpus all nvidia/cuda:11.0-base nvidia-smi
```

## Run Docker

### build docker image

Run following command to build a image:

```shell
docker build -t cloisim .
```

### run docker container

You can change paths for resources in docker run command.

For example,

change to

```shell
-v ${CLOISIM_RESOURCES_PATH}/assets/models:/opt/resources/models/
```

instead of

```shell
-v ${CLOISIM_RESOURCES_PATH}/models:/opt/resources/models/
```

#### Option A

Use following command to run CLOiSim docker container:

```shell
export CLOISIM_RESOURCES_PATH=/home/closim/SimulatorInstance/sample_resources/

docker run -ti --rm --gpus all --net=host \
    -e DISPLAY \
    -v /tmp/.Xauthority:/tmp/.Xauthority \
    -v /tmp/cloisim/unity3d:/root/.config/unity3d \
    -v /tmp/.X11-unix:/tmp/.X11-unix  \
    -v /usr/share/fonts/:/usr/share/fonts/ \
    -v ${CLOISIM_RESOURCES_PATH}/media:/opt/resources/media/ \
    -v ${CLOISIM_RESOURCES_PATH}/models:/opt/resources/models/ \
    -v ${CLOISIM_RESOURCES_PATH}/worlds:/opt/resources/worlds/ \
    cloisim lg_seocho.world
```

#### Option B

Option B: just run with target world file name in 'worlds'

```shell
export CLOISIM_RESOURCES_PATH=/home/closim/SimulatorInstance/sample_resources/
./start.sh lg_seocho.world
```

refer to [samples_resource](https://github.com/lge-ros2/sample_resources) more details about resource

-------------------------------

This docker image has been tested on __Ubuntu 20.04__.
