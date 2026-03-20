# Data Model: Settings Menu and Developer Diagnostics

## Entity: SettingsPreferenceSnapshot

- Purpose: Canonical persisted settings state consumed by UI and runtime services.
- Fields:
  - discoveryServerInput (string, optional): raw saved value in one of these formats: `hostname`, `hostname:port`, `IPv4`, `IPv4:port`, `[IPv6]`, or `[IPv6]:port`.
  - developerModeEnabled (boolean, required).
  - updatedAtEpochMillis (long, required).
- Validation rules:
  - `discoveryServerInput` may be null/empty only when user chooses default discovery mode.
  - Non-empty value is trimmed before validation and must pass discovery endpoint validation.

## Entity: DiscoveryEndpoint

- Purpose: Parsed discovery endpoint derived from settings for runtime application.
- Fields:
  - host (string, required when configured).
  - port (int, optional): explicit port when provided.
  - resolvedPort (int, required when configured): explicit port or default NDI discovery port.
  - usesDefaultPort (boolean, required).
  - isReachable (boolean, runtime-derived).
- Validation rules:
  - Host must be non-blank and valid as hostname, IPv4, or bracketed IPv6 source.
  - Port, when provided, must be in valid TCP range `1-65535`.
  - Unbracketed IPv6 with `:port` is invalid and must be rejected.
  - `resolvedPort` must always be populated for configured endpoint.

## Entity: DiscoveryApplyEvent

- Purpose: Represents one settings-apply action and its runtime effect.
- Fields:
  - applyId (string, required).
  - previousEndpoint (DiscoveryEndpoint, optional).
  - newEndpoint (DiscoveryEndpoint, optional).
  - applyMode (enum, required): IMMEDIATE.
  - interruptedActiveStream (boolean, required).
  - fallbackTriggered (boolean, required).
  - warningShown (boolean, required).
  - appliedAtEpochMillis (long, required).
- Validation rules:
  - If `fallbackTriggered=true`, then `warningShown=true`.
  - If active stream exists and endpoint changes, interruption may occur.

## Entity: ActiveStreamSession

- Purpose: Runtime representation of currently active stream in viewer/output path.
- Fields:
  - sessionId (string, optional).
  - direction (enum, required): INCOMING | OUTGOING | NONE.
  - sourceName (string, optional).
  - status (enum, required): IDLE | CONNECTING | ACTIVE | INTERRUPTED | ERROR.
  - updatedAtEpochMillis (long, required).
- Validation rules:
  - `direction=NONE` implies status is IDLE unless recovering from interruption.
  - `sourceName` is required when direction is INCOMING or OUTGOING and status is ACTIVE.

## Entity: DeveloperOverlayState

- Purpose: Top-band UI model rendered when developer mode is enabled.
- Fields:
  - visible (boolean, required).
  - mode (enum, required): DISABLED | IDLE | ACTIVE.
  - streamDirectionLabel (string, required).
  - streamStatusLabel (string, required).
  - streamSourceLabel (string, optional).
  - warningMessage (string, optional).
  - recentLogs (list<RedactedLogEntry>, required).
  - updatedAtEpochMillis (long, required).
- Validation rules:
  - If `mode=DISABLED`, overlay must not render.
  - If `mode=IDLE`, `visible=true` and status text must explicitly indicate no active stream.
  - `recentLogs` must contain at least 5 latest entries when available.

## Entity: RedactedLogEntry

- Purpose: Sanitized log message suitable for in-app diagnostic display.
- Fields:
  - timestampEpochMillis (long, required).
  - level (enum, required): DEBUG | INFO | WARN | ERROR.
  - category (string, required): DISCOVERY | VIEWER | OUTPUT | SYSTEM.
  - messageRedacted (string, required).
  - redactionApplied (boolean, required).
- Validation rules:
  - Displayed message must never contain raw secrets/tokens/credentials.
  - `redactionApplied=true` when masking transforms original content.

## Relationships

- `SettingsPreferenceSnapshot` drives `DiscoveryEndpoint` parsing and `DeveloperOverlayState.visible` behavior.
- Applying settings creates `DiscoveryApplyEvent` instances and may transition `ActiveStreamSession` to INTERRUPTED.
- `ActiveStreamSession` state feeds `DeveloperOverlayState` mode/status labels.
- `RedactedLogEntry` list populates `DeveloperOverlayState.recentLogs`.

## State Transitions

- Discovery configuration:
  - UNSET -> CONFIGURED when valid endpoint is saved.
  - CONFIGURED -> UNSET when field is cleared.
  - CONFIGURED -> FALLBACK_ACTIVE when endpoint unreachable (while preserving saved endpoint).
- Active stream behavior during settings change:
  - ACTIVE + endpoint change -> INTERRUPTED (if interruption required) -> CONNECTING/IDLE.
- Developer overlay:
  - DISABLED when developer mode off.
  - IDLE when developer mode on and no active stream.
  - ACTIVE when developer mode on and stream is connecting/active.
