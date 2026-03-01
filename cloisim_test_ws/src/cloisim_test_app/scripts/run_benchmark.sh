#!/usr/bin/env bash
# ============================================================================
#  CLOiSim Communication Benchmark — Automated Runner
#
#  Runs the benchmark for both ZMQ legacy and ROS native modes sequentially,
#  then produces a comparison report.
#
#  Prerequisites:
#    - CLOiSim simulator binary available
#    - cloisim_ros workspace built (for ZMQ mode)
#    - cloisim_test_app package built
#
#  Usage:
#    ./run_benchmark.sh                          # defaults
#    ./run_benchmark.sh --duration 60 --warmup 10
#    ./run_benchmark.sh --mode zmq               # run only ZMQ mode
#    ./run_benchmark.sh --mode native             # run only native mode
#    ./run_benchmark.sh --mode both               # run both and compare
# ============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKSPACE_ROOT="${SCRIPT_DIR}/../../../.."  # cloisim root
RESULTS_DIR="${WORKSPACE_ROOT}/benchmark_results"

# Defaults
DURATION=30
WARMUP=5
ROBOT_NAME="BenchmarkBot"
MODE="both"                 # zmq | native | both
WORLD="metahome_benchmark.world"

# ---- Parse arguments ----
while [[ $# -gt 0 ]]; do
    case "$1" in
        --duration)  DURATION="$2";    shift 2 ;;
        --warmup)    WARMUP="$2";      shift 2 ;;
        --robot)     ROBOT_NAME="$2";  shift 2 ;;
        --mode)      MODE="$2";        shift 2 ;;
        --world)     WORLD="$2";       shift 2 ;;
        --help|-h)
            echo "Usage: $0 [--duration N] [--warmup N] [--robot NAME] [--mode zmq|native|both] [--world FILE]"
            exit 0 ;;
        *)
            echo "Unknown argument: $1" >&2; exit 1 ;;
    esac
done

mkdir -p "$RESULTS_DIR"

echo "============================================================"
echo "  CLOiSim Communication Benchmark"
echo "  Mode      : ${MODE}"
echo "  Robot     : ${ROBOT_NAME}"
echo "  World     : ${WORLD}"
echo "  Duration  : ${DURATION}s  (warmup: ${WARMUP}s)"
echo "  Results   : ${RESULTS_DIR}"
echo "============================================================"

# ---- Helper: source ROS2 underlay + overlays ----
source_ros() {
    # shellcheck disable=SC1091
    source /opt/ros/${ROS_DISTRO:-humble}/setup.bash 2>/dev/null || true
    if [[ -f "${WORKSPACE_ROOT}/cloisim_ws/install/setup.bash" ]]; then
        # shellcheck disable=SC1091
        source "${WORKSPACE_ROOT}/cloisim_ws/install/setup.bash"
    fi
    if [[ -f "${WORKSPACE_ROOT}/cloisim/cloisim_test_ws/install/setup.bash" ]]; then
        # shellcheck disable=SC1091
        source "${WORKSPACE_ROOT}/cloisim/cloisim_test_ws/install/setup.bash"
    fi
    if [[ -f "${WORKSPACE_ROOT}/setup.bash" ]]; then
        # shellcheck disable=SC1091
        source "${WORKSPACE_ROOT}/setup.bash"
    fi
}

# ---- Run benchmark for a given mode ----
run_benchmark() {
    local mode="$1"
    echo ""
    echo "------------------------------------------------------------"
    echo "  Running benchmark: mode=${mode}"
    echo "------------------------------------------------------------"

    source_ros

    ros2 run cloisim_test_app comm_benchmark \
        --ros-args \
        -p mode:="${mode}" \
        -p robot_name:="${ROBOT_NAME}" \
        -p duration:="${DURATION}" \
        -p warmup:="${WARMUP}" \
        -p output_dir:="${RESULTS_DIR}"

    echo "  [${mode}] benchmark complete."
}

# ---- Execute ----
case "${MODE}" in
    zmq)
        run_benchmark zmq
        ;;
    native)
        run_benchmark native
        ;;
    both)
        echo ""
        echo ">>> STEP 1/3: Benchmark ZMQ Legacy mode"
        echo ">>> Make sure CLOiSim + cloisim_ros bridge are running."
        read -rp "Press ENTER when ready (or Ctrl-C to abort)..."
        run_benchmark zmq

        echo ""
        echo ">>> STEP 2/3: Benchmark ROS Native mode"
        echo ">>> Stop cloisim_ros bridge. Restart CLOiSim with native ROS2 plugins."
        read -rp "Press ENTER when ready (or Ctrl-C to abort)..."
        run_benchmark native

        echo ""
        echo ">>> STEP 3/3: Generating comparison report"
        ros2 run cloisim_test_app benchmark_compare \
            --results-dir "${RESULTS_DIR}"
        ;;
    *)
        echo "ERROR: Unknown mode '${MODE}'. Use: zmq, native, or both" >&2
        exit 1
        ;;
esac

echo ""
echo "Done! Results in: ${RESULTS_DIR}/"
