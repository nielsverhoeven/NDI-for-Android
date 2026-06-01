# Feature Specification: NDI Source Discovery and Viewing

**Feature Branch**: `001-scan-ndi-sources`  
**Created**: 2026-03-15  
**Status**: Draft (Aligned with Constitution 1.1.0)  
**Input**: User description: "I want this android app to be able to scan the network for NDI sources. The user should be able to select an NDI source and this NDI source should den be showed on the screen of the phone or tablet."

## Clarifications

### Session 2026-03-15

- Q: What should discovery refresh behavior be? -> A: Auto-refresh every 5 seconds while source list screen is in foreground, plus manual refresh.
- Q: On app launch, how should the previous source selection be handled? -> A: Preselect/highlight last source in list, but require user tap to start viewing.
- Q: What should interruption retry behavior be? -> A: Auto-retry for up to 15 seconds after interruption, then show recovery actions.
- Q: Should this feature request location permission? -> A: Feature must work without requesting location permission.
- Q: How should a source be uniquely identified? -> A: Identify source by stable endpoint identity; display name is label only.
- Q: How should toolchain-blocker scheduling be tracked? -> A: Track both target resolution date and target resolution cycle.
- Q: When must toolchain-blocker documentation exist? -> A: It must exist by planning completion and stay current through implementation.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Discover Available NDI Sources (Priority: P1)

As a user on a phone or tablet connected to the same network as NDI senders,
I want to scan and see a list of currently available NDI sources so I can pick
one source to view.

**Why this priority**: Without reliable discovery, users cannot start any NDI
viewing flow. This is the core entry point and minimum usable value.

**Independent Test**: On a network with at least one active NDI source, start
the discovery flow and verify that available sources appear with distinguishable
labels and status.

**Acceptance Scenarios**:

1. **Given** the device is connected to a network with active NDI sources,
   **When** the user opens the source browser or refreshes it,
   **Then** the app shows a list of detected NDI sources.
2. **Given** no NDI sources are currently reachable,
   **When** the user runs discovery,
   **Then** the app shows a clear empty-state message and offers retry.

---

### User Story 2 - Select and View a Source (Priority: P2)

As a user who discovered sources, I want to select one source and see its video
on my device screen so I can monitor that feed in real time.

**Why this priority**: Selection and playback deliver the primary user outcome.
Discovery alone is incomplete without viewing.

**Independent Test**: With at least one discoverable source, choose one source
from the list and verify that video starts and remains visible.

**Acceptance Scenarios**:

1. **Given** one or more NDI sources are listed,
   **When** the user selects a source,
   **Then** the app navigates to a viewer screen and displays that source feed.
2. **Given** a source is currently being viewed,
   **When** the user returns to the source list and selects another source,
   **Then** the app switches to the newly selected source and shows its feed.

---

### User Story 3 - Handle Source and Network Interruptions (Priority: P3)

As a user currently viewing an NDI source, I want clear feedback and recovery
options when the source disappears or the network becomes unavailable so I can
resume viewing quickly.

**Why this priority**: Recovery behavior improves reliability and user trust,
but depends on P1 and P2 being functional first.

**Independent Test**: Start playback, then stop the source sender or disconnect
network access and verify that the app shows status and supports retry/reselect.

**Acceptance Scenarios**:

1. **Given** a source is playing,
   **When** the source becomes unreachable,
   **Then** playback stops gracefully, the user sees an interruption message,
   and options to retry or select another source are presented.
2. **Given** the device regains network connectivity,
   **When** the user retries discovery,
   **Then** the source list refreshes and selectable sources are shown.

### Edge Cases

- Discovery returns duplicate source names; the app distinguishes entries so the
  user can select the intended source.
- A source appears in discovery but becomes unavailable before selection; the
  app prevents a frozen viewer and shows a recoverable error state.
- Rapid repeated refresh actions occur; the app avoids stacking concurrent scans
  and keeps the UI responsive.
- Device rotates during active playback; viewing continues without forcing
  unnecessary reselection.
- The app is opened without local network reachability; the user gets clear
  guidance instead of an indefinite loading state.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a user-triggered NDI discovery action to
  scan the local network for available NDI sources.
- **FR-002**: The system MUST present discovered NDI sources in a selectable list
  with a stable display label for each item.
- **FR-002a**: The system MUST use stable endpoint identity as the canonical key
  for source selection and persistence; display name MUST be treated as a UI
  label only.
- **FR-003**: The system MUST provide visible states for discovery in progress,
  discovery success, empty results, and discovery failure.
- **FR-003a**: While the source list screen is in the foreground, the system
  MUST auto-refresh discovery results at a 5-second interval.
- **FR-003b**: The system MUST provide a manual refresh action on the source
  list screen.
- **FR-003c**: Discovery auto-refresh MUST pause when the source list screen is
  no longer in the foreground.
- **FR-004**: Users MUST be able to select exactly one NDI source from the list
  and start viewing it on a dedicated viewer screen.
