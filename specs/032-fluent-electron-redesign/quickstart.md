# Quickstart: Validate Fluent + Electron UX Redesign

## Goal

Validate phased Fluent + Electron redesign delivery for in-scope flows (Source List, Viewer, Output, Settings, top-level nav shell) while preserving existing behavior and regression safety.

## 1. Preflight

Run required prerequisite checks before implementation and e2e gates:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
pwsh ./scripts/verify-e2e-dual-emulator-prereqs.ps1
adb devices
```

Validate Playwright runtime contract:

```powershell
pwsh -ExecutionPolicy Bypass -File ./testing/e2e/scripts/validate-command-contract.ps1
npm --prefix testing/e2e exec playwright --version
```

If any prerequisite fails, classify as `BLOCKED (environment)` and stop before final gates.

## 2. TDD and Implementation Order

1. Add failing tests first for changed user-visible contracts in the current flow slice.
2. Capture red-state evidence.
3. Implement minimal UI/presentation changes to satisfy tests.
4. Refactor while preserving module boundaries and behavior invariants.

## 3. Unit/Module Test Commands

```powershell
./gradlew.bat :feature:ndi-browser:data:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest
```

Run broader suite as needed:

```powershell
./gradlew.bat test
```

## 4. Flow Validation Scenarios

### Scenario A: Navigation shell consistency

1. Launch app and traverse top-level destinations.
2. Verify Fluent + Electron hierarchy, spacing, typography, and state treatment are consistent.

### Scenario B: Source List -> Viewer flow

1. Discover/select a source.
2. Open Viewer.
3. Verify redesigned controls/states and preserved behavior outcomes.

### Scenario C: Source List -> Output flow

1. Select source for output.
2. Verify redesigned output controls and unchanged functional outcomes.

### Scenario D: Settings flow

1. Open Settings.
2. Modify and save a preference.
3. Reopen and verify persistence unchanged under redesigned UI.

### Scenario E: Adaptive and accessibility checks

1. Validate one phone and one tablet profile.
2. Validate portrait and landscape where applicable.
3. Validate increased text scale readability/focusability.

## 5. Playwright and Regression Gates

Run redesigned-flow Playwright coverage:

```powershell
npm --prefix testing/e2e run test:pr:primary
```

Run full existing Playwright regression suite required by pipeline profile and record results.

## 6. Release Hardening Gate

```powershell
./gradlew.bat :app:verifyReleaseHardening
```

## 7. Evidence Recording

Create feature-scoped evidence under `test-results/` for each in-scope screen and flow with:

- Fluent + Electron checklist outcomes
- Traceable links to test outputs/logs/screenshots
- Final status (`Pass`, `Code failure`, `BLOCKED (environment)`)
- Blocker remediation notes where applicable

Recommended artifact naming:

- `test-results/032-fluent-electron-nav-shell.md`
- `test-results/032-fluent-electron-source-list-viewer.md`
- `test-results/032-fluent-electron-output.md`
- `test-results/032-fluent-electron-settings.md`
- `test-results/032-fluent-electron-regression-summary.md`
