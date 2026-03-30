# Data Model: NDI Output Validation with Local Screen Share and Dual Emulators

## Entity: OutputInputIdentity

- Purpose: Canonical identity for anything the output feature can publish.
- Fields:
  - sourceId (string, required): Canonical source key. Discovered NDI sources use
    their existing IDs; local screen share uses reserved namespace
    `device-screen:<hostInstanceId>`.
  - kind (enum, required): DISCOVERED_NDI | DEVICE_SCREEN.
  - displayName (string, required): Operator-visible source label.
  - hostInstanceId (string, optional): Required for DEVICE_SCREEN identities.
  - requiresCaptureConsent (boolean, required): True only for DEVICE_SCREEN.
- Validation rules:
  - `DEVICE_SCREEN` sources must use the `device-screen:` namespace and include
    `hostInstanceId`.
  - `DISCOVERED_NDI` sources must not use the reserved local namespace.
  - `displayName` must be non-empty.

## Entity: OutputSession

- Purpose: Represents one outbound NDI publishing session from this app.
- Fields:
  - sessionId (string, required): Unique session identity.
  - inputSourceId (string, required): Foreign key -> OutputInputIdentity.sourceId.
  - inputSourceKind (enum, required): DISCOVERED_NDI | DEVICE_SCREEN.
  - outboundStreamName (string, required): Operator-visible stream identity on
    the network.
  - consentState (enum, required): NOT_REQUIRED | PENDING | GRANTED | DENIED.
  - state (enum, required): READY | STARTING | ACTIVE | STOPPING | STOPPED |
    INTERRUPTED.
  - startedAt (datetime, optional)
  - stoppedAt (datetime, optional)
  - interruptionReason (string, optional)
  - retryAttempts (integer, required, default 0)
  - hostInstanceId (string, required): Distinguishes app instance/device role.
- Validation rules:
  - `inputSourceKind` must match the referenced OutputInputIdentity.kind.
  - `DEVICE_SCREEN` sessions require `consentState = GRANTED` before entering
    `STARTING` or `ACTIVE`.
  - `outboundStreamName` must be non-empty and unique on the network at
    activation time.
  - `stoppedAt` must be >= `startedAt` when both are present.
  - `interruptionReason` is required when `state = INTERRUPTED`.

## Entity: OutputConfiguration

- Purpose: Persists operator defaults and continuity preferences for output.
- Fields:
  - id (integer, required, singleton key = 1)
  - preferredStreamName (string, required)
  - lastSelectedInputSourceId (string, optional)
  - lastSelectedInputSourceKind (enum, optional): DISCOVERED_NDI | DEVICE_SCREEN.
  - autoRetryEnabled (boolean, required, default true)
  - retryWindowSeconds (integer, required, default 15)
  - updatedAt (datetime, required)
- Validation rules:
  - `preferredStreamName` must pass stream-name formatting constraints.
  - `retryWindowSeconds` must be > 0 and <= 15 for this feature version.
  - `lastSelectedInputSourceKind` is required whenever
    `lastSelectedInputSourceId` is present.

## Entity: OutputHealthSnapshot

- Purpose: Captures runtime health for user-visible output status.
- Fields:
  - snapshotId (string, required)
  - sessionId (string, required, foreign key -> OutputSession.sessionId)
  - capturedAt (datetime, required)
  - networkReachable (boolean, required)
  - inputReachable (boolean, required)
  - qualityLevel (enum, required): HEALTHY | DEGRADED | FAILED
  - messageCode (string, optional)
- Validation rules:
  - `qualityLevel = FAILED` requires `messageCode`.
  - `sessionId` must refer to an existing OutputSession.

## Entity: DualEmulatorValidationRun

- Purpose: Records one end-to-end interoperability validation run using two app
  instances in publisher and receiver roles.
- Fields:
  - runId (string, required)
  - executedAt (datetime, required)
  - publisherDeviceId (string, required): Emulator A serial.
  - receiverDeviceId (string, required): Emulator B serial.
  - publisherSourceId (string, required): Usually `device-screen:<publisher>`.
  - publisherRoleState (enum, required): IDLE | CONSENTING | PUBLISHING |
    STOPPED | FAILED.
  - receiverRoleState (enum, required): IDLE | DISCOVERED | PLAYING | STOPPED |
    FAILED.
  - preflightStatus (enum, required): NOT_RUN | PASSED | FAILED.
  - discoveryLatencyMs (integer, optional)
  - playbackStartLatencyMs (integer, optional)
  - stopPropagationLatencyMs (integer, optional)
  - artifactsDirectory (string, optional): Host path to logs/screenshots/report.
  - result (enum, required): PASS | FAIL.
  - failureReason (string, optional)
- Validation rules:
  - `publisherDeviceId` and `receiverDeviceId` must be different.
  - `preflightStatus` must be `PASSED` before `result = PASS`.
  - `result = FAIL` requires `failureReason`.
  - `result = PASS` requires publisher reached `PUBLISHING` and receiver reached
    `PLAYING` at least once.

## Entity: ToolchainCompatibilityBlocker

- Purpose: Captures blocker data when the repository baseline or its validation
  evidence is not yet fully synchronized with the latest stable compatible
  Android toolchain expectations.
- Fields:
  - blockerId (string, required): e.g., TOOLCHAIN-001.
  - owner (string, required)
  - affectedComponents (list of strings, required)
  - targetResolutionDate (date, required)
  - targetResolutionCycle (string, required)
  - status (enum, required): OPEN | IN_PROGRESS | RESOLVED.
- Validation rules:
  - `owner` and target resolution fields are mandatory while status != RESOLVED.

## Relationships

- OutputSession references one OutputInputIdentity.
- OutputConfiguration influences defaults for new OutputSessions.
- OutputHealthSnapshot belongs to one OutputSession.
- DualEmulatorValidationRun evaluates one publisher session against one receiver
  playback session executed on separate emulator instances.
- ToolchainCompatibilityBlocker applies at feature/release governance level.

## State Transitions

- OutputSession state:
  - READY -> STARTING -> ACTIVE
  - ACTIVE -> STOPPING -> STOPPED
  - ACTIVE -> INTERRUPTED -> STARTING (bounded retry)
  - INTERRUPTED -> STOPPED (retry exhausted or operator stop)
- OutputSession consent state:
  - NOT_REQUIRED for DISCOVERED_NDI
  - PENDING -> GRANTED -> STARTING for DEVICE_SCREEN
  - PENDING -> DENIED -> READY for rejected capture consent
- DualEmulatorValidationRun state summary:
  - preflight NOT_RUN -> PASSED | FAILED
  - publisherRoleState IDLE -> CONSENTING -> PUBLISHING -> STOPPED
  - receiverRoleState IDLE -> DISCOVERED -> PLAYING -> STOPPED
  - any unexpected terminal error -> FAILED and run result FAIL
