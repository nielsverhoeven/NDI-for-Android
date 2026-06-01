# Feature Specification: Optimize NDI Stream Playback with Quality Controls

**Feature Branch**: `020-optimize-stream-playback`  
**Created**: March 28, 2026  
**Status**: Draft  
**Input**: User description: "I want to make some additional adjustments to the view page: The player area should auto-fit the streamed content, when on the player screen, I want to have setting to manipulate the quality of the NDI stream, for instance choosing a smooth play over a higher quality, currently the NDI stream is not running smooth. Optimize the playing of the NDI stream to play smoothly by default, and then let the user adjust settings as he deems fit."

## Clarifications

### Session March 28, 2026

- Q1: How should the system respond to complete NDI stream disconnections? → A: Show "Stream Disconnected" dialog with "Reconnect" button, retain quality preference, retry up to 5 times then prompt user.
- Q2: How should user quality preferences be stored securely? → A: Local SharedPreferences (device-scoped only), non-encrypted. Quality presets are UX preferences, not sensitive data.
- Q3: What accessibility requirements apply to the quality settings UI? → A: Preset labels must be descriptive with use case hints. Menu must support TalkBack/VoiceOver semantics. Include in regression scope.

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.
  
  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - NDI Stream Plays Smoothly by Default (Priority: P1)

A user opens an NDI stream from the source list. The stream should playback smoothly without stuttering, frame drops, or playback interruptions. The default codec and quality settings are optimized for smooth playback on the device capabilities, prioritizing frames-per-second and consistency over maximum resolution.

**Why this priority**: Users expect smooth video playback as the baseline experience. A choppy or stuttering stream degrades the entire application experience and is the most critical user-facing issue. This must be resolved before adding quality options.

**Independent Test**: User can launch app, open any available NDI source, and observe continuous smooth playback without visible frame drops or audio/video sync issues for at least 30 seconds. This can be validated independently by optimizing default playback settings alone.

**Acceptance Scenarios**:

1. **Given** the app is running with a connected NDI source available, **When** the user navigates to the Viewer screen, **Then** the video stream begins playback without stuttering or frame drops for the entire viewing duration.
2. **Given** the video is playing, **When** the frame rate drops below 24 fps, **Then** the system automatically adjusts quality settings downward to restore smooth playback.
3. **Given** the video is streaming, **When** temporary network slowdown occurs, **Then** playback remains buffer-stable without interruption (system may pause briefly to buffer but does not reconnect).
4. **Given** the NDI stream is active, **When** the connection completely disconnects (NDI source becomes unreachable or network unavailable), **Then** playback stops, a "Stream Disconnected" dialog appears with a "Reconnect" button, and the user's quality preference is retained for the reconnect attempt.

---

### User Story 2 - Player Area Auto-Fits Streamed Content (Priority: P2)

A user views an NDI stream on the Viewer screen. The video player automatically scales and positions the streamed content to fit the available player area while maintaining the original aspect ratio, ensuring no distortion or letterboxing compromises the viewing experience.

**Why this priority**: Auto-fit prevents awkward viewing experiences and ensures the stream content is always visible and properly proportioned. This improves usability on different device sizes and orientations without requiring manual configuration by the user.

**Independent Test**: User can view an NDI stream on Viewer screen in both portrait and landscape orientations. In each orientation, the video content fills the available player area proportionally without distortion or unused space. This can be validated independently by adjusting the layout constraints and aspect ratio handling.

**Acceptance Scenarios**:

1. **Given** the Viewer screen is displayed with a 16:9 aspect ratio stream on a portrait device, **When** the video is rendered, **Then** it scales to fit within the available player bounds while maintaining 16:9 ratio (letterboxing applied to width if needed).
2. **Given** the Viewer screen is displayed, **When** the device is rotated from portrait to landscape, **Then** the video automatically re-scales and re-positions to fit the new player bounds without manual user action.
3. **Given** an NDI stream with different aspect ratio (4:3, 21:9, etc.), **When** it is displayed, **Then** the player correctly scales to maintain the original aspect ratio without distortion.
4. **Given** the player area has constraints defined, **When** the stream dimensions are known, **Then** scaling calculation ensures content is always visible and centered within the bounds.

---

### User Story 3 - User Can Adjust Stream Quality Settings (Priority: P3)

A user is viewing an NDI stream and wants fine-grained control over the playback quality. From the Viewer screen settings, the user can adjust stream quality parameters (e.g., resolution, frame rate, bitrate) to trade off between smooth playback and visual fidelity based on their preference and network conditions.

**Why this priority**: This is a power-user feature that extends the base smooth playback experience. Advanced users may want higher visual quality in good network conditions or maximum smoothness in poor conditions. This should only be implemented after smooth playback is the default.

