# Data Model: NDI Source Discovery and Viewing

## Entity: NdiSource

- Purpose: Canonical representation of one discoverable NDI endpoint.
- Fields:
  - sourceId (string, required): Stable endpoint identity, unique key.
  - displayName (string, required): Human-readable label.
  - endpointAddress (string, optional): Network address information if exposed.
  - isReachable (boolean, required): Current reachability at scan time.
  - lastSeenAt (datetime, required): Timestamp of latest detection.
- Validation rules:
  - sourceId must be non-empty and unique in a discovery snapshot.
  - displayName may duplicate across records; must not be used as unique key.

## Entity: DiscoverySnapshot

- Purpose: One completed discovery cycle result.
- Fields:
  - snapshotId (string, required): Unique snapshot identifier.
  - startedAt (datetime, required)
  - completedAt (datetime, required)
  - status (enum, required): IN_PROGRESS | SUCCESS | EMPTY | FAILURE.
  - sourceCount (integer, required)
  - sources (list of NdiSource, required)
  - errorCode (string, optional)
  - errorMessage (string, optional)
- Validation rules:
  - completedAt >= startedAt.
  - status EMPTY implies sourceCount = 0.
  - status FAILURE requires errorCode and/or errorMessage.

## Entity: ViewerSession

- Purpose: Active or recent playback session state.
- Fields:
  - sessionId (string, required)
  - selectedSourceId (string, required, foreign key -> NdiSource.sourceId)
  - playbackState (enum, required): IDLE | CONNECTING | PLAYING | INTERRUPTED | STOPPED.
  - interruptionReason (string, optional)
  - retryWindowSeconds (integer, required, default 15)
  - retryAttempts (integer, required)
  - startedAt (datetime, required)
  - endedAt (datetime, optional)
- Validation rules:
  - retryWindowSeconds must equal 15 for this feature version.
  - interruptionReason required when playbackState = INTERRUPTED.

## Entity: UserSelectionState

- Purpose: Persisted user continuity state for app relaunch.
- Fields:
  - id (integer, required, singleton key = 1)
  - lastSelectedSourceId (string, optional)
  - lastSelectedAt (datetime, optional)
  - shouldAutoplayOnLaunch (boolean, required, fixed false)
- Validation rules:
  - shouldAutoplayOnLaunch must remain false.
  - If lastSelectedSourceId is present, UI should highlight matching source if discovered.

## Entity: ToolchainCompatibilityBlocker

- Purpose: Planning and release-readiness record for any blocker preventing the
  feature from moving to the latest stable compatible Android baseline.
- Fields:
  - blockerId (string, required): Unique governance identifier, e.g. `TOOLCHAIN-001`.
  - owner (string, required): Responsible maintainer group or person.
  - currentBaseline (string, required): Current repo-supported compile/target SDK,
    AGP, Gradle, Kotlin, and Java target summary.
  - targetBaseline (string, required): Intended latest stable compatible Android
    baseline.
  - affectedComponents (list of strings, required): Modules, scripts, CI, or SDKs
    impacted by the blocker.
  - blockerReason (string, required): Why the newer baseline is not yet adopted.
  - targetResolutionCycle (string, required): Planned maintenance cycle or date.
  - status (enum, required): OPEN | IN_PROGRESS | RESOLVED.
- Validation rules:
  - blockerId must be unique.
  - owner, blockerReason, and targetResolutionCycle are mandatory whenever the
    repo baseline lags the latest stable compatible Android baseline.
  - affectedComponents must include the NDI SDK whenever proprietary SDK
    compatibility is part of the blocker.

## Relationships

- DiscoverySnapshot contains many NdiSource entries.
- ViewerSession references one NdiSource by sourceId.
- UserSelectionState references one last-selected NdiSource by sourceId when available.
- ToolchainCompatibilityBlocker references affected build modules, CI, scripts,
  and external SDK dependencies at the planning level.

## State Transitions

- Discovery state:
  - IN_PROGRESS -> SUCCESS | EMPTY | FAILURE
- Viewer playback state:
  - IDLE -> CONNECTING -> PLAYING
  - PLAYING -> INTERRUPTED -> CONNECTING (auto-retry window)
  - INTERRUPTED -> STOPPED (after 15s unresolved)
  - CONNECTING -> STOPPED (user cancel/navigation away)
