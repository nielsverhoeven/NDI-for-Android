# NDI Android E2E Harness

This workspace contains Playwright-based end-to-end coverage for dual-emulator
Android workflows.

## Required Inputs

- Two running Android emulators **with GUI visible** (non-headless) and distinct serials
- Debug app installed on both emulators
- `adb` available on `PATH`

Default package: `com.ndi.app.debug`

## Environment Variables

- `EMULATOR_A_SERIAL`: Primary/publisher emulator serial (default `emulator-5554`)
- `EMULATOR_B_SERIAL`: Receiver emulator serial (default `emulator-5556`)
- `APP_PACKAGE`: Android app package (default `com.ndi.app.debug`)
- `E2E_MATRIX_PROFILES`: Comma-separated profiles for matrix runs (default `api34,api35`)
- `E2E_WAIVER_FILE`: Optional waiver JSON path consumed by the summary script

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

## PR Primary Gate Run

Use one primary profile for required PR validation:

```powershell
npm run test:pr:primary
```

This run executes:

1. New settings suite coverage
2. Existing regression suite coverage
3. Summary evidence generation

Any failed, skipped, or partial execution is treated as a gate failure.

## Scheduled Matrix Run

Use matrix profiles (typically on a nightly schedule):

```powershell
$env:E2E_MATRIX_PROFILES = "api34,api35"
npm run test:matrix
```

The matrix runner executes full-suite coverage per profile and fails if any
profile is incomplete or failing.

## Regression-Pass Gate Policy

For this repository, a validation cycle is compliant only when both suites pass:

1. New settings suite (`@settings` coverage)
2. Existing regression suite (manifest-defined baseline scenarios)

Policy rules:

- Any unexpected failure blocks sign-off.
- Any skipped scenario is treated as an incomplete run and blocks sign-off.
- Waivers are temporary and require both approver roles:
  - `mobile-maintainer`
  - `architecture-quality-reviewer`

## Important: Emulator Visibility During Tests

Maintain the emulator windows **in focus and visible** while tests run. The e2e harness:
1. Captures emulator screenshots during test execution (`publisher-active.png`, `receiver-playing.png`)
2. Validates visual similarity between publisher and receiver streams
3. Analyzes frame content to confirm NDI streaming is working end-to-end

Minimizing or hiding emulator windows may cause screenshot validation to fail.

## Artifacts

The launcher stores preflight and post-run logcat files under:

- `testing/e2e/artifacts/dual-emulator-<timestamp>/`

Version-aware diagnostics are written to:

- `testing/e2e/artifacts/dual-emulator-<timestamp>/android-version-diagnostics.json`
  - Includes per-device SDK/release/major version profiles.
  - Includes computed rolling latest-five support window.
  - Used to explain fail-fast unsupported-version exits.

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

## Version-Aware Fail-Fast Behavior

The runner evaluates device Android versions before executing stream/view steps.

- If both device major versions are inside the rolling latest-five support window,
  tests proceed normally.
- If either device major version is unsupported, the run terminates immediately with
  non-zero exit code and diagnostics are preserved in the artifact directory.

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

## Background Stream Continuity Scenario (Feature 005)

This scenario proves that stream output stays active when the broadcaster leaves
the app and navigates to another application (Chrome, nos.nl).

### What the scenario tests

The six-step interop test (`@dual-emulator publish discover play stop interop`)
exercises:

1. **START_STREAM_A** — broadcaster starts NDI output on Emulator A, reaches ACTIVE.
2. **START_VIEW_B** — receiver discovers and plays the stream on Emulator B, reaches PLAYING.
3. **OPEN_CHROME_A** — broadcaster presses Home and opens Chrome on Emulator A.
4. **VERIFY_CHROME_ON_B** — receiver viewer surface is checked for Chrome-like visual content.
5. **OPEN_NOS_A** — broadcaster navigates Chrome to `https://nos.nl`.
6. **VERIFY_NOS_ON_B** — receiver viewer surface is checked for nos.nl page content.

Then the broadcaster returns to the app's output screen and taps **Stop Output**,
confirming the stream terminates cleanly.

### Fail-fast step diagnostics

Every run persists a checkpoint timeline under:

```
testing/e2e/artifacts/dual-emulator-<timestamp>/scenario-checkpoints.json
```

On any step failure the harness prints:

```
==========================================
FAILED STEP: <step-name> (step <N>)
REASON: <error detail>
Checkpoint artifact: <path>
==========================================
```

and exits with a non-zero code.

### Extending or modifying steps

Step names are the `SixStepCheckpointName` union type in
`testing/e2e/tests/support/scenario-checkpoints.ts`. Add new steps by extending
that union and `ORDERED_STEPS`. The recorder enforces strict sequential execution
— no step may be skipped.