**Independent Test**: User can access quality adjustment settings from the Viewer screen, select a different quality profile (e.g., "Smooth" vs "High Quality"), and observe the stream adapt its output. This can be validated independently by adding the settings UI and quality parameter logic.

**Acceptance Scenarios**:

1. **Given** the Viewer screen is displaying a playing stream, **When** the user opens the quality/settings menu, **Then** a list of quality presets is presented (e.g., "Auto", "Smooth", "Balanced", "High Quality").
2. **Given** a quality preset is selected (e.g., "Smooth"), **When** the change is applied, **Then** the stream parameters adjust (codec, resolution, frame rate) and playback adapts without requiring manual restart.
3. **Given** the user selects "High Quality" preset, **When** network conditions are poor, **Then** the system degrades gracefully to maintain playability (or displays a warning that quality may suffer).
4. **Given** a quality preset is active, **When** the user returns to the app after backgrounding, **Then** the selected quality setting persists and is re-applied to the stream.

### Visual Change Quality Gate *(mandatory when UI changes are present)*

This feature adds a new quality settings UI to the Viewer screen and adjusts the player layout for auto-fit. Visual behavior changes include:

- Player area resizing/repositioning for content auto-fit
- New quality settings menu with preset selection

**Playwright E2E Coverage Required**:

- Test launching viewer with NDI stream and verifying smooth playback without frame drops
- Test quality settings UI is accessible and functional
- Test selecting different quality presets updates stream parameters
- Test player area auto-fits content on different device orientations

**Regression Requirement**: All existing Playwright e2e tests for Viewer screen must continue passing, including:

- Navigation to/from Viewer screen
- Playback controls (play/pause/stop if present)
- Back button behavior
- Stream connection and error handling
- Accessibility validation for quality settings UI (TalkBack/VoiceOver semantics)

### Test Environment & Preconditions *(mandatory)*

**Required Runtime Dependencies**:

- Android emulator (API 34+) or physical device with NDI Android SDK bridge compiled
- Local NDI source or NDI test server available on network (per `testing/e2e` setup)
- Dual-emulator setup for full e2e validation (see `docs/dual-emulator-setup.md`)
- Gradle build tools (9.0.0+), AGP 9.0.0, Kotlin 2.2.10

**Preflight Checks**:

1. `scripts/verify-android-prereqs.ps1` must pass (NDI SDK, JDK 21, Gradle 9.2.1)
2. Emulator must be running and accessible via `adb devices`
3. NDI test server must be discoverable on network: `scripts/verify-e2e-dual-emulator-prereqs.ps1 -AllowMissingNdiSdk`
4. Gradle build must succeed: `./gradlew :app:assembleDebug`

**Blocked Execution Recording**:

- If emulator is unavailable: Skip e2e validation, record as "BLOCKED: Emulator not ready", unblock by starting emulator
- If NDI source unavailable: Skip stream quality tests, record as "BLOCKED: NDI source unavailable", unblock by starting NDI test server
- If Gradle build fails: Record build errors, unblock by fixing compilation issues

### Edge Cases

- What happens when the NDI stream resolution changes while playing (e.g., camera changes resolutions dynamically)?
- How does the system handle rapid quality preset changes while video is buffering?
- What occurs if the device's available memory drops below threshold while streaming (especially for high-quality presets)?
- How does the player respond to device rotation while actively playing a stream?

## Requirements *(mandatory)*

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right functional requirements.
-->

### Functional Requirements

- **FR-001**: System MUST optimize default NDI stream playback settings (codec selection, frame rate, resolution) to prioritize smooth playback (minimum 24 fps) on typical Android devices without stuttering or frame drops.

- **FR-002**: System MUST implement auto-fit scaling of the player area to display NDI stream content at the native aspect ratio, filling available space without distortion, for all common aspect ratios (4:3, 16:9, 21:9, etc.).

- **FR-003**: System MUST maintain correct aspect ratio scaling when device orientation changes (portrait ↔ landscape) without manual user intervention.

- **FR-004**: System MUST provide quality settings UI on the Viewer screen allowing users to select preset quality profiles: at minimum "Smooth" (best for slow networks), "Balanced" (adaptive), and "High Quality" (prioritize fidelity), with "Smooth" as the default. Settings UI must be fully accessible to TalkBack/VoiceOver with descriptive labels including use case hints.

- **FR-005**: System MUST apply quality profile changes to the NDI stream in real time without requiring stream restart or manual reconnection.

- **FR-006**: System MUST persist user's selected quality preset in local SharedPreferences (device-scoped, non-encrypted) and re-apply it when returning to the Viewer screen in a new session. Quality presets are UX preferences, not sensitive data.

- **FR-007**: System MUST implement graceful quality degradation when network conditions deteriorate—preferring to reduce resolution/frame rate rather than stall playback.

- **FR-008**: For visual changes to Viewer screen, system MUST include Playwright e2e tests covering all new and modified functionality (player auto-fit, quality settings) run on emulator and documented in test-results.

