#!/bin/bash
echo "Removing old build..."
rm -rf /home/nav/workspace/cloisim/build/linux_x86_64

UNITY_PATH="/home/nav/Unity/Hub/Editor/6000.3.9f1/Editor/Unity"
PROJECT_PATH="/home/nav/workspace/cloisim/cloisim"
LOG_FILE="/home/nav/workspace/cloisim/performance_build_gui.log"

echo "Building GUI App..."
DISPLAY=:1 $UNITY_PATH -projectPath "$PROJECT_PATH" -batchmode -force-vulkan -executeMethod GUIBuilder.Build -quit -logFile "$LOG_FILE"
echo "Build finished. Check $LOG_FILE for details."
