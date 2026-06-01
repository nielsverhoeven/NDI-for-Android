# Data Model: Three-Screen NDI Navigation

## Entity: TopLevelDestinationState

- Purpose: Represents the currently selected top-level destination and the
  selection state for Home, Stream, and View.
- Fields:
  - destination (enum, required): HOME | STREAM | VIEW.
  - selectedAtEpochMillis (long, required)
  - launchContext (enum, required): LAUNCHER | RECENTS_RESTORE | DEEP_LINK |
    IN_APP_SWITCH.
  - restoredFromProcessDeath (boolean, required)
- Validation rules:
  - `destination` must always be one of HOME/STREAM/VIEW.
  - `launchContext = LAUNCHER` must select `HOME` as initial destination.
  - `launchContext = RECENTS_RESTORE` may restore any valid destination.

## Entity: HomeDashboardSnapshot

- Purpose: Home dashboard summary model used to render current high-level
  status and quick actions.
- Fields:
  - generatedAtEpochMillis (long, required)
  - streamStatus (enum, required): IDLE | STARTING | ACTIVE | STOPPING |
    INTERRUPTED.
  - streamSourceId (string, optional)
  - selectedViewSourceId (string, optional)
  - selectedViewSourceDisplayName (string, optional)
  - viewPlaybackStatus (enum, required): STOPPED | CONNECTING | PLAYING |
    INTERRUPTED.
  - canNavigateToStream (boolean, required)
  - canNavigateToView (boolean, required)
- Validation rules:
  - Action flags must remain true while app is in normal operational state.
  - `selectedViewSourceDisplayName` is required when
    `selectedViewSourceId` is present and known.

## Entity: NavigationTransitionRecord

- Purpose: Captures one top-level navigation attempt for deterministic routing
  and telemetry.
- Fields:
  - transitionId (string, required)
  - fromDestination (enum, required): HOME | STREAM | VIEW.
  - toDestination (enum, required): HOME | STREAM | VIEW.
  - trigger (enum, required): BOTTOM_NAV | NAV_RAIL | HOME_ACTION | DEEP_LINK |
    SYSTEM_RESTORE.
  - outcome (enum, required): SUCCESS | NO_OP_ALREADY_SELECTED |
    FAILED_INVALID_ROUTE | FAILED_NAV_CONTROLLER.
  - occurredAtEpochMillis (long, required)
  - failureReasonCode (string, optional)
- Validation rules:
  - `SUCCESS` requires `toDestination` to be a valid top-level destination.
  - `NO_OP_ALREADY_SELECTED` requires `fromDestination = toDestination`.
  - Failed outcomes require `failureReasonCode`.

## Entity: StreamContinuityState

- Purpose: Models continuity behavior for Stream destination when navigating
  away or restoring after process death.
- Fields:
  - hasActiveOutput (boolean, required)
  - outputState (enum, required): READY | STARTING | ACTIVE | STOPPING |
    STOPPED | INTERRUPTED.
  - lastKnownOutputSourceId (string, optional)
  - lastKnownStreamName (string, optional)
  - restoredAfterProcessDeath (boolean, required)
  - autoRestartPermitted (boolean, required, default false)
- Validation rules:
  - `autoRestartPermitted` must be false for this feature version.
  - If `hasActiveOutput = true`, `outputState` must be STARTING, ACTIVE, or
    INTERRUPTED.
  - After process death restoration, state is contextual only; output must not
    auto-restart.

## Entity: ViewContinuityState

- Purpose: Models selection/viewing continuity for View destination during
  navigation and process restoration.
- Fields:
  - selectedSourceId (string, optional)
  - selectedSourceDisplayName (string, optional)
  - playbackState (enum, required): STOPPED | CONNECTING | PLAYING |
    INTERRUPTED.
  - stoppedByTopLevelNavigation (boolean, required)
  - restoredAfterProcessDeath (boolean, required)
  - autoplayPermitted (boolean, required, default false)
- Validation rules:
  - `autoplayPermitted` must be false for this feature version.
  - `selectedSourceDisplayName` is required when `selectedSourceId` is present
    and known.
  - Leaving View through top-level navigation sets
    `stoppedByTopLevelNavigation = true` and `playbackState = STOPPED`.

## Entity: NavigationLayoutProfile

- Purpose: Represents adaptive top-level nav UI mode selection by form factor.
- Fields:
  - profile (enum, required): PHONE_BOTTOM_NAV | TABLET_NAV_RAIL.
  - screenWidthDp (int, required)
  - selectedByRuleVersion (string, required)
- Validation rules:
  - PHONE profile requires compact-width threshold.
  - TABLET profile requires expanded-width threshold.
  - Profile must not change selected destination identity.

## Relationships

- `TopLevelDestinationState` determines active selection in
  `NavigationLayoutProfile`.
- `HomeDashboardSnapshot` aggregates read-only status from
  `StreamContinuityState` and `ViewContinuityState`.
- `NavigationTransitionRecord` is emitted for each user/system top-level route
  attempt and feeds telemetry.

## State Transitions

- Top-level destination transitions:
  - HOME <-> STREAM
  - HOME <-> VIEW
  - STREAM <-> VIEW
  - Re-select current destination -> `NO_OP_ALREADY_SELECTED` (no duplicate
    destination stacking)
- Stream continuity transitions:
  - ACTIVE in Stream + navigate away -> remains ACTIVE
  - ACTIVE + process death + relaunch -> contextual last-known state shown,
    explicit user action required to restart
- View continuity transitions:
  - PLAYING in View + navigate away -> STOPPED (not PLAYING)
  - Navigate back to View -> selected source remains highlighted, autoplay
    remains disabled
  - PLAYING + process death + relaunch -> selected source restored as context,
    explicit user action required to start playback

