#!/usr/bin/env bash
# validate-apk-startup.sh
# Deterministic local script to validate that an installed APK does NOT abort on startup.
#
# Usage:
#   bash testing/scripts/validate-apk-startup.sh <path-to-apk>
#
# Exit codes:
#   0 — APK installed and launched without a Fast Deployment / startup abort
#   1 — Startup abort detected in crash logcat buffer
#   2 — Bad argument / APK not found
#   3 — Device not found or not ready
#
# Requirements: adb on PATH, device/emulator connected and booted.

set -euo pipefail

PACKAGE="com.ndi.android"
APK_PATH="${1:-}"

if [[ -z "$APK_PATH" ]]; then
  echo "Usage: $0 <path-to-apk>"
  exit 2
fi

if [[ ! -f "$APK_PATH" ]]; then
  echo "ERROR: APK not found at: $APK_PATH"
  exit 2
fi

# ── 1. Verify device is ready ────────────────────────────────────────────────
echo "Checking device readiness..."
BOOT_COMPLETE=""
for i in $(seq 1 30); do
  BOOT_COMPLETE=$(adb shell getprop sys.boot_completed 2>/dev/null | tr -d '\r\n') || true
  if [[ "$BOOT_COMPLETE" == "1" ]]; then
    echo "Device ready."
    break
  fi
  echo "  Waiting for boot (attempt $i/30)..."
  sleep 2
done

if [[ "$BOOT_COMPLETE" != "1" ]]; then
  echo "ERROR: Device not ready after 60s. Run 'adb devices' and check connection."
  exit 3
fi

# ── 2. Uninstall existing package (avoids signature mismatch) ────────────────
echo "Uninstalling existing $PACKAGE (if present)..."
adb uninstall "$PACKAGE" 2>/dev/null || true

# ── 3. Install APK ───────────────────────────────────────────────────────────
echo "Installing APK: $APK_PATH"
adb install "$APK_PATH"
echo "Install succeeded."

# ── 4. Clear logcat crash buffer before launch ───────────────────────────────
adb logcat -b crash -c

# ── 5. Launch app via intent ──────────────────────────────────────────────────
echo "Launching $PACKAGE..."
adb shell monkey -p "$PACKAGE" -c android.intent.category.LAUNCHER 1 >/dev/null
sleep 3   # allow startup to either succeed or abort

# ── 6. Check crash buffer for Fast Deployment abort signature ────────────────
CRASH_LOG=$(adb logcat -b crash -d -v time 2>/dev/null || true)
ABORT_SIGNATURE="No assemblies found"

if echo "$CRASH_LOG" | grep -q "$ABORT_SIGNATURE"; then
  echo ""
  echo "FAIL: Fast Deployment startup abort detected."
  echo "      Crash buffer contains: '$ABORT_SIGNATURE'"
  echo ""
  echo "--- Relevant crash lines ---"
  echo "$CRASH_LOG" | grep -E "monodroid|SIGABRT|No assemblies|Abort message" || true
  echo "---------------------------"
  echo ""
  echo "Fix: Build a Release APK instead of Debug, or ensure"
  echo "     EmbedAssembliesIntoApk=true in NdiForAndroid.csproj Debug properties."
  exit 1
fi

# ── 7. Confirm process is alive ───────────────────────────────────────────────
PID=$(adb shell pidof "$PACKAGE" 2>/dev/null | tr -d '\r\n' || true)
if [[ -z "$PID" ]]; then
  echo ""
  echo "WARN: $PACKAGE process not found after launch — app may have exited silently."
  echo "      Check 'adb logcat -b crash -d -v time' for details."
  exit 1
fi

echo ""
echo "PASS: $PACKAGE launched successfully (PID $PID)."
echo "      No Fast Deployment startup abort detected in crash buffer."
