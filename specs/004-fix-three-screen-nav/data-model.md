# Data Model: Three-Screen Navigation Repairs and E2E Compatibility

## Entity: TopLevelNavigationState

- Purpose: Canonical state for active top-level destination and icon/highlight rendering.
- Fields:
  - destination (enum, required): HOME | STREAM | VIEW
  - iconHome (enum, required): HOUSE
  - iconStream (enum, required): CAMERA
  - iconView (enum, required): SCREEN
  - selectedDestination (enum, required): HOME | STREAM | VIEW
  - updatedAtEpochMillis (long, required)
- Validation rules:
  - Exactly one selected destination must exist at a time.
  - Stream setup/control screens must map to selected destination STREAM.

## Entity: ViewNavigationSession

- Purpose: Tracks the user path from View root list to viewer screen and back behavior.
- Fields:
  - enteredFromDestination (enum, required): HOME | STREAM | VIEW
  - selectedSourceId (string, optional)
  - viewerOpen (boolean, required)
  - lastTransition (enum, required): VIEW_ROOT_TO_VIEWER | VIEWER_TO_VIEW_ROOT | VIEW_ROOT_TO_HOME
  - backPolicy (enum, required): VIEWER_BACK_TO_VIEW_ROOT__VIEW_ROOT_BACK_TO_HOME
- Validation rules:
  - Selecting a source on View root must transition to VIEW_ROOT_TO_VIEWER.
  - Back from viewer must transition to VIEWER_TO_VIEW_ROOT.
  - Back from View root must transition to VIEW_ROOT_TO_HOME.

## Entity: DeviceVersionProfile

- Purpose: Runtime Android version snapshot for each e2e device.
- Fields:
  - deviceRole (enum, required): PUBLISHER | RECEIVER
  - serial (string, required)
  - sdkInt (int, required)
  - majorVersion (int, required)
  - detectedAtEpochMillis (long, required)
- Validation rules:
  - `serial` must be non-empty and unique per role.
  - `sdkInt` and `majorVersion` must be positive integers.

## Entity: SupportedVersionWindow

- Purpose: Represents the computed rolling latest-five Android major-version support window for a run.
- Fields:
  - highestSupportedMajor (int, required)
  - lowestSupportedMajor (int, required)
  - windowSize (int, required, default 5)
  - computedAtEpochMillis (long, required)
- Validation rules:
  - `windowSize` must equal 5.
  - `lowestSupportedMajor = highestSupportedMajor - 4`.
  - Device major versions outside `[lowestSupportedMajor, highestSupportedMajor]` are unsupported.

## Entity: ConsentFlowVariant

- Purpose: Encapsulates per-device screen-share consent branch behavior.
- Fields:
  - deviceRole (enum, required): PUBLISHER | RECEIVER
  - majorVersion (int, required)
  - prefersFullScreenShare (boolean, required, default true)
  - selectionLabels (list<string>, required)
  - confirmLabels (list<string>, required)
- Validation rules:
  - `selectionLabels` must prioritize full-screen options before one-app options.
  - `confirmLabels` must include platform-specific confirm text for supported versions.

## Entity: E2ETimingPolicy

- Purpose: Defines intentional static delay and polling cadence policy for e2e helpers.
- Fields:
  - maxStaticDelayMs (int, required)
  - postTapDelayMs (int, required)
  - pollIntervalMs (int, required)
  - retryIntervalMs (int, required)
- Validation rules:
  - `maxStaticDelayMs <= 1000`.
  - All intentional helper delays must be `<= maxStaticDelayMs`.

## Relationships

- `TopLevelNavigationState.selectedDestination` governs menu highlight rendering.
- `ViewNavigationSession` transitions update `TopLevelNavigationState` when back moves from View root to Home.
- `DeviceVersionProfile` entries feed `SupportedVersionWindow` eligibility checks.
- `SupportedVersionWindow` determines whether e2e run proceeds or fails fast.
- `ConsentFlowVariant` is selected per `DeviceVersionProfile.majorVersion` inside one unified suite.
- `E2ETimingPolicy` constrains wait behavior used by consent and navigation steps.

## State Transitions

- Navigation flow:
  - VIEW_ROOT + source select -> VIEWER_OPEN
  - VIEWER_OPEN + back -> VIEW_ROOT
  - VIEW_ROOT + back -> HOME
- Highlight flow:
  - Destination change updates selected destination immediately and singularly.
- E2E version flow:
  - Detect device versions -> compute support window -> fail fast if unsupported -> select per-device consent variant -> execute stream/view assertions.
