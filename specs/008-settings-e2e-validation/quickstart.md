# Quickstart: Settings Menu End-to-End Emulator Validation

## 1. Prerequisites

From repository root:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
./gradlew.bat --version
npm --prefix testing/e2e ci
adb devices
```

Expected:
- Android prerequisites pass.
- Emulator(s) available.
- Playwright dependencies installed for `testing/e2e`.

## 2. Primary PR Validation Run (Required)

Run full required e2e set on the primary emulator profile.

```powershell
$env:EMULATOR_A_SERIAL="emulator-5554"
$env:APP_PACKAGE="com.ndi.app.debug"
npm --prefix testing/e2e run test:dual-emulator
```

Minimum PR evidence to capture:
- New settings scenarios: pass/fail summary.
- Existing regression suite: pass/fail summary.
- Run status: complete and passing.

## 3. Scheduled Matrix Validation Run (Required)

Run full required e2e set across scheduled matrix profiles.

```powershell
# Example scheduled command shape; CI may fan this out per profile
$env:EMULATOR_A_SERIAL="emulator-5554"
$env:EMULATOR_B_SERIAL="emulator-5556"
$env:APP_PACKAGE="com.ndi.app.debug"
npm --prefix testing/e2e run test:dual-emulator
```

Matrix run expectations:
- All configured profiles execute.
- Any profile failure is triaged before release sign-off.

## 4. Required Settings Scenarios to Include

- Source List -> Settings -> Back.
- Viewer -> Settings -> Back.
- Output Control -> Settings -> Back.
- Valid discovery endpoint persistence.
- Invalid discovery endpoint validation feedback.
- Unreachable endpoint fallback warning behavior.

## 5. Regression Preservation Rule

In every validation cycle for this feature:
- Existing Playwright e2e scenarios are mandatory.
- Partial or aborted runs are non-compliant and must be rerun.

## 6. Evidence Locations

Collect and retain artifacts under:
- `testing/e2e/artifacts/`
- `testing/e2e/test-results/`
- `test-results/android-test-results.md` (summary entry)

## 7. Troubleshooting

- If emulator fails to boot, fail the run with explicit diagnostics and rerun.
- If suite aborts midway, treat as failed gate and rerun to completion.
- If locale/device profile differences affect fragile selectors, prefer stable user-outcome assertions.
