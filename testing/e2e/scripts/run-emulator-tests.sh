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

# Where the tests drop a screenshot, view hierarchy and device state for each failure (#312).
# Absolute: the test host's working directory is the project output directory, not the repo
# root, so a relative path would scatter artifacts somewhere the upload step never looks.
E2E_ARTIFACT_DIR="${E2E_ARTIFACT_DIR:-$PWD/test-results/failure-evidence}"
mkdir -p "$E2E_ARTIFACT_DIR"

# Optional xUnit filter, used by the regression-proof harness to run a single test against an
# old build of the app. Empty by default, so a normal run executes everything.
FILTER_ARGS=()
if [[ -n "${E2E_TEST_FILTER:-}" ]]; then
  FILTER_ARGS=(--filter "$E2E_TEST_FILTER")
  echo "Test filter: $E2E_TEST_FILTER"
fi

set +e
timeout 20m env ANDROID_APK_PATH="$APK_PATH" E2E_REQUIRE_DEVICE="$E2E_REQUIRE_DEVICE" \
  E2E_ARTIFACT_DIR="$E2E_ARTIFACT_DIR" \
  A11Y_MAX_VIOLATIONS="${A11Y_MAX_VIOLATIONS:-}" \
  dotnet test tests/MauiApp.UITests/NdiForAndroid.UITests.csproj -c Release \
  "${FILTER_ARGS[@]}" \
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

  # Name the evidence in the log so a reader knows to go and download it, and so a run where
  # capture itself failed is distinguishable from one where it was never attempted.
  EVIDENCE_COUNT=$(find "$E2E_ARTIFACT_DIR" -type f 2>/dev/null | wc -l | tr -d ' ')
  if [[ "$EVIDENCE_COUNT" -gt 0 ]]; then
    echo "Per-failure evidence ($EVIDENCE_COUNT file(s)) in the emulator-diagnostics artifact:"
    find "$E2E_ARTIFACT_DIR" -type f -printf '  %f\n' 2>/dev/null || ls -1 "$E2E_ARTIFACT_DIR"
  else
    echo "No per-failure evidence captured — the failure happened outside a test body"
    echo "(fixture setup, or the run timed out before any test threw)."
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

# The accessibility audit's own summary, echoed here rather than left where it was printed. It
# lands in the middle of several hundred lines of dotnet output, and the violation count is what
# decides where A11Y_MAX_VIOLATIONS should sit — so it belongs somewhere a reader actually looks.
A11Y_SUMMARY="$E2E_ARTIFACT_DIR/accessibility-summary.txt"
if [[ -s "$A11Y_SUMMARY" ]]; then
  echo
  cat "$A11Y_SUMMARY"
  echo
fi

# ── Regression-proof mode ────────────────────────────────────────────────────
# Used to demonstrate that a regression test actually catches the bug it was written for, by
# running it against a build of the app from before the fix. Here a FAILING test is the success
# condition, so the normal assertions are inverted: a pass means the test cannot detect the
# defect and proves nothing.
if [[ "${E2E_EXPECT_FAILURE:-false}" == "true" ]]; then
  if [[ "$FAILED" -gt 0 ]]; then
    echo "PROVEN: $FAILED test(s) failed against this build, which is the expected outcome."
    echo "        The regression test detects the defect."
    exit 0
  fi

  echo "NOT PROVEN: expected at least one failure against this build, but none failed"
  echo "            (total=$TOTAL passed=$PASSED skipped=$SKIPPED)."
  echo "            A regression test that passes on the original bug proves nothing."
  exit 1
fi

if [[ "$PASSED" -eq 0 ]]; then
  echo "FAIL: no UI test passed. An all-skipped or empty run is not a green e2e gate."
  exit 1
fi

if [[ "$SKIPPED" -gt 0 ]]; then
  echo "WARNING: $SKIPPED test(s) skipped. Conditional skips inside a test body are allowed,"
  echo "         but a skip caused by a missing device would have failed the fixture already."
fi

exit "$TEST_EXIT"
