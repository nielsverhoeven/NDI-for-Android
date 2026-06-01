# Data Model - Viewer Persistence and Stream Availability Status

## Entity: LastViewedContext

Represents the single persisted viewer restore snapshot.

Fields:
- contextId: constant singleton key (for example `last_viewed_context`)
- sourceId: string, required
- lastFrameImagePath: string, nullable when frame unavailable
- lastFrameCapturedAtEpochMillis: long, nullable
- restoredAtEpochMillis: long, nullable
- isSourceCurrentlyAvailable: boolean, derived/runtime cached

Validation rules:
- `sourceId` must be non-blank when persisted.
- `lastFrameImagePath` must reference app-internal file path when present.
- Only one LastViewedContext record may exist.

State transitions:
- Empty -> Active: first successfully rendered frame for a selected source.
- Active -> Replaced: different source becomes last-viewed and renders a frame.
- Active -> ActiveUnavailable: discovery marks source unavailable after threshold.
- Any -> Empty: app data clear/uninstall.

## Entity: ConnectionHistoryState

Per-source durable marker indicating successful historical playback.

Fields:
- sourceId: string, primary key
- previouslyConnected: boolean (always true once record exists)
- firstSuccessfulFrameAtEpochMillis: long, required
- lastSuccessfulFrameAtEpochMillis: long, required

Validation rules:
- Record is created only after first successful rendered frame.
- `lastSuccessfulFrameAtEpochMillis` >= `firstSuccessfulFrameAtEpochMillis`.

State transitions:
- Unknown -> ConnectedHistory: first successful frame.
- ConnectedHistory -> ConnectedHistoryUpdated: additional successful frames.
- ConnectedHistory persists across app restart; cleared only by app data reset.

## Entity: DiscoveryAvailabilityState

Runtime/stateful availability projection for source list rows.

Fields:
- sourceId: string, primary key
- isAvailable: boolean, required
- consecutiveMissedPolls: integer, required, default 0
- lastSeenAtEpochMillis: long, nullable
- lastStatusChangedAtEpochMillis: long, required

Validation rules:
- `consecutiveMissedPolls` cannot be negative.
- When source appears in a poll, `isAvailable=true` and `consecutiveMissedPolls=0`.
- When source misses two consecutive polls, `isAvailable=false`.

State transitions:
- Available(0 misses) -> Available(1 miss): first miss.
- Available(1 miss) -> Unavailable(2 misses): second consecutive miss.
- Unavailable -> Available(0 misses): source seen again.

## Relationship Summary

- LastViewedContext.sourceId references a stream identifier that may also exist in ConnectionHistoryState and DiscoveryAvailabilityState.
- ConnectionHistoryState and DiscoveryAvailabilityState are keyed by sourceId and are merged for source list rendering:
  - previouslyConnected indicator from ConnectionHistoryState
  - availability/disabled action state from DiscoveryAvailabilityState

## Derived UI Contract Fields

Computed per source row:
- showPreviouslyConnectedBadge = ConnectionHistoryState exists for sourceId
- showUnavailableBadge = DiscoveryAvailabilityState.isAvailable == false
- isViewStreamEnabled = DiscoveryAvailabilityState.isAvailable == true

Computed for viewer restore:
- showSavedPreview = LastViewedContext.lastFrameImagePath is present and readable
- autoPlayAllowedOnRestore = false when source unavailable