- **FR-009**: For visual changes to Viewer screen, system MUST execute and keep passing all existing Playwright e2e tests for the Viewer screen (navigation, playback, error handling).

- **FR-010**: System MUST handle complete NDI stream disconnections by stopping playback, displaying a "Stream Disconnected" dialog with a "Reconnect" button, and retrying connection up to 5 times before prompting user to manually reconnect or return to Source List.

- **FR-011**: Validation MUST record all preflight check results (emulator readiness, NDI source availability, build status) before executing e2e or release gates, classifying failures as code issues or environment blockers.

### Key Entities

- **Quality Profile**: Represents a preset collection of codec, resolution, frame rate, and bitrate parameters. Attributes: name (string), smoothIndex (priority weighting), isDefault (Boolean).
- **PlaybackOptimization**: Encapsulates current stream quality settings. Attributes: selectedProfile (Quality Profile reference), currentFrameRate (int), currentResolution (dimensions), bufferHealth (percentage).
- **PlayerLayout**: Manages player area dimensions and scaling. Attributes: availableBounds (rectangle), contentAspectRatio (float), scalingMode (Enum: fit-to-bounds, letterbox, etc.).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: NDI stream playback maintains a minimum of 24 frames per second by default without stuttering or visible frame drops during a 60-second continuous playback session on Android devices with typical specifications.

- **SC-002**: Player area correctly displays NDI content at its native aspect ratio, filling no less than 90% of available player space on a 6-inch device screen in both portrait and landscape orientations.

- **SC-003**: User can access quality settings from the Viewer screen within 2 taps and change quality preset within 1 second with immediately observable effect on stream output.

- **SC-004**: At least 95% of attempted quality preset changes apply successfully without requiring stream restart, reconnection, or error dialogs.

- **SC-005**: When network bandwidth drops by 50%, playback remains continuous—system reduces resolution or frame rate automatically rather than stalling or disconnecting.

- **SC-006**: User-selected quality preference persists across app backgrounding/foregrounding cycles 100% of the time over 5 consecutive session tests.

- **SC-007**: All existing Viewer screen e2e tests pass with zero regressions after implementation.

- **SC-008**: New Viewer screen e2e tests (player auto-fit, quality settings) achieve 90% code coverage of modified Viewer components.

- **SC-009**: When NDI stream disconnects completely, dialog appears within 2 seconds with "Reconnect" button. User's quality preference persists and is re-applied on successful reconnection.

- **SC-010**: Quality settings UI is fully accessible with TalkBack/VoiceOver enabled—all preset options are vocalized with descriptive use-case hints, and menu navigation works via accessibility gestures.

---

## Assumptions

1. **NDI SDK Availability**: The NDI Android SDK bridge is available and compiled; the app has access to current NDI frame data and codec options.

2. **Default Codec Selection**: The system has access to device capability information (CPU, GPU, memory) to make intelligent default codec/quality choices. Reasonable default: H.264 at 720p@30fps on mid-range devices, adjusted down for lower-end devices.

3. **Quality Profile Storage**: User quality preferences are stored in Android SharedPreferences (device-scoped, non-encrypted). Quality presets are UX preferences, not sensitive data. No new database schema required.

4. **Accessibility Framework**: Android TalkBack/VoiceOver support is available on emulator (API 34+) for accessibility testing. UI framework (likely Compose) supports contentDescription semantics out-of-box.

5. **Disconnection Handling**: NDI SDK provides disconnect signals or timeout-based detection. System can reliably differentiate between temporary network slowdown vs. complete disconnection.

6. **Emulator Capability**: Test emulator (API 34+) has sufficient virtualized graphics and network performance to meaningfully validate playback smoothness and quality switching.

7. **No New External Dependencies**: Feature uses only NDI SDK and Android framework APIs already in the project; no new third-party libraries are introduced.

---

## Out of Scope (explicitly NOT included)

- Audio synchronization adjustments (audio/video sync is assumed to be a separate concern)
- Custom user profiles (only preset profiles, not editable/custom quality configs)
- Network bandwidth estimation or automatic quality selection based on real-time bandwidth probing
- Detailed codec comparison tables or educational documentation
- Changes to Source List screen or other screens outside Viewer

---

## Glossary

| Term | Definition |
| --- | --- |
| Smooth Playback | Video rendering at minimum 24 fps without visible stuttering, frame drops, or interruptions |
| Auto-Fit | Automatic scaling and centering of video content within player bounds while preserving aspect ratio |
| Quality Profile | Preconfigured set of codec, resolution, frame rate, and bitrate parameters (e.g., "Smooth", "High Quality") |
| Graceful Degradation | Reducing stream quality parameters (resolution, frame rate) when network conditions deteriorate, prioritizing continuity over visual fidelity |
