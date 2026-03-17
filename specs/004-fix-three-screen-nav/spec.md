# Feature Specification: Three-Screen Navigation Repairs and E2E Compatibility

**Feature Branch**: `004-fix-three-screen-nav`  
**Created**: 2026-03-17  
**Status**: Draft  
**Input**: User description: "Repair three-screen navigation iconography and routing behavior, enforce correct active menu highlighting, and make Android-version-aware E2E validation faster and deterministic."

## Clarifications

### Session 2026-03-17

- Q: For dual-emulator E2E runs, how should mixed Android versions be handled across publisher and receiver? -> A: Allow mixed supported versions and select consent/navigation handling per device based on each detected version.
- Q: How should version-specific E2E behavior be organized in test architecture? -> A: Use one unified E2E suite that branches at runtime based on detected Android version per device.
- Q: What should happen when a device is on an unsupported Android major version? -> A: Fail the E2E job immediately with clear unsupported-version diagnostics.
- Q: From View root, what should Back do? -> A: Back from View root returns to Home.
- Q: How should the supported Android-version window be defined for E2E compatibility checks? -> A: Use a rolling latest-5 Android major versions window automatically at runtime.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View Flow Routes to Viewer Correctly (Priority: P1)

As a user in the View destination, I want selecting a discovered stream to open the viewer screen (not stream setup) and allow me to return to the View root screen so I can browse and watch sources without getting stuck in the wrong flow.

**Why this priority**: This is a core journey regression that blocks the main purpose of the View destination.

**Independent Test**: From View root, select a discovered source, verify viewer playback screen opens, then navigate back and confirm the View root list is shown again.

**Acceptance Scenarios**:

1. **Given** the user is on the View root list, **When** the user selects a discovered source, **Then** the app opens the viewer screen for that source and does not open stream setup.
2. **Given** the user is on the viewer screen after selecting a source from View, **When** the user navigates back once, **Then** the app returns to the View root list.
3. **Given** the user reached viewer from View, **When** the user navigates back from the View root list, **Then** the app returns to Home.

---

### User Story 2 - Navigation Icons and Highlight State Are Consistent (Priority: P2)

As a user, I want each top-level destination to have the correct icon and active-state highlighting so I can always understand where I am in the app.

**Why this priority**: Incorrect iconography and highlighted state reduce navigation clarity and cause user mistakes.

**Independent Test**: Open Home, Stream, and View destinations and verify icon mapping and highlighted destination state in each screen, including screens reached through deep links.

**Acceptance Scenarios**:

1. **Given** the top-level navigation is visible, **When** destinations are rendered, **Then** Home shows a house icon, Stream shows a camera icon, and View shows a screen icon.
2. **Given** the user is on any stream setup/control screen, **When** navigation is visible, **Then** the Stream destination is highlighted as active.
3. **Given** the user navigates between Home, Stream, and View through menu taps, **When** each destination opens, **Then** exactly one destination is highlighted and it matches the visible destination.

---

### User Story 3 - E2E Validation Adapts to Android Version and Runs Faster (Priority: P3)

As a release engineer, I want E2E validation to detect device Android versions, run the correct consent/navigation flow for those versions, and use short inter-step waits so test runs are reliable and quick.

**Why this priority**: Current E2E runs are slow and brittle across Android versions, reducing confidence and delaying verification.

**Independent Test**: Execute E2E on devices with supported Android major versions; verify each run records version detection, follows the matching consent flow, and enforces a maximum 1-second intentional delay between sequential UI steps.

**Acceptance Scenarios**:

1. **Given** an E2E run starts on publisher and receiver devices, **When** environment validation executes, **Then** the run records each device Android major version and either proceeds with a supported flow or exits with a clear unsupported-version message.
2. **Given** publisher and receiver use different supported Android major versions, **When** flow selection runs, **Then** the run continues and applies consent/navigation handling per device version.
3. **Given** the device shows a version-specific capture/share consent sequence, **When** consent handling runs, **Then** the automation selects the correct full-screen sharing path and reaches active streaming state.
4. **Given** the E2E suite executes step transitions, **When** intentional static step delays are applied, **Then** no delay exceeds 1 second.
5. **Given** supported Android versions are detected, **When** the test run starts, **Then** one unified suite executes runtime version branching instead of selecting separate scripts.
6. **Given** publisher or receiver reports an unsupported Android major version, **When** pre-run validation executes, **Then** the E2E job fails immediately with explicit diagnostics and does not continue into flow steps.
7. **Given** the platform Android ecosystem advances, **When** a test run starts, **Then** support eligibility is evaluated against a rolling latest-five major-version window automatically at runtime.

