#!/bin/bash
# Script to launch CLOiSim from source code (Unity Editor) for performance testing

# Source environment variables
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
cd "$SCRIPT_DIR"

if [ -f "../setup.bash" ]; then
    source ../setup.bash
else
    echo "Warning: setup.bash not found in parent directory."
fi

UNITY_PATH="/home/nav/Unity/Hub/Editor/6000.3.9f1/Editor/Unity"
PROJECT_PATH="/home/nav/workspace/cloisim/cloisim"
LOG_FILE="/home/nav/workspace/cloisim/performance_codes.log"
DEFAULT_WORLD="metahome.world"

# Use the world provided as argument, or fallback to default
WORLD=${1:-$DEFAULT_WORLD}

echo "------------------------------------------------"
echo "CLOiSim Code Performance Test Launcher (Unity Editor)"
echo "------------------------------------------------"
echo "Project: $PROJECT_PATH"
echo "World:   $WORLD"
echo "Log:     $LOG_FILE"
echo "------------------------------------------------"

# Launch Unity Editor to test performance of the current code
$UNITY_PATH -projectPath "$PROJECT_PATH" -worldFile "$WORLD" -logFile "$LOG_FILE" "${@:2}"
