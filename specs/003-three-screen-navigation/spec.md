# Feature Specification: Three-Screen NDI Navigation

**Feature Branch**: `003-three-screen-navigation`  
**Created**: 2026-03-16  
**Status**: Draft  
**Input**: User description: "split app UI into 3 screens with a navigation menu. Requirements: 1) Homepage with dashboard, 2) Stream page to set up an NDI stream, 3) View page to select and then view an NDI stream discovered on network. Ensure navigation menu enables moving between all pages. Keep aligned with current Android multi-module architecture and existing NDI specs conventions in `specs/`."

## Clarifications

### Session 2026-03-16

- Q: Which top-level navigation pattern should this feature implement across phone and tablet layouts? -> A: Use Material 3 bottom navigation on phones and a navigation rail on tablets.
- Q: When users navigate away from Stream or View, what should happen to active session behavior? -> A: Keep Stream output running, and stop View playback; on return, keep source selected without autoplay.
- Q: For FR-010b, should leaving View pause or stop playback? -> A: Leaving View MUST stop playback (pause is not allowed).
- Q: When Android kills the app process while Stream output was active, what should happen on next app launch? -> A: Do not auto-restart; show last-known state and require explicit user restart.
- Q: When Android kills the app process while View playback was active, what should happen on next app launch? -> A: Keep the last selected source highlighted, but do not autoplay; require explicit user action to start playback.
- Q: When the app is relaunched after process death, what should determine the initial top-level destination? -> A: If launched from launcher icon, open Home; if resumed from Recents/task restore, reopen last destination.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Navigate Between Core Screens (Priority: P1)

As an app user, I want a persistent navigation menu that lets me move between
Home, Stream, and View screens so I can access each core task without dead ends
or back-stack confusion.

**Why this priority**: If navigation is unclear or incomplete, users cannot
reliably reach stream setup or network viewing, which blocks all feature value.

**Independent Test**: Launch the app, open the navigation menu from each of the
three screens, move to the other two screens, and verify each destination loads
correctly with the selected destination visibly highlighted.

**Acceptance Scenarios**:

1. **Given** the user is on Home, **When** the user chooses Stream or View from
   the navigation menu, **Then** the app navigates to the selected screen and
   visually indicates the active destination.
2. **Given** the user is on Stream or View, **When** the user opens the
   navigation menu, **Then** Home, Stream, and View are all available as direct
   destinations.
3. **Given** the user repeatedly switches across destinations, **When**
   navigation completes, **Then** the app remains responsive and avoids creating
   duplicate top-level screen instances.

---

### User Story 2 - Use Home Dashboard as App Entry Point (Priority: P2)

As an app user, I want a Home screen dashboard that summarizes available actions
and current NDI-related status so I can quickly decide whether to start
streaming or open network viewing.

**Why this priority**: Home provides orientation and makes the three-screen
structure understandable for new and returning users.

**Independent Test**: Open the app to Home and confirm the dashboard displays
high-level status plus clear actions that route to Stream and View.

**Acceptance Scenarios**:

1. **Given** the app is launched from the launcher icon, **When** initial
   navigation resolves, **Then** Home is shown as the default destination.
2. **Given** Home is displayed, **When** the user selects the action to set up
   streaming, **Then** the app routes to Stream.
3. **Given** Home is displayed, **When** the user selects the action to browse
   and watch discovered streams, **Then** the app routes to View.

---

### User Story 3 - Set Up and View NDI Streams Across Dedicated Pages (Priority: P3)

As an operator, I want Stream and View to stay as dedicated pages so stream
setup and network source viewing remain focused while still being reachable from
the common navigation menu.

**Why this priority**: The app already has stream/output and discovery/viewing
flows; this story preserves those flows while reorganizing them into a clear
three-screen information architecture.

**Independent Test**: From Home, navigate to Stream and perform stream setup,
then navigate to View, select a discovered source, and verify viewing starts
without breaking existing flow expectations.

**Acceptance Scenarios**:

1. **Given** the user opens Stream from navigation or Home, **When** the user
   configures and starts an NDI stream, **Then** stream setup behavior matches
   existing feature expectations for output control and status.
2. **Given** the user opens View from navigation or Home, **When** the user
   selects an NDI source discovered on the network, **Then** the app starts
   viewing the selected source on the View page flow.
3. **Given** the user is actively using Stream or View, **When** the user
   switches destinations via navigation, **Then** the app transitions cleanly
   and preserves continuity by keeping Stream output active while stopping View
   playback until the user explicitly starts playback again.

### Edge Cases

- The user opens the navigation menu while one destination is already active;
  the active destination must be clearly indicated and selecting it must not
  reset critical in-progress state.
- The network has no discoverable NDI sources when user opens View; the app
  must show a clear empty state and retain access to Home and Stream.
- The user navigates away from Stream during setup before start/stop completion;
  the app must avoid inconsistent status and show the latest valid state when
  returning.
- The user navigates away from View during active playback; playback must stop
  immediately, and returning must keep the last selected source without
  autoplay.
- A deep link opens directly into View or Stream; top-level navigation must
  still expose all three destinations and preserve expected back behavior.
- Device rotation or process recreation occurs while on any top-level screen;
  the app must preserve top-level navigation availability and restore expected
  state without stranding the user.
- Android kills the app process while Stream output is active; on next launch,
  the app must show last-known Stream state without auto-restarting output and
  require explicit user action to restart.
- Android kills the app process while View playback is active; on next launch,
  the app must highlight the last selected source without autoplay and require
  explicit user action to start playback.
- After process death, launching from the launcher icon must open Home, while
  restoring from Recents/task restore must reopen the last top-level
  destination.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide exactly three top-level destinations:
  Home, Stream, and View.
