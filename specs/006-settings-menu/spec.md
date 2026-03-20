# Feature Specification: Settings Menu

**Feature Branch**: `006-settings-menu`  
**Created**: 2025-07-14  
**Status**: Draft  
**Input**: User description: "add a settings menu. The settings menu should provide the following functionality: configure an NDI discovery server, turn on or off a developer mode. The developer mode should display on the top of the screen: show information about the NDI stream going in or out, logging information"

## Clarifications

### Session 2026-03-20

- Q: When the configured NDI discovery server is unreachable, what should the app do? -> A: Fall back to default multicast discovery, show a visible warning, and keep the configured server value saved.
- Q: How should the discovery server field format be defined for saved values? -> A: Hostname/IP with optional port; if omitted, use the default NDI discovery port.
- Q: When Developer Mode is enabled but no stream is active, what should the overlay show? -> A: Show an explicit idle state (for example, "No active stream") and still show recent logs.
- Q: What log-content policy should Developer Mode overlay follow for sensitive values? -> A: Redact sensitive values before display (mask secrets, tokens, credentials).
- Q: When discovery server settings are changed while a stream session is active, when should the change take effect? -> A: Apply immediately, including interrupting current stream sessions if needed.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Access and Navigate the Settings Menu (Priority: P1)

As a user, I want to open a Settings screen from within the app so that I can configure application-level options without leaving the main workflow.

**Why this priority**: The Settings screen is the entry point for all configuration described in this feature. Without it, no other story can be delivered to users.

**Independent Test**: Open the app, navigate to Settings, confirm the screen appears with at minimum two configurable items (NDI discovery server and developer mode toggle). Fully testable with no external dependencies.

**Acceptance Scenarios**:

1. **Given** the app is open on any main screen, **When** the user taps the Settings entry point (e.g., toolbar icon or overflow menu item), **Then** a Settings screen appears listing all available options.
2. **Given** the Settings screen is open, **When** the user presses Back, **Then** the user returns to the previous screen with no state change.
3. **Given** the Settings screen is open, **Then** it displays at minimum: an NDI discovery server field and a developer mode toggle.

---

### User Story 2 - Configure NDI Discovery Server (Priority: P2)

As a user, I want to enter a custom NDI discovery server address in Settings so that the app connects to a specific discovery server instead of using default multicast discovery.

**Why this priority**: Configuring a discovery server is a core use case for professional NDI environments where multicast is disabled or a central registry is required. It delivers direct workflow value independently of developer mode.

**Independent Test**: Open Settings, enter a valid NDI discovery server address, save, and verify the app uses the configured server for NDI source discovery on the Source List screen. Fully testable without enabling developer mode.

**Acceptance Scenarios**:

1. **Given** the Settings screen is open, **When** the user enters a valid server address (hostname/IP with optional port) in the discovery server field and saves, **Then** the app immediately uses that address for NDI source discovery on the next refresh.
2. **Given** a discovery server address has been saved, **When** the app is restarted, **Then** the configured address is retained and still in use.
3. **Given** the Settings screen is open with a previously entered address, **When** the user clears the field and saves, **Then** the app reverts to default multicast/local discovery.
4. **Given** the user enters an address, **When** the address field contains only whitespace, **Then** the app shows an inline validation message and does not save the invalid value.
5. **Given** a custom discovery server is configured, **When** that server is unreachable during discovery, **Then** the app falls back to default multicast/local discovery, shows a visible warning, and keeps the configured server value saved.
6. **Given** the user saves a hostname/IP without an explicit port, **Then** discovery uses the default NDI discovery port.
7. **Given** an active stream session is running, **When** the user saves a different discovery server setting, **Then** the new setting applies immediately, and active stream sessions may be interrupted if required to apply the change.

---

### User Story 3 - Enable Developer Mode and View Stream Diagnostics (Priority: P3)

As a developer or power user, I want to toggle Developer Mode in Settings so that a persistent diagnostic overlay appears at the top of the screen, showing live NDI stream information and log output.

**Why this priority**: Developer mode is a supporting diagnostic tool. It does not affect core stream or discovery functionality and is naturally built after the settings entry point and discovery server configuration are in place.

**Independent Test**: Enable Developer Mode in Settings, navigate to a screen where an NDI stream is active (output or viewer), confirm the diagnostic overlay appears at the top showing stream state and recent log lines. Disable Developer Mode, confirm the overlay disappears. Testable independently of discovery server configuration.

**Acceptance Scenarios**:

1. **Given** Developer Mode is off, **When** the user opens Settings and toggles Developer Mode on and saves, **Then** a diagnostic overlay band appears at the top of all main app screens.
2. **Given** Developer Mode is on and an output stream is active, **Then** the overlay shows at minimum: stream direction (outgoing), stream name or source identifier, and current stream status.
3. **Given** Developer Mode is on and the viewer is playing a stream, **Then** the overlay shows at minimum: stream direction (incoming), source name, and current playback status.
4. **Given** Developer Mode is on, **Then** the overlay also displays recent log lines (minimum last 5 entries) relevant to NDI activity.
5. **Given** Developer Mode is on, **When** the user returns to Settings and toggles Developer Mode off and saves, **Then** the overlay is hidden on every screen immediately without a restart.
6. **Given** Developer Mode is off, **Then** no diagnostic overlay or additional computation overhead from overlay rendering is visible to the user.
7. **Given** Developer Mode is on and no stream is active, **Then** the overlay remains visible, shows an explicit idle state (for example, "No active stream"), and still shows recent logs.
8. **Given** Developer Mode is on and logs include sensitive values, **Then** the overlay shows redacted/masked content instead of raw secrets or credentials.

