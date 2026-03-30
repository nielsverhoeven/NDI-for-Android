# Data Model: Refine View Screen Controls

## Entity: ViewSourceItem

- Purpose: Renderable source entry for the View screen list.
- Fields:
  - sourceId (string, required)
  - displayName (string, required)
  - isCurrentDevice (boolean, required)
  - canView (boolean, required)
  - actionLabel (string, required, fixed to "view stream")
- Validation rules:
  - `sourceId` must be non-empty and unique per visible list.
  - Items with `isCurrentDevice=true` must not be included in rendered list.
  - `canView` must be true for all rendered items.

## Entity: ViewSourceListState

- Purpose: Screen state container for displayed sources and empty-state behavior.
- Fields:
  - visibleSources (list<ViewSourceItem>, required)
  - lastUpdatedEpochMillis (long, optional)
  - hasAnyVisibleSource (boolean, required)
- Validation rules:
  - `hasAnyVisibleSource` must equal `visibleSources.isNotEmpty`.
  - List updates replace the previous list atomically when refresh succeeds.

## Entity: RefreshUiState

- Purpose: Control and feedback state for refresh interactions.
- Fields:
  - isRefreshing (boolean, required)
  - refreshEnabled (boolean, required)
  - refreshButtonPosition (enum, required): BOTTOM_LEFT
  - showLoadingIndicator (boolean, required)
- Validation rules:
  - If `isRefreshing=true`, then `refreshEnabled=false` and `showLoadingIndicator=true`.
  - If `isRefreshing=false`, then `refreshEnabled=true` and `showLoadingIndicator=false` unless transitional animation explicitly defined.
  - `refreshButtonPosition` remains `BOTTOM_LEFT` for all supported layouts.

## Entity: RefreshErrorState

- Purpose: Non-blocking inline error representation for refresh failures.
- Fields:
  - hasInlineError (boolean, required)
  - message (string, optional)
  - placement (enum, required): NEAR_REFRESH_CONTROLS
  - occurredAtEpochMillis (long, optional)
- Validation rules:
  - On refresh failure, `hasInlineError=true` and placement is `NEAR_REFRESH_CONTROLS`.
  - Inline error visibility must not force list clearing.

## Entity: ViewInteractionPolicy

- Purpose: Defines clickable and non-clickable zones for list rows.
- Fields:
  - rowTapEnabled (boolean, required, fixed false)
  - actionButtonTapEnabled (boolean, required, fixed true)
  - actionType (enum, required): OPEN_VIEWER
- Validation rules:
  - `rowTapEnabled` must remain false for source rows.
  - Only action button interaction may emit `OPEN_VIEWER(sourceId)`.

## Relationships

- `ViewSourceListState.visibleSources` is rendered using `ViewInteractionPolicy`.
- `RefreshUiState` governs refresh button enabled state and loading indicator presence.
- `RefreshErrorState` is independent of `ViewSourceListState` and must not clear existing visible sources.
- Refresh success transitions update `ViewSourceListState`; refresh failure transitions update `RefreshErrorState` while preserving `ViewSourceListState`.

## State Transitions

- Idle -> Refreshing:
  - Trigger: user presses refresh button.
  - Effects: `isRefreshing=true`, button disabled, loading icon visible, list preserved.

- Refreshing -> Success:
  - Trigger: refreshed source payload received.
  - Effects: replace visible list with filtered result, `isRefreshing=false`, button enabled, loading icon hidden, clear inline error if present.

- Refreshing -> Failure:
  - Trigger: refresh operation error/timeout.
  - Effects: keep current visible list unchanged, `isRefreshing=false`, button enabled, loading icon hidden, show inline non-blocking error near refresh controls.

- Source row interaction:
  - Trigger: non-button row tap.
  - Effects: no navigation event emitted.

- Source action interaction:
  - Trigger: "view stream" button tap for source.
  - Effects: emit viewer navigation for tapped `sourceId`.