- **FR-002**: The system MUST provide a navigation menu reachable from each
  top-level destination that allows direct movement to the other two
  destinations.
- **FR-002a**: The top-level navigation control MUST use a Material 3 bottom
  navigation bar on phone layouts and a Material 3 navigation rail on tablet
  layouts.
- **FR-003**: The system MUST visually indicate the currently active
  top-level destination in the navigation menu.
- **FR-004**: The system MUST open Home as the default destination on standard
  launcher-icon app launch.
- **FR-004a**: After process death, if the user resumes the app from
  Recents/task restore, the system MUST reopen the last active top-level
  destination (Home, Stream, or View).
- **FR-005**: The Home destination MUST present dashboard content that summarizes
  available stream and view actions and provides explicit actions to open Stream
  and View.
- **FR-006**: The Stream destination MUST allow users to set up and control NDI
  stream output behavior consistent with existing output feature contracts.
- **FR-007**: The View destination MUST allow users to discover/select a network
  NDI source and view the selected stream consistent with existing viewer
  feature contracts.
- **FR-008**: Top-level navigation MUST prevent duplicate destination stacking
  during repeated taps and keep navigation outcomes deterministic.
- **FR-009**: The system MUST preserve existing deep-link entry paths for stream
  output and source viewing while keeping Home, Stream, and View mutually
  reachable through the navigation menu.
- **FR-010**: The system MUST preserve continuity expectations already defined in
  existing NDI specs, including foreground-aware discovery refresh behavior and
  no-autoplay continuity for previously selected sources.
- **FR-010a**: Leaving Stream through top-level navigation MUST keep active
  stream output running until the user explicitly stops output.
- **FR-010b**: Leaving View through top-level navigation MUST stop View
  playback; when returning to View, the last selected source MUST remain selected
  and MUST NOT autoplay.
- **FR-010c**: If the app process is killed while Stream output is active, the
  next launch MUST restore last-known Stream status for user context and MUST
  NOT auto-restart output; restarting output MUST require explicit user action.
- **FR-010d**: If the app process is killed while View playback is active, the
  next launch MUST restore the previously selected source as highlighted context
  and MUST NOT autoplay playback; starting playback MUST require explicit user
  action.
- **FR-011**: The system MUST emit non-sensitive telemetry for top-level
  navigation destination changes and failed navigation attempts.
- **FR-012**: The system MUST remain usable on phone and tablet form factors and
  support Android API 24+ on the repository's current stable compatible
  toolchain baseline.

### Constitutional Requirements *(mandatory)*

- **CR-001 (Architecture)**: Top-level navigation composition MUST stay in
  `app`, and feature screens MUST continue using existing presentation-layer
  dependency providers and repository-mediated domain contracts across
  `feature/ndi-browser:{presentation,domain,data}`.
- **CR-002 (Quality)**: Tests MUST be defined before implementation for
  top-level navigation routing, Home default launch, and cross-screen transition
  stability. Automated coverage MUST include unit and UI flow validation for
  Home -> Stream, Home -> View, Stream -> View, and View -> Stream transitions.
- **CR-003 (UX and Performance)**: Navigation and dashboard interactions MUST
  align with Material Design 3 expectations, avoid unnecessary background work,
  and preserve existing foreground-only discovery refresh behavior.
- **CR-004 (Data and Security)**: The feature MUST not expand sensitive data
  collection; any persistence added for dashboard/navigation continuity MUST stay
  local and non-sensitive, with no new dangerous permission unless explicitly
  justified.
- **CR-005 (Build and Modularity)**: Changes MUST respect module boundaries,
  keep native NDI integration isolated to `ndi/sdk-bridge`, and pass release
  build validation with shrinking/obfuscation enabled.
- **CR-006 (Toolchain Currency)**: The feature MUST remain compatible with the
  repo toolchain baseline (compile/target SDK, AGP, Gradle, Kotlin, JDK/JBR,
  AndroidX/Jetpack, NDK/CMake, and NDI SDK) and document any blocker that
  prevents adoption of newer stable compatible versions.

### Key Entities *(include if feature involves data)*

- **Top-Level Destination**: Represents one primary app destination (Home,
  Stream, View), including route identity and selected state.
- **Home Dashboard Snapshot**: Represents user-facing summary information shown
  on Home, including high-level stream status and quick-action availability.
- **Stream Setup Session**: Represents user stream setup state and control
  status while using the Stream destination.
- **View Selection Session**: Represents source discovery, selected source
  identity, and active viewing state while using the View destination.

## Assumptions

- Existing Stream/output and View/discovery behavior remains authoritative from
  `specs/001-scan-ndi-sources` and `specs/002-stream-ndi-source`; this feature
  focuses on top-level IA/navigation and Home dashboard entry.
- The navigation menu pattern is a top-level app navigation control appropriate
  for both phone and tablet layouts.
- Users should be able to reach all top-level destinations in at most one menu
  interaction from any current top-level screen.
- No migration of native NDI code or database schema is required for this
  feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In validation runs, 95% of users can navigate from any top-level
  destination to any other top-level destination in 2 interactions or fewer.
- **SC-002**: In at least 95% of test runs, the app lands on Home as the first
  visible destination after normal launch.
- **SC-003**: In moderated usability testing, at least 90% of participants can
  complete the sequence Home -> Stream -> View -> Home on first attempt without
  assistance.
- **SC-004**: In at least 95% of scripted runs, users can complete setup/start
  on Stream and discover/select/view on View after navigating from Home without
  app restart.
- **SC-005**: Across representative phone and tablet validation devices,
  top-level navigation regressions (blocked routes, wrong active destination,
  or duplicate destination stacking) occur in fewer than 2% of runs.
