# US2 Validation: Cross-App Content Propagation

## Scope
User Story 2 validates that broadcaster transitions to Chrome and `https://nos.nl` propagate to the receiver viewer surface.

## Implemented Changes
- Added reusable app-switch helpers in `android-ui-driver.ts` (`pressHome`, `launchChrome`, `launchChromeUrl`).
- Extended dual-emulator interop scenario with:
  - Step 4: Chrome visible on receiver assertion.
  - Step 6: nos.nl visible on receiver assertion.
- Added per-checkpoint screenshot and JSON diagnostic attachments for Chrome and nos checkpoints.
- Added visual assertion support tests in `testing/e2e/tests/support/visual-assertions.spec.ts`.

## Test Evidence

### Visual Assertion Support Tests
Command:

```powershell
npm --prefix testing/e2e run test -- tests/support/visual-assertions.spec.ts --project=android-dual-emulator
```

Result: PASS (3/3)

Latest run:
- Date: 2026-03-20
- Duration: 6.8s

Covered checks:
- Non-black visibility detection.
- Baseline-delta detection.
- Publisher-reference similarity comparison.

### Dual-Emulator Runtime Scenario
- Command:

```powershell
powershell -ExecutionPolicy Bypass -File testing/e2e/scripts/run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556
```

- Result: PASS (2/2)
  - `@dual-emulator publish discover play stop interop`
  - `@dual-emulator restart output with new stream name remains discoverable`
- Runtime: 5.0m

Primary artifact set:
- `testing/e2e/artifacts/dual-emulator-20260320-141359/scenario-checkpoints.json`
- `testing/e2e/artifacts/dual-emulator-20260320-141359/screenshots/publisher-chrome.png`
- `testing/e2e/artifacts/dual-emulator-20260320-141359/screenshots/receiver-chrome.png`
- `testing/e2e/artifacts/dual-emulator-20260320-141359/screenshots/publisher-nos.png`
- `testing/e2e/artifacts/dual-emulator-20260320-141359/screenshots/receiver-nos.png`

## Outcome
US2 is fully validated at helper and runtime levels. Cross-app Chrome and nos.nl propagation is confirmed on the receiver with checkpoint diagnostics and screenshots captured.
