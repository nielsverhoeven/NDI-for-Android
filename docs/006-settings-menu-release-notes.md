<!-- Last updated: 2026-03-20 -->

# Release Notes: 006 Settings Menu

## Scope

This release adds the Settings UI surface, discovery configuration persistence/validation, and a developer diagnostics overlay path across Source, Viewer, and Output screens.

Spec 018 extends settings with a dedicated Discovery Servers submenu for multi-server management.

## User-Facing Features

- Open Settings from main screens (Source List, Viewer, Output) via settings menu action.
- Configure custom discovery endpoint input (hostname, IPv4, bracketed IPv6, optional port).
- Toggle Developer Mode on/off from Settings.
- Show diagnostics overlay on main screens when Developer Mode is enabled.
- Overlay content includes stream status, redacted session ID, and recent redacted logs.
- Open Discovery Servers submenu from Settings.
- Add/edit/remove multiple discovery servers with separate host and port fields.
- Leave port blank to default to 5959.
- Toggle each server enabled/disabled independently.
- Drag and reorder saved servers to control runtime failover priority.

## Operator and Admin Notes

- Settings are persisted locally in Room (`settings_preference` table).
- No server-side sync exists; settings are device-local only.
- Sensitive diagnostics data is redacted before display:
  - IPv4 and IPv6 values are replaced with `[redacted-ip]`.
  - Session IDs are masked to `****` + last 4 characters.
- Telemetry events are emitted for settings open/close, save, toggles, and overlay transitions.

Discovery server operations:

- Discovery server rows are persisted in Room `discovery_servers` and survive app restart.
- Duplicate host+port entries are rejected with inline validation feedback.
- Runtime target selection iterates enabled servers in persisted order.
- If all enabled servers are unreachable, runtime returns explicit failure guidance.

Current implementation note:

- Fallback warning and immediate apply/interruption semantics are represented in spec/contracts/telemetry and test seams, but production runtime wiring for endpoint reachability fallback and bridge reconfiguration is still incomplete in current code.

## Deprecations

- None.

## Known Limitations

- Dual-emulator e2e validation remains environment-dependent and may be pending where emulator/device setup is unavailable.
- Instrumentation timing tests for overlay visibility/status are currently scaffold placeholders.
- Playwright pre-install may fail if auto-incremented app version and pre-install expected version drift; rerun with aligned version metadata.

## Verification Snapshot

- Unit tests: green for settings models/viewmodels and redaction/state mapper.
- Compile and release hardening gates: configured and passing in recorded test results.
- E2E: harness and scripts are available through `testing/e2e/README.md`.

## Spec 019 Addendum: Three-Pane Settings Workspace

New behavior:

- Adds wide-layout three-pane settings workspace (navigation, categories, details).
- Maintains compact fallback for unsupported layouts.
- Preserves selected category context across layout transitions.

Validation status:

- Android unit tests: PASS
- Release hardening (`:app:verifyReleaseHardening :app:assembleRelease`): PASS
- Playwright regression and feature spec suite: BLOCKED-ENV in current run due emulator UI hierarchy dump instability.

## SC-004 Feedback Query and One-Cycle Comparison Method

Baseline query (pre-release cycle):

```text
source = support_feedback
AND app = "NDI-for-Android"
AND release_cycle = "pre-019"
AND tags CONTAINS ANY ("settings", "navigation", "large-screen", "tablet")
AND sentiment = "negative"
```

Post-release query (first cycle after release):

```text
source = support_feedback
AND app = "NDI-for-Android"
AND release_cycle = "post-019"
AND tags CONTAINS ANY ("settings", "navigation", "large-screen", "tablet")
AND sentiment = "negative"
```

Comparison method:

1. Count matching records for pre-019 and post-019 cycles.
2. Compute relative delta: `(post - pre) / pre * 100`.
3. SC-004 passes when delta is `<= -30%`.
