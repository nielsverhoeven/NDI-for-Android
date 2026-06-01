# US3 Validation: Deterministic Dual-Emulator Verification Flow

## Scope
User Story 3 validates that the dual-emulator scenario enforces a strict six-step execution sequence with fail-fast semantics and explicit per-step diagnostic reporting.

## Implemented Changes

- `testing/e2e/tests/support/scenario-checkpoints.ts`:
  - `ScenarioCheckpointRecorder` class enforcing sequential begin/pass/fail calls.
  - Out-of-order `begin()` calls throw `Scenario step order violation` immediately.
  - Failed step marks all subsequent steps `SKIPPED` in the artifact timeline.
  - `writeArtifact(path)` persists the timeline regardless of pass or failure.
  - `createScenarioCheckpointRecorder()` factory function.

- `testing/e2e/tests/interop-dual-emulator.spec.ts`:
  - `beginStep` / `passStep` helpers drive strict sequential execution.
  - `catch` block calls `checkpoints.fail(activeStep, reason)` on any step error.
  - `finally` block calls `checkpoints.writeArtifact(checkpointArtifactPath)` unconditionally.

- `testing/e2e/scripts/run-dual-emulator-e2e.ps1`:
  - `finally` block reads `scenario-checkpoints.json` after Playwright exits.
  - Prints `FAILED STEP`, `REASON`, and artifact path to stdout when a step fails.

- `testing/e2e/tests/support/android-ui-driver.ts`:
  - `STATIC_DELAY_MAX_MS` constant exported (2000 ms policy guard).
  - `assertAllowedStaticDelay(ms)` exported; throws on >2000 ms to keep static sleeps bounded.

## Test Evidence

### Unit Tests — Ordered-step enforcement (T030)
Command:
```powershell
npm --prefix testing/e2e run test -- tests/support/scenario-checkpoints.spec.ts --project=android-dual-emulator
```
Result: PASS (9/9)

Covered scenarios:
- `recorder rejects out-of-order step begin`
- `recorder accepts steps in declared order`
- `failing a step marks trailing steps as SKIPPED`
- `failedStepIndex and failedStepName captured on failure`
- `timeline contains exactly six ordered checkpoints`
- `timeline has runStartedAtEpochMillis set on creation`
- `fresh timeline has PENDING checkpoints and no failure fields`
- `finish sets runFinishedAtEpochMillis`
- `passed step has timestamps populated`

### Unit Tests — Policy guard (T032 / assertAllowedStaticDelay)
Command:
```powershell
npm --prefix testing/e2e run test -- tests/support/android-ui-driver.spec.ts --project=android-dual-emulator
```
Result: PASS (3/3)

Covered scenarios:
- `assertAllowedStaticDelay accepts policy maximum`
- `assertAllowedStaticDelay rejects values above policy maximum`

### Runtime Integration — All-support specs combined
Command:
```powershell
npm --prefix testing/e2e run test -- tests/support/ --project=android-dual-emulator --reporter=list
```
Result: PASS (19/19)

All `@us2` and `@us3` tagged support unit tests pass.

### End-to-End — Full dual-emulator harness
Command:
```powershell
powershell -ExecutionPolicy Bypass -File testing/e2e/scripts/run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556
```
Result: PASS (2/2 scenarios)
- `@dual-emulator publish discover play stop interop`
- `@dual-emulator restart output with new stream name remains discoverable`

Checkpoint artifact produced at:
`testing/e2e/artifacts/dual-emulator-20260320-141359/scenario-checkpoints.json`

## Outcome
US3 is fully validated. Step ordering is strictly enforced; fail-fast semantics are proven by unit tests; failed-step diagnostics appear in runner stdout; checkpoint timeline artifacts are captured on every run.
