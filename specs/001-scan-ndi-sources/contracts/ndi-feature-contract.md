# Contract: NDI Discovery and Viewer Feature

## 1. Repository Contracts

### 1.1 NdiDiscoveryRepository

- discoverSources(trigger: MANUAL | FOREGROUND_TICK): DiscoverySnapshot
- observeDiscoveryState(): stream of DiscoverySnapshot
- startForegroundAutoRefresh(intervalSeconds = 5): void
- stopForegroundAutoRefresh(): void

Behavioral requirements:

- Auto-refresh must run only while source list screen is foreground.
- Overlapping discovery jobs are not allowed.
- DiscoverySnapshot.sources must use sourceId as canonical identity key.

### 1.2 NdiViewerRepository

- connectToSource(sourceId: string): ViewerSession
- observeViewerSession(): stream of ViewerSession
- retryReconnectWithinWindow(sourceId: string, windowSeconds = 15): ViewerSession
- stopViewing(): void

Behavioral requirements:

- Retry window is bounded to 15 seconds.
- On unresolved interruption, repository emits INTERRUPTED then STOPPED with recovery metadata.

### 1.3 UserSelectionRepository

- saveLastSelectedSource(sourceId: string): void
- getLastSelectedSource(): string?

Behavioral requirements:

- Persisted state is used only for preselection/highlighting.
- Repository must not trigger automatic playback at app launch.

## 2. ViewModel Contracts

### 2.1 SourceListViewModel

Inputs:

- onScreenVisible()
- onScreenHidden()
- onManualRefresh()
- onSourceSelected(sourceId)

Outputs (state):

- discoveryStatus: IN_PROGRESS | SUCCESS | EMPTY | FAILURE
- sources: list item models keyed by sourceId
- highlightedSourceId: string?
- navigationEvent: OpenViewer(sourceId)

Guarantees:

- Discovery starts/refreshes only while visible.
- highlightedSourceId may be set from persisted selection but does not auto-navigate.

### 2.2 ViewerViewModel

Inputs:

- onViewerOpened(sourceId)
- onRetryPressed()
- onBackToListPressed()

Outputs (state):

- playbackState: CONNECTING | PLAYING | INTERRUPTED | STOPPED
- interruptionMessage: string?
- recoveryActionsVisible: Boolean

Guarantees:

- Auto-retry window is attempted once interruption occurs.
- Recovery actions are shown when retry window expires unresolved.

## 3. Navigation Contract

Routes:

- SourceListRoute
- ViewerRoute(sourceId: string)

Rules:

- sourceId argument is required for ViewerRoute.
- Navigation is single-activity with Navigation Component.
- Returning to SourceListRoute preserves latest discovery state where possible.

## 4. Permission Contract

- No location permission request is allowed for this feature.
- Any new dangerous permission addition is a contract violation and requires spec amendment.

## 5. Observability Contract

Required non-sensitive event categories:

- discovery_started
- discovery_completed
- discovery_failed
- source_selected
- playback_started
- playback_interrupted
- recovery_action_taken

Event payload constraints:

- Must not include raw video/audio data.
- Must not include personally identifiable information.

## 6. Release Validation Contract

- Any change to compile SDK, target SDK, AGP, Gradle, Kotlin, Java/JBR,
  AndroidX, NDK, CMake, or the NDI SDK requires rerunning the feature's
  prerequisite gate, unit tests, UI/instrumentation tests, and release build
  validation before release approval.
- If the repository is not yet on the latest stable compatible Android baseline,
  blocker `TOOLCHAIN-001` (or its successor) must remain documented with owner,
  affected components, and target resolution cycle.
- Discovery, source selection, viewer playback, and interruption recovery are
  the minimum feature flows that must pass after any toolchain-affecting change.
