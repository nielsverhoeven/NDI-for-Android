# Technical Plan — Output Session Lifecycle

**Issues:** #326, #334 (Slice 1 — this branch), #327 (Slice 2 — follow-up branch)
**Branch:** `feature/326-output-session-state`
**Companion spec:** `docs/features/output-session-state/spec.md`
**Status:** Ready for implementation — read spec.md's **Known Test-Coverage Gap** before writing
any test; it is not repeated in full here.

Every step below names the exact file and member. "Anchor" quotes the existing line(s) to find;
only the delta is shown, not full before/after method bodies — the file is short enough to read
directly. Apply steps in order; each should leave `dotnet build NdiForAndroid.sln` green.

---

## SLICE 1 (#326 + #334) — this branch

### 1.1 `src/Core/Services/ICaptureSources.cs` — MODIFY

Above `IVideoCaptureSource`, add:
```csharp
public enum CaptureStopReason { ProjectionStopped, CameraDisconnected, CameraError, DeviceError }

/// <summary>Raised only for an autonomous stop — never for a caller-requested StopAsync().</summary>
public sealed record CaptureStoppedEventArgs(CaptureStopReason Reason, string? Message = null);
```
In `IVideoCaptureSource`, after `FrameReady`: `event EventHandler<CaptureStoppedEventArgs>? Stopped;`
In `IAudioCaptureSource`, after `ChunkReady`: `event EventHandler<CaptureStoppedEventArgs>? Stopped;`

### 1.2 `src/Core/NdiBridge/INdiBridges.cs` — MODIFY

In `INdiOutputBridge`, anchor `Task StopOutputAsync(CancellationToken cancellationToken = default);`
— insert directly after it, before `IsOnProgramTally`:
```csharp
/// <summary>True while either a capture-output sender or a re-stream sender holds a live
/// native handle — the authoritative "is output really active" signal, independent of any
/// persisted app-state claim.</summary>
bool IsActive { get; }
```
Update the `OutputStatusChanged` doc comment (signature unchanged) to also mention `IsActive`.

### 1.3 Noop capture sources — MODIFY

`src/MauiApp/Services/NoopVideoCaptureSource.cs` and `NoopAudioCaptureSource.cs`: add, next to the
existing empty `FrameReady`/`ChunkReady` accessor:
```csharp
public event EventHandler<CaptureStoppedEventArgs>? Stopped { add { } remove { } }
```

### 1.4 `src/MauiApp/Platforms/Android/Services/AndroidVideoCaptureSource.cs` — MODIFY

Add near `FrameReady`/`IsActive`:
```csharp
public event EventHandler<CaptureStoppedEventArgs>? Stopped;

private void RaiseStopped(CaptureStopReason reason, string? message = null)
{
    try { Stopped?.Invoke(this, new CaptureStoppedEventArgs(reason, message)); }
    catch { /* subscriber failures must never break capture teardown */ }
}
```
Anchor `public override void OnStop() => _owner.StopAsync().FireAndForget();` (in
`ProjectionCallback`) — replace with:
```csharp
public override void OnStop()
{
    _owner.RaiseStopped(CaptureStopReason.ProjectionStopped,
        "Screen capture was stopped (system revoked or the user ended it from the share indicator).");
    _owner.StopAsync().FireAndForget();
}
```
Anchor `HandleLoss(CameraDevice camera, Exception error)` (in `CameraStateCallback`) — thread a
`CaptureStopReason` parameter through it and its two callers:
```csharp
public override void OnDisconnected(CameraDevice camera) =>
    HandleLoss(camera, CaptureStopReason.CameraDisconnected, new InvalidOperationException("Camera was disconnected."));

public override void OnError(CameraDevice camera, CameraError error) =>
    HandleLoss(camera, CaptureStopReason.CameraError, new InvalidOperationException($"Camera reported error '{error}'."));

private void HandleLoss(CameraDevice camera, CaptureStopReason reason, Exception error)
{
    try
    {
        if (!_opened.TrySetException(error))
        {
            _owner.RaiseStopped(reason, error.Message);
            _owner.StopAsync().FireAndForget();
        }
        camera.Close();
    }
    catch { }
}
```
(Nested-class access to the outer `_owner`'s private `RaiseStopped`/`StopAsync` already matches
the file's existing pattern. When the TCS still accepts the exception — failure during open,
before any frame ever shipped — no `Stopped` is raised; that path already faults the `StartAsync`
awaiter directly.)

