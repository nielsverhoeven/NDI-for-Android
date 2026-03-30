# Quickstart: NDI Screen Share Output Redesign Validation

## 1. Prerequisites

- Windows host with Android SDK tooling on PATH.
- Java/JDK aligned with repository baseline.
- NDI Android SDK present per docs/android-prerequisites.md.
- Two emulators available for end-to-end validation.

## 2. Preflight

Run project prerequisite checks first:

```powershell
./scripts/verify-android-prereqs.ps1
```

Optional dual-emulator preflight evidence:

```powershell
./scripts/verify-e2e-dual-emulator-prereqs.ps1 -AllowMissingNdiSdk
```

Record blocked status with command output and missing dependency if either fails.

## 3. Build

```powershell
./gradlew.bat :app:assembleDebug
./gradlew.bat :ndi:sdk-bridge:assembleRelease
```

## 4. Test-First Execution

1. Add/update failing unit tests for output consent reset, discovery-mode decision, and unreachable configured discovery behavior.
2. Implement minimal changes to pass tests.
3. Refactor while preserving behavior.

Suggested test targets:

```powershell
./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest
./gradlew.bat :feature:ndi-browser:data:testDebugUnitTest
```

## 5. Emulator Playwright Validation

Install e2e dependencies and browsers:

```powershell
npm --prefix testing/e2e ci
npm --prefix testing/e2e run preflight:dual-emulator
```

Run dual-emulator scenario:

```powershell
npm --prefix testing/e2e run test:dual-emulator
```

Run full primary regression gate:

```powershell
npm --prefix testing/e2e run test:pr:primary
```

## 6. Manual Acceptance Spot Checks

- Output tab is visually distinct from Viewer tab.
- Start requires consent every new session.
- Stop clears consent and restart prompts again.
- Active stream persists when app is backgrounded.
- Configured reachable discovery server path works.
- No configured discovery server path uses mDNS.
- Configured unreachable discovery server blocks start and shows clear error.

## 6.1 Material 3 Verification

- Verify updated Output screen components, hierarchy, spacing, typography, color usage, and interaction affordances follow Material 3 patterns used in this repository.
- Record verification results and any deviations in `test-results/017-ndi-screen-output-validation.md`.

## 6.2 Timed Discovery/Playback Assertion

- In dual-emulator validation, assert receiver visibility occurs within 10 seconds for configured reachable server mode and mDNS mode.
- Record measured timings in `test-results/017-ndi-screen-output-validation.md`.

## 7. Release Hardening Gate

```powershell
./gradlew.bat :app:verifyReleaseHardening :app:assembleRelease
```

## 8. Evidence Capture

Collect and keep artifacts/logs:

- testing/e2e/artifacts/**
- testing/e2e/playwright-report/**
- relevant unit test reports under module build directories

For blocked gates, classify as environment-blocked vs code-failure and include exact unblocking step.

## 9. Task Trace

- T001-T004 evidence: test-results/017-ndi-screen-output-preflight.md
- T005 and final gates evidence: test-results/017-ndi-screen-output-validation.md
- Feature Playwright spec: testing/e2e/tests/output-screen-share.spec.ts
- Feature Playwright helpers: testing/e2e/tests/support/output-screen-share-helpers.ts
