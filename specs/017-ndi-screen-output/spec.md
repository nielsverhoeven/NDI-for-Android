# Feature Specification: NDI Screen Share Output - Redesign

**Feature Branch**: 017-ndi-screen-output  
**Created**: 2026-03-27  
**Status**: Draft  
**Input**: User description: "Redo stream menu so it is a real NDI screen-share control with consent, background persistence, and correct discovery behavior."

## Clarifications

### Session 2026-03-27

- Q: If discovery server is configured but unreachable, should streaming fail or fall back? -> A: Fail stream start and show a clear error.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Start Screen Share as NDI Output (Priority: P1)

A user opens the Output/Stream tab and sees a purpose-built screen-share panel, not viewer-like incoming stream content. They tap Share Screen, confirm consent, and the device screen starts broadcasting as an NDI source.

**Why this priority**: This is the core value of the feature.

**Independent Test**: On publisher device, tap Share Screen and confirm consent; verify source becomes available and playable on receiver within 30 seconds.

**Acceptance Scenarios**:

1. **Given** Output tab is open and stream is idle, **When** user taps Share Screen and grants consent, **Then** state transitions STARTING to ACTIVE and receiver can view stream.
2. **Given** stream is ACTIVE, **When** user inspects Output tab, **Then** current stream name and ACTIVE status are visible.
3. **Given** stream is ACTIVE, **When** user taps Stop Sharing, **Then** state transitions to STOPPED and source is withdrawn from discovery.
4. **Given** Output tab is open and no stream is active, **When** user views screen, **Then** only output controls are shown and no viewer tab telemetry/content is shown.

---

### User Story 2 - Consent Always Required Per Start (Priority: P2)

Every new start requires explicit consent. Stop fully ends the session so next start prompts again.

**Why this priority**: Privacy and trust requirement.

**Independent Test**: Start stream and grant consent, stop stream, start again; consent prompt must appear again.

**Acceptance Scenarios**:

1. **Given** no stream is active, **When** user taps Share Screen, **Then** consent prompt appears before capture starts.
2. **Given** a stream was stopped, **When** user starts again, **Then** consent prompt appears again.
3. **Given** user denies consent, **When** prompt closes, **Then** stream remains idle and explanation is shown.
4. **Given** stream is already ACTIVE, **When** user revisits Output tab, **Then** no duplicate consent prompt appears for that active session.

---

### User Story 3 - Background Stream Persistence (Priority: P3)

A user starts streaming and minimizes the app; streaming remains active and mirrors whatever is on the device screen. Notification remains visible with stop action.

**Why this priority**: Main real-world usage depends on background continuity.

**Independent Test**: Start stream, press Home, open another app, verify receiver still shows content for at least 5 minutes.

**Acceptance Scenarios**:

1. **Given** stream is ACTIVE, **When** app is backgrounded, **Then** stream remains ACTIVE and visible on receiver.
2. **Given** stream runs in background, **When** user taps notification, **Then** app opens Output tab with ACTIVE state.
3. **Given** stream runs in background, **When** user taps notification Stop action, **Then** stream stops and notification is dismissed.

---

### User Story 4 - Discovery Behavior (Priority: P4)

When active, stream announces through configured discovery server; if no server is configured, use mDNS. If configured server is unreachable, stream start fails with clear error.

**Why this priority**: Discoverability must be deterministic and testable.

**Independent Test**: Validate three modes: configured reachable server, no server configured, configured unreachable server.

**Acceptance Scenarios**:

1. **Given** discovery server is configured and reachable, **When** stream is ACTIVE, **Then** source is listed via discovery server.
2. **Given** no discovery server is configured, **When** stream is ACTIVE, **Then** source is discoverable via mDNS on local subnet.
3. **Given** discovery server is configured but unreachable, **When** user starts sharing, **Then** stream does not enter ACTIVE and a clear discovery error is shown.

---

### Visual Change Quality Gate *(mandatory when UI changes are present)*

- Add emulator-run Playwright coverage for idle panel, consent-to-active flow, stop flow, restart re-consent, and background notification behavior.
- Run and keep passing all existing Playwright e2e tests.

### Test Environment & Preconditions *(mandatory)*

- Required devices: two emulators or emulator plus physical receiver path.
- Preflight command: scripts/verify-android-prereqs.ps1 must pass.
- NDI SDK prerequisite must match docs/android-prerequisites.md.
- For discovery-server tests, a reachable server is required; if absent, record blocked with unblocking step.

### Edge Cases

- Consent denied: remain idle and show explanatory message.
- Consent revoked during ACTIVE: transition to INTERRUPTED and expose retry.
- App force-stop during ACTIVE: stream terminates and session consent is not reused.
- Android OS chooser behavior: use full-screen path where API permits; if OS requires user choice, continue with selected mode.
- Duplicate start tap during STARTING: ignore duplicate and keep single in-flight start.
- Configured discovery server unreachable: fail start and show discovery connectivity error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Output tab MUST present a distinct screen-share control UI and must not replicate viewer tab information.
- **FR-002**: Output tab MUST provide a prominent Share Screen action in idle states.
- **FR-003**: Share Screen MUST present a user consent prompt before capture starts.
- **FR-004**: Consent MUST be required for every new stream start.
- **FR-005**: Stop MUST fully clear active session consent state.
- **FR-006**: Stream MUST continue while app is backgrounded.
- **FR-007**: Active stream MUST show persistent notification with direct Stop action.
- **FR-008**: If discovery server is configured and reachable, stream MUST register there.
- **FR-009**: If no discovery server is configured, stream MUST advertise via mDNS.
- **FR-010**: If discovery server is configured but unreachable, stream start MUST fail, output state MUST transition to INTERRUPTED, and UI MUST show a clear actionable error.
- **FR-011**: Output tab MUST display human-readable state: READY, STARTING, ACTIVE, STOPPING, STOPPED, INTERRUPTED.
- **FR-012**: User MUST be able to set outgoing stream name with default pre-populated value.
- **FR-013**: Denied consent MUST leave stream idle and show explanation.
- **FR-014**: Interrupted stream MUST support retry within the existing 15-second recovery window.
- **FR-015**: Share Screen flow MUST initiate full-device capture where Android APIs permit pre-selection.
- **FR-016**: Visual updates MUST include emulator-run Playwright coverage for changed flows.
- **FR-017**: Existing Playwright suites MUST continue passing.
- **FR-018**: Validation MUST run and record preflight checks before e2e/release gates.
- **FR-019**: Validation reporting MUST classify failures as code defect or environment blocker with reproduction details.

### Key Entities

- **NDI Screen Share Session**: outgoing stream session state, stream name, discovery mode, error state.
- **Screen Capture Consent**: per-session grant/deny decision and token reference, cleared on stop.
- **Discovery Configuration**: configured server endpoint or none.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: First-time user reaches ACTIVE discoverable stream within 30 seconds in supported environment.
- **SC-002**: 100% of new starts show consent prompt before capture.
- **SC-003**: 100% of stop then restart sequences re-prompt consent.
- **SC-004**: Stream remains visible on receiver for at least 5 minutes while app is backgrounded.
- **SC-005**: Stream appears on receiver within 10 seconds when discovery server is reachable or when mDNS mode is active.
- **SC-006**: For unreachable configured discovery server, 100% of start attempts fail safely with explicit discovery error and no ACTIVE session.
- **SC-007**: Existing navigation/source list/viewer e2e suites remain passing.

## Assumptions

- NDI SDK and MediaProjection support are available per project prerequisites.
- Android platform may still show a system capture chooser depending on OS policy.
- Existing foreground service architecture is sufficient for background persistence.