### 1.5 `src/MauiApp/Platforms/Android/Services/AndroidMicrophoneCaptureSource.cs` — MODIFY

Add near `ChunkReady`/`IsActive`:
```csharp
public event EventHandler<CaptureStoppedEventArgs>? Stopped;

private void RaiseStopped(CaptureStopReason reason, string? message = null)
{
    try { Stopped?.Invoke(this, new CaptureStoppedEventArgs(reason, message)); }
    catch { /* subscriber failures must never break capture teardown */ }
}
```
In `CaptureLoop()`, anchor `if (read < 0)\n    break; // ERROR_* code...` — replace with:
```csharp
if (read < 0)
{
    if (_running)
    {
        _running = false;
        RaiseStopped(CaptureStopReason.DeviceError, $"AudioRecord.Read returned error code {read}.");
    }
    break;
}
```
Anchor the surrounding `catch { break; }` — replace with:
```csharp
catch
{
    if (_running)
    {
        _running = false;
        RaiseStopped(CaptureStopReason.DeviceError, "The microphone capture loop threw an unexpected exception.");
    }
    break;
}
```
Why the `_running` check: `StopAsync()` sets `_running = false` **before** calling
`record.Stop()`, so by the time a `Stop()`-induced read interruption reaches here, `_running` is
already `false` and no event fires — only a genuine autonomous failure (mic taken by another app,
hardware fault) has `_running` still `true`. Setting `_running = false` here also fixes a latent
bug: today `IsActive` can stay `true` after the loop silently exits on error.

### 1.6 `src/MauiApp/NdiBridge/NdiOutputBridge.cs` — MODIFY

**a. Add `IsActive`** near `IsOnProgramTally`/`ConnectionCount`/`IsReStreamActive`:
```csharp
/// <inheritdoc />
public bool IsActive { get { lock (_sendLock) { return _send != IntPtr.Zero || _reStreamRunning; } } }
```

**b. `StartOutputCoreAsync`** — anchor `_videoSource.FrameReady += OnFrameReady;` — add right
after it `_videoSource.Stopped += OnCaptureStopped;`; anchor
`if (captureMicrophone)\n    _audioSource.ChunkReady += OnAudioChunkReady;` — add
`_audioSource.Stopped += OnCaptureStopped;` inside that same `if` block. As the **last line** of
the method (after `_statusTimer = new Timer(...)`), add `RaiseOutputStatusChanged();` (confirms
`IsActive == true` immediately, without waiting for the first 1 s poll).

