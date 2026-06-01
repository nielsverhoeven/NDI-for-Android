# Data Model: NDI Screen Share Output Redesign

## Entity: ScreenShareSession

- Purpose: Represents one outgoing NDI screen-share run and its lifecycle.
- Fields:
  - sessionId: String (unique)
  - inputSourceId: String (reserved local id, example device-screen:<hostId>)
  - outboundStreamName: String
  - state: READY | STARTING | ACTIVE | STOPPING | STOPPED | INTERRUPTED
  - interruptionReason: String?
  - startedAtEpochMillis: Long
  - stoppedAtEpochMillis: Long
  - retryAttempts: Int
  - consentState: NOT_REQUIRED | GRANTED | DENIED | REQUIRED
  - discoveryMode: SERVER | MDNS
  - discoveryEndpoint: String?
- Validation rules:
  - inputSourceId must be non-empty for start.
  - outboundStreamName must be non-empty after normalization.
  - state ACTIVE is invalid when discovery server is configured but unreachable.

## Entity: ScreenCaptureConsent

- Purpose: Tracks per-session screen-capture authorization.
- Fields:
  - sourceId: String
  - granted: Boolean
  - tokenRef: String?
  - capturedAtEpochMillis: Long
- Validation rules:
  - granted=true requires tokenRef for local screen-share source.
  - consent must be cleared on explicit stop.

## Entity: DiscoveryConfiguration

- Purpose: Resolves which discovery path is required before stream activation.
- Fields:
  - endpoint: String? (null means no configured server)
  - isReachable: Boolean
  - effectiveMode: SERVER | MDNS | START_BLOCKED
- Validation rules:
  - endpoint null -> effectiveMode MDNS.
  - endpoint non-null and unreachable -> effectiveMode START_BLOCKED.

## Entity: OutputUiState

- Purpose: UI projection for output control screen.
- Fields:
  - sourceId: String
  - streamName: String
  - outputState: OutputState
  - canStart: Boolean
  - canStop: Boolean
  - canRetry: Boolean
  - consentRequired: Boolean
  - errorMessage: String?
  - showRecoveryActions: Boolean
- Validation rules:
  - canStart true only in READY/STOPPED/INTERRUPTED-recoverable.
  - errorMessage required when start blocked by unreachable configured discovery server.

## Relationships

- ScreenShareSession 1:1 ScreenCaptureConsent for local-device source sessions.
- ScreenShareSession 1:1 DiscoveryConfiguration at start-time decision point.
- OutputUiState is derived from ScreenShareSession + DiscoveryConfiguration + Consent.

## State Transitions

- READY -> STARTING: user presses Share Screen and consent is granted.
- STARTING -> ACTIVE: sender started and discovery registration succeeded for effective mode.
- STARTING -> INTERRUPTED: sender startup failure or consent denied.
- STARTING -> INTERRUPTED: configured discovery server unreachable (start blocked with actionable error).
- ACTIVE -> STOPPING: explicit stop action.
- STOPPING -> STOPPED: sender and discovery teardown complete, consent cleared.
- INTERRUPTED -> STARTING: retry requested within 15-second window.
- STOPPED -> STARTING: new start attempt (must prompt consent again).
