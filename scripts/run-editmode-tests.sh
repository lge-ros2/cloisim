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

mkdir -p "${TEST_RESULTS_DIR}"
rm -f "${RESULTS_FILE}"

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