**c. `StopOutputCoreAsync`** — as the **first line** of the method, capture:
```csharp
var wasActive = _send != IntPtr.Zero;   // read BEFORE any teardown below
```
Anchor the two unsubscribe lines `_videoSource.FrameReady -= OnFrameReady;` /
`_audioSource.ChunkReady -= OnAudioChunkReady;` — add alongside them
`_videoSource.Stopped -= OnCaptureStopped;` / `_audioSource.Stopped -= OnCaptureStopped;`
(unsubscribe **before** calling `StopAsync()` on either source — this is what makes
`OnCaptureStopped`'s call back into `StopOutputAsync()` non-reentrant, see Risks). Anchor
`var statusChanged = _isOnProgramTally || _connectionCount != 0;` — change to:
```csharp
var statusChanged = _isOnProgramTally || _connectionCount != 0 || wasActive;
```
(Without `|| wasActive`, a session stopped before any tally/connection ever ticked non-zero would
flip `IsActive` true→false with no `OutputStatusChanged` raised — exactly the case #326/#334 must
catch.)

**d. Add the handler**, next to `OnFrameReady`/`OnAudioChunkReady`:
```csharp
/// <summary>Raised on the capture source's native callback thread when it stops itself
/// autonomously. Stops the whole output session; the resulting OutputStatusChanged/IsActive
/// transition is what OutputViewModel/HomeViewModel react to.</summary>
private void OnCaptureStopped(object? sender, CaptureStoppedEventArgs e) =>
    StopOutputAsync().FireAndForget();
```

**e. `StartReStreamFromSourceCoreAsync`** — after the `lock (_reStreamLock) { ... }` block that
starts `_reStreamThread` completes (as the method's last line, outside the lock — the `catch`
already re-throws before reaching it), add `RaiseOutputStatusChanged();`.

**f. `StopReStreamCoreAsync`** — hoist the `hadHandles` local out of the final
`lock (_reStreamLock) { ... }` block (declare `bool hadHandles;` before the lock, assign inside),
then after the lock closes:
```csharp
if (hadHandles)
    RaiseOutputStatusChanged();
```

No `MauiProgram.cs` change — only contracts grew, all already registered.

### 1.7 `src/Core/Features/Output/ViewModels/OutputViewModel.cs` — MODIFY

**`OnAppResumed()`** — anchor the whole method body; replace with:
```csharp
private async void OnAppResumed()
{
    // Re-attach on resume if there was an active stream — but only claim "restored"
    // when the bridge corroborates it (#326). A stale persisted flag (process death,
    // revoked permission, camera/mic loss while backgrounded) must not lie to the user.
    var state = await _appStateRepo.RestoreStateAsync();
    if (state.IsOutputActive && !string.IsNullOrEmpty(state.StreamName))
    {
        StreamName = state.StreamName;

        if (_bridge.IsActive)
        {
            IsOutputActive = true;
            StatusMessage = "Output session restored.";
        }
        else
        {
            IsOutputActive = false;
            StatusMessage = "Tap Start to resume output";
            await _appStateRepo.SaveAsync(new AppStateSnapshot(
                state.LastViewerSourceId, state.StreamName, false, state.LastSelectedSourceId));
        }
    }
}
```
Exact string `"Tap Start to resume output"` (no trailing period) matches the issue's binding
wording verbatim — tests must assert it exactly. `StreamName` is set either branch, so a
subsequent Start reuses it.

**`OnOutputStatusChanged`** — anchor the body inside `_dispatcher.BeginInvokeOnMainThread(() => { ... })`
— after the existing `ConnectionCount = _bridge.ConnectionCount;` line, add:
```csharp
// The bridge stopped itself (autonomous capture loss #334, or the notification
// Stop action #327) — correct local state to match.
if (IsOutputActive && !_bridge.IsActive)
{
    IsOutputActive = false;
    StatusMessage = "Output stopped";
}
```
This single addition is the **only** ViewModel change Slice 2's notification Stop action needs
(§2.4). No constructor/DI change for `OutputViewModel`.

### 1.8 `src/Core/Features/Home/ViewModels/HomeViewModel.cs` — MODIFY

Constructor: add a 5th parameter `INdiOutputBridge outputBridge` (before `dispatcher`, which stays
last); add `using NdiForAndroid.NdiBridge;`; add field `_outputBridge`, assign it, and in the ctor
body alongside `_discoveryService.SnapshotReady += OnDiscoverySnapshot;` add
`_outputBridge.OutputStatusChanged += OnOutputStatusChanged;`. (Already a registered singleton —
no `MauiProgram.cs` change.)

`RefreshAsync()` — anchor:
```csharp
OutputStatus = state.IsOutputActive
    ? $"Active output to \"{state.StreamName ?? "unknown"}\""
    : "Idle (no active output)";
```
change the condition to `state.IsOutputActive && _outputBridge.IsActive`.

Add a new handler near `OnDiscoverySnapshot`:
```csharp
private void OnOutputStatusChanged(object? sender, EventArgs e) =>
    _dispatcher.BeginInvokeOnMainThread(() => RefreshCommand.Execute(null));
```

`Dispose()` — add `_outputBridge.OutputStatusChanged -= OnOutputStatusChanged;` alongside the
existing `_discoveryService.SnapshotReady -= OnDiscoverySnapshot;`.

`HomeViewModel.ResumeOutput()` is **unchanged** (spec.md Out of Scope).

### 1.9 Tests (Slice 1) — see tasks.md for full Arrange/Act/Assert detail

- NEW `tests/MauiApp.Tests/Services/CaptureSourcesTests.cs` — Core-only contract tests for
  `CaptureStoppedEventArgs`/`CaptureStopReason` via a minimal private fake `IVideoCaptureSource`.
- MODIFY `tests/MauiApp.Tests/Features/Output/OutputViewModelTests.cs` — 4 new tests covering
  `OnAppResumed` corroboration (both branches) and `OnOutputStatusChanged` correction (both
  branches), all via `Mock<INdiOutputBridge>`.
- MODIFY `tests/MauiApp.Tests/Features/Home/HomeViewModelTests.cs` — add `Mock<INdiOutputBridge>`,
  update `CreateSut()` to a 6-argument call, 4 new tests covering corroboration and live refresh.

No `MockBehavior.Strict` exists anywhere in this suite (verified) and only these two files mock
`INdiOutputBridge` (verified) — adding `IsActive` breaks nothing else.

---

## SLICE 2 (#327) — follow-up branch

Depends on Slice 1 being merged (`IsActive` + the `OutputViewModel.OnOutputStatusChanged`
correction must already exist).

### 2.1 `src/Core/Features/Navigation/Services/NdiNavigationHandoffService.cs` — MODIFY

Replace the whole class body with:
```csharp
public sealed class NdiNavigationHandoffService : INavigationHandoffService
{
    private readonly INdiViewerBridge _viewerBridge;

    public NdiNavigationHandoffService(INdiViewerBridge viewerBridge)
    {
        _viewerBridge = viewerBridge;
    }

    public Task HandlePrimaryDestinationChangeAsync(
        PrimaryNavDestination from, PrimaryNavDestination to, CancellationToken cancellationToken = default)
    {
        if (from != to && from == PrimaryNavDestination.View)
            _viewerBridge.StopReceiver();

        return Task.CompletedTask;
    }
}
```
Drop the now-unused `using NdiForAndroid.Features.AppState.Models;` /
`.Repositories;` (keep `using NdiForAndroid.NdiBridge;` for `INdiViewerBridge`). No
`MauiProgram.cs` change — DI resolves the reduced constructor automatically. The `Stream` branch
(stop output + clear `IsOutputActive` on leaving the Stream tab) is fully removed — this is the
obsolete interim fix the issue calls out; the `View` branch is unchanged.

### 2.2 `tests/MauiApp.Tests/Features/Navigation/NdiNavigationHandoffServiceTests.cs` — MODIFY

Drop `_outputBridgeMock`/`_appStateRepoMock`; `CreateSut()` becomes `new(_viewerBridgeMock.Object)`.
Replace the two existing tests with three:
- `HandlePrimaryDestinationChangeAsync_LeavingView_StopsReceiver` — `(View → Home)`, verify
  `_viewerBridgeMock.Verify(b => b.StopReceiver(), Times.Once)`.
- `HandlePrimaryDestinationChangeAsync_LeavingStream_DoesNotStopReceiver` — `(Stream → Home)`,
  verify `StopReceiver()` is `Times.Never` (proves the Stream branch is gone).
- `HandlePrimaryDestinationChangeAsync_SameDestination_IsNoOp` — `(View → View)`, verify
  `StopReceiver()` is `Times.Never`.

Confirmed (repo-wide search): no other test file mocks `INavigationHandoffService` or
`NdiNavigationHandoffService` alongside these dependencies.

### 2.3 `src/MauiApp/Platforms/Android/Services/ScreenShareForegroundService.cs` — MODIFY

Add usings: `Microsoft.Extensions.DependencyInjection`, `Microsoft.Maui`, `NdiForAndroid.NdiBridge`.

Add, alongside the existing action constants:
```csharp
internal const string ActionStopRequested = "com.ndi.android.action.STOP_REQUESTED"; // notification button only
...
private const int StopActionRequestCode = 4108; // distinct from NotificationId (4107)
```
In `OnStartCommand`, insert a new branch **before** the existing `if (intent?.Action == ActionStop)`:
```csharp
if (intent?.Action == ActionStopRequested)
{
    // Notification Stop button: kick off the FULL teardown (bridge → capture sources →
    // this service). Do NOT stop the service here — that would leave camera/mic/projection
    // running with the notification gone. The capture source's StopAsync() eventually calls
    // IScreenSharePlatformService.StopForegroundSessionAsync(), which sends the EXISTING
    // ActionStop back here to remove the foreground state.
    var bridge = IPlatformApplication.Current?.Services.GetService<INdiOutputBridge>();
    bridge?.StopOutputAsync().FireAndForget();
    return StartCommandResult.NotSticky;
}
```
(`IPlatformApplication.Current?.Services.GetService<T>()` mirrors the pattern already used in
`MainActivity.cs` to reach a DI-registered singleton from Android platform code.)

In `BuildNotification`, before `return builder.Build()!;`, add a Stop action:
```csharp
var stopIntent = new Intent(this, typeof(ScreenShareForegroundService));
stopIntent.SetAction(ActionStopRequested);
var stopPendingIntent = PendingIntent.GetService(
    this, StopActionRequestCode, stopIntent,
    PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

builder.AddAction(new NotificationCompat.Action.Builder(
    global::Android.Resource.Drawable.IcMenuCloseClearCancel, "Stop", stopPendingIntent).Build());
```
If `IcMenuCloseClearCancel` doesn't resolve on this binding, substitute any stable framework
drawable — the icon is cosmetic, the `PendingIntent` is what matters. No `AndroidManifest.xml`
change: the four `FOREGROUND_SERVICE*` permissions already declared cover an action button on an
existing notification.

### 2.4 UI reflection of a notification-triggered stop — NO NEW CODE

`ScreenShareForegroundService` now calls `INdiOutputBridge.StopOutputAsync()` directly, which
already raises `OutputStatusChanged` with `IsActive == false` (§1.6.c/d). Both
`OutputViewModel.OnOutputStatusChanged` (§1.7) and `HomeViewModel.OnOutputStatusChanged` (§1.8)
already correct their displayed state from that event, regardless of which autonomous-stop path
triggered it. **No ViewModel or View change in this slice.**

### 2.5 Background streaming — verification only, no code change expected

Confirmed by repo-wide search: nothing subscribes to `IAppLifecycleService.AppPaused` or
`MainActivity.OnPause` to stop output (only `DiscoveryRefreshService` subscribes to `AppPaused`,
for discovery polling — unrelated). `ScreenShareForegroundService` already declares
`ForegroundServiceType = TypeMediaProjection | TypeCamera | TypeMicrophone`, gated per-API-level in
`GetGrantedServiceTypes`. The manual device-verification task in tasks.md is the deliverable here,
not a code change — unless it surfaces a real regression, in which case treat that as new scope.

### 2.6 Tests (Slice 2)

Only §2.2. `ScreenShareForegroundService`/`AndroidScreenSharePlatformService` have no unit-test
coverage today (same Known Test-Coverage Gap as Slice 1) — the notification Stop action and
background survival are validated exclusively via the `android-build-install-run` skill.

---

## Constitution / Architecture Compliance

| Rule | Compliance |
|---|---|
| No NDI SDK types cross the bridge boundary | `IsActive` is `bool`; `CaptureStopReason`/`CaptureStoppedEventArgs` are plain Core types. |
| Bridge events on background threads; ViewModels marshal via `IMainThreadDispatcher` | All new raise sites run on native callback threads; both ViewModels wrap reactions in `_dispatcher.BeginInvokeOnMainThread(...)`. |
| Android APIs isolated behind Core interfaces; `Noop*` elsewhere | `Stopped` added to both Noop capture sources, never raised, same pattern as `FrameReady`/`ChunkReady`. |
| No business logic in Views | No XAML/View changes in either slice. |
| `dotnet build`/`dotnet test` green after each task | Task ordering in tasks.md keeps every step independently compilable. |

## Risks & Edge Cases

| Risk | Mitigation |
|---|---|
| `OnCaptureStopped` → `StopOutputAsync()` re-entering via the capture source's own teardown | Bridge unsubscribes `Stopped` before calling the sources' `StopAsync()` (§1.6.c); any resulting second `StopAsync()` call on a capture source is an idempotent no-op. |
| `OnOutputStatusChanged`'s correction racing a user-initiated `StopOutputCommand` | Benign — `StopOutputCommand`'s own later `StatusMessage = null;` always runs last and wins; not separately tested since Moq mocks never auto-raise events during a command under test. |
| Mic `CaptureLoop` misclassifying a `StopAsync()`-driven read error as autonomous | Guarded by `_running`, set `false` by `StopAsync()` *before* `record.Stop()` — an ordering guarantee already present. |
| `IcMenuCloseClearCancel` binding not resolving | Documented fallback to any stable framework drawable (§2.3). |
| Slice 2 landing before Slice 1 | Explicitly sequenced — §2.4 has no ViewModel code because it depends on Slice 1's correction path. |

## Open Questions

None — all decisions are bound by the issue comments (spec.md D1–D4). The icon-resource fallback
in §2.3 is the only detail left to the developer's discretion, and it is cosmetic.
