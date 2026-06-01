# Feature Specification: Viewer Persistence and Stream Availability Status

**Feature Branch**: 021-viewer-persistence-status  
**Created**: 2026-03-29  
**Status**: Draft  
**Input**: User description: "I want to add some additional features to the viewing part of the app:
- Retain previously viewed stream including the image of the last viewed frame.
- show for each available stream if it was previously connected and if it is availble to connect to right now. If not available, the 'view stream' button should be disabled."

## Clarifications

### Session 2026-03-29

- Q: How long should Previously Connected state be retained? -> A: Persist locally across app restarts; reset on app data clear or uninstall.
- Q: What qualifies a stream as Previously Connected? -> A: Mark only after at least one video frame is successfully rendered.
- Q: If last viewed stream is unavailable at relaunch, what should viewer do? -> A: Show saved last-frame preview and unavailable state; do not auto-start playback.
- Q: How many saved last-frame images are in scope? -> A: Keep exactly one image for the last viewed stream only.
- Q: How should availability handle stale discovery data? -> A: Treat stream as unavailable after two consecutive missed discovery polls.

## User Scenarios and Testing *(mandatory)*

### User Story 1 - Restore Last Viewed Stream on App Relaunch (Priority: P1)

A user views an NDI stream, then closes the app or navigates away. On returning to the app, they expect to find the same stream context with the last captured frame visible, even if live playback is not immediately possible.

**Why this priority**: Restoring the last viewing context removes repeated setup work and improves continuity for frequent viewers.

**Independent Test**: Launch app, view stream A until at least one frame renders, close app, relaunch app, verify stream A is restored as last context and one saved frame preview is shown.

**Acceptance Scenarios**:

1. **Given** user is viewing stream Cam1 and at least one frame has rendered, **When** a newer frame is rendered, **Then** system updates persisted stream ID and last-frame image.
2. **Given** app is relaunched after Cam1 was viewed, **When** viewer state is restored, **Then** Cam1 is shown as last viewed and its saved preview frame is displayed.
3. **Given** last viewed stream is currently unavailable at relaunch, **When** viewer is opened, **Then** saved preview is shown with unavailable state and live playback is not auto-started.
4. **Given** a different stream becomes last viewed, **When** at least one frame renders for that stream, **Then** previous saved frame is replaced and only one saved last-frame image remains.

---

### User Story 2 - Show Availability and Connection History in Source List (Priority: P1)

A user sees each stream row indicate whether it was previously connected and whether it is currently available to connect. If unavailable, the View Stream action is disabled.

**Why this priority**: Immediate status visibility reduces failed connection attempts and improves user trust.

**Independent Test**: With streams in mixed states (new, previously connected, available, unavailable), open source list and verify indicators plus button state for each row.

**Acceptance Scenarios**:

1. **Given** Cam1 was previously connected and is currently available, **When** source list is shown, **Then** Previously Connected is visible and View Stream is enabled.
2. **Given** Cam2 was previously connected and becomes unavailable, **When** source list refreshes, **Then** Previously Connected and Unavailable are visible and View Stream is disabled.
3. **Given** Cam3 has never been connected and is available, **When** source list is shown, **Then** Previously Connected is not shown and View Stream is enabled.
4. **Given** Cam4 is stale after two consecutive missed discovery polls, **When** source list refreshes, **Then** Cam4 is marked unavailable and View Stream is disabled.
5. **Given** user taps disabled View Stream for an unavailable source, **When** tap occurs, **Then** no navigation occurs.

---

### Visual Change Quality Gate *(mandatory when UI changes are present)*

This feature changes visual behavior on source list and viewer screens. Emulator-run Playwright end-to-end coverage must include:

- Indicator rendering for Previously Connected and Unavailable states.
- View Stream enabled and disabled behavior per availability state.
- Viewer restore behavior with saved preview frame.
- Unavailable last-viewed restore behavior without auto-play.
- Regression execution of all existing Playwright e2e tests with no failures.

### Test Environment and Preconditions *(mandatory)*

**Runtime Dependencies**:
- Android emulator matching project baseline.
- NDI discovery environment with at least one stable source and one intermittently unavailable source.
- Writable local storage for persisted metadata and one saved preview image.

**Preflight Verification Command**:

```bash
scripts/verify-android-prereqs.ps1
```

**Preconditions Before E2E**:

1. Discovery path is reachable from emulator test environment.
2. Test fixtures provide deterministic available and unavailable transitions.
3. App data is reset before each scenario to validate persistence transitions deterministically.

**Blocked Gate Handling**:

- If discovery is unreachable, record Environment Blocked with reproduction notes and stop e2e execution.
- If storage write path is unavailable, record Environment Blocked for persistence coverage and continue non-persistence validations where possible.

### Edge Cases

- App closes during frame write: restore stream ID and fall back to placeholder if saved image is unreadable.
- Previously connected source reappears: keep Previously Connected and re-enable View Stream once available.
- Storage full: do not crash; keep stream ID persistence and skip preview update with error logging.
- Rapid availability flapping: availability state changes only after two consecutive missed polls for unavailable transition.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST persist the identifier of the last viewed stream.
- **FR-002**: System MUST persist exactly one preview image representing the last rendered frame of the last viewed stream.
- **FR-003**: System MUST restore last viewed stream context and saved preview image on app relaunch.
- **FR-004**: System MUST mark a stream as Previously Connected only after at least one frame from that stream is successfully rendered.
- **FR-005**: Previously Connected state MUST persist across app restarts and reset only on app data clear or uninstall.
- **FR-006**: System MUST show current availability state for each stream in source list.
- **FR-007**: System MUST classify a stream as unavailable after two consecutive missed discovery polls.
- **FR-008**: System MUST display unavailable state in source list and disable View Stream for unavailable streams.
- **FR-009**: System MUST block navigation when disabled View Stream is tapped.
- **FR-010**: If last viewed stream is unavailable at restore time, system MUST show saved preview and unavailable state without auto-starting playback.
- **FR-011**: UI changes introduced by this feature MUST be covered by emulator-run Playwright e2e tests.
- **FR-012**: Existing Playwright e2e tests MUST remain passing.
- **FR-013**: Validation runs MUST execute and report preflight readiness checks before e2e execution.

### Key Entities *(include if feature involves data)*

- **LastViewedContext**: stream identifier, saved preview path, last successful frame timestamp.
- **ConnectionHistoryState**: per-stream Previously Connected flag with first-success timestamp.
- **DiscoveryAvailabilityState**: per-stream availability flag and missed-poll counter.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Last viewed context is restored within 1 second of app relaunch in test conditions.
- **SC-002**: Source list availability and button enabled-state accuracy is at least 99 percent across test scenarios.
- **SC-003**: Previously Connected indicator appears only for streams with at least one successful rendered frame in 100 percent of evaluated cases.
- **SC-004**: Unavailable transition occurs only after two consecutive missed polls in 100 percent of evaluated cases.
- **SC-005**: All existing Playwright e2e tests pass with zero regressions.

## Assumptions

- Existing discovery polling is reused and not replaced.
- One preview image is sufficient for user value in this iteration.

## Constraints

- Preview persistence must not block playback rendering path.
- Storage footprint must remain bounded to one persisted preview image.
