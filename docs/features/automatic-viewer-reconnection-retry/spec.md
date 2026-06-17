# Feature Spec — Automatic Viewer Reconnection with Retry Window

**Issue:** #233
**Branch:** `feature/233-automatic-viewer-reconnection-retry`
**Status:** Clarified — ready for `feature.planner`

---

## Summary

The MAUI viewer must automatically attempt to reconnect for up to **15 seconds** when an
active NDI connection drops unexpectedly, mirroring the legacy Kotlin app's 15-second retry
window. During the window the user sees a counting-down indicator and can cancel the retry.
On expiry, the viewer transitions to an explicit stopped/error state with a clear message and
an explicit way to restart. A user-initiated `Stop` must **never** trigger the retry flow.

---

## Resolved Decisions

All `[NEEDS CLARIFICATION]` markers have been resolved. Decisions were taken in autopilot mode
using the recommended defaults, validated against codebase evidence and legacy Kotlin parity.

### D1 — Drop-detection mechanism  *(Scope / Architecture / NDI)*

**Decision:** Add an **explicit connection-state signal** to `INdiViewerBridge`.

- Add a `ConnectionState GetConnectionState()` method (NOT an event/property).
- New enum `ConnectionState { Connecting, Connected, Disconnected }`
  (`Disconnected` represents an unexpected drop).

**Rationale:**
- The existing `INdiViewerBridge` contract is entirely **`Get*()` method-style**
  (`GetLatestFrame()`, `GetMeasuredFps()`, `GetDroppedFramePercent()`,
  `GetActualResolution()`). A `GetConnectionState()` getter is consistent with that
  convention and is trivially mockable with Moq (per `ViewerViewModelTests.cs`).
- An explicit signal removes brittle FPS/null-frame inference from the ViewModel and keeps the
  watchdog an **internal bridge concern** once real libndi is wired (out of scope here).
- Polling the getter on each `TimeProvider` countdown tick fits the timer-driven design and
  avoids cross-thread event marshaling complexity in the ViewModel.

**Bridge stub impact:** `NdiViewerBridge` (stub) must surface `GetConnectionState()` returning
`Connected` after a successful `StartReceiver`, `Disconnected` to simulate a drop, and
`Connecting`/`Disconnected` appropriately — so reconnection logic is verifiable against the
mock with no NDI hardware.

### D2 — Drop-trigger threshold  *(NDI behaviour)*

**Decision:** **3 seconds** of no successful frames (`GetMeasuredFps() == 0` / `GetLatestFrame()
== null`, or `GetConnectionState() == Disconnected`) defines a "dropped" connection that starts
the 15s retry window.

**Rationale:** Short enough to react quickly to a real interruption, long enough to tolerate a
single missed-frame hiccup at 30 fps. With the explicit `ConnectionState` (D1) the bridge owns
this judgement; 3 s is the documented default for any inference fallback path and for the
bridge's future internal watchdog.

### D3 — Retry cadence within the 15s window  *(Architecture / NDI)*

**Decision:** **Discrete retry attempts every 2 seconds** across the 15s window (~7 attempts).
Each attempt performs a full **`StopReceiver()` → `StartReceiver(SourceId)`** cycle. The loop
stops on the **first** attempt that yields `GetConnectionState() == Connected`.

**Rationale:**
- Discrete fixed-interval attempts are deterministic and easy to assert in unit tests by
  advancing the injected `TimeProvider` (e.g. "after 6 s, expect 3 `StartReceiver` calls").
- A clean `Stop→Start` cycle per attempt guarantees the native receiver is fully reset rather
  than relying on a half-open handle — safe once real libndi is wired.
- Continuous re-attempts would spin the CPU and produce non-deterministic call counts.

### D4 — Countdown display semantics  *(UX)*

**Decision:**
- The indicator counts **down remaining whole seconds**: `"Reconnecting... {n}s remaining"`
  (15, 14, … 1), driven by the injected `TimeProvider`.
- The countdown resets **only when a new retry window starts** (a fresh unexpected drop), not
  per attempt.
- A successful reconnection clears the indicator and resumes normal playback
  (`IsPlaying = true`, normal status).
