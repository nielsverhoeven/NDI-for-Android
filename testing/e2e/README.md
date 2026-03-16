# NDI Android E2E Harness

This workspace contains Playwright-based end-to-end coverage for dual-emulator
Android workflows.

## Required Inputs

- Two running Android emulators with distinct serials
- Debug app installed on both emulators
- `adb` available on `PATH`

Default package: `com.ndi.app.debug`

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

## Artifacts

The launcher stores preflight and post-run logcat files under:

- `testing/e2e/artifacts/dual-emulator-<timestamp>/`

Use these logs together with Playwright HTML output in:

- `testing/e2e/playwright-report/`

The interop test now captures `publisher-active.png` and `receiver-playing.png`, uploads the publisher frame through relay diagnostics, and validates that the receiver viewer surface (`viewerSurfacePlaceholder`) is both non-black and visually similar to the publisher content.

