# Data Model: Background Stream Persistence

## Entity: StreamContinuitySession

- Purpose: Canonical continuity snapshot for an active broadcaster output session when app visibility changes.
- Fields:
  - sessionId (string, required)
  - inputSourceId (string, required)
  - outboundStreamName (string, required)
  - outputState (enum, required): READY | STARTING | ACTIVE | STOPPING | STOPPED | INTERRUPTED
  - hasActiveOutput (boolean, required)
  - runningWhileBackgrounded (boolean, required)
  - backgroundReason (enum, optional): NONE | APP_BACKGROUND
  - startedAtEpochMillis (long, required)
  - lastUpdatedAtEpochMillis (long, required)
- Validation rules:
  - `runningWhileBackgrounded` can only be true when `outputState` is ACTIVE.
  - `backgroundReason` must be APP_BACKGROUND when `runningWhileBackgrounded` is true.
  - Explicit stop transitions must set `outputState` to STOPPED and `runningWhileBackgrounded` to false.

## Entity: ViewerContinuityObservation

- Purpose: Receiver-side evidence that playback remained continuous while broadcaster app context changed.
- Fields:
  - observationId (string, required)
  - receiverSerial (string, required)
  - playbackState (enum, required): IDLE | CONNECTING | PLAYING | INTERRUPTED | STOPPED
  - selectedSourceId (string, required)
  - viewerSurfaceVisible (boolean, required)
  - nonBlackRatio (decimal, required)
  - meanAbsoluteDeltaFromBaseline (decimal, required)
  - similarityToPublisherFrame (decimal, required)
  - observedAtEpochMillis (long, required)
- Validation rules:
  - Continuity checkpoints require `playbackState = PLAYING` and `viewerSurfaceVisible = true`.
  - Similarity and delta metrics must exceed scenario thresholds before checkpoint passes.

## Entity: BackgroundNavigationStep

- Purpose: Ordered broadcaster-side transition checkpoints for the six-step scenario.
- Fields:
  - stepIndex (int, required, range 1..6)
  - stepName (enum, required): START_STREAM_A | START_VIEW_B | OPEN_CHROME_A | VERIFY_CHROME_ON_B | OPEN_NOS_A | VERIFY_NOS_ON_B
  - actorRole (enum, required): PUBLISHER | RECEIVER
  - status (enum, required): PENDING | PASSED | FAILED
  - failureReason (string, optional)
  - completedAtEpochMillis (long, optional)
- Validation rules:
  - Step indices must be unique and strictly increasing.
  - Any FAILED step stops execution and marks remaining steps as not executed.

## Entity: BrowserProjectionCheckpoint

- Purpose: Specialized cross-app visual checkpoint proving browser transitions are projected to receiver.
- Fields:
  - checkpointId (string, required)
  - checkpointType (enum, required): CHROME_VISIBLE | NOS_VISIBLE
  - publisherEvidencePath (string, required)
  - receiverEvidencePath (string, required)
  - uiSnapshotPath (string, optional)
  - logcatSnapshotPath (string, optional)
  - passed (boolean, required)
- Validation rules:
  - `CHROME_VISIBLE` must pass before `NOS_VISIBLE` is evaluated.
  - `NOS_VISIBLE` requires URL navigation step to have completed on publisher device.

## Entity: DualEmulatorRunReport

- Purpose: Aggregate execution and diagnostics record for one dual-emulator run.
- Fields:
  - runId (string, required)
  - publisherSerial (string, required)
  - receiverSerial (string, required)
  - startedAtEpochMillis (long, required)
  - finishedAtEpochMillis (long, optional)
  - outcome (enum, required): PASS | FAIL
  - failedStepIndex (int, optional)
  - failedStepName (string, optional)
  - artifactsDir (string, required)
- Validation rules:
  - FAIL outcomes must include `failedStepIndex` and `failedStepName`.
  - PASS outcome requires all six ordered steps to be PASSED.

## Relationships

- `StreamContinuitySession` drives source-of-truth continuity state during broadcaster background transitions.
- `BackgroundNavigationStep` records ordered execution for each scenario action and gates subsequent actions.
- `BrowserProjectionCheckpoint` validates receiver visibility for steps 4 and 6 using evidence tied to `ViewerContinuityObservation`.
- `DualEmulatorRunReport` aggregates all steps/checkpoints and points to artifact evidence.

## State Transitions

- Stream continuity flow:
  - READY/STARTING -> ACTIVE (after start output)
  - ACTIVE + Home/App switch -> ACTIVE with `runningWhileBackgrounded=true`
  - ACTIVE + explicit stop -> STOPPING -> STOPPED
- Six-step test flow:
  - START_STREAM_A -> START_VIEW_B -> OPEN_CHROME_A -> VERIFY_CHROME_ON_B -> OPEN_NOS_A -> VERIFY_NOS_ON_B
  - On first failed transition, scenario terminates and run report records failed step.

## Runtime Artifacts (Implemented)

The `DualEmulatorRunReport` entity is realized at runtime as:

```
testing/e2e/artifacts/dual-emulator-<timestamp>/scenario-checkpoints.json
```

Produced by `ScenarioCheckpointRecorder` in
`testing/e2e/tests/support/scenario-checkpoints.ts`.

Schema alignment:
- `runStartedAtEpochMillis` / `runFinishedAtEpochMillis` → run timestamps
- `failedStepIndex` / `failedStepName` → FAIL outcome fields
- `checkpoints[N].status` (PENDING | PASSED | FAILED | SKIPPED) → per-step `BackgroundNavigationStep.status`
- `checkpoints[N].failureReason` → `BackgroundNavigationStep.failureReason`

