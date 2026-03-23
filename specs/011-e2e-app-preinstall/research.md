# Research: E2E App Pre-Installation Gate

**Branch**: `011-e2e-app-preinstall` | **Date**: 2026-03-23

---

## Decision 1: ADB Version Extraction Method

**Decision**: Extract `versionName` and `versionCode` from the APK artifact *before* install using `aapt dump badging` (or `aapt2`), then confirm from the device *after* install via `adb -s <serial> shell dumpsys package com.ndi.app.debug`.

**Rationale**:
- Pre-install extraction validates the artifact before committing to installation; if the APK is corrupt, we abort early.
- Post-install confirmation via `dumpsys package` closes the loop and produces the authoritative per-device record for the Pre-Flight Report.
- `aapt`/`aapt2` are bundled with Android SDK build-tools (already required by the project) and available in CI (via `android-actions/setup-android@v3`).
- `dumpsys package <pkg> | grep -E "versionName|versionCode"` is a standard, stable ADB pattern used in existing `android-device-fixtures.ts`.

**Alternatives considered**:
- `pm dump` — same data, less commonly used pattern; `dumpsys package` preferred.
- Read `AndroidManifest.xml` from APK with `apkanalyzer` — heavier tool, not required for version extraction alone.

**Commands**:
```powershell
# Pre-install (APK metadata)
aapt dump badging app-debug.apk | Select-String "package: name="
# Post-install (device confirmation)
adb -s emulator-5554 shell dumpsys package com.ndi.app.debug | Select-String "versionName|versionCode"
```

---

## Decision 2: Launch Verification Method

**Decision**: Use `adb -s <serial> shell am start -W -n com.ndi.app.debug/com.ndi.app.MainActivity` and check the `result=` line in the output for `ACTIVITY_LAUNCHED` (exit code 0). Force-stop the app immediately after to leave the emulator in a clean state.

**Rationale**:
- `-W` (wait) flag causes `am start` to block until the activity is fully launched or fails, producing a deterministic result.
- `am start` returns non-zero exit code on launch failure (e.g., `ACTIVITY_NOT_STARTED_CLASS_NOT_FOUND`), which is caught by `Invoke-Adb`'s existing error handling.
- The main activity is `com.ndi.app.MainActivity` in package `com.ndi.app.debug` (confirmed in `app/src/main/AndroidManifest.xml`).
- Force-stopping after verification ensures no residual app state bleeds into the first test.

**Alternatives considered**:
- `pidof <package>` polling — non-deterministic timing; `-W` is strictly better.
- `monkey -p <package> 1` — invokes random input; not a clean launch test.
- `am instrument` — heavyweight and requires a test APK.

---

## Decision 3: Per-Emulator Timeout Enforcement

**Decision**: Use `Start-Job` / `Wait-Job -Timeout 60` / `Remove-Job -Force` pattern in PowerShell to impose the 60-second per-emulator install timeout.

**Rationale**:
- Aligns with the 60s constraint from FR-008 / SC-003.
- Native PowerShell pattern available in both PowerShell 5.1 and 7+, matching the Windows CI runner environment (`windows-latest`).
- `Remove-Job -Force` kills the ADB process tree if timeout fires.
- Existing `emulator-adb.ps1` helpers (`Install-ApkToEmulator`, `Invoke-Adb`) are synchronous; wrapping in a job bounds their wall-clock time without rewriting them.

**Alternatives considered**:
- `System.Diagnostics.Process` with `WaitForExit(milliseconds)` — viable but requires wrapping `adb` binary invocation directly; job approach reuses existing helpers cleanly.
- `Invoke-Command -AsJob` — for remote PS sessions; not needed for local ADB invocation.

---

## Decision 4: Pre-Flight Report Artifact Location

**Decision**: Write the Pre-Flight Report to `testing/e2e/artifacts/runtime/preinstall-report.json`, consistent with the provisioning result at `testing/e2e/artifacts/runtime/provisioning-result.json` (already used by `provision-dual-emulator.ps1`).

**Rationale**:
- Keeps all runtime infrastructure outputs in one directory, picked up by `collect-test-artifacts.ps1 -SessionId ci-<run_id>` and uploaded via `actions/upload-artifact`.
- Playwright specs reading the report use the same base path convention used for `provisioning-result.json`.

