#!/bin/bash
# Script to launch CLOiSim from the compiled build artifact for performance testing

# Source environment variables
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
cd "$SCRIPT_DIR"

if [ -f "../setup.bash" ]; then
    source ../setup.bash
else
    echo "Warning: setup.bash not found in parent directory."
fi

# Source ROS 2 environment so native plugin can find RMW/DDS libs
if [ -f "/opt/ros/jazzy/setup.bash" ]; then
    source /opt/ros/jazzy/setup.bash
else
    echo "Warning: /opt/ros/jazzy/setup.bash not found. Native ROS 2 plugin may not work."
fi

# Define path to build output
BUILD_DIR="/home/nav/workspace/cloisim/build/linux_x86_64"
BINARY="CLOiSim.x86_64"
LOG_FILE="/home/nav/workspace/cloisim/performance_build.log"
DEFAULT_WORLD="metahome.world"

# Use the world provided as argument, or fallback to default
WORLD=${1:-$DEFAULT_WORLD}

if [ ! -f "$BUILD_DIR/$BINARY" ]; then
    echo "Error: Binary not found at $BUILD_DIR/$BINARY"
    echo "Please ensure you have built the Standalone Linux Player."
    exit 1
fi

echo "------------------------------------------------"
echo "CLOiSim Build Performance Test Launcher (Artifact)"
echo "------------------------------------------------"
echo "Binary: $BUILD_DIR/$BINARY"
echo "World:  $WORLD"
echo "Log:    $LOG_FILE"
echo "------------------------------------------------"

# Change to build directory to ensure native plugins and data load correctly
cd "$BUILD_DIR"

# Launch the compiled CLOiSim binary
./$BINARY -worldFile "$WORLD" -logFile "$LOG_FILE" "${@:2}"
