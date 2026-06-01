# Quickstart: Validate Fix Appearance Settings

## 1. Preflight environment

1. Run Android prerequisites check:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
```

2. Confirm emulator availability:

```powershell
adb devices
```

- Expected: at least one emulator in `device` state.
- If blocked: record `adb devices` output and rerun preflight after starting/restarting emulator.

## 2. Red-Green test workflow

1. Add/update failing unit tests first (theme propagation and settings persistence behavior).
2. Add/update failing e2e test for Appearance flow.
3. Implement minimal code changes to pass tests.
4. Refactor safely and keep tests green.

## 3. Execute focused verification

1. Run relevant unit tests:

```powershell
./gradlew.bat :app:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest :feature:theme-editor:data:testDebugUnitTest
```

2. Run appearance e2e specs (new/updated):

```powershell
Push-Location ./testing/e2e
npx playwright test tests/024-settings-menu-rebuild.spec.ts
Pop-Location
```

## 4. Run required Playwright regression gate

```powershell
pwsh ./testing/e2e/scripts/run-primary-pr-e2e.ps1 -Profile pr-primary
```

- Required outcome: normalized status `pass`.
- If `blocked`: publish blocker evidence and unblocking command in test-results artifact.

## 6. Evidence mapping

- Preflight: `test-results/025-preflight-android-prereqs.md`
- Playwright command contract: `test-results/025-preflight-node-playwright.md`
- US1 targeted appearance e2e: `test-results/025-us1-targeted-e2e.md`
- US2 targeted appearance e2e: `test-results/025-us2-targeted-e2e.md`
- Full appearance suite: `test-results/025-e2e-suite-rebuild-summary.md`
- Full regression + release hardening summary: `test-results/025-final-regression-summary.md`

## 5. Manual smoke (optional but recommended)

1. Open Settings > Appearance.
2. Save Light mode, then Dark mode, then System Default.
3. Toggle device theme while app is running under System Default.
4. Open Theme Editor from Appearance and select a different accent.
5. Relaunch app and confirm mode/accent persistence.
