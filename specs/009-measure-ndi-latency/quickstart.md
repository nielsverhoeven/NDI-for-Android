# Quickstart: Dual-Emulator NDI Latency Measurement

## 1. Prerequisites

From repository root:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
./gradlew.bat :app:assembleDebug
npm --prefix testing/e2e ci
adb devices
```

Expected:
- Android prerequisites pass.
- Two emulators are online (publisher + receiver).
- `com.ndi.app.debug` installed on both emulators.

## 2. Run Primary Latency Scenario

```powershell
$env:EMULATOR_A_SERIAL="emulator-5554"
$env:EMULATOR_B_SERIAL="emulator-5556"
$env:APP_PACKAGE="com.ndi.app.debug"
npm --prefix testing/e2e run test -- --project=android-primary tests/interop-dual-emulator.spec.ts
```

## 3. Run Gate-Oriented Dual-Emulator Harness

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File testing/e2e/scripts/run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556
```

Expected outputs:
- Run summary JSON under `testing/e2e/artifacts/dual-emulator-*/run-summary.json`
- Scenario checkpoints JSON under `testing/e2e/artifacts/dual-emulator-*/scenario-checkpoints.json`
- Per-test recording/log artifacts under `testing/e2e/test-results/`

## 4. Validate Evidence

Confirm artifacts include:
- Source and receiver recordings/snapshots
- Latency analysis output with method `MOTION_CROSS_CORRELATION`
- Explicit failed-step reason when run status is not PASSED
- Existing regression suite status in quality-gate summary

## 5. Troubleshooting

- If receiver never reaches playback screen, fail run with step-level reason and preserve artifacts.
- If app startup fails on an emulator, reinstall APK and rerun scenario.
- If analysis is invalid, retain recordings and invalidate run rather than reporting latency.
- If regression suite fails, treat gate as failed even if latency scenario passes.
