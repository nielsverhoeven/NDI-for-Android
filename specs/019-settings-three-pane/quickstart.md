# Quickstart: Three-Column Settings Layout Validation

## 1. Prerequisites

- Windows host with Android SDK and emulator tooling configured.
- Repository-compatible Java/JBR baseline available.
- NDI SDK prerequisites installed where required by existing project setup.
- At least one emulator/device profile that satisfies existing wide-layout criteria and one compact phone profile.

## 2. Preflight

Run prerequisite validation before feature tests:

```powershell
./scripts/verify-android-prereqs.ps1
```

Optional dual-emulator preflight when using dual-profile validation:

```powershell
./scripts/verify-e2e-dual-emulator-prereqs.ps1 -AllowMissingNdiSdk
```

If preflight fails, mark gate as `BLOCKED-ENV`, attach logs, and include concrete retry/unblock steps.

## 3. Build

```powershell
./gradlew.bat :app:assembleDebug
```

## 4. Test-First Execution (Red-Green-Refactor)

1. Add failing unit tests first for:
   - layout-mode selection using existing wide-layout criteria
   - selected settings category preservation across layout transitions
   - detail-state behavior when selected category has no adjustable controls
2. Implement minimal code to satisfy failing tests.
3. Refactor while retaining green tests.

Suggested test targets:

```powershell
./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest
./gradlew.bat :feature:ndi-browser:data:testDebugUnitTest
./gradlew.bat :app:testDebugUnitTest
```

## 5. Playwright Emulator Validation

Install and prepare e2e dependencies:

```powershell
npm --prefix testing/e2e ci
npm --prefix testing/e2e run preflight:dual-emulator
```

Run feature-focused e2e coverage:

```powershell
npm --prefix testing/e2e run test -- tests/settings-three-column-layout.spec.ts
```

Run primary regression gate:

```powershell
npm --prefix testing/e2e run test:pr:primary
```

## 6. Manual Acceptance Spot Checks

- Settings opens in three columns on wide-layout profile.
- Column 1 contains Home, Stream, View, Settings.
- Column 2 category selection updates only column 3 details.
- Selected category indication remains clear after navigation and return.
- Non-wide layout uses compact settings flow.
- Rotation/layout change preserves selected category context when available.

## 7. Material 3 and Architecture Checks

- Verify updated settings UI follows Material 3 patterns used in existing settings screens.
- Confirm business logic remains in ViewModel with Fragment/UI as rendering and intent dispatch only.

## 8. Release Hardening Gate

```powershell
./gradlew.bat :app:verifyReleaseHardening :app:assembleRelease
```

## 9. Evidence Capture

Collect and retain:

- `testing/e2e/artifacts/**`
- `testing/e2e/playwright-report/**`
- unit test reports in `**/build/reports/tests/**`

For blocked gates, record:

- failing command
- environment blocker classification
- exact unblock/retry command

## 10. Command-to-Evidence Trace

| Task | Command | Evidence File |
|---|---|---|
| T001 | `./scripts/verify-android-prereqs.ps1` | `test-results/019-settings-three-pane-preflight.md` |
| T002 | `./scripts/verify-e2e-dual-emulator-prereqs.ps1 -AllowMissingNdiSdk` | `test-results/019-settings-three-pane-preflight.md` |
| T003 | `./gradlew.bat :app:assembleDebug` | `test-results/019-settings-three-pane-preflight.md` |
| T019/T031/T042 | `npm --prefix testing/e2e run test:pr:primary` | `test-results/019-settings-three-pane-validation.md` |
| T052 | `npm --prefix testing/e2e run test -- tests/settings-three-column-layout.spec.ts` | `test-results/019-settings-three-pane-validation.md` |
| T051 | `./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest :feature:ndi-browser:data:testDebugUnitTest :app:testDebugUnitTest` | `test-results/019-settings-three-pane-validation.md` |
| T053 | `./gradlew.bat :app:verifyReleaseHardening :app:assembleRelease` | `test-results/019-settings-three-pane-validation.md` |
