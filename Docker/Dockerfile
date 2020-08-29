FROM ubuntu:18.04

# configuration for Nvidia capability
ENV DEBIAN_FRONTEND=noninteractive
ENV NVIDIA_VISIBLE_DEVICES=all
ENV NVIDIA_DRIVER_CAPABILITIES=graphics,display

# Prerequisite for Vulkan capability
ADD https://gitlab.com/nvidia/container-images/vulkan/raw/master/nvidia_icd.json /etc/vulkan/icd.d/nvidia_icd.json

RUN apt-get update -qq
RUN apt-get upgrade -qq -y
RUN apt-get install --no-install-recommends -qq -y \
    xz-utils wget curl ca-certificates libgl1 libvulkan1

RUN apt-get clean

# Get version info and extract simulator and remove downloaded file
RUN export CLOISIM_VERSION=$(curl -Ls -o /dev/null -w %{url_effective} --url https://github.com/lge-ros2/cloisim/releases/latest | cut -d'/' -f8) && \
    export CLOISIM_TARGETFILE=CLOiSim-${CLOISIM_VERSION} && \
    wget -q --directory-prefix /tmp "https://github.com/lge-ros2/cloisim/releases/download/${CLOISIM_VERSION}/${CLOISIM_TARGETFILE}.tar.xz" && \
    tar -xf /tmp/${CLOISIM_TARGETFILE}.tar.xz -C /tmp && rm /tmp/${CLOISIM_TARGETFILE}.tar.xz && \
    mv /tmp/${CLOISIM_TARGETFILE} /opt/CLOiSim

ENV CLOISIM_FILES_PATH=/opt/resources/materials
ENV CLOISIM_MODEL_PATH=/opt/resources/models
ENV CLOISIM_WORLD_PATH=/opt/resources/worlds

# final simulatior location
WORKDIR /opt/CLOiSim

ENTRYPOINT ["./run.sh"]