# Telemetry Compliance Report: Three-Screen Navigation

**Feature**: Spec 003 – Three-Screen NDI Navigation  
**Date**: 2026-03-17

## Required Non-Sensitive Event Categories

| Event Name | Payload Fields | Sensitive? | Status |
|---|---|---|---|
| `top_level_destination_selected` | `from`, `to`, `trigger` | No | ✓ Implemented |
| `top_level_destination_reselected_noop` | `destination` | No | ✓ Implemented |
| `top_level_navigation_failed` | `to`, `reasonCode` | No | ✓ Implemented |
| `home_dashboard_viewed` | *(none)* | No | ✓ Implemented |
| `home_action_open_stream` | *(none)* | No | ✓ Implemented |
| `home_action_open_view` | *(none)* | No | ✓ Implemented |

## Payload Constraints Verification

- ✓ No raw media payloads in any event
- ✓ No personally identifiable information
- ✓ All attributes use destination IDs (`HOME`, `STREAM`, `VIEW`) or anonymized reason codes
- ✓ Timestamps are epoch millis (non-sensitive)

## Non-Sensitive Compliance

All telemetry events implemented in `TopLevelNavigationTelemetry.kt` and `HomeViewModel.kt`
use only destination enum names and trigger enum names as attribute values. No source display
names, user credentials, or media content are included in any event payload.

## Implementation References

- `app/src/main/java/com/ndi/app/navigation/TopLevelNavigationTelemetry.kt`
- `feature/ndi-browser/presentation/.../home/HomeViewModel.kt`
- `core/model/src/main/java/com/ndi/core/model/TelemetryEvent.kt` (constants added)

