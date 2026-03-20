# Feature Specification: Dual-Emulator NDI Latency Measurement

**Feature Branch**: `009-measure-ndi-latency`  
**Created**: 2026-03-20  
**Status**: Draft  
**Input**: User description: "setup a test scenario in which 1. in emulater A an NDI stream is started, 2. then in emulator B the NDI stream is opened to view 3. start recording both screens 4. in emulator A, a random video is started on youtube 5. on emulator B the video is visible and playing through the NDI stream view 6. then analyzes both videos and determine the latency"

## Clarifications

### Session 2026-03-20

- Q: Which primary latency analysis method should this feature require? -> A: Motion/content cross-correlation between Emulator A and Emulator B recordings.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Measure end-to-end stream latency (Priority: P1)

As a QA engineer, I can run an automated dual-emulator scenario that starts an NDI source on Emulator A, views that source on Emulator B, records both displays, and reports latency between source playback and receiver playback.

**Why this priority**: This provides direct validation of real-time streaming quality and is the core business value of the request.

**Independent Test**: Run the single scenario from start to finish and verify a structured latency result is produced from synchronized recordings.

**Acceptance Scenarios**:

1. **Given** both emulators are online and app prerequisites are met, **When** the scenario starts output on Emulator A and opens the stream on Emulator B, **Then** both sides reach active streaming state.
2. **Given** active streaming state on both emulators, **When** recording starts and a YouTube video plays on Emulator A, **Then** Emulator B visibly renders the same moving content through NDI.
3. **Given** recordings from both emulators are captured for the same playback window, **When** latency analysis runs, **Then** the run outputs a numeric latency result and an analysis artifact.

---

### User Story 2 - Detect invalid measurement runs (Priority: P2)

As a QA engineer, I can see explicit failure reasons when a latency measurement is invalid, so results are not mistaken for successful runs.

**Why this priority**: Reliable diagnostics prevent false confidence and reduce triage time.

**Independent Test**: Trigger a run where the receiver never starts playback and verify the run fails with a clear reason and retains captured evidence.

**Acceptance Scenarios**:

1. **Given** stream playback cannot be established on Emulator B, **When** measurement is attempted, **Then** the run fails with a specific cause and stores recordings/logs for investigation.
2. **Given** recordings are missing or unusable, **When** latency analysis starts, **Then** no latency value is emitted and the run is marked invalid.

---

### User Story 3 - Preserve baseline regression confidence (Priority: P3)

As a release owner, I can run this latency scenario without regressing existing emulator test coverage.

**Why this priority**: New test capability must not reduce confidence in already protected user flows.

**Independent Test**: Execute the new scenario and then execute existing e2e coverage; verify existing suites remain passing (outside known waivers).

**Acceptance Scenarios**:

1. **Given** the new latency scenario is added, **When** existing end-to-end suites are executed, **Then** they remain green under current quality-gate policy.

### Visual Change Quality Gate *(mandatory when UI changes are present)*

- This feature adds visual test behavior (new end-to-end viewing and measurement flow), so emulator-run end-to-end coverage is required for the user-visible scenario.
- Existing end-to-end suites must still execute and pass according to the project regression-gate policy.

### Edge Cases

- Emulator B shows the source list but does not begin playback within timeout.
- YouTube playback on Emulator A is blocked by network restrictions, consent prompts, or app not installed.
- Screen recording starts on one emulator but fails on the other.
- Recorded clips have different durations or cannot be aligned to a common comparison window.
- Video remains visible but is frozen, making motion-based latency analysis invalid.
- Multiple discoverable sources exist and the scenario selects the wrong source.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a deterministic dual-emulator test flow that starts NDI output on Emulator A and opens that output in the viewer on Emulator B.
- **FR-002**: The system MUST start recording on both emulators before source playback begins.
- **FR-003**: The system MUST launch and play a random YouTube video on Emulator A after both recordings are active.
- **FR-004**: The system MUST verify that Emulator B displays actively playing video content from the NDI viewer during the measurement window.
- **FR-005**: The system MUST analyze both recordings and compute end-to-end latency as a numeric result in milliseconds.
- **FR-005a**: The primary latency computation method MUST use motion/content cross-correlation between source and receiver recordings.
- **FR-006**: The system MUST persist run artifacts required to audit the measurement result, including both recordings and analysis output.
- **FR-007**: The system MUST fail the run with explicit, step-level reasons when prerequisites, playback visibility, recording integrity, or analysis validity are not met.
- **FR-008**: The system MUST include emulator-run end-to-end coverage for this user-visible latency scenario.
- **FR-009**: The system MUST execute and keep passing all existing end-to-end suites covered by the regression gate policy.

### Key Entities *(include if feature involves data)*

- **LatencyMeasurementRun**: One end-to-end scenario execution, including start time, end time, completion status, and failure reason when applicable.
- **RecordingArtifact**: Captured video evidence for each emulator, including role (source/receiver), duration, and file reference.
- **LatencyAnalysisResult**: Measurement output containing computed latency (milliseconds), confidence/validity status, and supporting metadata.
- **ScenarioStepCheckpoint**: Ordered step outcomes for stream start, stream view, recording start, playback trigger, receiver visibility, and analysis completion.

### Assumptions

- Emulator A and Emulator B are pre-provisioned and reachable by automation.
- The NDI app build under test is installed on both emulators.
- YouTube is available on Emulator A and playable content can be selected automatically.
- Existing regression-gate definitions remain the source of truth for pass/fail policy.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a stable test environment, at least 90% of scheduled runs complete with a valid latency value.
- **SC-002**: For successful runs, the scenario reaches analyzed latency output in under 10 minutes end-to-end.
- **SC-003**: 100% of failed runs include a single explicit failed step and retrievable evidence artifacts for both emulators.
- **SC-004**: 100% of successful runs produce both source and receiver recordings plus one persisted latency analysis artifact.
- **SC-005**: Existing regression suites continue to satisfy the project quality gate with no newly introduced unexpected failures.
