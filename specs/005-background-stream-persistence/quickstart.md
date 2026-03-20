# Quickstart: Background Stream Persistence with Chrome/nos.nl Dual-Emulator Validation

## 1. Prerequisites

From repository root:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
.\gradlew.bat --version
npm --prefix testing/e2e ci
adb devices
```

Expected environment:

- Two Android emulators connected with distinct serials.
- Debug app installed on both emulators.
- Emulator windows visible during run.
- Chrome available on Emulator A.
- Network access available for `https://nos.nl`.

## 2. Test-First Development Sequence

1. Add failing tests first:
   - Unit/integration assertions for stream continuity when app moves to Home/another app.
   - New failing dual-emulator e2e scenario for six ordered steps.
2. Implement minimum continuity and e2e changes to satisfy failing tests.
3. Refactor safely with all tests green.
4. Preserve diagnostics and artifact capture behavior.

## 2.1 Feature-Specific Execution Checklist

- [ ] Stream remains ACTIVE after leaving app to Home on broadcaster.
- [ ] Stream remains ACTIVE after opening another app (Chrome) on broadcaster.
- [ ] Viewer remains in PLAYING while broadcaster app context changes.
- [ ] E2E executes all six requested steps in strict order.
- [ ] Step 4 proves Chrome visibility on receiver via visual evidence.
- [ ] Step 6 proves nos.nl visibility on receiver via visual evidence.
- [ ] Any failed step reports exact checkpoint and exits non-zero.

## 2.2 Scenario Execution Notes

- Use the checkpoint timeline artifact to record strict six-step ordering and fail-fast status.
- Keep publisher and receiver screenshots for each checkpoint under one run-scoped artifact folder.
- Preserve receiver UI dump and logcat snapshots whenever a checkpoint fails.
- Record the artifact directory path and failed step (if any) in the quickstart validation report.

## 3. Build and Validation Commands

```powershell
.\gradlew.bat test
.\gradlew.bat connectedAndroidTest
.\gradlew.bat :app:assembleDebug
```

Run dual-emulator preflight:

```powershell
powershell -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556 -PreflightOnly
```

Run dual-emulator suite:

```powershell
powershell -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556
```

Optional targeted run (when scenario tag is available):

```powershell
npm --prefix testing/e2e run test:dual-emulator -- --grep "background stream persists"
```

## 4. Functional Validation Checklist

- Broadcaster output starts and reaches ACTIVE.
- Viewer discovers and reaches PLAYING.
- Broadcaster navigates away from app without stopping output.
- Viewer keeps receiving live content throughout app switch.

## 5. Six-Step E2E Validation Checklist

1. Start stream on Emulator A.
2. Start viewing stream on Emulator B.
3. Navigate Emulator A to Chrome app.
4. Confirm Chrome app view is visible on Emulator B.
5. Navigate Emulator A to `https://nos.nl` in Chrome.
6. Confirm nos.nl page is visible on Emulator B.

## 6. Artifact and Diagnostics Expectations

Artifacts should be present under:

- `testing/e2e/artifacts/dual-emulator-<timestamp>/`
- `testing/e2e/test-results/`

At minimum capture:

- Preflight and post-run screenshots for both emulator roles.
- Receiver viewer-surface evidence around Chrome and nos.nl checkpoints.

## 7. Deterministic-Flow Instructions (US3)

The dual-emulator scenario is deterministic and fails fast on the first step that does not complete successfully.

### Step enforcement

- The six checkpoint steps are enforced in strict sequential order by `createScenarioCheckpointRecorder()` in `testing/e2e/tests/support/scenario-checkpoints.ts`.
- Starting a step out of sequence throws a `Scenario step order violation` error immediately.
- Any step failure marks all subsequent steps `SKIPPED` in the checkpoint timeline.

### Fail-fast semantics

- On any step error, the scenario catches the exception, calls `checkpoints.fail(activeStep, reason)`, and re-throws so Playwright marks the test as failed.
- The checkpoint artifact is always written in the `finally` block regardless of pass/fail.
- The runner script reads the checkpoint artifact after Playwright exits and prints the failed step name and reason to the console before the script exits.

### Reading checkpoint diagnostics

After a run (passed or failed), the checkpoint timeline is at:

```
testing/e2e/artifacts/dual-emulator-<timestamp>/scenario-checkpoints.json
```

Fields to note:
- `failedStepName` — name of the first step that failed, or `null` on full pass.
- `failedStepIndex` — ordinal index (1–6) of the failed step.
- `checkpoints[N].failureReason` — exact error message from the failing assertion or timeout.

The runner also prints a summary to `stdout` when a step fails:

```
==========================================
FAILED STEP: <step-name> (step <N>)
REASON: <error detail>
Checkpoint artifact: <path>
==========================================
```

### Extending or modifying steps

- All step names are declared as the `SixStepCheckpointName` union type in `scenario-checkpoints.ts`.
- Add new steps by extending the type union and appending to `ORDERED_STEPS`.
- The recorder enforces the order array is exhausted in sequence — no step can be skipped.
- Step-level failure context (if any) with UI snapshot and logcat attachments.

## 7. Release-Grade Validation

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
.\gradlew.bat verifyReleaseHardening
.\gradlew.bat :app:assembleRelease
```

Include dual-emulator evidence artifacts in release validation notes.
