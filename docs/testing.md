<!-- Last updated: 2026-03-20 -->

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

## 8. Feature 021 Validation Snapshot (Viewer Persistence + Source Availability)

Validation date: 2026-03-29

Completed gates:

- Environment preflight pass (`scripts/verify-android-prereqs.ps1`, `./gradlew.bat --version`)
- Source-list focused unit tests pass (`:feature:ndi-browser:presentation:testDebugUnitTest --tests "com.ndi.feature.ndibrowser.source_list.*"`)
- Module unit test suites pass (`:feature:ndi-browser:data:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest`)
- Release hardening pass (`:app:verifyReleaseHardening`)

Deferred gates:

- Playwright e2e execution for feature 021 is intentionally deferred while e2e scenarios are rebuilt.
- Deferment evidence and unblock command are tracked in `test-results/021-us2-playwright-regression.md`.
