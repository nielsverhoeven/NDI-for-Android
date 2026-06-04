#!/usr/bin/env bash
set -euo pipefail

APK_PATH="${1:-}"
if [[ -z "$APK_PATH" ]]; then
  echo "Missing APK path argument"
  exit 2
fi

if [[ ! -f "$APK_PATH" ]]; then
  echo "APK not found at: $APK_PATH"
  exit 2
fi

mkdir -p test-results
APPIUM_LOG="test-results/appium.log"

cleanup() {
  if [[ -n "${APPIUM_PID:-}" ]]; then
    kill "$APPIUM_PID" 2>/dev/null || true
  fi
}
trap cleanup EXIT

echo "Installing APK: $APK_PATH"
adb install -r "$APK_PATH"

echo "Starting Appium"
appium --port 4723 --log-level info > "$APPIUM_LOG" 2>&1 &
APPIUM_PID=$!

echo "Waiting for Appium readiness..."
APP_READY=0
for i in $(seq 1 60); do
  if curl -sf http://127.0.0.1:4723/status > /dev/null 2>&1; then
    echo "Appium ready after ${i}s"
    APP_READY=1
    break
  fi
  sleep 1
done

if [[ "$APP_READY" -ne 1 ]]; then
  echo "Appium did not become ready within 60 seconds"
  exit 1
fi

set +e
timeout 20m env ANDROID_APK_PATH="$APK_PATH" dotnet test tests/MauiApp.UITests/MauiApp.UITests.csproj -c Release \
  --logger "trx;LogFileName=emulator-test-results.trx" \
  --results-directory test-results
TEST_EXIT=$?
set -e

if [[ "$TEST_EXIT" -eq 124 ]]; then
  echo "dotnet test timed out after 20 minutes"
fi

echo "dotnet test exit code: $TEST_EXIT"
exit "$TEST_EXIT"
