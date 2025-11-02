#!/usr/bin/env bash
set -euo pipefail

UNITY_PATH="${UNITY_PATH:-/Applications/Unity/Hub/Editor/2022.3.14f1/Unity.app/Contents/MacOS/Unity}"
PROJECT_PATH="${PROJECT_PATH:-$(pwd)}"
RESULTS_DIR="${RESULTS_DIR:-$PROJECT_PATH/CI/TestResults}"

mkdir -p "$RESULTS_DIR"

"$UNITY_PATH" \
  -batchmode \
  -projectPath "$PROJECT_PATH" \
  -runTests \
  -testPlatform playmode \
  -testResults "$RESULTS_DIR/playmode-results.xml" \
  -logFile "$RESULTS_DIR/playmode.log" \
  -quit
