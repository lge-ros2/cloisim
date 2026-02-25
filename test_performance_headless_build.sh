#!/bin/bash
# Script to launch CLOiSim Headless from the dedicated server build for performance testing

# Source environment variables
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
cd "$SCRIPT_DIR"

if [ -f "../setup.bash" ]; then
    source ../setup.bash
else
    echo "Warning: setup.bash not found in parent directory."
fi

# Define path to headless player build output
SERVER_BUILD_DIR="/home/nav/workspace/cloisim/build/linux_headless"
BINARY="CLOiSim.x86_64"
LOG_FILE="/home/nav/workspace/cloisim/performance_headless_build.log"
DEFAULT_WORLD="metahome.world"

# Use the world provided as argument, or fallback to default
WORLD=${1:-$DEFAULT_WORLD}

if [ ! -f "$SERVER_BUILD_DIR/$BINARY" ]; then
    echo "Error: Headless build not found at $SERVER_BUILD_DIR/$BINARY"
    echo "Build with: Unity -executeMethod HeadlessBuilder.Build"
    exit 1
fi

echo "------------------------------------------------"
echo "CLOiSim Headless Performance Test (Player Build)"
echo "------------------------------------------------"
echo "Binary: $SERVER_BUILD_DIR/$BINARY"
echo "World:  $WORLD"
echo "Log:    $LOG_FILE"
echo "------------------------------------------------"

# Ensure symbolic link for Assimp exists (hotfix for hardcoded paths)
if [ ! -L "$SERVER_BUILD_DIR/CLOiSim_Data" ] && [ -d "$SERVER_BUILD_DIR/CLOiSim_Data" ]; then
    echo "CLOiSim_Data directory found"
fi

# Change to server build directory
cd "$SERVER_BUILD_DIR"

# Launch the headless player build with batchmode + Vulkan (no Xvfb needed)
./$BINARY -batchmode -force-vulkan -worldFile "$WORLD" -logFile "$LOG_FILE" "${@:2}"
