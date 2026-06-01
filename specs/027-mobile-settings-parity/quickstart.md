# Quickstart: Mobile Settings Parity

## 1. Prerequisites

1. Android SDK and emulator prerequisites installed.
2. Node dependencies installed for Playwright harness.
3. Emulator/device profiles available:
   - phone baseline profile,
   - phone compact-height profile,
   - tablet reference profile.

## 2. Preflight (Required Before Quality Gates)

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
pwsh ./scripts/verify-e2e-dual-emulator-prereqs.ps1
```

If either command fails for environment reasons, stop and record a blocked result with unblock action.

## 3. Red-Green-Refactor Sequence

1. Add/update failing unit tests for ViewModel/state preservation behavior in impacted feature modules.
2. Add/update failing Playwright e2e scenarios for mobile parity flows.
3. Implement minimal presentation/layout changes.
4. Re-run tests and refactor safely.

## 4. Suggested Local Validation Commands

### Unit + module checks

```powershell
./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest
./gradlew.bat :feature:theme-editor:presentation:testDebugUnitTest
./gradlew.bat :feature:theme-editor:data:test
```

### Targeted Playwright parity tests (add feature-specific spec file during implementation)

```powershell
npx --prefix testing/e2e playwright test testing/e2e/tests/027-mobile-settings-parity.spec.ts
```

### Existing Playwright regression profile

```powershell
npm --prefix testing/e2e run test:pr:primary
```

### Release hardening guard

```powershell
./gradlew.bat verifyReleaseHardening
```

## 5. Evidence to Capture

1. Preflight outputs and pass/fail status.
2. Phone baseline + compact-height parity test results.
3. Tablet reference parity comparison result.
4. Regression suite result (`test:pr:primary`).
5. Failure classification: `code-failure` vs `blocked-environment` with unblock step.

## 6. Current Execution Evidence (Feature 027)

1. `test-results/027-preflight-android-prereqs.md`
2. `test-results/027-preflight-dual-emulator.md`
3. `test-results/027-preflight-node-playwright.md`
4. `test-results/027-us1-phone-section-visibility.md`
5. `test-results/027-us2-cross-screen-parity.md`
6. `test-results/027-us3-orientation-continuity.md`
7. `test-results/027-us1-regression.md`
8. `test-results/027-us2-regression.md`
9. `test-results/027-us3-regression.md`
10. `test-results/027-unit-regression.md`
11. `test-results/027-release-hardening.md`
12. `test-results/027-material3-compliance.md`
13. `test-results/027-final-regression-summary.md`
14. `test-results/027-mobile-settings-parity-summary.md`
