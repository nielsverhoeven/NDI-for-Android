# Technical Plan — Automatic Viewer Reconnection with Retry Window

**Issue:** #233
**Branch:** `feature/233-automatic-viewer-reconnection-retry`
**Companion spec:** `docs/features/automatic-viewer-reconnection-retry/spec.md`
**Status:** Ready for `feature.breakdown`

---

## 1. Overview

When an active NDI viewer connection drops **unexpectedly** while playing, the
`ViewerViewModel` must automatically attempt to re-establish the receiver for up to
**15 seconds**, showing a per-second countdown indicator and offering a manual **Cancel**.
A successful reconnection inside the window resumes normal playback; expiry transitions to an
explicit stopped/error state with a terminal message and a **Reconnect** button. A
user-initiated `Stop` must never trigger the retry flow.

All retry/countdown/state-machine logic lives in `ViewerViewModel` (Core), is driven by an
injected `TimeProvider`, and is fully unit-testable against a mocked `INdiViewerBridge` with no
NDI hardware and no real wall-clock delays. The `NdiViewerBridge` implementation remains a stub;
this work only adds a stubbed `GetConnectionState()` to it.

This plan honors the five resolved clarifications (D1–D5) recorded in the spec.

---

## 2. Requirements

### 2.1 Functional Requirements

- **FR1** — While `IsPlaying`, the ViewModel detects an unexpected drop by polling
  `INdiViewerBridge.GetConnectionState()` returning `ConnectionState.Disconnected` and enters the
  reconnecting state. (D1)
- **FR2** — The reconnect window lasts a fixed **15 seconds**, sub-divided into discrete attempts
  every **~2 seconds** (~7 attempts). Each attempt performs a full
  `StopReceiver()` → `StartReceiver(SourceId)` cycle. (D3)
- **FR3** — The loop stops on the first attempt that yields
  `GetConnectionState() == Connected`; playback resumes (`IsPlaying = true`, normal status,
  retry state cleared). (D3/D4)
- **FR4** — While reconnecting, a countdown indicator displays whole remaining seconds:
  `"Reconnecting... {n}s remaining"`, decremented each second via the injected `TimeProvider`. (D4)
- **FR5** — On window expiry with no `Connected` result, the ViewModel transitions to a
  stopped/error state: `StatusMessage = "Connection lost. Reconnection failed."`,
  `IsPlaying = false`, `IsReconnecting = false`. (D4)
- **FR6** — A `CancelRetry` command immediately aborts the window, stops the receiver, and
  transitions to the stopped state without re-triggering retry.
- **FR7** — A `Reconnect` command, available in the stopped/error state, restarts the full flow
  with the last `SourceId`. (D5)
- **FR8** — A user-initiated `Stop` ends playback with **no** auto-retry. The drop handler must
  distinguish intentional stop from unexpected drop via an explicit flag/state. (D5)
- **FR9** — `INdiViewerBridge` gains `ConnectionState GetConnectionState()`; the
  `ConnectionState` enum lives in `src/Core/NdiBridge/NdiBridgeModels.cs`. (D1)

### 2.2 Non-Functional Requirements

- **NFR1** — All timing uses an injected `TimeProvider` (`TimeProvider.System` in production,
  a fake in tests). No `Task.Delay`/`DateTimeOffset.UtcNow` in testable logic. (Constitution §3)
- **NFR2** — Every state change driven by a timer or connection poll marshals to the UI thread
  via `MainThread.BeginInvokeOnMainThread`. (Constitution §2.3)
- **NFR3** — No NDI types cross the bridge boundary; `ConnectionState` is a plain C# enum.
  (Constitution §2.1/§2.3)
- **NFR4** — All new behaviour is covered by xUnit + Moq tests in `tests/MauiApp.Tests/`,
  deterministic via the fake `TimeProvider`. (Constitution §3)
