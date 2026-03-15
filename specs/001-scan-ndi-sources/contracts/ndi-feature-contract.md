# Contract: NDI Discovery and Viewer Feature

## 1. Repository Contracts

### 1.1 NdiDiscoveryRepository
- discoverSources(trigger: MANUAL | FOREGROUND_TICK): DiscoverySnapshot
- observeDiscoveryState(): stream<DiscoverySnapshot>
- startForegroundAutoRefresh(intervalSeconds = 5): void
- stopForegroundAutoRefresh(): void

Behavioral requirements:
- Auto-refresh must run only while source list screen is foreground.
- Overlapping discovery jobs are not allowed.
- DiscoverySnapshot.sources must use sourceId as canonical identity key.

### 1.2 NdiViewerRepository
- connectToSource(sourceId: string): ViewerSession
- observeViewerSession(): stream<ViewerSession>
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
- Repository must not trigger autoplay at app launch.

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
- recoveryActionsVisible: boolean

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