- **FR-005**: The system MUST stop or replace the currently viewed source when
  the user selects a different source.
- **FR-006**: The system MUST detect playback interruption and provide recovery
  actions (retry current source and return to source selection).
- **FR-006a**: On interruption, the system MUST auto-retry reconnecting to the
  active source for up to 15 seconds before declaring interruption unresolved.
- **FR-006b**: If reconnection is not successful within 15 seconds, the system
  MUST show recovery actions to retry manually or return to source selection.
- **FR-007**: The system MUST preserve the most recent selected source identity
  locally so the user can quickly resume selection after app restart.
- **FR-007a**: On app launch, the system MUST highlight the most recently
  selected source in the source list when available.
- **FR-007b**: The system MUST NOT automatically start viewing a source on app
  launch without explicit user action.
- **FR-008**: The system MUST log non-sensitive operational events for discovery,
  source selection, playback start, and playback interruption.
- **FR-009**: The system MUST remain usable on both phones and tablets.
- **FR-010**: The system MUST support Android API 24+ behavior while remaining
  buildable and compliant on the latest stable compatible compileSdk/targetSdk
  baseline.
- **FR-011**: The system MUST provide discovery, selection, and viewing behavior
  without requesting location permission.

### Constitutional Requirements *(mandatory)*

- **CR-001 (Architecture)**: Discovery, selection, and playback state management
  MUST be handled in ViewModels; UI screens only render state and dispatch user
  actions. Navigation between source list and viewer MUST use single-activity
  navigation flows. Data access for source discovery and last-selection state
  MUST go through repositories.
- **CR-002 (Quality)**: The team MUST define tests first for discovery states,
  source selection, and interruption handling. Core behavior MUST be covered by
  automated unit tests and user-facing transitions MUST be covered by automated
  UI flow tests.
- **CR-003 (UX and Performance)**: Source list and viewer flows MUST follow
  Material Design 3 interaction and feedback patterns. Discovery and playback
  operations MUST avoid unjustified background execution and remain compatible on
  API 24+ devices.
- **CR-004 (Data and Security)**: User-critical state (for example, recent source
  identity) MUST be stored locally to support offline-first behavior. Any new
  permission request MUST include explicit feature-level justification and be
  rejected if not strictly necessary. Location permission is explicitly out of
  scope for this feature.
- **CR-005 (Build and Modularity)**: Implementation MUST fit feature-based module
  boundaries, and release readiness MUST include validation with code shrinking,
  optimization, and the repo-supported latest stable compatible Android
  toolchain enabled. Any repo-supported toolchain upgrade that affects this
  feature MUST include release-mode validation of discovery, selection, and
  viewing flows before release approval.
- **CR-006 (Toolchain Currency)**: The feature MUST document the expected
  compileSdk/targetSdk, AGP, Gradle, Kotlin, JDK/JBR, AndroidX/Jetpack,
  NDK/CMake, and NDI SDK compatibility baseline, and MUST record any blocker to
  adopting the latest stable compatible versions. If the NDI SDK or another
  required dependency prevents adoption of a newer stable baseline, the blocker
  MUST be tracked with an owner, affected component list, target resolution
  date, and target resolution cycle before the feature can be considered
  release-ready.
- **CR-007 (Blocker Documentation State)**: For any open toolchain blocker,
  blocker documentation MUST already exist at planning completion (not deferred
  to implementation) and MUST remain synchronized with plan and task artifacts.

### Key Entities *(include if feature involves data)*

- **NDI Source**: A discoverable network stream endpoint with identifying fields
  such as source name, endpoint identity, and current reachability status.
- **Source Discovery Result**: A time-bounded snapshot of discovered sources,
  including scan timestamp, list entries, and discovery outcome state.
- **Viewer Session**: The active user viewing context containing selected source,
  current playback state, and interruption reason if playback fails.

## Assumptions

- Users and NDI senders are on the same reachable local network segment.
- The app is focused on one source viewed at a time.
- Audio requirements are out of scope for this feature unless needed to show
  basic source playback viability.
- The repository's active Android toolchain baseline at release time follows the
  latest stable compatible versions required by Constitution 1.1.0, unless a
  documented blocker has been approved and tracked.
- Toolchain blocker documentation for this feature is expected to be present in
  feature validation artifacts before implementation work begins.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a test environment with active NDI senders, users can discover
  at least one available source within 5 seconds in at least 90% of attempts.
- **SC-002**: After selecting a source, video becomes visible on the viewer
  screen within 3 seconds in at least 90% of attempts.
- **SC-003**: At least 95% of tested interruption events present a clear recovery
  path (retry or reselect) without requiring app restart.
- **SC-004**: In usability testing, at least 90% of participants can complete the
  flow discover -> select -> view on first attempt without assistance.
- **SC-005**: After any repo-supported platform maintenance update, the primary
  discover -> select -> view flow continues to succeed on representative phone
  and tablet validation devices in at least 95% of release-readiness runs.
