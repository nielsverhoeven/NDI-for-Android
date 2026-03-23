<!-- Last updated: 2026-03-20 -->

# Release Notes: 006 Settings Menu

## Scope

This release adds the Settings UI surface, discovery configuration persistence/validation, and a developer diagnostics overlay path across Source, Viewer, and Output screens.

## User-Facing Features

- Open Settings from main screens (Source List, Viewer, Output) via settings menu action.
- Configure custom discovery endpoint input (hostname, IPv4, bracketed IPv6, optional port).
- Toggle Developer Mode on/off from Settings.
- Show diagnostics overlay on main screens when Developer Mode is enabled.
- Overlay content includes stream status, redacted session ID, and recent redacted logs.

## Operator and Admin Notes

- Settings are persisted locally in Room (`settings_preference` table).
- No server-side sync exists; settings are device-local only.
- Sensitive diagnostics data is redacted before display:
  - IPv4 and IPv6 values are replaced with `[redacted-ip]`.
  - Session IDs are masked to `****` + last 4 characters.
- Telemetry events are emitted for settings open/close, save, toggles, and overlay transitions.

Current implementation note:

- Fallback warning and immediate apply/interruption semantics are represented in spec/contracts/telemetry and test seams, but production runtime wiring for endpoint reachability fallback and bridge reconfiguration is still incomplete in current code.

## Deprecations

- None.

## Known Limitations

- Dual-emulator e2e validation remains environment-dependent and may be pending where emulator/device setup is unavailable.
- Instrumentation timing tests for overlay visibility/status are currently scaffold placeholders.

## Verification Snapshot

- Unit tests: green for settings models/viewmodels and redaction/state mapper.
- Compile and release hardening gates: configured and passing in recorded test results.
- E2E: harness and scripts are available through `testing/e2e/README.md`.
