#!/bin/bash
rm -f /home/nav/workspace/cloisim/performance_build.log
rm -f /home/nav/workspace/cloisim/bench_v2.log

source /opt/ros/jazzy/setup.bash

echo "Starting CLOiSim Built Artifact via DISPLAY=:1..."
DISPLAY=:1 ./test_performance_build.sh metahome_benchmark.world -force-vulkan > /home/nav/workspace/cloisim/sim_v2.log 2>&1 &
SIM_PID=$!

echo "Starting Python Benchmark script (will block until first msg received)..."
python3 -u /home/nav/workspace/cloisim/benchmark_v2.py > /home/nav/workspace/cloisim/bench_v2.log 2>&1

echo "Benchmark finished. Cleaning up simulator PIDs..."
kill -9 $SIM_PID
pkill -f CLOiSim
echo "Done!"
