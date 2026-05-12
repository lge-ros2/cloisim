#!/bin/bash
# Copyright (c) 2026 LG Electronics Inc.
#
# SPDX-License-Identifier: MIT

set -euo pipefail

## 1. resolve paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "${PROJECT_ROOT}/ProjectSettings/ProjectVersion.txt")"

DEFAULT_UNITY_EDITOR="${HOME}/Unity/Hub/Editor/${UNITY_VERSION}/Editor/Unity"
UNITY_EDITOR="${UNITY_EDITOR:-${DEFAULT_UNITY_EDITOR}}"

if [[ ! -x "${UNITY_EDITOR}" ]]; then
  UNITY_EDITOR="$(command -v unity-editor || command -v unity || true)"
fi

if [[ -z "${UNITY_EDITOR}" || ! -x "${UNITY_EDITOR}" ]]; then
  echo "Unable to locate a Unity Editor executable." >&2
  echo "Set UNITY_EDITOR or install Unity Hub editor ${UNITY_VERSION}." >&2
  exit 1
fi

## 2. configure outputs
TEST_RESULTS_DIR="${PROJECT_ROOT}/Logs/TestResults"
RESULTS_FILE="${TEST_RESULTS_DIR}/editmode-results.xml"
LOG_FILE="${PROJECT_ROOT}/Logs/EditModeTests.log"
TEST_FILTER="${1:-${TEST_FILTER:-}}"
TEST_PROGRESS="${TEST_PROGRESS:-1}"
PROGRESS_DONE_FILE="${TEST_RESULTS_DIR}/.editmode-progress.done.$$"
PROGRESS_PID=""

mkdir -p "${TEST_RESULTS_DIR}"
rm -f "${RESULTS_FILE}"
rm -f "${LOG_FILE}"
rm -f "${PROGRESS_DONE_FILE}"

cleanup_progress_reporter() {
  touch "${PROGRESS_DONE_FILE}" 2>/dev/null || true

  if [[ -n "${PROGRESS_PID}" ]]; then
  wait "${PROGRESS_PID}" 2>/dev/null || true
  fi

  rm -f "${PROGRESS_DONE_FILE}"
}

start_progress_reporter() {
  if [[ "${TEST_PROGRESS}" == "0" ]]; then
  return
  fi

  python3 -u - "${LOG_FILE}" "${PROGRESS_DONE_FILE}" <<'PY' &
import json
import os
import sys
import time

log_file = sys.argv[1]
done_file = sys.argv[2]

state_names = {
  1: "INCONCLUSIVE",
  2: "SKIPPED",
  3: "SKIPPED",
  4: "PASS",
  5: "FAIL",
}

total = None
completed = 0


def stamp() -> str:
  return time.strftime("%H:%M:%S")


while not os.path.exists(log_file):
  if os.path.exists(done_file):
    sys.exit(0)
  time.sleep(0.1)

with open(log_file, "r", encoding="utf-8", errors="replace") as handle:
  while True:
    line = handle.readline()
    if line:
      if not line.startswith("##utp:"):
        continue

      payload = line[len("##utp:"):].strip()
      try:
        event = json.loads(payload)
      except json.JSONDecodeError:
        continue

      if event.get("type") == "TestPlan":
        tests = event.get("tests") or []
        total = len(tests)
        if total > 0:
          print(f"[{stamp()}] Tracking {total} EditMode tests", flush=True)
        continue

      if event.get("type") != "TestStatus" or event.get("phase") != "End":
        continue

      completed += 1
      name = event.get("name", "<unknown>").replace("CLOiSim.Tests.EditMode.", "")
      state = event.get("state")
      status = state_names.get(state, f"STATE-{state}")
      duration_ms = event.get("duration")
      message = (event.get("message") or "").strip().splitlines()
      progress = f"{completed}/{total}" if total else str(completed)
      duration_suffix = f" ({duration_ms} ms)" if duration_ms is not None else ""

      if message and status != "PASS":
        print(
          f"[{stamp()}] [{progress}] {status} {name}{duration_suffix} - {message[0]}",
          flush=True,
        )
      else:
        print(f"[{stamp()}] [{progress}] {status} {name}{duration_suffix}", flush=True)
      continue

    if os.path.exists(done_file):
      break

    time.sleep(0.1)
PY

  PROGRESS_PID=$!
}

trap cleanup_progress_reporter EXIT

## 3. build command
UNITY_COMMAND=(
  "${UNITY_EDITOR}"
  -batchmode
  -projectPath "${PROJECT_ROOT}"
  -runTests
  -automated
  -testPlatform editmode
  -testResults "${RESULTS_FILE}"
  -logFile "${LOG_FILE}"
)

if [[ -n "${TEST_FILTER}" ]]; then
  UNITY_COMMAND+=( -testFilter "${TEST_FILTER}" )
fi

echo "Running EditMode tests"
echo "Unity: ${UNITY_EDITOR}"
echo "Results: ${RESULTS_FILE}"
echo "Log: ${LOG_FILE}"

if [[ -n "${TEST_FILTER}" ]]; then
  echo "Filter: ${TEST_FILTER}"
fi

start_progress_reporter

if "${UNITY_COMMAND[@]}"; then
  UNITY_EXIT_CODE=0
else
  UNITY_EXIT_CODE=$?
fi

if [[ ${UNITY_EXIT_CODE} -ne 0 ]]; then
  if [[ -f "${LOG_FILE}" ]] && grep -q "another Unity instance is running with this project open" "${LOG_FILE}"; then
    echo "Unity test execution failed because the project is already open in another Unity instance." >&2
    echo "Close the other Unity editor or background import process, then rerun this script." >&2
  elif [[ -f "${LOG_FILE}" ]] && grep -q "Scripts have compiler errors" "${LOG_FILE}"; then
    echo "Unity test execution failed because the project has script compilation errors." >&2
    echo "Check ${LOG_FILE} for the exact compiler diagnostics, then rerun this script." >&2
  else
    echo "Unity test execution failed. Check ${LOG_FILE} for details." >&2
  fi

  exit "${UNITY_EXIT_CODE}"
fi

echo "EditMode test run completed."

if [[ -f "${RESULTS_FILE}" ]]; then
  python3 - "${RESULTS_FILE}" <<'PY'
import sys
import xml.etree.ElementTree as ET

results_file = sys.argv[1]
root = ET.parse(results_file).getroot()

total = root.attrib.get("total", "0")
passed = root.attrib.get("passed", "0")
failed = root.attrib.get("failed", "0")
skipped = root.attrib.get("skipped", "0")
inconclusive = root.attrib.get("inconclusive", "0")
result = root.attrib.get("result", "Unknown")

print(
  f"Summary: result={result}, total={total}, passed={passed}, failed={failed}, skipped={skipped}, inconclusive={inconclusive}"
)
PY
else
  echo "No test results file was generated: ${RESULTS_FILE}" >&2
fi