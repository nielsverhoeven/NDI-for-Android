# Feature Spec — PTZ Control over VISCA-over-IP (TCP) for NDI Sources

**Issue:** #339
**Branch:** `feature/339-visca-ptz-endpoint`
**Status:** Decisions resolved (issue comments, 2026-09-04) — ready for `feature-planner`/implementation

---

## User Story

As an operator whose NDI video comes from an SDI/HDMI-to-NDI converter with a separate physical
PTZ camera at a different IP address behind it, I want to configure an alternate PTZ control
endpoint for that NDI source, so that the pan/tilt/zoom/preset/focus controls in the Viewer
operate the actual camera instead of silently doing nothing against a converter that isn't a PTZ
device.

## Current State (evidence)

PTZ today is entirely a function of the ONE active NDI receiver: `NdiViewerBridge` owns the
single `_recv` handle and all five `NDIlib_recv_ptz_*` passthroughs, gated on
`NDIlib_recv_ptz_is_supported`. `INdiViewerBridge` is the only PTZ contract; `ViewerViewModel`
calls it directly (`PtzNudge`/`PtzZoomNudge`/`PtzAutoFocus`). There is no way to target a
different (non-NDI) device for PTZ, and `NdiSource` / the `sources` SQLite table have no field for
an alternate control endpoint. Full detail: issue #339 body (touchpoints list).

## Binding Decisions (from issue #339 comments, Niels, 2026-09-04)

- **D1 — Endpoint scoped per NDI source.** Host + port stored on the `NdiSource` record (new
  nullable columns + migration in `NdiDatabase`), automatically active whenever that source is
  viewed. No reusable "PTZ camera" entity for now.
- **D2 — Configured from the Viewer's PTZ panel.** A "PTZ endpoint" control next to the PTZ pad
  opens a small form (host, port — default 5678, "Test" button); a status indicator shows
  connected/error; command timeouts give a short message. No Settings-screen management in this
  slice.
- **D3 — Transport: raw VISCA over TCP** (PTZOptics-style — no Sony VISCA-over-IP UDP header),
  port configurable (5678 default; 1259 for Avonic CM93 / current firmware), VISCA address 1, one
  persistent connection per endpoint with reconnect, ACK/Completion/Error frame parsing. Must be
  verified against real Avonic hardware (tracked as a follow-up, out of scope for this plan).
- **D4 — Backend selection seam.** `ViewerViewModel` selects the PTZ backend through a new Core
  `IPtzController` abstraction (NDI receiver PTZ vs VISCA), with the raw socket behind a mockable
  Core transport interface, so the protocol encoder/parser/controller are fully unit-tested with a
  fake transport (`tests/MauiApp.Tests`).
- **D5 — Minimal command set**, matching what the PTZ pad already exposes plus preset
  recall/store (trivially supported since `INdiViewerBridge` already has
  `PtzStorePreset`/`PtzRecallPreset`, just not surfaced in the ViewModel/View yet):
  - Pan/tilt continuous move + stop — `81 01 06 01 VV WW 0p 0t FF`
  - Zoom tele/wide + stop — `81 01 04 07 2p FF` / `3p FF` / `00 FF`
  - Auto-focus one-push — `81 01 04 18 01 FF`
  - Preset recall/store — `81 01 04 3F 02 pp FF` (recall) / `81 01 04 3F 01 pp FF` (store)
- **D6 — Route through `solution-architect` before implementation** (new Core contract + DB
  migration) — noted by the product owner in the issue; this plan's "Architecture Notes" section
  (in `plan.md`) records the design points the architect should validate.

## Scope

### In scope
- `NdiSource.PtzOverrideHost` / `PtzOverridePort` (nullable), additive SQLite migration.
- Core `IPtzController` seam with two backends: `NdiPtzController` (wraps `INdiViewerBridge`) and
  `ViscaPtzController` (raw VISCA/TCP, via a mockable `IViscaTransport`).
- VISCA command encoder + response parser (pure, unit-tested) for the D5 command set.
- A small "PTZ endpoint" edit form reachable from the Viewer's PTZ panel (host, port, Test,
  Save/Cancel) plus a connected/error status indicator.
- Preset store/recall surfaced in the ViewModel/View (net-new UI, since the pad doesn't have it
  today) — trivial addition per D5, backed by the existing `INdiViewerBridge` methods for the NDI
  path and the new VISCA command set for the override path.
- Minimal, isolated additions to `ViewerViewModel.cs` / `ViewerView.xaml(.cs)` — both files are
  shared with a parallel in-flight feature and are integrated last; see plan.md §3 for the exact,
  small diff.

### Out of scope
- A reusable "PTZ camera" entity / Settings-screen management of endpoints (D1/D2).
- Sony VISCA-over-IP UDP transport/header (D3 — explicitly raw TCP only).
- On-device verification against real Avonic CM93 hardware (D3 — flagged as a required follow-up
  before this ships, not blocking the plan itself).
- Manual focus, exposure, white-balance PTZ controls (not in the existing pad, not requested).
- Any change to the NDI-native PTZ passthrough behavior (`NdiViewerBridge` PTZ methods are reused
  unchanged via `NdiPtzController`).

## Acceptance Criteria

- [ ] An NDI source can have an optional VISCA endpoint (host + port) configured from the Viewer's
      PTZ panel; it persists across app restarts (SQLite).
- [ ] When a source has a configured endpoint, all PTZ pad actions (pan/tilt, zoom, auto-focus,
      preset store/recall) are sent as raw VISCA-over-TCP commands to that endpoint instead of the
      NDI receiver PTZ passthrough.
- [ ] When no endpoint is configured, PTZ pad actions behave exactly as today (NDI receiver PTZ,
      gated on `IsPtzSupported`).
- [ ] The PTZ panel remains reachable (to configure an endpoint) even when the connected NDI source
      does not itself support NDI PTZ — i.e., configuring an override must not be blocked by
      `IsPtzSupported == false`.
- [ ] A "Test" action in the edit form attempts a TCP connect to the entered host/port and reports
      success/failure without disturbing the active persistent connection.
- [ ] A status indicator shows Connected / Connecting / Error for the active VISCA endpoint; a
      command/connect timeout surfaces a short, specific message (not a silent failure).
- [ ] The VISCA connection is a single persistent connection per endpoint that reconnects
      automatically after a transport failure (next command triggers reconnect).
- [ ] `ViscaCommandEncoder`, `ViscaResponseParser`, `ViscaPtzController`, `NdiPtzController`,
      `PtzControllerFactory`, and `PtzEndpointFormViewModel` are unit-tested in
      `tests/MauiApp.Tests` with no real sockets/hardware (fake transport).
- [ ] `dotnet build NdiForAndroid.sln` and `dotnet test tests/MauiApp.Tests` stay green.

## Companion Docs

- `docs/features/ptz-visca-endpoint/plan.md` — fully explicit implementation plan.
- `docs/features/ptz-visca-endpoint/tasks.md` — dependency-ordered task breakdown (T1..Tn).
