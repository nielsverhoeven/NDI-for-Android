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
