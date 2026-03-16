# Feature Specification: Stream NDI Source to Network

**Feature Branch**: `002-stream-ndi-source`  
**Created**: 2026-03-16  
**Status**: Draft  
**Input**: User description: "add a feature to the app that enables it to stream an NDI source to the network."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Start Network Output from a Selected Source (Priority: P1)

As an operator using the app, I want to select an NDI source and start a network
output stream so other NDI-capable devices on the same network can receive it.

**Why this priority**: This is the core business value of the feature. Without
starting a network output, the feature does not exist in a usable form.

**Independent Test**: With at least one available NDI source and one external
NDI receiver on the same network, the operator can select a source, start output,
and observe the stream on the receiver.

**Acceptance Scenarios**:

1. **Given** the app lists at least one available source, **When** the operator
   selects a source and starts network output, **Then** the app starts
   transmitting that source as an outbound NDI stream discoverable by receivers
   on the same network.
2. **Given** output is active, **When** an external receiver browses network NDI
   streams, **Then** the receiver can discover and open the stream produced by
   this app.

---

### User Story 2 - Control and Monitor Output Session (Priority: P2)

As an operator, I want to see clear output status and be able to stop output so
I can control when the app is broadcasting.

**Why this priority**: Operators need confidence and control over active network
broadcasting to avoid unintended transmission and reduce operational mistakes.

**Independent Test**: Start output, verify status indicators update, stop output,
and verify receivers no longer see an active stream from this app.

**Acceptance Scenarios**:

1. **Given** output is not active, **When** the operator starts output,
   **Then** the app shows that output is active and identifies the source being
   broadcast.
2. **Given** output is active, **When** the operator stops output, **Then** the
   app ends transmission and updates status to inactive.

---

### User Story 3 - Recover from Interruptions Gracefully (Priority: P3)

As an operator, I want interruption messages and recovery actions when source or
network connectivity fails so I can restore output quickly.

**Why this priority**: Recovery improves reliability, but depends on the core
start/stop flow already working.

**Independent Test**: Start output, simulate source or network loss, and verify
the app surfaces interruption status and supports retry or stop actions.

**Acceptance Scenarios**:

1. **Given** output is active, **When** the input source becomes unavailable,
   **Then** the app shows an interruption state and provides actions to retry or
   stop output.
2. **Given** output failed due to transient network issues, **When** connectivity
   returns and the operator retries, **Then** output resumes without requiring
   app restart.

### Edge Cases

- The selected source disappears between selection and output start; the app
  must block activation and show a recoverable message.
- The operator starts output with a stream name that conflicts with an existing
  network stream name; the app must resolve the conflict with a clear,
  user-visible unique naming outcome.
- The operator rapidly taps start or stop multiple times; the app must prevent
  duplicate session creation and preserve consistent status.
- The app moves to background while output is active; the app must keep behavior
  predictable and communicate any state changes when returning to foreground.
- Network bandwidth degrades during output; the app must surface degraded-output
  status instead of silently appearing successful.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow the operator to select one discoverable NDI
  source as the input for network output.
- **FR-002**: The system MUST allow the operator to start broadcasting the
  selected input source as an outbound NDI stream on the local network.
- **FR-003**: The system MUST expose clear output session states: ready, starting,
  active, stopping, stopped, and interrupted.
- **FR-004**: The system MUST show the active input source and outbound stream
  identity while output is active.
- **FR-005**: The system MUST allow the operator to stop an active output session
  at any time.
- **FR-006**: The system MUST prevent concurrent duplicate output sessions for the
  same operator action sequence.
- **FR-007**: The system MUST validate that the selected input source is reachable
  before entering active output state.
- **FR-008**: If input source reachability is lost during output, the system MUST
  transition to interrupted state and provide recovery actions.
- **FR-009**: Recovery actions MUST include retry output and stop output.
- **FR-010**: The system MUST preserve a default outbound stream name preference
  so operators do not need to re-enter it each session.
- **FR-011**: The system MUST ensure outbound stream naming is unique and
  discoverable on the local network.
- **FR-012**: The system MUST emit non-sensitive telemetry events for output
  start, output stop, interruption, and recovery attempts.
- **FR-013**: The system MUST keep the feature usable on both phone and tablet
  layouts.
- **FR-014**: The system MUST support Android API 24+ and remain compliant with
  the repo-supported latest stable compatible compileSdk/targetSdk baseline.

### Constitutional Requirements *(mandatory)*

- **CR-001 (Architecture)**: Output control and lifecycle state MUST be managed
  through ViewModel and repository boundaries. UI screens MUST only render state
  and dispatch user actions. Data and session orchestration MUST stay behind
  domain contracts and repository interfaces.
- **CR-002 (Quality)**: Tests MUST be defined before implementation for start,
  stop, interruption, and retry behavior. Automated tests MUST cover state
  transitions, user actions, and failure handling before completion.
- **CR-003 (UX and Performance)**: Output control UI MUST follow Material Design 3
  patterns for status communication and error feedback. The feature MUST avoid
  unnecessary background work and minimize device battery impact during inactive
  periods.
- **CR-004 (Data and Security)**: Persisted data MUST be limited to operator
  preferences and non-sensitive continuity state. Any new permission request MUST
  include explicit feature-level justification and MUST be rejected if not
  strictly required.
- **CR-005 (Build and Modularity)**: Feature behavior MUST stay within existing
  module boundaries (presentation, domain, data, sdk bridge) without bypassing
  repository-mediated access. Release validation MUST include optimized/shrunk
  build verification.
- **CR-006 (Toolchain Currency)**: The feature MUST document and remain compatible
  with the repo-supported baseline for compileSdk/targetSdk, AGP, Gradle,
  Kotlin, JDK/JBR, AndroidX/Jetpack, and NDI SDK dependencies. Any blocker to
  adopting newer stable compatible versions MUST be tracked with owner and target
  resolution timing.

### Key Entities *(include if feature involves data)*

- **Output Session**: Represents one broadcast attempt, including selected input
  source identity, outbound stream identity, session state, start/stop timestamps,
  and interruption reason when relevant.
- **Output Configuration**: Operator-controlled settings for broadcast behavior,
  including preferred outbound stream name and last selected source identity.
- **Output Health Snapshot**: A current operational view containing reachability,
  status, and quality indicators used to inform user-visible output state.

## Assumptions

- Operators and receiving devices are on a reachable local network segment.
- One outbound output session is active at a time per app instance.
- The initial release targets local-network broadcasting only (no internet relay
  or cloud routing).
- Audio/video fidelity tuning is outside scope unless required to keep output
  operationally usable.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In controlled validation runs with active input sources, operators
  can start outbound network output within 5 seconds in at least 90% of attempts.
- **SC-002**: In controlled validation runs, at least 95% of stop commands end
  outbound transmission within 2 seconds.
- **SC-003**: At least 95% of interruption events present a clear recovery path
  (retry or stop) without requiring app restart.
- **SC-004**: In usability testing, at least 90% of operators can complete the
  flow select source -> start output -> stop output on first attempt without
  assistance.
- **SC-005**: Across representative phone and tablet validation devices, the
  primary output flow succeeds in at least 95% of release-readiness runs.
