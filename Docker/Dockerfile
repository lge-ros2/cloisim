FROM ubuntu:20.04

# Configuration for Nvidia device capability
ENV DEBIAN_FRONTEND=noninteractive \
    NVIDIA_VISIBLE_DEVICES=all \
    NVIDIA_DRIVER_CAPABILITIES=all

ENV XDG_RUNTIME_DIR=/tmp

# Prerequisite for Vulkan capability in Docker Container
ADD https://gitlab.com/nvidia/container-images/vulkan/-/raw/ubuntu18.04/nvidia_icd.json /etc/vulkan/icd.d/nvidia_icd.json

RUN \
apt-get update -qq && \
apt-get upgrade -qq -y && \
apt-get install --no-install-recommends -qq -y \
 sudo libgomp1 libxi6 \
 libglu1 libxcursor1 xvfb \
 xz-utils wget curl ca-certificates \
 libgl1 libvulkan1 libc6-dev libxcursor1 libminizip-dev && \
rm -rf /var/lib/apt/lists/* && \
apt-get clean

# Get version info and extract simulator and remove downloaded file
RUN \
export CLOISIM_VERSION=$(curl -Ls -o /dev/null -w %{url_effective} --url https://github.com/lge-ros2/cloisim/releases/latest | cut -d'/' -f8) && \
export CLOISIM_TARGETFILE=CLOiSim-linux-${CLOISIM_VERSION} && \
wget -q --directory-prefix /tmp "https://github.com/lge-ros2/cloisim/releases/download/${CLOISIM_VERSION}/${CLOISIM_TARGETFILE}.tar.xz" && \
tar -xf /tmp/${CLOISIM_TARGETFILE}.tar.xz -C /tmp && \
rm /tmp/${CLOISIM_TARGETFILE}.tar.xz && \
mv /tmp/${CLOISIM_TARGETFILE} /opt/ && \
ln -s /opt/${CLOISIM_TARGETFILE} /opt/CLOiSim

ENV CLOISIM_FILES_PATH=/opt/resources/media \
    CLOISIM_MODEL_PATH=/opt/resources/models \
    CLOISIM_WORLD_PATH=/opt/resources/worlds

# final simulatior location
WORKDIR /opt/CLOiSim

ENTRYPOINT ["./run.sh"]
