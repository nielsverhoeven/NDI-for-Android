# NDI Android E2E Harness

This workspace contains Playwright-based end-to-end coverage for dual-emulator
Android workflows.

## Required Inputs

- Two running Android emulators **with GUI visible** (non-headless) and distinct serials
- Debug app installed on both emulators
- `adb` available on `PATH`

Default package: `com.ndi.app.debug`

## Launching Emulators with Visible GUI

Before running e2e tests, launch both emulators visibly in separate terminal windows:

```powershell
# Terminal 1: Launch first emulator (Publisher)
emulator -avd Emulator-A -feature WindowsHypervisorPlatform

# Terminal 2: Launch second emulator (Receiver - in a separate window)
emulator -avd Emulator-B -feature WindowsHypervisorPlatform

# Wait for both emulators to fully boot (check 'adb devices' shows both with 'device' status)
adb devices
```

**Note:** Emulators must stay visible (GUI windows open) throughout test execution. The test harness captures screenshots and validates visual content on the emulator screens.

## Preflight Only

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556 -PreflightOnly
```

## Run Dual-Emulator Interop

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556
```

Note: the launcher starts a lightweight local relay (`scripts/ndi-relay-server.mjs`) used by debug bridge fallback discovery across emulators.

## Direct Playwright Run

```powershell
$env:EMULATOR_A_SERIAL = "emulator-5554"
$env:EMULATOR_B_SERIAL = "emulator-5556"
$env:APP_PACKAGE = "com.ndi.app.debug"
npm run test:dual-emulator
```

## Important: Emulator Visibility During Tests

Maintain the emulator windows **in focus and visible** while tests run. The e2e harness:
1. Captures emulator screenshots during test execution (`publisher-active.png`, `receiver-playing.png`)
2. Validates visual similarity between publisher and receiver streams
3. Analyzes frame content to confirm NDI streaming is working end-to-end

Minimizing or hiding emulator windows may cause screenshot validation to fail.

## Artifacts

The launcher stores preflight and post-run logcat files under:

- `testing/e2e/artifacts/dual-emulator-<timestamp>/`

The launcher also stores emulator screenshots under:

- `testing/e2e/artifacts/dual-emulator-<timestamp>/screenshots/`
  - `publisher-preflight.png`
  - `receiver-preflight.png`
  - `publisher-active.png`
  - `receiver-before-play.png`
  - `receiver-playing.png`
  - `publisher-postrun.png`
  - `receiver-postrun.png`

Additional restart-flow screenshots are written with `restart-*` prefixes when
the restart interop scenario runs.

Use these logs together with Playwright HTML output in:

- `testing/e2e/playwright-report/`

The interop test now captures `publisher-active.png` and `receiver-playing.png`, uploads the publisher frame through relay diagnostics, and validates that the receiver viewer surface (`viewerSurfacePlaceholder`) is both non-black and visually similar to the publisher content.

## Manual Visual Validation Checklist

When debugging "publisher active but receiver not showing stream", review the
artifacts in this order:

1. Confirm publisher output is active in `publisher-active.png`.
2. Compare `receiver-before-play.png` and `receiver-playing.png`.
3. Verify receiver playing image is not black and shows visible content.
4. If mismatch persists, inspect the paired UTP logs and logcat snapshots.

