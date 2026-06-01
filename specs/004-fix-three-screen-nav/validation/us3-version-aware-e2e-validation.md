# US3 Validation: Version-Aware Unified Dual-Emulator E2E

Date: 2026-03-17
Story: US3

## Scope

- Runtime per-device Android version detection.
- Rolling latest-five support-window evaluation.
- Unsupported-version fail-fast behavior with diagnostics.
- Unified suite branching by per-device consent-flow variant.
- Static delay policy capped at <=1000ms.

## Implemented Changes

- `android-device-fixtures.ts`
  - added rolling support-window helpers (`computeSupportedVersionWindow`, `isMajorVersionSupported`)
  - added explicit fail-fast checker (`assertDeviceVersionSupported`)
  - extended version info to include `majorVersion`
- `android-ui-driver.ts`
  - added reusable consent branch helpers (`resolveConsentFlowVariant`, `completeScreenShareConsent`)
  - enforced static delay policy (`assertAllowedStaticDelay`, max 1000ms)
- `interop-dual-emulator.spec.ts`
  - attached support-window diagnostics to test artifacts
  - switched to role-aware version verification
  - integrated consent branching by `majorVersion`
- `run-dual-emulator-e2e.ps1`
  - added pre-run android version diagnostics file
  - added unsupported-version fail-fast checks with non-zero failure

## Helper Test Execution

Command:

```powershell
npm --prefix testing/e2e run test -- tests/support/android-device-fixtures.spec.ts tests/support/android-ui-driver.spec.ts --reporter=list
```

Result:

- 7 passed, 0 failed

## Notes

Dual-emulator full-run validation is preserved in quickstart flow and should be executed in the target emulator environment for end-to-end runtime evidence.
