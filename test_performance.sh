#!/bin/bash

export CLOISIM_ROOT=/home/nav/workspace/cloisim/cloisim
export CLOISIM_RESOURCES_PATH=${CLOISIM_ROOT}/sample_resources
export CLOISIM_RESOURCES_ROOT_PATH=${CLOISIM_ROOT}
export CLOISIM_MODEL_PATH=${CLOISIM_ROOT}/cloi_resources:${CLOISIM_ROOT}/world_resources/smallhouse/models:${CLOISIM_ROOT}/gazebo_models:${CLOISIM_ROOT}/world_resources/models
export CLOISIM_FILES_PATH=${CLOISIM_ROOT}/world_resources/smallhouse:${CLOISIM_ROOT}/world_resources/meta-home
export CLOISIM_WORLD_PATH=${CLOISIM_ROOT}/world_resources/smallhouse/worlds:${CLOISIM_ROOT}/world_resources/meta-home/worlds

echo "Running Cloisim headless to survey 60fps & 1kHz physics..."

# Run unity in batchmode headless to execute small_house_with_r7.world or at least the default empty scene
/home/nav/Unity/Hub/Editor/6000.3.9f1/Editor/Unity -batchmode -disable-audio -projectPath /home/nav/workspace/cloisim/cloisim -executeMethod UnityEditor.SyncVS.SyncSolution -quit -logFile /home/nav/workspace/cloisim/cloisim/Logs/test_compile.log
echo "Project setup and compilation finish."

# Timeout after 20 seconds, allowing for a few seconds of log gathering
timeout 20s /home/nav/Unity/Hub/Editor/6000.3.9f1/Editor/Unity -batchmode -disable-audio -projectPath /home/nav/workspace/cloisim/cloisim -executeMethod PerfTestRunner.StartTest -world metahome.world -logFile /home/nav/workspace/cloisim/cloisim/Logs/test_run.log

echo "Survey log results:"
grep "\[PerformanceSurvey\]" /home/nav/workspace/cloisim/cloisim/Logs/test_run.log
