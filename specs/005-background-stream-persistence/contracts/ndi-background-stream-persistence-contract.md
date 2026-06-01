# Contract: Background Stream Persistence and Cross-App Viewer Continuity

## 1. Stream Continuity Contract

### 1.1 ActiveOutputBackgroundContinuityContract

Behavioral requirements:

- When broadcaster output is ACTIVE, navigating to Home must not stop output.
- When broadcaster output is ACTIVE, opening another app must not stop output.
- Output stop is only valid on explicit user stop action or terminal interruption event.
- Continuity behavior must preserve existing repository-mediated state updates.

Guarantees:

- Viewer sessions connected to the active stream continue receiving live updates across broadcaster app switches.
- No autoplay or autonomous output restart is introduced.

## 2. Repository and ViewModel Contract

### 2.1 StreamContinuityRepositoryContract

Inputs:

- captureLastKnownState()
- clearTransientStateOnExplicitStop()
- observeContinuityState()

Outputs:

- Stream continuity state aligned with active output lifecycle and background transitions.

Rules:

- Must not emit stopped state solely due to top-level navigation or app backgrounding.
- Must clear transient continuity only when explicit stop action is processed.

### 2.2 OutputFlowViewModelContract

Inputs:

- start output action
- explicit stop output action
- app lifecycle/navigation background transitions

Outputs:

- UI state with ACTIVE/STOPPED semantics
- telemetry/checkpoints for continuity transitions

Rules:

- Background transitions cannot trigger implicit stop.
- Explicit stop must always transition output to STOPPED.

## 3. Dual-Emulator Six-Step E2E Contract

### 3.1 OrderedScenarioContract

Required ordered scenario steps (must execute in this exact order):

1. Start a stream on Emulator A.
2. Start viewing that stream on Emulator B.
3. Navigate to Chrome app on Emulator A.
4. Validate Chrome navigation is visible in viewer on Emulator B.
5. Navigate to `https://nos.nl` in Chrome on Emulator A.
6. Validate `https://nos.nl` is visible in viewer on Emulator B.

Rules:

- The runner must fail immediately when any step fails.
- Failure output must indicate exact failed step index and checkpoint name.

### 3.2 CrossAppProjectionValidationContract

Validation requirements:

- Step 4 requires evidence that receiver viewer surface changed from pre-Chrome baseline and matches publisher Chrome frame sufficiently.
- Step 6 requires evidence that receiver viewer surface changed to nos.nl content and remains in PLAYING state.
- Pure text-label checks are insufficient without visual evidence.

## 4. Observability Contract

Required artifacts/events:

- Step-level pass/fail timeline for all six steps.
- Publisher and receiver screenshots at pre/post checkpoints.
- Receiver viewer-surface evidence for Chrome-visible and nos.nl-visible checkpoints.
- UI snapshot and logcat attachments on failure.

Payload constraints:

- No raw media payload storage beyond test screenshot artifacts.
- No PII included in telemetry payloads.

## 5. Security and Permission Contract

- No additional dangerous Android permissions are introduced.
- Existing MediaProjection consent flow remains mandatory before active output.
- App behavior remains within existing least-permission model.

## 6. Release Validation Contract

Completion evidence must include:

- Failing-first and passing tests for continuity behavior in unit/e2e layers.
- Successful dual-emulator run covering all six ordered steps.
- Run artifacts demonstrating Chrome and nos.nl visibility on receiver.
- Release hardening verification (`verifyReleaseHardening`) retained in delivery checklist.
