# Feature Spec — Output Session Lifecycle

**Issues:** #326 (session-active signal), #334 (autonomous capture-stop event), #327 (background streaming)
**Branch:** `feature/326-output-session-state` (based on `bugfix/336`; PR base `integration/watch-and-discovery`)
**Status:** Ready for `feature-breakdown` / implementation

---

## Summary

Three related bugs share one root cause: the app has no reliable, real-time signal for whether
an NDI **output** session is genuinely still running. Today "active output" is inferred purely
from a persisted `AppStateSnapshot.IsOutputActive` flag that is set on Start and only cleared by
an explicit Stop or by the (soon to be removed) Stream-tab navigation handoff. If the OS revokes
screen-capture permission, the camera disconnects, the microphone read loop errors, or the process
is killed and restarted, the persisted flag goes stale and the UI lies to the user ("Output session
restored" when nothing is actually streaming).

This is designed as **one coherent output-session-lifecycle change**, delivered as **two PRs**:

- **Slice 1 (this branch, #326 + #334):** give the bridge and the capture sources a live,
  corroborated activity signal, and make the ViewModels trust that signal instead of the
  persisted flag alone.
- **Slice 2 (follow-up branch, #327):** once the signal is trustworthy, stop tearing output down
  on tab switches, keep it running across backgrounding (the foreground service already supports
  this), and give the user a Stop action on the persistent notification.

Both slices reuse the exact same plumbing: Slice 2 introduces no new ViewModel correction logic —
the notification Stop action is just another autonomous-stop path that flows through the Slice-1
`IsActive` / `OutputStatusChanged` correction already built.

---

## Resolved Decisions (binding — from issue comments, 2026-09-04)

### D1 — Session-active signal shape (#326)

**Decision:** Add **`bool IsActive { get; }`** to `INdiOutputBridge` (NOT an `OutputSessionState`
enum + a dedicated `SessionStateChanged` event).

**Rationale:**
- `INdiOutputBridge` already reports sender status as **plain properties polled through one
  generic `OutputStatusChanged` event** (`IsOnProgramTally`, `ConnectionCount`, `IsReStreamActive`)
  — no per-property change events, no state-machine enum anywhere on this interface. A binary
  `IsActive` property fits that existing shape exactly; `OutputStatusChanged` already is the
  single "something about sender status changed, go re-read the properties" signal.
- Unlike the viewer bridge's `ConnectionState` (`Connecting/Connected/Disconnected`, added for
  #233), the output bridge has **no analogous "connecting" phase** worth modeling: `StartOutputAsync`
  is awaited by the caller until it either succeeds (sender handle created, capture started) or
  throws. There is no partial/negotiating state to distinguish — the sender either has a live
  native handle or it does not. A tri-state enum would model a state that never actually exists,
  adding ceremony (new enum in `NdiBridgeModels.cs`, a new event, more Moq setup surface) with no
  behavioral payoff.
- `IsActive` aggregates **both** output modes the bridge can run — capture-output
  (`_send != IntPtr.Zero`) and re-stream (`_reStreamRunning`, already exposed separately as
  `IsReStreamActive`) — because `OutputViewModel`/`HomeViewModel` treat "an output session is
  active" as one concept regardless of which mode started it (both `StartOutputAsync` and
  `StartReStreamFromSourceAsync` set the ViewModel's single `IsOutputActive` flag).

### D2 — Autonomous capture-stop event shape (#334)

**Decision:** Add `event EventHandler<CaptureStoppedEventArgs>? Stopped;` to both
`IVideoCaptureSource` and `IAudioCaptureSource`, with a shared Core model:

```csharp
public enum CaptureStopReason { ProjectionStopped, CameraDisconnected, CameraError, DeviceError }
public sealed record CaptureStoppedEventArgs(CaptureStopReason Reason, string? Message = null);
```

**Rationale:**
- Mirrors the existing `IVideoCaptureSource`/`IAudioCaptureSource` event-based contract
  (`FrameReady`, `ChunkReady`) — a third event of the same shape needs no new abstraction.
- `Reason` lets `NdiOutputBridge` log/diagnose without string-matching, while `Message` carries
  the human-readable detail already constructed at each call site
  (`"Camera reported error '{error}'."`, `"AudioRecord.Read returned error code {read}."`, …).
- One shared enum/record for both interfaces (not two parallel `VideoCaptureStopReason`/
  `AudioCaptureStopReason` types) because the consumer (`NdiOutputBridge`) reacts identically
  regardless of which capture source stopped: stop the whole output session. `CameraDisconnected`/
  `CameraError` are meaningless for audio and `DeviceError` covers both.
- The event fires **only for autonomous stops** — never for a caller-requested `StopAsync()`.
  This is what makes it safe for `NdiOutputBridge` to subscribe and react by calling its own
  `StopOutputAsync()` without an infinite loop: see Interface Changes and the plan's ordering
  notes.

### D3 — Corroboration semantics (#326, ties to D1)

**Decision:** A persisted `IsOutputActive == true` claim is only honored when
`INdiOutputBridge.IsActive` also reports `true`. Otherwise the UI shows
**"Tap Start to resume output"** (exact issue wording, no trailing period) and the persisted
`IsOutputActive` flag is cleared so a later resume does not repeat the stale claim.

### D4 — Background streaming (#327)

**Decision:** An active output must keep streaming across tab switches and app backgrounding,
with the persistent notification, stopping only via the in-app Stop button or a **new** Stop
action on that notification. Consequences (binding, from the issue):
- Remove the `Stream` branch of `NdiNavigationHandoffService` — the interim fix that stopped
  output on leaving the Stream tab is now obsolete; the `View` branch (`StopReceiver()` on
  leaving View) is unaffected and stays.
- The existing `ScreenShareForegroundService` (foreground-service types
  `mediaProjection|camera|microphone`, gated per-API-level in `GetGrantedServiceTypes`) already
  keeps native capture alive while the app is backgrounded — this is the entire purpose of a
  foreground service. No teardown of output is currently wired to `IAppLifecycleService.AppPaused`
  (only `DiscoveryRefreshService` subscribes to it, unrelated to output) or to `MainActivity.OnPause`.
  Slice 2 therefore needs **no new code** to keep output alive while backgrounded — only a
  device-verification pass to confirm no regression, since the actual bug today is the
  Stream-tab-leave handoff, not backgrounding.
- Add a Stop action to the persistent notification: `PendingIntent` → `ScreenShareForegroundService`
  → `INdiOutputBridge.StopOutputAsync()` → the existing `OutputStatusChanged`/`IsActive` correction
  path (built in Slice 1) updates the UI. No new ViewModel code is needed for "the UI reflects a
  stop from the notification" — it is the same autonomous-stop correction as #334.

---

## Interface Changes

```csharp
// src/Core/Services/ICaptureSources.cs
public enum CaptureStopReason
{
    /// <summary>MediaProjection.Callback.OnStop — system/user revoked screen-capture consent.</summary>
    ProjectionStopped,
    /// <summary>CameraDevice.StateCallback.OnDisconnected.</summary>
    CameraDisconnected,
    /// <summary>CameraDevice.StateCallback.OnError.</summary>
    CameraError,
    /// <summary>A capture read loop failed autonomously (e.g. AudioRecord.Read error).</summary>
    DeviceError,
}

public sealed record CaptureStoppedEventArgs(CaptureStopReason Reason, string? Message = null);

public interface IVideoCaptureSource
{
    event EventHandler<CapturedVideoFrame>? FrameReady;
    /// <summary>Raised (on a native callback thread) when capture stops itself autonomously —
    /// NEVER for a caller-requested StopAsync().</summary>
    event EventHandler<CaptureStoppedEventArgs>? Stopped;   // NEW
    bool IsActive { get; }
    Task StartAsync(VideoInputKind kind, CancellationToken cancellationToken = default);
    Task StopAsync();
}

public interface IAudioCaptureSource
{
    event EventHandler<CapturedAudioChunk>? ChunkReady;
    event EventHandler<CaptureStoppedEventArgs>? Stopped;   // NEW
    bool IsActive { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
}
```

```csharp
// src/Core/NdiBridge/INdiBridges.cs — INdiOutputBridge
Task StartOutputAsync(...);
Task StopOutputAsync(...);

/// <summary>True while either a capture-output sender or a re-stream sender holds a live
/// native handle — the authoritative signal for whether output is genuinely active,
/// independent of any persisted app-state claim.</summary>
bool IsActive { get; }   // NEW

bool IsOnProgramTally { get; }
int ConnectionCount { get; }

/// <summary>Raised (on a background thread) when IsActive, IsOnProgramTally, or
/// ConnectionCount changed. Subscribers marshal to the UI thread.</summary>
event EventHandler? OutputStatusChanged;   // doc comment updated, signature unchanged
```

---

## Out of Scope

- `HomeViewModel.ResumeOutput()` navigating unconditionally on a stale persisted flag — left
  unchanged in Slice 1. It navigates to the Stream tab regardless; `OutputViewModel.OnAppResumed`'s
  corrected D3 logic self-heals once that page loads ("Tap Start to resume output" instead of a
  false "restored" claim), so no user-visible harm from leaving `ResumeOutput` as-is.
- Real device-level backgrounding regression fixes — analysis (D4) found no code currently tears
  output down on backgrounding, so Slice 2 only needs a verification pass, not a fix, unless that
  verification surfaces a real regression.
- A `NoopNdiOutputBridge` — `NdiOutputBridge` has one always-registered implementation across all
  targets (soft-disables internally via `NdiRuntime.EnsureInitialized() == false`); no Noop split
  needed for `IsActive`.
- Any change to `NdiViewerBridge`/`ConnectionState` (viewer/receive path) — out of scope, this
  feature is output-only.

## Known Test-Coverage Gap (read before writing tests)

`tests/MauiApp.Tests/NdiForAndroid.Tests.csproj` references **only**
`src/Core/NdiForAndroid.Core.csproj` (plain `net10.0`). It has **no** reference to
`src/MauiApp/NdiForAndroid.csproj` (the `net10.0-android`-only project), and could not usefully
have one even if added — Android-targeted assemblies are not loadable/runnable in a plain
`net10.0` xUnit host, and `NdiOutputBridge` P/Invokes `libndi.so` which does not exist on the CI
build agent. Confirmed by the current repo state: there are **no existing unit tests** for
`NdiOutputBridge`, `AndroidVideoCaptureSource`, or `AndroidMicrophoneCaptureSource` — this is the
established pattern (Core interfaces are unit-tested via ViewModel tests against `Mock<T>`; the
concrete bridge/platform classes are validated on-device).

Consequently, for this feature:
- `NdiOutputBridge`'s new `IsActive` getter, its `OnCaptureStopped` handler, and its
  `OutputStatusChanged` raise sites are **not unit-testable** — validate them via the
  `android-build-install-run` skill (see tasks.md's manual verification task).
- `AndroidVideoCaptureSource`/`AndroidMicrophoneCaptureSource`'s new `Stopped`-raising call sites
  (`ProjectionCallback.OnStop`, `CameraStateCallback`, the mic `CaptureLoop`) are likewise
  device-verified only.
- What **is** unit-testable and required: the `CaptureStoppedEventArgs`/`CaptureStopReason`
  contract itself (Core-only, via a minimal fake capture source), and all `OutputViewModel`/
  `HomeViewModel` behavior driven through `Mock<INdiOutputBridge>`.

---

## Acceptance Criteria

- [x] `INdiOutputBridge.IsActive` reflects reality (true only while a native sender or re-stream
  handle is live) and `OutputStatusChanged` fires whenever it transitions. (#326)
- [x] `OutputViewModel.OnAppResumed` shows "Output session restored." only when the bridge
  corroborates `IsActive`; otherwise shows "Tap Start to resume output" and persists
  `IsOutputActive = false`. (#326)
- [x] `HomeViewModel`'s output status corroborates the persisted flag against
  `INdiOutputBridge.IsActive` rather than trusting the persisted flag alone, and refreshes live
  on `OutputStatusChanged`. (#326)
- [x] `IVideoCaptureSource`/`IAudioCaptureSource` raise `Stopped` from
  `ProjectionCallback.OnStop`, camera loss/error, and the mic capture-loop error path — and only
  for those autonomous cases, never for a caller-requested `StopAsync()`. (#334)
- [x] `NdiOutputBridge` subscribes to both sources' `Stopped` event while output is active and
  corrects its own state (stops the sender) in response. (#334)
- [x] `OutputViewModel.OnOutputStatusChanged` corrects `IsOutputActive`/`StatusMessage` in real
  time when the bridge reports `IsActive == false` while the ViewModel still thinks it is active.
  (#334, and reused by #327's notification Stop action)
- [ ] `NdiNavigationHandoffService`'s `Stream` branch is removed; leaving the Stream tab no longer
  stops output; the `View` branch is unchanged. (#327)
- [ ] The persistent notification gains a Stop action that stops the sender end-to-end
  (bridge → capture sources → foreground service) without requiring the app to be foregrounded.
  (#327)
- [ ] Output survives backgrounding (device-verified, no code regression expected). (#327)
- [ ] `dotnet build NdiForAndroid.sln` and `dotnet test tests/MauiApp.Tests` stay green throughout.
