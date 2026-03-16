# Data Model: NDI Source Network Output and Interop Validation

## Entity: OutputSession

- Purpose: Represents one outbound NDI publishing session from this app.
- Fields:
  - sessionId (string, required): Unique session identity.
  - inputSourceId (string, required): Canonical selected source ID used as input.
  - outboundStreamName (string, required): Operator-visible stream identity on
    the network.
  - state (enum, required): READY | STARTING | ACTIVE | STOPPING | STOPPED |
    INTERRUPTED.
  - startedAt (datetime, optional)
  - stoppedAt (datetime, optional)
  - interruptionReason (string, optional)
  - retryAttempts (integer, required, default 0)
  - hostInstanceId (string, required): Distinguishes app instance/device role.
- Validation rules:
  - inputSourceId must be non-empty and map to a known source identity model.
  - outboundStreamName must be non-empty and unique on network at activation.
  - stoppedAt must be >= startedAt when both are present.
  - interruptionReason is required when state = INTERRUPTED.

## Entity: OutputConfiguration

- Purpose: Persists operator defaults and continuity preferences for output.
- Fields:
  - id (integer, required, singleton key = 1)
  - preferredStreamName (string, required)
  - lastSelectedInputSourceId (string, optional)
  - autoRetryEnabled (boolean, required, default true)
  - retryWindowSeconds (integer, required, default 15)
  - updatedAt (datetime, required)
- Validation rules:
  - preferredStreamName must pass stream-name formatting constraints.
  - retryWindowSeconds must be > 0 and <= 15 for this feature version.

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
  - qualityLevel FAILED requires messageCode.
  - sessionId must refer to an existing OutputSession.

## Entity: DualEmulatorValidationRun

- Purpose: Records one end-to-end interoperability validation run using two
  emulator app instances in publisher and receiver roles.
- Fields:
  - runId (string, required)
  - executedAt (datetime, required)
  - publisherDeviceId (string, required): Emulator A identifier.
  - receiverDeviceId (string, required): Emulator B identifier.
  - publisherRoleState (enum, required): IDLE | PUBLISHING | STOPPED | FAILED
  - receiverRoleState (enum, required): IDLE | DISCOVERED | PLAYING | STOPPED |
    FAILED
  - discoveryLatencyMs (integer, optional)
  - playbackStartLatencyMs (integer, optional)
  - result (enum, required): PASS | FAIL
  - failureReason (string, optional)
- Validation rules:
  - publisherDeviceId and receiverDeviceId must be different.
  - result FAIL requires failureReason.
  - PASS requires receiverRoleState reached PLAYING at least once.

## Entity: ToolchainCompatibilityBlocker

- Purpose: Captures blocker data when the repository is not yet on the latest
  stable compatible Android baseline.
- Fields:
  - blockerId (string, required): e.g., TOOLCHAIN-001.
  - owner (string, required)
  - affectedComponents (list of strings, required)
  - targetResolutionDate (date, required)
  - targetResolutionCycle (string, required)
  - status (enum, required): OPEN | IN_PROGRESS | RESOLVED.
- Validation rules:
  - owner and target resolution fields are mandatory while status != RESOLVED.

## Relationships

- OutputSession references selected input source identity from source discovery.
- OutputHealthSnapshot belongs to one OutputSession.
- OutputConfiguration influences new OutputSession defaults.
- DualEmulatorValidationRun evaluates interaction between one publisher session
  and one receiver session executed on separate emulator instances.
- ToolchainCompatibilityBlocker applies at feature/release governance level.

## State Transitions

- OutputSession state:
  - READY -> STARTING -> ACTIVE
  - ACTIVE -> STOPPING -> STOPPED
  - ACTIVE -> INTERRUPTED -> STARTING (bounded retry)
  - INTERRUPTED -> STOPPED (retry exhausted or operator stop)
- DualEmulatorValidationRun state summary:
  - publisherRoleState IDLE -> PUBLISHING -> STOPPED
  - receiverRoleState IDLE -> DISCOVERED -> PLAYING -> STOPPED
  - any unexpected terminal error -> FAILED and run result FAIL
