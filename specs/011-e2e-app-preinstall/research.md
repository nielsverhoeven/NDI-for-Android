# Research: E2E App Pre-Installation Gate

**Branch**: `011-e2e-app-preinstall` | **Date**: 2026-03-23

---

## Decision 1: ADB Version Extraction Method

**Decision**: Extract `versionName` and `versionCode` from the APK artifact before install using `aapt dump badging` (or `aapt2`), then confirm from the device after install via `adb -s <serial> shell dumpsys package com.ndi.app.debug`.

**Rationale**:
- Pre-install extraction validates the artifact before committing to installation; if the APK is corrupt, the run aborts early.
- Post-install confirmation via `dumpsys package` closes the loop and produces the authoritative per-device record for the Pre-Flight Report.
- `aapt` and `aapt2` are bundled with Android SDK build-tools already required by the repo and available in CI.

**Alternatives considered**:
- `pm dump` only: workable but less explicit for host-side artifact validation.
- `apkanalyzer`: heavier than necessary for version extraction alone.

**Commands**:
```powershell
# Pre-install (APK metadata)
aapt dump badging app-debug.apk | Select-String "package: name="

# Post-install (device confirmation)
adb -s emulator-5554 shell dumpsys package com.ndi.app.debug | Select-String "versionName|versionCode"
```

---

## Decision 2: Launch Verification Method

**Decision**: Use `adb -s <serial> shell am start -W -n com.ndi.app.debug/com.ndi.app.MainActivity`, treat zero exit code plus `Status: ok` in stdout as success, and force-stop the app immediately afterward.

**Rationale**:
- `-W` blocks until Android reports launch success or failure, giving a deterministic verification point.
- `Status: ok` is the stable success signal exposed by `am start -W` across emulator images.
- Force-stopping after verification leaves the first real test in control of app startup state.

**Alternatives considered**:
- `pidof` polling: timing-sensitive and less deterministic.
- `monkey -p`: noisy and not a clean launch signal.

---

## Decision 3: Per-Device Timeout Enforcement

**Decision**: Enforce one 60-second per-device deadline that covers readiness wait, installation, post-install version confirmation, and launch verification. Use a PowerShell stopwatch and remaining-time budget for each step, wrapping the install call in `Start-Job` and `Wait-Job -Timeout <remaining>` only when needed.

**Rationale**:
- Aligns FR-008 and FR-011 under one measurable device budget.
- Prevents separate waits from silently exceeding the user-visible timeout commitment.
- Reuses existing synchronous helper functions without rewriting them around process objects.

**Alternatives considered**:
- Separate readiness and install timeouts: introduces conflicting interpretations of the 60-second requirement.
- Raw `System.Diagnostics.Process` management: workable but unnecessary for the current helper structure.

---

## Decision 4: Emulator Readiness Signal

**Decision**: Treat a device as ready only when `adb -s <serial> get-state` returns `device` and `adb -s <serial> shell getprop sys.boot_completed` returns `1`. Poll within the per-device 60-second deadline before attempting installation.

**Rationale**:
- Distinguishes a reachable emulator from one whose framework is ready to accept installs.
- Maps directly to the clarified FR-011 readiness behavior.
- Supports a distinct `NOT_READY` failure state in the report.

**Alternatives considered**:
- Immediate failure on the first miss: too brittle for fresh boots.
- Assuming Feature 010 always guarantees readiness: conflicts with the clarified spec and hides boot-lag regressions.

---

## Decision 5: Pre-Flight Report Artifact Location

**Decision**: Write the Pre-Flight Report to `testing/e2e/artifacts/runtime/preinstall-report.json`, aligned with the runtime artifact conventions already used by Feature 010.

**Rationale**:
- Keeps runtime infrastructure outputs in a single predictable directory.
- Allows Playwright support specs and CI artifact upload steps to consume the same file without extra path translation.
- Avoids mixing operational data with Playwright HTML report output.

**Alternatives considered**:
- Root `testing/e2e/artifacts/`: too flat for session/runtime separation.
- `playwright-report/`: conflates harness state with reporter output.

---

## Decision 6: Integration Point - CI vs. Global Setup

**Decision**: Keep dual entry points with report reuse:
1. **CI**: explicit `Install app on emulators` step in `.github/workflows/e2e-dual-emulator.yml`, after provisioning and before any test step.
2. **Local / global setup**: `global-setup-dual-emulator.ts` validates whether a fresh matching `preinstall-report.json` already exists for the current APK and target serials; otherwise it invokes the install script.

**Rationale**:
- Satisfies FR-009's explicit CI gate ordering requirement.
- Preserves SC-001 for local runs started directly from Playwright.
- Avoids needless duplicate installs when CI already created a valid report for the same run.

**Alternatives considered**:
- CI only: local runs would not be protected.
- Global setup only: CI step ordering would be opaque and harder to diagnose.

---

## Decision 7: APK Artifact Path

**Decision**: Default path is `app/build/outputs/apk/debug/app-debug.apk` relative to repo root, overridable via `-ApkPath` or `APP_APK_PATH`.

**Rationale**:
- Matches the standard output of `./gradlew :app:assembleDebug`.
- Supports future variant overrides without changing script logic.

**Alternatives considered**:
- Hard-coded debug-only path with no override: simpler now, but blocks future variant experimentation.

---

## Decision 8: Package Name and Debug Application ID

**Decision**: The canonical debug package name is `com.ndi.app.debug`.

**Rationale**:
- Matches `app/build.gradle.kts` (`applicationId = "com.ndi.app"` plus `.debug` suffix).
- Matches existing Playwright Android fixture assumptions in the repo.

---

## Decision 9: CI Build Step Placement

**Decision**: Insert a `Build app debug APK` step in `.github/workflows/e2e-dual-emulator.yml` after Android SDK setup and before emulator provisioning.

**Rationale**:
- Ensures the required artifact exists before the install gate executes.
- Preserves a clear CI ordering model: build, provision, install, validate, regress.
- Keeps build failures separate from emulator or Playwright failures in workflow logs.

**Alternatives considered**:
- Building after provisioning: no functional advantage and more confusing failure ordering.

---

## Decision 10: Playwright Validation Strategy

**Decision**: Add a new failing-first `testing/e2e/tests/support/app-preinstall.spec.ts` support spec tagged `@preinstall`, then rerun the existing Playwright suites under `testing/e2e/tests/**/*.spec.ts` after the support gate passes.

**Rationale**:
- Provides a clear Red-Green-Refactor entry point.
- Keeps validation in the repo's default end-to-end framework.
- Closes the regression gap by explicitly re-running the existing suites, not only the new support spec.

**Alternatives considered**:
- Script-only validation: insufficient against the constitution's Playwright default.
- PowerShell unit tests: conflicts with the repo's JUnit governance for unit testing.

---

## No NEEDS CLARIFICATION Items Remain

All technical-context unknowns were resolved from the clarified spec and repo facts. The plan can proceed to task generation.