- **NFR5** — The reconnect loop must be idempotent and self-cancelling: starting a new window,
  cancel, stop, or success must dispose any active timer to avoid overlapping loops/leaks.

### 2.3 Success Criteria → Acceptance Criteria Mapping

The issue defines four primary acceptance criteria (plus the testability gate). Mapping:

| # | Acceptance Criterion (issue #233) | Satisfied by | Verified by test |
|---|---|---|---|
| AC1 | Auto-retry on unexpected drop, up to 15 s | FR1, FR2 | `Drop_WhilePlaying_EntersReconnecting`, `RetryWindow_RunsAttemptsEvery2s` |
| AC2 | Counting-down retry indicator via `TimeProvider` | FR4 | `Countdown_DecrementsEachSecond` |
| AC3 | Expiry → stopped/error state, `IsPlaying == false` | FR5 | `Expiry_NoReconnect_SetsErrorState` |
| AC4 | Manual cancel ends retries immediately | FR6 | `CancelRetry_DuringWindow_StopsAndClears` |
| AC5 (gate) | No retry on user `Stop` | FR8 | `Stop_ByUser_DoesNotTriggerRetry` |
| AC6 (gate) | Unit-testable without hardware | NFR1, NFR4 | entire test suite (mock bridge + fake `TimeProvider`) |
| (D3) | First `Connected` resumes playback | FR3 | `Reconnect_Succeeds_ResumesPlayback` |
| (D5) | Post-expiry `Reconnect` button restarts flow | FR7 | `Reconnect_FromErrorState_RestartsFlow` |

---

## 3. Architecture Fit

Maps to the MAUI layering in `docs/constitution.md` §2.1–§2.3:

- **View (XAML)** — `ViewerPage.xaml` gains bindings only; code-behind stays a pure
  `BindingContext` shim (no business logic). (§2.1 "No business logic in Views")
- **ViewModel (Core)** — `ViewerViewModel` owns the entire reconnection state machine,
  countdown, and commands. This keeps logic testable and bridge-agnostic. (§2.1)
- **NDI Bridge** — `INdiViewerBridge` is extended with one method-style getter
  (`GetConnectionState()`), consistent with the existing `Get*()` contract and §2.3's
  "no NDI types cross the boundary" rule (`ConnectionState` is a plain enum).
- **DI root** — `MauiProgram.cs` registers `TimeProvider.System`; `ViewerViewModel` stays
  `Transient`. (§1 DI table, §2.2)
- **Tests** — `tests/MauiApp.Tests/Features/Viewer/` extends `ViewerViewModelTests` patterns
  with a fake `TimeProvider`, per §3.

