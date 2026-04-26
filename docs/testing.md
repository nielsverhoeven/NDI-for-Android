<!-- Last updated: 2026-04-07 -->

# Testing and Validation Guide

This guide documents how to validate settings-menu functionality and the broader NDI app pipeline.

## Table of Contents

1. [Test Pyramid](#1-test-pyramid)
2. [Unit Test Organization and Commands](#2-unit-test-organization-and-commands)
3. [Instrumentation Tests](#3-instrumentation-tests)
4. [Dual-Emulator E2E Harness](#4-dual-emulator-e2e-harness)
5. [Timing Assertion Patterns](#5-timing-assertion-patterns)
6. [Release Hardening Validation](#6-release-hardening-validation)
7. [Contract Correlation](#7-contract-correlation)
8. [Regression-First Test Triage Policy](#8-regression-first-test-triage-policy)
9. [Feature 021 Validation Snapshot](#9-feature-021-validation-snapshot-viewer-persistence--source-availability)
10. [Feature 025 Validation Snapshot](#10-feature-025-validation-snapshot-fix-appearance-settings)
11. [Feature 029 Validation Snapshot](#11-feature-029-validation-snapshot-ndi-discovery-server-compatibility)

## 1. Test Pyramid

| Level | Purpose | Typical Command |
|---|---|---|
| Unit | Fast correctness checks for models, repos, ViewModels | `./gradlew.bat test` |
| Instrumentation | UI/navigation checks on emulator/device | `./gradlew.bat connectedAndroidTest` |
| E2E (dual-emulator) | Full publish/discover/view interoperability | `run-dual-emulator-e2e.ps1` |

## 2. Unit Test Organization and Commands

Focused module commands:

```powershell
./gradlew.bat :core:model:test
./gradlew.bat :feature:ndi-browser:data:test
./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest
```

Relevant settings feature unit tests:

- `NdiSettingsRepositoryImplTest` (endpoint parsing/default-port behavior)
- `SettingsViewModelTest` (validation and save flow)
- `DeveloperOverlayStateMapperTest` (disabled/idle/active mapping)
- `OverlayLogRedactorTest` (IPv4/IPv6 redaction)

## 3. Instrumentation Tests

Run full instrumentation suite:

```powershell
./gradlew.bat connectedAndroidTest
```

Run presentation instrumentation only:

```powershell
./gradlew.bat :feature:ndi-browser:presentation:connectedDebugAndroidTest
```

Implemented settings-related instrumentation files include:

- `SourceListSettingsNavigationTest.kt`
- `ViewerSettingsNavigationTest.kt`
- `OutputSettingsNavigationTest.kt`
- `SourceListFallbackWarningTest.kt`
- `DeveloperOverlayTimingTest.kt`
- `DeveloperOverlayStreamStatusTimingTest.kt`

Current implementation note:

- `DeveloperOverlayTimingTest.kt` and `DeveloperOverlayStreamStatusTimingTest.kt` are currently scaffold tests with placeholder bodies.

## 4. Dual-Emulator E2E Harness

Primary harness documentation: `testing/e2e/README.md`

Preflight only:

```powershell
powershell -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556 -PreflightOnly
```

Full dual-emulator run:

```powershell
powershell -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556
```

Primary PR gate run (required for settings e2e feature scope):

```powershell
npm --prefix testing/e2e run test:pr:primary
```

Scheduled/nightly matrix run:

```powershell
$env:E2E_MATRIX_PROFILES = "api34,api35"
npm --prefix testing/e2e run test:matrix
```

Gate policy:

- `new settings`, `latency scenario`, and `existing regression` suites must pass.
- Skipped or partial runs are treated as failures.
- Waivers require both approver roles and are validated from waiver metadata.

Latency scenario expectations:

- The dual-emulator latency flow uses motion/content cross-correlation and writes a structured latency artifact.
- Run summary must include failed-step diagnostics (`failedStepName`, `failedStepReason`) for invalid runs.
- SC-002 is enforced by a 600000ms per-run timeout gate on latency tests.

Artifacts:

- `testing/e2e/artifacts/dual-emulator-<timestamp>/`
- `testing/e2e/playwright-report/`

## 5. Timing Assertion Patterns

Timing helper source: `testing/e2e/tests/support/timingAssertions.ts`

Implemented pattern:

- Run action 3 times.
- Compute median elapsed time.
- Assert threshold by scenario type.

Threshold constants currently in code:

- overlay toggle: `1000ms`
- discovery apply: `1000ms`
- stream status update: `3000ms`
- fallback warning: `3000ms`

## 6. Release Hardening Validation

Release checks are enforced in `app/build.gradle.kts` (`verifyReleaseHardening`):

- release `isMinifyEnabled == true`
- release `isShrinkResources == true`

Run:

```powershell
./gradlew.bat verifyReleaseHardening
./gradlew.bat :app:assembleRelease
```

## 7. Contract Correlation

When tests fail, map them to feature contracts:

- Spec 001 contracts: `specs/001-scan-ndi-sources/contracts/`
- Spec 002 contracts: `specs/002-stream-ndi-source/contracts/`
- Spec 006 contracts: `specs/006-settings-menu/contracts/ndi-settings-feature-contract.md`

Use this mapping to determine whether failures are implementation bugs, test regressions, or contract drift.

## 8. Regression-First Test Triage Policy

When a pre-existing automated test fails during feature work, treat the failure
as evidence of a regression in production code, shared integration behavior, or
environment setup first.

- Do not weaken, delete, or rewrite an existing test as the initial fix.
- Change production code first unless the feature request explicitly changes the
  behavior that the test covers.
- Change an existing test only when one of these is true:
  - the requested feature changes the documented behavior or contract;
  - the test is independently proven incorrect, obsolete, or flaky; or
  - supporting infrastructure changed and the old assertion is no longer the
    correct way to verify the intended behavior.
- Any change to an existing test must be minimal and should be traceable to the
  specific feature requirement or contract that required the edit.

This policy applies to JUnit, instrumentation, and Playwright tests.

## 9. Feature 021 Validation Snapshot (Viewer Persistence + Source Availability)

Validation date: 2026-03-29

Completed gates:

- Environment preflight pass (`scripts/verify-android-prereqs.ps1`, `./gradlew.bat --version`)
- Source-list focused unit tests pass (`:feature:ndi-browser:presentation:testDebugUnitTest --tests "com.ndi.feature.ndibrowser.source_list.*"`)
- Module unit test suites pass (`:feature:ndi-browser:data:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest`)
- Release hardening pass (`:app:verifyReleaseHardening`)

Deferred gates:

- Playwright e2e execution for feature 021 is intentionally deferred while e2e scenarios are rebuilt.
- Deferment evidence and unblock command are tracked in `test-results/021-us2-playwright-regression.md`.

## 10. Feature 025 Validation Snapshot (Fix Appearance Settings)

Validation date: 2026-03-31

Completed gates:

- Android preflight and emulator evidence (`test-results/025-preflight-android-prereqs.md`)
- Playwright command-contract validation (`test-results/025-preflight-node-playwright.md`)
- Targeted appearance e2e scenarios for US1 and US2 (`test-results/025-us1-targeted-e2e.md`, `test-results/025-us2-targeted-e2e.md`)
- Full appearance suite execution (`test-results/025-e2e-suite-rebuild-summary.md`)
- Primary regression profile execution (`test-results/025-final-regression-summary.md`)
- Release hardening verification (`test-results/025-release-hardening.md`)

Feature-specific notes:

- Hybrid Light/Dark validation uses persisted mode plus a deterministic app-bar luminance bucket token helper in `testing/e2e/tests/support/android-ui-driver.ts`.
- Theme-mode latency validation is tracked as an automated assertion in the appearance suite with a <=1000ms threshold.

## 11. Feature 029 Validation Snapshot (NDI Discovery Server Compatibility)

Validation date: 2026-04-07

Completed gates:

- US1 discovery compatibility behavior tests pass:
  - `:feature:ndi-browser:data:testDebugUnitTest --tests "com.ndi.feature.ndibrowser.data.NdiDiscoveryRepositoryContractTest" --tests "com.ndi.feature.ndibrowser.data.DiscoveryCompatibilityClassifierTest"`
  - `:feature:ndi-browser:presentation:testDebugUnitTest --tests "com.ndi.feature.ndibrowser.source_list.SourceListViewModelTest"`
- US2 matrix and diagnostics repository tests pass:
  - `:feature:ndi-browser:data:testDebugUnitTest --tests "com.ndi.feature.ndibrowser.data.DiscoveryCompatibilityMatrixRepositoryTest" --tests "com.ndi.feature.ndibrowser.data.DeveloperDiagnosticsRepositoryImplTest"`
- US3 diagnostics mapping/rendering tests pass:
  - `:feature:ndi-browser:presentation:testDebugUnitTest --tests "com.ndi.feature.ndibrowser.settings.DeveloperOverlayStateMapperTest" --tests "com.ndi.feature.ndibrowser.settings.DiscoveryServerSettingsViewModelTest"`
- Diagnostics Playwright scenario pass:
  - `npx playwright test testing/e2e/tests/029-discovery-compatibility-diagnostics.spec.ts`
- US3 profile regression pass:
  - `pwsh -NoProfile -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-matrix-e2e.ps1 -Profiles us3-only`

Validation posture note:

- Phase tasks are complete for code-level and harness validation; runtime per-version endpoint verification remains operationally blocked until baseline and venue endpoint/version capture is provided.

Evidence artifacts:

- `test-results/029-compatibility-matrix.md`
- `test-results/029-us1-venue-discovery.md`
- `test-results/029-us2-test-change-traceability.md`
- `test-results/029-us3-compatibility-diagnostics.md`
- `test-results/029-us3-regression.md`
- `test-results/029-us3-test-change-traceability.md`
- `test-results/029-us1-test-change-traceability.md`
- `test-results/029-unit-regression.md`
- `test-results/029-release-hardening.md`
- `test-results/029-final-validation-summary.md`

Current blocker classification:

- Runtime per-version baseline/venue endpoint validation remains `blocked` until target endpoint host and exact server version are captured in `specs/029-ndi-server-compatibility/validation/server-targets.md`.
