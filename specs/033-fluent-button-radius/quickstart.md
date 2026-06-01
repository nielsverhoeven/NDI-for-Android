# Quickstart: Validate Fluent Button Radius Alignment

## Goal

Validate that in-scope app buttons use a strict uniform less-rounded Fluent-aligned corner profile across Home, Source List, Viewer, Output, and Settings, with no behavior regressions.

## 1. Preflight

Run prerequisite checks before feature validation:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
pwsh ./scripts/verify-e2e-dual-emulator-prereqs.ps1
adb devices
```

Validate Playwright command/runtime contract:

```powershell
pwsh -ExecutionPolicy Bypass -File ./testing/e2e/scripts/validate-command-contract.ps1
npm --prefix testing/e2e exec playwright --version
```

If preflight fails, classify result as `BlockedEnvironment` and log remediation in `test-results/033-button-radius-regression.md`.

## 2. Test-First Workflow

1. Add/adjust failing tests that assert button corner profile consistency where automated coverage exists.
2. Capture red-state output.
3. Implement minimal visual-only changes to pass tests.
4. Refactor without altering behavior.

## 3. Flow Validation

Validate each in-scope flow:

1. Home button surfaces
2. Source List button surfaces
3. Viewer button surfaces
4. Output button surfaces
5. Settings button surfaces

Checks per flow:

- Less-rounded corner shape present
- Same corner profile across roles/states in scope
- No change in button-triggered behavior

## 4. Playwright Gates

Run feature and regression suites:

```powershell
npm --prefix testing/e2e exec playwright test tests/033-fluent-button-radius.spec.ts
npm --prefix testing/e2e run test:pr:primary
```

## 5. Unit and Release Gates

```powershell
./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest -x lint
./gradlew.bat :app:testDebugUnitTest -x lint
./gradlew.bat :app:verifyReleaseHardening
```

## 6. Evidence Artifacts

Record outcomes under `test-results/` with pass/fail/blocked classification:

- `test-results/033-button-radius-flow-evidence.md`
- `test-results/033-button-radius-regression.md`
- `test-results/033-button-radius-release-hardening.md`

Include:

- Commands executed
- Result status
- Behavior-regression note (expected: none)
- Blocker remediation if blocked