---

### Edge Cases

- What happens when the discovery server address field contains only whitespace?
- If the configured discovery server is unreachable at runtime, the app falls back to default multicast/local discovery, shows a visible warning, and keeps the configured server value saved.
- Discovery server value format supports hostname/IP with optional port; if omitted, the default NDI discovery port is used.
- When Developer Mode is enabled and no stream is active, the overlay stays visible and shows an explicit idle state while still showing recent logs.
- Sensitive values in overlay logs (for example secrets/tokens/credentials) are redacted before display.
- What if the app is rotated or the screen changes while the overlay is visible?
- When discovery server settings are changed while a stream is active, the new setting applies immediately, and active stream sessions may be interrupted if required.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The app MUST provide a navigable Settings screen accessible from at least one main screen (source list, viewer, or output screen).
- **FR-002**: The Settings screen MUST display a text input for configuring a custom NDI discovery server address in `hostname/IP` or `hostname/IP:port` format.
- **FR-003**: The Settings screen MUST display a toggle control for enabling and disabling Developer Mode.
- **FR-004**: A configured NDI discovery server address MUST be persisted across app restarts.
- **FR-005**: Developer Mode state MUST be persisted across app restarts.
- **FR-006**: When a discovery server address is set, NDI source discovery MUST use that address instead of default multicast discovery.
- **FR-007**: When the discovery server address is cleared, NDI source discovery MUST revert to default discovery behavior.
- **FR-008**: When Developer Mode is enabled, a diagnostic overlay MUST appear at the top of all main app screens.
- **FR-009**: The diagnostic overlay MUST display the direction of any active NDI stream (incoming or outgoing), the stream or source name, and the current stream status.
- **FR-010**: The diagnostic overlay MUST display recent log entries relevant to NDI activity (minimum last 5 entries).
- **FR-011**: When Developer Mode is disabled, the diagnostic overlay MUST be removed from all screens immediately without requiring an app restart.
- **FR-012**: Discovery server address input MUST reject entries that are empty, contain only whitespace, or are not valid `hostname/IP` with optional `:port`; the user MUST see an inline validation message.
- **FR-013**: If the configured discovery server is unreachable during discovery, the app MUST fall back to default multicast/local discovery, show a visible warning to the user, and keep the configured server value persisted.
- **FR-014**: If no port is provided in the saved discovery server value, the app MUST use the default NDI discovery port.
- **FR-015**: When Developer Mode is enabled and no stream is active, the diagnostic overlay MUST remain visible, display an explicit idle state (for example, "No active stream"), and continue displaying recent logs.
- **FR-016**: Developer Mode overlay logs MUST redact sensitive values (including secrets, tokens, and credentials) before display.
- **FR-017**: When the discovery server setting is changed while a stream session is active, the new discovery setting MUST apply immediately, even if active stream sessions must be interrupted.

### Key Entities

- **Settings**: Persisted user configuration containing the NDI discovery server address (optional string) and the developer mode enabled state (boolean).
- **NDI Discovery Configuration**: The active discovery address used by the NDI engine for source discovery, consisting of hostname/IP and optional port; sourced from Settings or defaulting to multicast/local if unset.
- **Stream Diagnostic**: A live read of current NDI stream direction, name/source, and status used to populate the overlay.
- **Log Entry**: A timestamped string of NDI-relevant log output, buffered and surfaced in redacted form in the overlay when Developer Mode is on.

## Assumptions

- The existing NDI source discovery flow can be redirected to a custom discovery server via a configuration address at startup or discovery refresh time, without requiring a full app restart in all cases.
- The app already has a navigation graph that supports adding a Settings destination.
- Developer mode is intended for internal diagnostic use; no hidden unlock or special build variant is required — a visible toggle in Settings is sufficient.
- Log entries displayed in the overlay are NDI-activity-scoped; they do not include general Android system logcat.
- The diagnostic overlay is a non-interactive read-only band; it does not capture touch events.
- Discovery server setting changes apply immediately after save, even during active stream sessions when interruption is required.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can open the Settings screen from any main screen within 2 taps.
- **SC-002**: A configured NDI discovery server address survives an app restart and is reflected in the next source discovery refresh.
- **SC-003**: Enabling Developer Mode causes the diagnostic overlay to appear on all main screens within 1 second of saving the setting, without restarting the app.
- **SC-004**: Disabling Developer Mode causes the overlay to disappear from all screens within 1 second of saving the setting, without restarting the app.
- **SC-005**: The diagnostic overlay reflects stream direction, name, and status within 3 seconds of any change in the active stream state.
- **SC-006**: If a configured discovery server is unreachable, fallback discovery continues and a visible warning is shown within 3 seconds while preserving the configured server value.
- **SC-007**: Discovery accepts both `hostname/IP` and `hostname/IP:port` inputs; when port is omitted, the app uses the default NDI discovery port on the next refresh.
- **SC-008**: With Developer Mode enabled and no active stream, the overlay shows an explicit idle state and recent logs within 1 second.
- **SC-009**: In Developer Mode, sensitive values in overlay logs are redacted in all displayed entries.
- **SC-010**: Changing discovery server settings during an active stream applies the new setting within 1 second, with stream interruption allowed when required.