- **Terminal message on expiry:** `"Connection lost. Reconnection failed."`

**Rationale:** Whole-second countdown matches the issue's `"Reconnecting... 12s remaining"`
example and is easy to assert deterministically. The terminal wording is the autopilot-selected
default; it supersedes the issue body's illustrative `"e.g. Connection lost. Unable to
reconnect."` (both were examples). The planner/architect may align either string — the
behaviour is what matters; tests should reference a single constant.

### D5 — Post-expiry recovery UX  *(UX / Scope)*

**Decision:** Provide an explicit **"Reconnect" button** shown in the stopped/error state that
restarts the full flow (`StartReceiver(SourceId)` with the last `SourceId`). Existing `Stop`
semantics are unchanged: a user `Stop` ends playback with **no** auto-retry.

**Rationale:** A one-tap "Reconnect" is the cleanest recovery and avoids forcing the user to
navigate back to Home and re-select the source. It reuses the existing `SourceId` already held
by the ViewModel. Keeps user-Stop intentional and retry-free (acceptance criterion).

---

## Interface Changes

```csharp
// src/Core/NdiBridge/NdiBridgeModels.cs   (enum lives here, alongside DiscoveryMode)
public enum ConnectionState { Connecting, Connected, Disconnected }
```

```csharp
// src/Core/NdiBridge/INdiBridges.cs   (interface member only)
public interface INdiViewerBridge
{
    void StartReceiver(string sourceId);
    void StopReceiver();
    NdiVideoFrame? GetLatestFrame();
    float GetDroppedFramePercent();
    (int Width, int Height) GetActualResolution();
    float GetMeasuredFps();
    ConnectionState GetConnectionState();   // NEW (D1)
}
```

## ViewModel Changes (`ViewerViewModel`)

- New observable state: `IsReconnecting` (bool), `RetryStatusMessage` (string?), and a
  `_userInitiatedStop` flag (or distinct state) so the drop handler suppresses retry on
  intentional `Stop`.
- Inject `TimeProvider` (constructor) for the 15s window + per-tick countdown (D4) and the 2s
  attempt cadence (D3).
- Inject `IMainThreadDispatcher` (Core abstraction) for UI-thread marshaling — the Core project
  does not reference MAUI, so `MainThread`/`IDispatcher` are unavailable directly.
- New `[RelayCommand] CancelRetry()` — aborts the window immediately → stopped state.
- New `[RelayCommand] Reconnect()` — restarts the flow from the stopped/error state (D5).
- All callback/timer-driven state changes marshal to the UI thread via the injected
  `IMainThreadDispatcher` (constitution §2.3).

## View Changes (`ViewerPage.xaml`)

- Retry-status indicator bound to `RetryStatusMessage` / `IsReconnecting`.
- "Cancel" (cancel-retry) button visible while `IsReconnecting`.
- "Reconnect" button visible in the stopped/error state.

---

## Out of Scope (unchanged from issue)

- Wiring the real libndi P/Invoke receive loop (bridge stays a stub).
- Discovery / sender feature changes.
- Configurable retry-window length or backoff (fixed 15s for legacy parity).
- Audio handling / rendering-quality changes.
- Reconnection history / telemetry.

---

## Planner Notes / Caveats

- The issue references `DiscoveryRefreshService` as an existing `TimeProvider`-injected pattern,
  but **no such service currently exists** in `src/`. The `TimeProvider` injection approach is
  still the correct, constitution-aligned (xUnit + Moq, no wall-clock) choice — the planner
  should introduce it fresh rather than expecting a reference implementation.
- Acceptance criteria from the issue remain authoritative; D1–D5 fill in the underspecified
  details those criteria depend on.

## Acceptance Criteria (from issue #233)

- [ ] Auto-retry on unexpected drop (not via user Stop) for up to 15 s.
- [ ] Counting-down retry indicator driven by injected `TimeProvider`.
- [ ] Expiry → stopped/error state with clear message; `IsPlaying == false`.
- [ ] Manual cancel of retry ends retries immediately → stopped state.
- [ ] No retry on user Stop.
- [ ] Unit-testable without hardware (mocked `INdiViewerBridge` + controllable `TimeProvider`).