**Alternatives considered**:
- Root `testing/e2e/artifacts/` — too flat; `runtime/` sub-directory groups live session data.
- Inside `playwright-report/` — mixing infra reports with test reporter output creates confusion.

---

## Decision 5: Integration Point — CI vs. Global Setup

**Decision**: Dual-track integration:
1. **CI**: Explicit step `Install app on emulators` in `.github/workflows/e2e-dual-emulator.yml`, after `Provision dual emulators` and before any test step. The step calls `testing/e2e/scripts/install-app-preinstall.ps1`.
2. **Local / global-setup**: `global-setup-dual-emulator.ts` calls `install-app-preinstall.ps1` after provisioning (gated by `DUAL_EMULATOR_AUTOMATION !== "0"`), so local `npx playwright test` runs also get the guarantee.

**Rationale**:
- CI explicit step is observable and restartable independently; failure surfaces at the right step in GitHub Actions logs.
- Global-setup integration ensures the guarantee holds for local developer runs that don't invoke the script directly.
- No duplication risk: the script is idempotent (FR-007), so double-execution (CI step + global setup) when both are active is safe — the second run simply overwrites the report with identical data.

**Alternatives considered**:
- CI-only (no global-setup hook) — local developer runs would not have the guarantee; violates SC-001.
- Global-setup-only (no explicit CI step) — CI step ordering is less visible; harder to observe or skip independently.

---

## Decision 6: APK Artifact Path

**Decision**: Default path `app/build/outputs/apk/debug/app-debug.apk` relative to repo root; parametrize via `$ApkPath` script parameter with environment variable `APP_APK_PATH` as override.

**Rationale**:
- `./gradlew :app:assembleDebug` outputs to `app/build/outputs/apk/debug/app-debug.apk` — standard Gradle AGP output convention, confirmed by project structure.
- Explicit parameter allows artifact path override for future variant or flavor support without script modification.

---

## Decision 7: Package Name and Debug Application ID

**Decision**: Canonical debug package name is `com.ndi.app.debug` (from `app/build.gradle.kts`: `applicationId = "com.ndi.app"` + `applicationIdSuffix = ".debug"`).

**Rationale**: Matches the constant `DEFAULT_PACKAGE = "com.ndi.app.debug"` already in `android-device-fixtures.ts` and the emulator test fixtures; no divergence introduced.

---

## Decision 8: CI Build Step — `assembleDebug` placement

**Decision**: Insert a `Build app debug APK` step in `.github/workflows/e2e-dual-emulator.yml` after `Setup Android SDK` and before `Validate emulator images` (i.e., before provisioning). This ensures the APK artifact exists before the install step runs.

**Rationale**:
- Emulator image validation and provisioning depend on ADB tooling, not on the APK.  Building before provisioning keeps parallel resource use reasonable (build consumes CPU while emulators would be starting — but since provisioning `SkipBootIfAlreadyRunning` applies, no real contention).
- Placing the build step before provisioning mirrors the logical dependency: `build → provision → install → test`.
- The CI workflow currently builds only `:ndi:sdk-bridge:assembleRelease` (NDI bridge); this feature adds `:app:assembleDebug` as a separate step with a clear, distinct label.

**Workflow triggers**: `app/**` and `app/build.gradle.kts` paths not currently in the `on.pull_request.paths` filter. Per spec scope, the workflow trigger paths are not modified by this feature (out of scope).

---

## Decision 9: Playwright Spec for Pre-Flight Validation (TDD anchor)

**Decision**: New `testing/e2e/tests/support/app-preinstall.spec.ts` spec, tagged `@preinstall`, reads `preinstall-report.json` and asserts: overall status is PASS, both emulators have `apkInstalled: true`, `launchVerified: true`, `versionName` matches expected, and `elapsedMs ≤ 60000` per device.

**Rationale**:
- Constitution IV requires TDD (failing-first). The spec file is the failing test; `install-app-preinstall.ps1` is the implementation that makes it pass.
- Placing it in `tests/support/` keeps it alongside other infra validation specs (`e2e-infrastructure.spec.ts`, `dual-emulator-provisioning.spec.ts`).
- Tag `@preinstall` allows targeted CI step: `npx playwright test tests/support/app-preinstall.spec.ts`.

---

## No NEEDS CLARIFICATION Items Remain

All NEEDS CLARIFICATION items from the Technical Context were resolved via spec clarifications (2026-03-23) and the research decisions above. The plan proceeds to Phase 1.
