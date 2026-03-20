# Quickstart: Three-Screen Navigation Repairs and Version-Aware E2E

## 1. Prerequisites

From repository root:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
.\gradlew.bat --version
npm --prefix testing/e2e ci
adb devices
```

Expected environment:

- Two Android emulators connected with distinct serials.
- NDI debug app installed on both emulators.
- Emulator windows visible during test execution.

## 2. Test-First Development Sequence

1. Add failing unit/UI tests for:
   - View source selection routes to viewer (not stream setup).
   - Back from viewer returns to View root.
   - Back from View root returns to Home.
   - Stream screen always highlights Stream destination.
   - Home/Stream/View icon mapping is house/camera/screen.
2. Add failing e2e tests for:
   - Android version detection recorded per device.
   - Unified runtime version branching.
   - Unsupported-version fail-fast.
   - Full-screen share path selection and confirm action.
   - Static delays constrained to <= 1 second.
3. Implement minimum changes to pass tests.
4. Refactor with all tests green.

## 2.1 Feature-Specific Execution Checklist

- [ ] Run prerequisite validation (`scripts/verify-android-prereqs.ps1`) and confirm both emulator serials are online.
- [ ] Confirm View source selection opens `ndi://viewer/{sourceId}` only.
- [ ] Confirm back sequence: Viewer -> View root -> Home.
- [ ] Confirm top-level icon mapping is Home=house, Stream=camera, View=screen.
- [ ] Confirm exactly one top-level destination is highlighted after taps and deep links.
- [ ] Confirm runtime support-window diagnostics are emitted before suite steps.
- [ ] Confirm unsupported Android versions fail fast with non-zero runner outcome.
- [ ] Confirm intentional static waits in helper code are <= 1000ms.

## 3. Build and Validation Commands

```powershell
.\gradlew.bat test
.\gradlew.bat connectedAndroidTest
.\gradlew.bat :app:assembleDebug
```

Run dual-emulator e2e:

```powershell
powershell -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556
```

Optional preflight-only check:

```powershell
powershell -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556 -PreflightOnly
```

### 3.1 Command Matrix and Expected Diagnostics

| Command | Purpose | Expected Diagnostics |
|---------|---------|----------------------|
| `pwsh ./scripts/verify-android-prereqs.ps1` | Local environment readiness | SDK/JDK/emulator readiness output with pass/fail gates |
| `./gradlew.bat test` | Unit test regression gate | `BUILD SUCCESSFUL` and module test reports |
| `./gradlew.bat connectedAndroidTest` | Instrumentation/device gate | Android test result summary and report paths |
| `powershell -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-dual-emulator-e2e.ps1 ...` | Full dual-emulator e2e run | `android-version-diagnostics.json`, pre/post screenshots/logcat, Playwright report |
| `... -PreflightOnly` | Fast e2e environment + version gate | Device/package checks and version-window diagnostics only |

Expected version-aware diagnostics:

- Artifact file: `testing/e2e/artifacts/dual-emulator-<timestamp>/android-version-diagnostics.json`
- Includes support-window payload: `lowestSupportedMajor`, `highestSupportedMajor`, `windowSize`
- Includes per-role payload: `publisher` and `receiver` version profiles
- Unsupported versions terminate run early with non-zero outcome and explicit error context

## 4. Functional Validation Checklist

- View source selection opens viewer screen (never stream setup).
- Back from viewer returns to View root.
- Back from View root returns to Home.
- Top-level icons are correct: Home=house, Stream=camera, View=screen.
- Stream setup/control screens always highlight Stream destination.
- Exactly one destination is highlighted at any time.

## 5. E2E Compatibility Validation Checklist

- Publisher and receiver Android versions are detected and attached to test run.
- Support window is evaluated as rolling latest five major versions at runtime.
- Mixed supported major versions are accepted.
- Unsupported major version fails run before stream/view interactions.
- Consent flow selects full-screen sharing path and confirm action.
- No intentional static helper delay exceeds 1000ms.

## 6. Release-Grade Validation

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
.\gradlew.bat verifyReleaseHardening
.\gradlew.bat :app:assembleRelease
```

Collect evidence (screenshots, logcat, version diagnostics, and Playwright output)
under `testing/e2e/artifacts/` and `testing/e2e/test-results/` for release review.
