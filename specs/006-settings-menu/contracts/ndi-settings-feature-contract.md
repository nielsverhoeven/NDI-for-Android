# Contract: Settings Menu, Discovery Configuration, and Developer Diagnostics

## 1. Navigation Contract

### 1.1 SettingsRouteContract

Required routes and entry behavior:

- App exposes a Settings destination within existing single-activity nav graph.
- User can navigate to Settings from each main screen (Source List, Viewer, Output) in <= 2 taps.
- Back from Settings returns to prior screen without side effects.

Guarantees:

- No duplicate activity stack creation.
- Existing viewer/output deep-link routes remain valid.

## 2. Discovery Configuration Contract

### 2.1 DiscoveryInputContract

Input rules:

- Discovery field accepts `hostname`, `hostname:port`, `IPv4`, `IPv4:port`, `[IPv6]`, and `[IPv6]:port` formats.
- Input is trimmed before validation; `port`, when present, must be in range `1-65535`.
- Unbracketed IPv6 with `:port` is invalid.
- Whitespace-only or invalid values are rejected with inline validation.
- Empty field is allowed only as explicit revert-to-default discovery.

Guarantees:

- If port is omitted, default NDI discovery port is used.
- Saved value is persisted across app restarts.

### 2.2 DiscoveryApplyContract

Apply behavior:

- Saving a valid discovery setting applies immediately.
- Immediate apply may interrupt active stream sessions when required.
- If configured endpoint is unreachable, runtime falls back to default multicast/local discovery.
- Fallback displays a visible warning and preserves configured endpoint in settings.

## 3. Developer Mode Contract

### 3.1 DeveloperModeToggleContract

Toggle behavior:

- Developer Mode value is persisted.
- Enabling mode shows top-band diagnostics on main screens within 1 second.
- Disabling mode hides overlay within 1 second without restart.

### 3.2 OverlayStateContract

Overlay content requirements:

- Displays stream direction, source/name context, and stream status.
- Displays minimum recent log buffer of 5 entries when available.
- In no-stream state, overlay remains visible with explicit idle text (for example, "No active stream").

## 4. Logging and Redaction Contract

### 4.1 RedactionContract

Sanitization guarantees:

- Sensitive values (secrets, tokens, credentials) are redacted before overlay render.
- Overlay must not display raw sensitive values in any entry.
- Redaction applies consistently regardless of build type.

## 5. ViewModel and Repository Contracts

### 5.1 SettingsViewModelContract

Inputs:

- onOpenSettings()
- onDiscoveryServerChanged(input)
- onSaveSettings()
- onDeveloperModeToggled(enabled)

Outputs:

- settingsUiState
- validationState
- applyResultState
- warningState

Guarantees:

- ViewModel emits validation failure for invalid discovery input.
- Save triggers repository apply and emits immediate-apply result.

### 5.2 SettingsRepositoryContract

Responsibilities:

- Persist/retrieve settings snapshot.
- Parse and normalize discovery endpoint.
- Trigger immediate runtime apply.
- Emit interruption/fallback warning outcomes.

## 6. Observability Contract

Required events:

- settings_opened
- discovery_server_saved
- discovery_server_apply_immediate
- active_stream_interrupted_for_discovery_apply
- discovery_fallback_to_default
- developer_mode_toggled
- developer_overlay_state_changed
- overlay_log_redaction_applied

Payload constraints:

- No PII.
- No raw secrets/tokens/credentials.

## 7. Security and Permission Contract

- No new dangerous Android permissions.
- No direct persistence or network configuration writes from presentation layer.
- Diagnostic overlay remains read-only and non-interactive.

## 8. Release Validation Contract

Validation must include:

- Unit tests for endpoint parsing/validation and redaction behavior.
- Unit/integration tests for immediate apply + interruption outcomes.
- UI tests for settings navigation and overlay visibility states.
- End-to-end validation for fallback warning behavior and idle overlay rendering.
- Release hardening verification (`verifyReleaseHardening`) prior to completion.
