# Feature Architecture Validation — Automatic Viewer Reconnection with Retry Window

**Issue:** #233
**Branch:** `feature/233-automatic-viewer-reconnection-retry`
**Validated against:** `docs/constitution.md` (v1.0), `docs/architecture.md` (updated 2026-06-09)
**Verdict:** ✅ **APPROVED WITH CHANGES**

---

## Decision-by-decision validation

### D1 — `ConnectionState GetConnectionState()` on `INdiViewerBridge` — ✅ ALIGNED
- `ConnectionState { Connecting, Connected, Disconnected }` is a plain C# enum → no NDI SDK type
  crosses the bridge boundary (Constitution §2.1/§2.3, architecture Dependency Rule 5).
- A method-style `Get*()` getter is consistent with the existing `INdiViewerBridge` contract
  (`GetLatestFrame()`, `GetMeasuredFps()`, `GetDroppedFramePercent()`, `GetActualResolution()`).
- **Minor doc fix required:** the spec "Interface Changes" snippet comments the enum into
  `INdiBridges.cs`, while plan T001 and the task instruction place it in
  `NdiBridgeModels.cs`. `NdiBridgeModels.cs` is correct (consistent with `DiscoveryMode`).
  Align the spec snippet to `NdiBridgeModels.cs`.

### D2/state machine in `ViewerViewModel` (Core) — ✅ ALIGNED
- Confirmed `ViewerViewModel` lives in `src/Core/Features/Viewer/ViewModels` — it is genuinely a
  Core ViewModel. Business logic belongs here (Constitution §2.1, Dependency Rule 1).
- `ViewerPage.xaml.cs` stays a binding shim; XAML adds bindings only. Compliant.

### D3 — `TimeProvider` injection + `TimeProvider.System` in `MauiProgram.cs` — ✅ ALIGNED
- Constructor injection, no service locator (Constitution §1). `TimeProvider` is in `System`
  (available to the plain `net10.0` Core project — no new MAUI dependency).
- `AddSingleton(TimeProvider.System)` is the correct lifetime.

### D4 — UI-thread marshaling — ⚠️ **REQUIRED CHANGE**
- **Finding:** the Core project (`NdiForAndroid.Core.csproj`) targets plain `net10.0` and
  references only `CommunityToolkit.Mvvm` + DI abstractions. It does **not** reference MAUI, so
  `MainThread.BeginInvokeOnMainThread` (and `IDispatcher`) are **not available** inside
  `ViewerViewModel`. The plan's NFR2 / §4.3 describes calling
  `MainThread.BeginInvokeOnMainThread` "at runtime" from the Core ViewModel — that will not
  compile and is a layering violation.
- **Required change:** the `InvokeOnMainThread(Action)` seam must be an **injected main-thread
  dispatcher abstraction** (e.g. `IMainThreadDispatcher` in `src/Core/Services`) with a MAUI
  implementation in `src/MauiApp` (wrapping `MainThread.BeginInvokeOnMainThread`/`IDispatcher`)
  registered in `MauiProgram.cs`. This mirrors the existing platform-abstraction pattern
  (`INavigationService`, `IMulticastLockService`, `IAppLifecycleService`). In tests the fake
  runs the action inline. The plan's "seam" intent is correct; only its implementation location
  and mechanism must change (abstraction, not a direct `MainThread.*` call).
- Add a task (e.g. T006a) to introduce the dispatcher interface + MAUI implementation +
  `MauiProgram.cs` registration, and inject it into `ViewerViewModel`.

### D5 — Polling-based drop detection vs. bridge event — ✅ ACCEPTABLE (with future note)
- Polling `GetConnectionState()` on a `TimeProvider` cadence is architecturally acceptable while
  the bridge is a stub; it avoids cross-thread event plumbing.
- **Future-architecture implication (documented in `architecture.md`):** when real `libndi` is
  wired, the frame watchdog + 3 s drop threshold (D2) become an internal bridge concern and the
  bridge may surface drops via a native callback marshaled per the §2.3 bridge threading rule.
  `GetConnectionState()` remains the stable contract, insulating the ViewModel.

---

## Required plan changes (summary)

1. **D4 (blocking):** Replace direct `MainThread.BeginInvokeOnMainThread` usage in the Core
   `ViewerViewModel` with an injected main-thread dispatcher abstraction (interface in
   `src/Core/Services`, MAUI implementation in `src/MauiApp`, registered in `MauiProgram.cs`).
   Add the corresponding task and inject it into the ViewModel constructor alongside
   `TimeProvider`.
2. **D1 (minor):** Align the spec's interface snippet to place the `ConnectionState` enum in
   `src/Core/NdiBridge/NdiBridgeModels.cs` (matches plan T001 / `DiscoveryMode`).

No constitution amendment is required — the dispatcher abstraction is a clarification of how
§2.3's UI-thread rule is honored from a non-MAUI Core ViewModel, already added to
`docs/architecture.md`.

---

## Architecture doc updates made (this branch)

- `docs/architecture.md`:
  - New **"INdiViewerBridge connection state (Issue #233)"** subsection (contract, enum location,
    stub behavior, polling model + future libndi callback implication).
  - New **"Viewer reconnection component (Issue #233)"** subsection (Core-located state machine,
    `TimeProvider` injection, and the main-thread dispatcher abstraction requirement) with a
    Mermaid `graph TB` reconnection-flow diagram.
  - "Last updated" bumped to 2026-06-09.