> **Planner caveat (carried from spec):** the issue references `DiscoveryRefreshService` as an
> existing `TimeProvider` pattern. It does **not** exist on this branch (it is in unmerged
> PR #242). `TimeProvider` injection is introduced **fresh** here — do not copy or depend on it.

---

## 4. Technical Approach

### 4.1 Reconnection State Machine

States (conceptual — represented by the combination of `IsPlaying`, `IsReconnecting`,
`StatusMessage`, and the internal `_userInitiatedStop` flag; not a separate enum unless tests
demand it):

```
            StartReceiver ok / poll Connected
   Idle ───────────────────────────────► Connecting ──────► Connected
    ▲                                                          │
    │ user Stop / CancelRetry / expiry                         │ poll == Disconnected
    │                                                          ▼
    │                                                       Dropped
    │                                          (unexpected, _userInitiatedStop == false)
    │                                                          │ start 15s window
    │                                                          ▼
    │                          ┌──────────────────────────► Retrying ──────────────┐
    │                          │  every 2s: Stop→Start; countdown ticks each 1s     │
    │                          │                                                    │
    │              poll Connected on an attempt                       window elapsed (15s)
    │                          │                                                    │
    │                          ▼                                                    ▼
    └──────────── Connected (Reconnected: resume playback)                       Failed
                                                                  (StatusMessage = terminal,
                                                                   IsPlaying=false, Reconnect btn)
```

Transition rules:

1. **Idle → Connecting** — `Start()`/`OnSourceIdChanged` calls `StartReceiver(SourceId)`.
2. **Connecting/Connected → Dropped** — a connection poll returns `Disconnected` while
   `IsPlaying && !_userInitiatedStop`. (Drop during initial connect is also handled — see §8.)
3. **Dropped → Retrying** — begin the 15s window: set `IsReconnecting = true`, start the
   countdown (1s cadence) and the attempt loop (2s cadence) using the injected `TimeProvider`.
4. **Retrying → Reconnected** — an attempt's post-start poll returns `Connected`: dispose timers,
   `IsReconnecting = false`, `IsPlaying = true`, `StatusMessage` = normal/connected.
5. **Retrying → Failed** — window elapses (15s) with no `Connected`: dispose timers,
   `IsReconnecting = false`, `IsPlaying = false`,
   `StatusMessage = "Connection lost. Reconnection failed."`.
6. **Retrying → Idle (cancel)** — `CancelRetry()`: dispose timers, `StopReceiver()`,
   `IsReconnecting = false`, `IsPlaying = false`, stopped status.
7. **Any → Idle (user Stop)** — `Stop()` sets `_userInitiatedStop = true`, disposes any active
   timers, calls `StopReceiver()`. The drop handler is suppressed because the flag is set.
8. **Failed → Connecting (Reconnect)** — `Reconnect()` clears error state and re-runs the
   `Start()` path with the retained `SourceId`.

How drop detection is wired (D1, polling model): the connection state is **polled** rather than
event-driven, eliminating cross-thread event marshaling complexity. A lightweight monitor timer
(or the existing attempt/countdown tick) reads `GetConnectionState()`; on `Disconnected` while
playing and not user-stopped, it enters the window. Polling on a `TimeProvider`-backed timer keeps
it deterministic in tests. (Spec D1 rationale.)

### 4.2 Interface & Model Changes

```csharp
// src/Core/NdiBridge/NdiBridgeModels.cs   (NEW enum — per task instruction)
/// <summary>Connection state of an NDI receiver, surfaced for reconnection logic.</summary>
public enum ConnectionState
{
    Connecting,
    Connected,
    Disconnected,
}
```

```csharp
// src/Core/NdiBridge/INdiBridges.cs   (INdiViewerBridge — add one member)
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

```csharp
// src/MauiApp/NdiBridge/NdiBridgeImplementations.cs   (NdiViewerBridge stub ~line 209)
// Stub: Connected while an active source is set, otherwise Disconnected.
public ConnectionState GetConnectionState() =>
    _activeSourceId is null ? ConnectionState.Disconnected : ConnectionState.Connected;
```

> The real frame-arrival watchdog / 3s drop threshold (D2) becomes an internal bridge concern
> once real libndi is wired — out of scope for this work. The stub only needs to be deterministic
> so the ViewModel state machine is verifiable.

### 4.3 ViewModel Changes (`ViewerViewModel`)

New constructor signature (TimeProvider injected — **fresh**):

```csharp
public ViewerViewModel(INdiViewerBridge bridge, TimeProvider timeProvider)
```

New observable properties (`[ObservableProperty]`):

| Property | Type | Purpose |
|---|---|---|
| `IsReconnecting` | `bool` | Drives retry indicator + Cancel button visibility |
| `RetryStatusMessage` | `string?` | `"Reconnecting... {n}s remaining"` countdown text |
| `CanReconnect` | `bool` | Drives Reconnect button visibility in the error state |

New private fields:

| Field | Purpose |
|---|---|
| `_timeProvider` | injected `TimeProvider` |
| `_userInitiatedStop` | suppress retry on intentional `Stop` (FR8) |
| `_retryDeadline` / `_remainingSeconds` | countdown bookkeeping |
| `_countdownTimer` (`ITimer`) | 1s tick for countdown text |
| `_attemptTimer` (`ITimer`) | 2s tick for Stop→Start attempts |
| `_lastSourceId` | retained for `Reconnect` (FR7) |

Constants (single source of truth for tests):

```csharp
private const int RetryWindowSeconds = 15;
private const int AttemptIntervalSeconds = 2;
private const string TerminalMessage = "Connection lost. Reconnection failed.";
```

New commands:

- `[RelayCommand] CancelRetry()` — FR6.
- `[RelayCommand] Reconnect()` — FR7 (guarded by `CanReconnect`).

Modified members:

- `Start()` — clears `_userInitiatedStop = false`, records `_lastSourceId = SourceId`.
- `Stop()` — sets `_userInitiatedStop = true`, disposes timers, then stops/clears state.
- New private `BeginReconnectWindow()`, `RunAttempt()`, `TickCountdown()`,
  `CompleteReconnect()`, `FailReconnect()`, `DisposeTimers()` helpers.

UI-thread rule: every timer callback body wraps its observable mutations in
`MainThread.BeginInvokeOnMainThread(...)` (NFR2). To keep this testable, route through a small
`InvokeOnMainThread(Action)` seam that runs the action inline when no MAUI dispatcher is present
(tests) and via `MainThread.BeginInvokeOnMainThread` at runtime.

Timers use `_timeProvider.CreateTimer(...)`; the fake `TimeProvider` advances them
deterministically in tests.

### 4.4 View Changes (`ViewerPage.xaml`)

Pure XAML additions (no code-behind logic):

```xml
<!-- Retry indicator -->
<Label Text="{Binding RetryStatusMessage}"
       IsVisible="{Binding IsReconnecting}"
       HorizontalOptions="Center" />

<!-- Cancel retry -->
<Button Text="Cancel"
        Command="{Binding CancelRetryCommand}"
        IsVisible="{Binding IsReconnecting}"
        HorizontalOptions="Center" />

<!-- Post-expiry recovery -->
<Button Text="Reconnect"
        Command="{Binding ReconnectCommand}"
        IsVisible="{Binding CanReconnect}"
        HorizontalOptions="Center" />
```

`ViewerPage.xaml.cs` remains unchanged in structure (still just sets `BindingContext`).

### 4.5 DI Changes (`MauiProgram.cs`)

```csharp
// Register the system clock for TimeProvider-driven services/ViewModels.
builder.Services.AddSingleton(TimeProvider.System);
```

`ViewerViewModel` stays `AddTransient<ViewerViewModel>()` — DI resolves the new `TimeProvider`
parameter automatically. Verify no other registration relies on the old single-arg constructor.

---

## 5. Data Layer

No SQLite / EF Core schema or repository changes. The reconnection feature is entirely transient
runtime state held by the ViewModel.

---

## 6. Ordered Task List (for `feature.breakdown`)

Dependency-aware; each task should leave `dotnet build` green (Constitution §4.1).

- **T001** — Add `ConnectionState` enum to `src/Core/NdiBridge/NdiBridgeModels.cs`. *(no deps)*
- **T002** — Add `ConnectionState GetConnectionState()` to `INdiViewerBridge`
  (`src/Core/NdiBridge/INdiBridges.cs`). *(dep: T001)*
- **T003** — Implement stub `GetConnectionState()` in `NdiViewerBridge`
  (`src/MauiApp/NdiBridge/NdiBridgeImplementations.cs`). *(dep: T002)*
- **T004** — Inject `TimeProvider` into `ViewerViewModel` constructor; add `_timeProvider` field;
  update existing `ViewerViewModelTests` construction (pass a fake). *(dep: T002)*
- **T005** — Register `TimeProvider.System` in `MauiProgram.cs`; verify `ViewerViewModel`
  lifetime/registration. *(dep: T004)*
- **T006** — Add observable state (`IsReconnecting`, `RetryStatusMessage`, `CanReconnect`),
  fields, constants, and the `InvokeOnMainThread` seam to `ViewerViewModel`. *(dep: T004)*
- **T007** — Implement drop detection + `BeginReconnectWindow()` (state machine entry, FR1/FR2).
  *(dep: T006)*
- **T008** — Implement the 2s attempt loop `RunAttempt()` (Stop→Start, stop on `Connected`)
  and the success path `CompleteReconnect()` (FR3). *(dep: T007)*
- **T009** — Implement the 1s countdown `TickCountdown()` + `RetryStatusMessage` formatting (FR4).
  *(dep: T007)*
- **T010** — Implement expiry `FailReconnect()` → terminal message + `CanReconnect = true` (FR5).
  *(dep: T008, T009)*
- **T011** — Implement `CancelRetryCommand` (FR6) and `DisposeTimers()` cleanup. *(dep: T008)*
- **T012** — Update `Stop()` to set `_userInitiatedStop`, dispose timers, suppress retry (FR8);
  update `Start()` to reset the flag and record `_lastSourceId`. *(dep: T007)*
- **T013** — Implement `ReconnectCommand` (FR7, guarded by `CanReconnect`). *(dep: T010, T012)*
- **T014** — Add XAML bindings (retry label, Cancel button, Reconnect button) to
  `ViewerPage.xaml`. *(dep: T006)*
- **T015** — Write/extend unit tests in `ViewerViewModelTests` (see §7), incl. fake `TimeProvider`.
  *(dep: T007–T013)*
- **T016** — Run `dotnet test` (non-NDI) green; update spec/plan checkboxes if needed.
  *(dep: all)*

---

## 7. Testing Strategy

Project: `tests/MauiApp.Tests/Features/Viewer/ViewerViewModelTests.cs` (extend existing).
Stack: xUnit + Moq (already referenced). Add **`Microsoft.Extensions.TimeProvider.Testing`**
(`FakeTimeProvider`) to `MauiApp.Tests.csproj`; if it cannot be added, implement a minimal manual
`FakeTimeProvider : TimeProvider` exposing `Advance(TimeSpan)` and timer support.

SUT factory updated to `new ViewerViewModel(_bridgeMock.Object, _fakeTime)`.

Unit tests (each deterministic; advance the fake clock, never real delays):

1. **`Drop_WhilePlaying_EntersReconnecting`** — playing; bridge poll returns `Disconnected`;
   advance one poll tick → `IsReconnecting == true`, `RetryStatusMessage` set. (AC1)
2. **`RetryWindow_RunsAttemptsEvery2s`** — bridge stays `Disconnected`; advance 6s →
   verify ~3 `StopReceiver`/`StartReceiver` cycles. (AC1/FR2)
3. **`Countdown_DecrementsEachSecond`** — advance 1s/2s/3s; assert `RetryStatusMessage` =
   `"Reconnecting... 14s remaining"`, `13s`, `12s`. (AC2)
4. **`Reconnect_Succeeds_ResumesPlayback`** — bridge returns `Connected` on the 2nd attempt;
   advance past it → `IsReconnecting == false`, `IsPlaying == true`, no more attempts. (FR3)
5. **`Expiry_NoReconnect_SetsErrorState`** — bridge stays `Disconnected`; advance 15s →
   `IsReconnecting == false`, `IsPlaying == false`,
   `StatusMessage == "Connection lost. Reconnection failed."`, `CanReconnect == true`. (AC3)
6. **`CancelRetry_DuringWindow_StopsAndClears`** — enter window; `CancelRetryCommand.Execute` →
   `IsReconnecting == false`, `IsPlaying == false`, `StopReceiver` called, no further attempts
   after advancing the clock. (AC4)
7. **`Stop_ByUser_DoesNotTriggerRetry`** — playing; `StopCommand.Execute`; then advance the clock
   / simulate `Disconnected` → never enters reconnecting; `StartReceiver` not re-called. (AC5)
8. **`Reconnect_FromErrorState_RestartsFlow`** — drive to Failed state; `ReconnectCommand.Execute`
   → `StartReceiver(lastSourceId)` called, error state cleared. (FR7)
9. **`Drop_DuringInitialConnect_EntersReconnecting`** — never reached `Connected`; poll returns
   `Disconnected` → window starts (edge case, §8).
10. **Regression** — existing three tests still pass with the new constructor arg.

NDI e2e / on-device validation: out of scope (bridge is a stub); covered later when libndi
receive is wired.

---

## 8. Risks & Edge Cases

| Risk / Edge case | Mitigation |
|---|---|
| **User `Stop` during retry** | `Stop()` sets `_userInitiatedStop` and disposes timers before stopping; drop handler checks the flag (FR8, test 7). |
| **`SourceId` null when retry/Reconnect fires** | `Start()`/`RunAttempt()`/`Reconnect()` guard on `string.IsNullOrEmpty`; null → go to stopped state, no `StartReceiver`. |
| **App backgrounded mid-retry** | Timers are `TimeProvider`-backed and disposed on Stop/Cancel/success/expiry; document that lifecycle suspension (via `IAppLifecycleService`) should cancel the window. Wiring lifecycle is a follow-up; ensure `DisposeTimers()` is reachable from a future lifecycle hook. |
| **Drop during initial connect (never reached Connected)** | Treat `Disconnected` while `IsPlaying` uniformly; window starts regardless of prior `Connected` (test 9). |
| **Overlapping windows / timer leak** | `BeginReconnectWindow()` calls `DisposeTimers()` first; success/cancel/stop/expiry all dispose (NFR5). |
| **Cross-thread observable mutation** | All timer callbacks route through the `InvokeOnMainThread` seam → `MainThread.BeginInvokeOnMainThread` at runtime (NFR2). |
| **`FakeTimeProvider` package unavailable** | Fall back to a minimal hand-rolled `TimeProvider` fake with `Advance()` (§7). |
| **Existing tests break on ctor change** | T004 updates the SUT factory in the same task that changes the constructor (test 10). |

---

## 9. Constitution Compliance

| Principle (`docs/constitution.md`) | How this plan satisfies it |
|---|---|
| §1 Tech stack (MAUI, C#, CommunityToolkit MVVM, DI via MauiProgram, xUnit+Moq) | Uses `[ObservableProperty]`/`[RelayCommand]`; DI registers `TimeProvider.System`; tests are xUnit+Moq. |
| §2.1 Layering — no business logic in Views | All state-machine logic in `ViewerViewModel`; `ViewerPage.xaml.cs` stays a binding shim. |
| §2.1/§2.3 No NDI types cross the bridge boundary | `GetConnectionState()` returns the plain `ConnectionState` enum. |
| §2.3 Threading — marshal to UI thread | Timer/poll callbacks use `MainThread.BeginInvokeOnMainThread` via the seam (NFR2). |
| §2.3 Mock bridge for unit tests | Logic verified against `Mock<INdiViewerBridge>` + fake `TimeProvider`; no native lib. |
| §3 Testing standards (happy + error path per method; no native dependency) | §7 covers success/expiry/cancel/stop/edge paths deterministically. |
| §4.1 `dotnet build` green per task | Task list (§6) ordered so each task compiles independently. |
| §4.2 Conventional commits w/ `Task`/`Issue` trailers | Commits use `feat(viewer): …` + `Issue: #233` / `Task: T###`. |
| §4.6 Nullable enabled | New members respect nullable annotations; no unjustified `!`. |

---

## 10. Open Questions

None. All five clarifications (D1–D5) are resolved in the spec. The only deferred item — wiring
the real libndi frame watchdog and the 3s drop threshold (D2) inside the bridge — is explicitly
out of scope and documented for the future libndi integration.
