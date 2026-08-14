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

echo "Waiting for emulator to fully boot..."
BOOT_COMPLETE=0
for i in $(seq 1 60); do
  BOOT=$(adb shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')
  if [[ "$BOOT" == "1" ]]; then
    echo "Emulator boot complete after ${i}s"
    BOOT_COMPLETE=1
    break
  fi
  sleep 2
done
if [[ "$BOOT_COMPLETE" -ne 1 ]]; then
  echo "Emulator did not report boot_completed within 120s — aborting"
  exit 1
fi

# Extra settle time so the launcher and system services are stable
sleep 5

echo "Installing APK: $APK_PATH"
adb uninstall com.ndi.android >/dev/null 2>&1 || true
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

# A device is guaranteed here, so AppiumDriverFixture must treat an unavailable session as a
# failure rather than a skip. Without this the suite reports success while executing nothing.
E2E_REQUIRE_DEVICE="${E2E_REQUIRE_DEVICE:-true}"

TRX="test-results/emulator-test-results.trx"
rm -f "$TRX"

set +e
timeout 20m env ANDROID_APK_PATH="$APK_PATH" E2E_REQUIRE_DEVICE="$E2E_REQUIRE_DEVICE" \
  dotnet test tests/MauiApp.UITests/NdiForAndroid.UITests.csproj -c Release \
  --logger "trx;LogFileName=emulator-test-results.trx" \
  --results-directory test-results
TEST_EXIT=$?
set -e

if [[ "$TEST_EXIT" -eq 124 ]]; then
  echo "dotnet test timed out after 20 minutes"
fi

echo "dotnet test exit code: $TEST_EXIT"

# Capture device-side state while the emulator is still alive — the workflow step that
# reports the failure runs after the emulator-runner action has torn it down, so anything
# not collected here is gone. Distinguishes "app crashed on launch" from "app was merely
# slow to reach the foreground", which look identical from Appium's side.
if [[ "$TEST_EXIT" -ne 0 ]]; then
  echo "Collecting device diagnostics..."
  adb logcat -b crash -d > test-results/logcat-crash.txt 2>&1 || true
  adb logcat -d -v time | tail -400 > test-results/logcat-tail.txt 2>&1 || true
  adb shell dumpsys package com.ndi.android 2>/dev/null | head -60 > test-results/package-info.txt || true

  if [[ -s test-results/logcat-crash.txt ]]; then
    echo "===== crash buffer ====="
    head -60 test-results/logcat-crash.txt
    echo "======================="
  else
    echo "Crash buffer empty — the app did not abort, so the activity wait timed out instead."
  fi
fi

# ── Result assertion ─────────────────────────────────────────────────────────
# A zero exit code is not sufficient evidence that the suite ran: xunit.skippablefact
# reports an all-skipped run as success. Read the counters back out of the TRX and
# require that something actually executed and passed.
if [[ "$E2E_REQUIRE_DEVICE" != "true" ]]; then
  exit "$TEST_EXIT"
fi

if [[ ! -f "$TRX" ]]; then
  echo "FAIL: no TRX produced at $TRX — the suite did not run."
  exit 1
fi

# grep -m1 rather than `grep | head -1`: under `set -euo pipefail`, head closing the pipe first
# sends SIGPIPE to grep and fails the script.
COUNTERS=$(grep -o -m1 '<Counters[^/]*/>' "$TRX" || true)
read_counter() {
  local match
  match=$(printf '%s' "$COUNTERS" | grep -o "$1=\"[0-9]*\"" || true)
  match="${match#*\"}"
  printf '%s' "${match%\"}"
}

TOTAL=$(read_counter total);   TOTAL=${TOTAL:-0}
PASSED=$(read_counter passed); PASSED=${PASSED:-0}
FAILED=$(read_counter failed); FAILED=${FAILED:-0}
SKIPPED=$(( TOTAL - PASSED - FAILED ))

echo "Counters — total=$TOTAL passed=$PASSED failed=$FAILED skipped=$SKIPPED"

if [[ "$PASSED" -eq 0 ]]; then
  echo "FAIL: no UI test passed. An all-skipped or empty run is not a green e2e gate."
  exit 1
fi

if [[ "$SKIPPED" -gt 0 ]]; then
  echo "WARNING: $SKIPPED test(s) skipped. Conditional skips inside a test body are allowed,"
  echo "         but a skip caused by a missing device would have failed the fixture already."
fi

exit "$TEST_EXIT"