### Edge Cases

- Publisher and receiver run different Android major versions inside the supported window; run must proceed with per-device flow handling.
- A supported Android version presents either combined consent UI or two-step consent UI for screen capture.
- Full-screen sharing choice text varies slightly while still meaning full-screen share.
- A device reports an unsupported Android major version; run must fail early with a clear reason.
- User enters stream setup from View-related navigation paths; Stream must still be highlighted as active.
- Back navigation from viewer is triggered after a failed stream start; app must still return to View root without routing to Stream.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST keep three top-level destinations: Home, Stream, and View.
- **FR-002**: The Home destination MUST display a house icon in top-level navigation.
- **FR-003**: The Stream destination MUST display a camera icon in top-level navigation.
- **FR-004**: The View destination MUST display a screen icon in top-level navigation.
- **FR-005**: Selecting a source from the View root list MUST open the viewer screen for that source and MUST NOT route to stream setup.
- **FR-006**: From the viewer screen reached via View, one back navigation action MUST return to the View root list.
- **FR-006a**: From the View root list, one back navigation action MUST return to Home.
- **FR-007**: On every stream setup/control screen, the Stream destination MUST be highlighted as the active top-level destination.
- **FR-008**: Top-level destination highlighting MUST always match the currently visible destination and MUST never highlight multiple destinations simultaneously.
- **FR-009**: E2E validation MUST detect and record the Android major version of each test device at run start.
- **FR-010**: E2E validation MUST support a rolling latest-five Android major-version window, evaluated automatically at runtime, and fail fast for unsupported versions with a clear diagnostic message.
- **FR-010a**: E2E validation MUST allow publisher and receiver to run different supported Android major versions in the same test run.
- **FR-010b**: Unsupported-version detection MUST terminate the E2E job with a non-zero outcome before any stream/view interaction steps are executed.
- **FR-011**: For supported versions, E2E validation MUST execute consent/navigation flow selection per device version for screen capture and continue to streaming validation only after the full-screen sharing path is confirmed on the publisher device.
- **FR-011a**: E2E validation MUST use a single unified test suite with runtime version-branching logic and MUST NOT require separate version-specific top-level scripts.
- **FR-012**: E2E validation MUST limit intentional static delay between sequential UI steps to a maximum of 1 second.

### Key Entities *(include if feature involves data)*

- **Navigation Destination State**: Represents current top-level destination identity, icon mapping, and active-highlight state.
- **View Navigation Session**: Represents user movement from View root list to viewer screen and back-navigation return point.
- **Device Version Profile**: Represents detected Android major version per test device and support-window eligibility.
- **Consent Flow Variant**: Represents the expected full-screen capture consent sequence associated with a supported Android version.
- **Runtime Version Branch**: Represents the flow branch chosen at runtime for a device based on detected Android major version within one unified suite.

## Assumptions

- The supported Android-version window is rolling and automatically evaluated at runtime rather than pinned to hardcoded major versions in this spec.
- Existing stream and viewer business behavior remains unchanged except for route correction, back-navigation correction, and destination highlight correctness.
- E2E speed improvements are achieved by reducing static delays, while dynamic waits for required UI state changes remain allowed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In 100% of validation runs, selecting a source from View opens viewer (not stream setup).
- **SC-002**: In at least 98% of validation runs, back navigation from viewer returns to View root in one action.
- **SC-003**: In 100% of audited screens where stream setup/control is visible, Stream is the active highlighted destination.
- **SC-004**: In 100% of E2E runs, Android major version is recorded for both test devices before flow execution.
- **SC-005**: In 100% of E2E step definitions, no intentional static inter-step delay exceeds 1 second.
- **SC-006**: Median end-to-end runtime for the dual-emulator regression suite improves by at least 25% versus the pre-change baseline while maintaining equivalent pass/fail intent.
