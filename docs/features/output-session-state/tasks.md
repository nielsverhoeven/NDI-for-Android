# Tasks: Output Session Lifecycle

**Parent issues:** #326, #334 (Slice 1 — this branch), #327 (Slice 2 — follow-up branch)
**Branch:** `feature/326-output-session-state`
**Source plan:** `docs/features/output-session-state/plan.md`
**Spec:** `docs/features/output-session-state/spec.md`

Every task should leave `dotnet build NdiForAndroid.sln` green; test-writing tasks should leave
`dotnet test tests/MauiApp.Tests` green. Read spec.md's **Known Test-Coverage Gap** before T010 —
`NdiOutputBridge` and the Android capture-source classes cannot be unit tested from
`tests/MauiApp.Tests` (Core-only test project, no native NDI, no Android runtime); those changes
are validated on-device (T014).

---

## Dependency Graph — Slice 1

```
T001 → T003, T004
T002 → T006
T003 → T005
T004 → T005
T005 → T006, T007, T009
T006 → T007
T007 → T009
T008 → T009 (independent of T001-T007 otherwise)
T001..T007 → T010
T008 → T010
T009 → T011
T001..T009 → T012
```
Ready immediately (no deps): **T001**, **T002**.

## Dependency Graph — Slice 2

```
T013 → T015
T014 (verification only, no code dep beyond Slice 1 merged)
T015 → T016
```

---

## Task List (Slice 1)

| T-ID | Title | Depends on | Layer |
|------|-------|------------|-------|
| T001 | Add `CaptureStopReason`/`CaptureStoppedEventArgs` + `Stopped` event to capture source interfaces | none | Core |
| T002 | Add `IsActive` to `INdiOutputBridge` | none | Core |
| T003 | Add `Stopped` (never-raised) to Noop capture sources | T001 | MauiApp |
| T004 | Raise `Stopped` from `AndroidVideoCaptureSource` (projection stop, camera loss/error) | T001 | MauiApp/Android |
| T005 | Raise `Stopped` from `AndroidMicrophoneCaptureSource` (capture-loop error) | T001 | MauiApp/Android |
| T006 | Implement `NdiOutputBridge.IsActive` + subscribe/react to `Stopped` + fix `OutputStatusChanged` raise sites | T001, T002, T004, T005 | MauiApp/NdiBridge |
| T007 | `OutputViewModel`: corroborate `OnAppResumed`, correct `OnOutputStatusChanged` | T002 | Core ViewModel |
| T008 | `HomeViewModel`: inject bridge, corroborate `RefreshAsync`, subscribe `OutputStatusChanged` | T002 | Core ViewModel |
| T009 | NEW `CaptureSourcesTests.cs` — Core contract tests for `CaptureStoppedEventArgs` | T001 | Tests |
| T010 | Extend `OutputViewModelTests.cs` (4 new tests) | T002, T007 | Tests |
| T011 | Extend `HomeViewModelTests.cs` (4 new tests) | T002, T008 | Tests |
| T012 | `dotnet build` + `dotnet test` full green; tick spec/plan checkboxes | T001–T011 | Verification |

## Task List (Slice 2 — follow-up branch)

| T-ID | Title | Depends on | Layer |
|------|-------|------------|-------|
| T013 | Remove Stream branch of `NdiNavigationHandoffService`; simplify ctor | Slice 1 merged | Core |
| T014 | Add notification Stop action to `ScreenShareForegroundService` | Slice 1 merged | MauiApp/Android |
| T015 | Rewrite `NdiNavigationHandoffServiceTests.cs` (3 tests) | T013 | Tests |
| T016 | Device verification: background survival + notification Stop action (`android-build-install-run` skill) | T014, T015 | Manual/Device |

---

## Detailed Tasks (Slice 1)

### T001 — Add `CaptureStopReason`/`CaptureStoppedEventArgs` + `Stopped` event
- **Layer**: Core
- **Files**: `src/Core/Services/ICaptureSources.cs`
- **Description**: Add `CaptureStopReason` enum (`ProjectionStopped`, `CameraDisconnected`,
  `CameraError`, `DeviceError`) and `CaptureStoppedEventArgs(CaptureStopReason Reason, string?
  Message = null)` record. Add `event EventHandler<CaptureStoppedEventArgs>? Stopped;` to both
  `IVideoCaptureSource` and `IAudioCaptureSource`. See plan.md §1.1.
- **Depends on**: none
- **Acceptance**: Compiles; enum/record are plain Core types (no NDI/Android types); `dotnet build`
  green.

### T002 — Add `IsActive` to `INdiOutputBridge`
- **Layer**: Core
- **Files**: `src/Core/NdiBridge/INdiBridges.cs`
- **Description**: Add `bool IsActive { get; }` after `StopOutputAsync`, before `IsOnProgramTally`.
  Update the `OutputStatusChanged` XML doc to mention `IsActive`. See plan.md §1.2.
- **Depends on**: none
- **Acceptance**: Compiles (interface-only change — no implementer yet satisfies it until T006);
  `dotnet build` fails at this point for `NdiOutputBridge` alone (expected, resolved by T006) — if
  the developer wants a green build at every single commit, land T002+T006 as one commit.

### T003 — Noop capture sources: `Stopped` never raised
- **Layer**: MauiApp
- **Files**: `src/MauiApp/Services/NoopVideoCaptureSource.cs`, `NoopAudioCaptureSource.cs`
- **Description**: Add `public event EventHandler<CaptureStoppedEventArgs>? Stopped { add { }
  remove { } }` to each, matching the existing `FrameReady`/`ChunkReady` empty-accessor pattern
  (avoids CS0067). See plan.md §1.3.
- **Depends on**: T001
- **Acceptance**: Both classes satisfy the updated interfaces; `dotnet build` green.

### T004 — `AndroidVideoCaptureSource` raises `Stopped`
- **Layer**: MauiApp/Android
- **Files**: `src/MauiApp/Platforms/Android/Services/AndroidVideoCaptureSource.cs`
- **Description**: Add the `Stopped` event + `RaiseStopped` helper. Raise
  `CaptureStopReason.ProjectionStopped` from `ProjectionCallback.OnStop()` before calling
  `StopAsync()`. Thread a `CaptureStopReason` through `CameraStateCallback.HandleLoss` and raise
  `CameraDisconnected`/`CameraError` from `OnDisconnected`/`OnError` (only in the branch where
  `_opened.TrySetException` returns `false` — i.e. the camera was already running). See plan.md
  §1.4.
- **Depends on**: T001
- **Acceptance**: Compiles against the updated `IVideoCaptureSource`; `dotnet build` green. (No
  unit test — see Known Test-Coverage Gap; validated in T016.)

### T005 — `AndroidMicrophoneCaptureSource` raises `Stopped`
- **Layer**: MauiApp/Android
- **Files**: `src/MauiApp/Platforms/Android/Services/AndroidMicrophoneCaptureSource.cs`
- **Description**: Add the `Stopped` event + `RaiseStopped` helper. In `CaptureLoop()`, on
  `read < 0` or an unhandled exception, if `_running` is still `true`, set it `false` and raise
  `CaptureStopReason.DeviceError` before breaking (guards against misclassifying a
  `StopAsync()`-driven exit as autonomous — see plan.md §1.5 for the ordering argument).
- **Depends on**: T001
- **Acceptance**: Compiles against the updated `IAudioCaptureSource`; `IsActive` correctly becomes
  `false` after an autonomous loop exit (previously stayed `true` — latent bug fix); `dotnet build`
  green. (No unit test — validated in T016.)

### T006 — `NdiOutputBridge.IsActive` + `Stopped` subscription + `OutputStatusChanged` fixes
- **Layer**: MauiApp/NdiBridge
- **Files**: `src/MauiApp/NdiBridge/NdiOutputBridge.cs`
- **Description**: Implement `IsActive` (`_send != IntPtr.Zero || _reStreamRunning`, read under
  `_sendLock`). Subscribe `_videoSource.Stopped`/`_audioSource.Stopped` to a new
  `OnCaptureStopped` handler in `StartOutputCoreAsync` (unsubscribe in `StopOutputCoreAsync`,
  **before** calling the sources' own `StopAsync()`). `OnCaptureStopped` calls
  `StopOutputAsync().FireAndForget()`. Capture `wasActive` at the top of `StopOutputCoreAsync` and
  OR it into the `statusChanged` condition. Raise `OutputStatusChanged` at the end of
  `StartOutputCoreAsync` and (when `hadHandles`) at the end of `StopReStreamCoreAsync`, and after
  starting the re-stream thread in `StartReStreamFromSourceCoreAsync`. See plan.md §1.6 for exact
  anchors.
- **Depends on**: T001, T002, T004, T005
- **Acceptance**: `INdiOutputBridge` fully implemented; `dotnet build` green. (No unit test —
  `NdiOutputBridge` P/Invokes `libndi.so`, unreachable from `tests/MauiApp.Tests`; validated in
  T016.)

### T007 — `OutputViewModel` corroboration
- **Layer**: Core ViewModel
- **Files**: `src/Core/Features/Output/ViewModels/OutputViewModel.cs`
- **Description**: Rewrite `OnAppResumed()` to only claim "Output session restored." when
  `_bridge.IsActive`; otherwise set `IsOutputActive = false`, `StatusMessage = "Tap Start to
  resume output"` (exact string, no period), and persist `IsOutputActive = false` via
  `_appStateRepo.SaveAsync(...)`. Extend `OnOutputStatusChanged` to set `IsOutputActive = false` /
  `StatusMessage = "Output stopped"` when `IsOutputActive && !_bridge.IsActive`. See plan.md §1.7
  for exact code.
- **Depends on**: T002
- **Acceptance**: Compiles; `dotnet build` green. Covered by T010.

### T008 — `HomeViewModel` corroboration
- **Layer**: Core ViewModel
- **Files**: `src/Core/Features/Home/ViewModels/HomeViewModel.cs`
- **Description**: Add `INdiOutputBridge outputBridge` as the 5th constructor parameter (before
  `dispatcher`); add `using NdiForAndroid.NdiBridge;`, field, subscription to
  `OutputStatusChanged` in the ctor, and unsubscription in `Dispose()`. Change
  `RefreshAsync()`'s `OutputStatus` condition to `state.IsOutputActive && _outputBridge.IsActive`.
  Add `OnOutputStatusChanged` re-running `RefreshCommand.Execute(null)` via the dispatcher. Leave
  `ResumeOutput()` unchanged. See plan.md §1.8.
- **Depends on**: T002
- **Acceptance**: Compiles; `dotnet build` green (no `MauiProgram.cs` change needed — verify DI
  still resolves `HomeViewModel` at runtime, e.g. by launching the app). Covered by T011.

### T009 — NEW `CaptureSourcesTests.cs`
- **Layer**: Tests
- **Files**: `tests/MauiApp.Tests/Services/CaptureSourcesTests.cs` (new)
- **Description**: Core-only contract tests for `CaptureStoppedEventArgs`/`CaptureStopReason`
  using a minimal private fake `IVideoCaptureSource` (see plan.md §1.9 / full listing below).
- **Depends on**: T001
- **Test list** (xUnit, Arrange/Act/Assert):
  1. `Stopped_WhenRaised_CarriesReasonAndMessage` — fake source raises `Stopped` with
     `(ProjectionStopped, "consent revoked")`; assert the subscriber receives both values.
  2. `CaptureStoppedEventArgs_MessageIsOptional` — construct with only `Reason`; assert
     `Message == null`.
  3. `AllReasons_ConstructEventArgsWithoutThrowing` — `[Theory]` over all four `CaptureStopReason`
     values; assert construction round-trips `Reason`.
- **Acceptance**: All 3 (6 counting the `[Theory]` cases) pass; `dotnet test` green.

### T010 — Extend `OutputViewModelTests.cs`
- **Layer**: Tests
- **Files**: `tests/MauiApp.Tests/Features/Output/OutputViewModelTests.cs`
- **Depends on**: T002, T007
- **Test list**:
  1. `OnAppResumed_WhenBridgeCorroboratesActive_ShowsRestoredMessage` — Arrange:
     `_appStateRepoMock.Setup(r => r.RestoreStateAsync()).ReturnsAsync(new
     AppStateSnapshot(null, "X", true, null));` `_bridgeMock.SetupGet(b =>
     b.IsActive).Returns(true);` `var sut = CreateSut();` Act:
     `_lifecycleMock.Raise(l => l.AppResumed += null);` Assert: `sut.IsOutputActive == true`;
     `sut.StatusMessage == "Output session restored."`; `sut.StreamName == "X"`.
  2. `OnAppResumed_WhenBridgeDoesNotCorroborate_ShowsTapStartAndClearsPersistedFlag` — Arrange:
     same snapshot; `_bridgeMock.SetupGet(b => b.IsActive).Returns(false);` `var sut =
     CreateSut();` Act: raise `AppResumed`. Assert: `sut.IsOutputActive == false`;
     `sut.StatusMessage == "Tap Start to resume output"`; `_appStateRepoMock.Verify(r =>
     r.SaveAsync(It.Is<AppStateSnapshot>(s => s.IsOutputActive == false && s.StreamName ==
     "X")), Times.Once);`
  3. `OutputStatusChanged_WhenBridgeReportsInactive_CorrectsIsOutputActiveAndStatusMessage` —
     Arrange: `var sut = CreateSut(); await sut.StartOutputCommand.ExecuteAsync(null);`
     `_bridgeMock.SetupGet(b => b.IsActive).Returns(false);` Act:
     `_bridgeMock.Raise(b => b.OutputStatusChanged += null, EventArgs.Empty);` Assert:
     `sut.IsOutputActive == false`; `sut.StatusMessage == "Output stopped"`.
  4. `OutputStatusChanged_WhenBridgeStillActive_DoesNotChangeIsOutputActiveOrStatusMessage` —
     Arrange: same as (3) but `_bridgeMock.SetupGet(b => b.IsActive).Returns(true);` Act: raise
     the event. Assert: `sut.IsOutputActive == true`; `sut.StatusMessage == "Output active"`
     (unchanged from `StartOutputCommand`).
- **Acceptance**: All 4 pass; existing tests in the file remain green (verified: no
  `MockBehavior.Strict`, adding `IsActive` doesn't affect unrelated tests since `OutputStatusChanged`
  is never auto-raised by Moq); `dotnet test` green.

### T011 — Extend `HomeViewModelTests.cs`
- **Layer**: Tests
- **Files**: `tests/MauiApp.Tests/Features/Home/HomeViewModelTests.cs`
- **Depends on**: T002, T008
- **Setup change**: add `private readonly Mock<NdiForAndroid.NdiBridge.INdiOutputBridge>
  _outputBridgeMock = new();`; update `CreateSut()` to pass `_outputBridgeMock.Object` as the 5th
  argument (before `_dispatcher`).
- **Test list**:
  1. `RefreshCommand_WhenAppStateActiveButBridgeNotActive_ShowsIdle` — Arrange:
     `_appStateRepoMock.Setup(r => r.RestoreStateAsync()).ReturnsAsync(new
     AppStateSnapshot(null, "X", true, null));` `_outputBridgeMock.SetupGet(b =>
     b.IsActive).Returns(false);` Act: `var sut = CreateSut();` (ctor runs
     `RefreshCommand.Execute(null)`). Assert: `sut.OutputStatus == "Idle (no active output)"`.
  2. `RefreshCommand_WhenAppStateActiveAndBridgeActive_ShowsActiveOutput` — Arrange: same snapshot;
     `_outputBridgeMock.SetupGet(b => b.IsActive).Returns(true);` Act: `var sut = CreateSut();`
     Assert: `sut.OutputStatus == "Active output to \"X\""`.
  3. `OutputStatusChanged_FromBridge_RefreshesOutputStatus` — Arrange: default idle mocks; `var sut
     = CreateSut();` assert idle first; then reconfigure `_appStateRepoMock`/`_outputBridgeMock` to
     the active combination from (2). Act: `_outputBridgeMock.Raise(b => b.OutputStatusChanged +=
     null, EventArgs.Empty);` Assert: `sut.OutputStatus == "Active output to \"X\""`.
  4. `Dispose_UnsubscribesFromOutputStatusChanged` — Arrange: `var sut = CreateSut();` (idle);
     `sut.Dispose();` then reconfigure mocks to the active combination (as in (3), so a live
     subscription *would* change the status). Act: raise `OutputStatusChanged`. Assert:
     `sut.OutputStatus == "Idle (no active output)"` (unchanged — proves unsubscription).
- **Acceptance**: All 4 pass; `dotnet test` green.

### T012 — Full green + docs
- **Layer**: Verification
- **Files**: `docs/features/output-session-state/{spec,plan,tasks}.md`
- **Description**: `dotnet build NdiForAndroid.sln` (0 errors) and `dotnet test
  tests/MauiApp.Tests` (all green, including the 3+4+4 new tests). Tick acceptance-criteria
  checkboxes in spec.md that Slice 1 satisfies.
- **Depends on**: T001–T011

---

## Detailed Tasks (Slice 2 — follow-up branch)

### T013 — Remove Stream branch of `NdiNavigationHandoffService`
- **Layer**: Core
- **Files**: `src/Core/Features/Navigation/Services/NdiNavigationHandoffService.cs`
- **Description**: Reduce the constructor to `(INdiViewerBridge viewerBridge)`; remove the
  `INdiOutputBridge`/`IAppStateRepository` fields and the `if (from ==
  PrimaryNavDestination.Stream) { ... }` block entirely; keep the `View` branch. Drop now-unused
  usings. See plan.md §2.1 for the full replacement class body.
- **Depends on**: Slice 1 merged (no hard code dependency, but do not start Slice 2 before it)
- **Acceptance**: `dotnet build` green; no other file references the removed ctor params (verified
  — `MauiProgram.cs` resolves `INavigationHandoffService` via DI with no explicit `new(...)` call
  sites elsewhere).

### T014 — Notification Stop action
- **Layer**: MauiApp/Android
- **Files**: `src/MauiApp/Platforms/Android/Services/ScreenShareForegroundService.cs`
- **Description**: Add `ActionStopRequested` action constant + `StopActionRequestCode`. In
  `OnStartCommand`, add a branch (before the existing `ActionStop` branch) that resolves
  `INdiOutputBridge` via `IPlatformApplication.Current?.Services.GetService<INdiOutputBridge>()`
  and calls `StopOutputAsync().FireAndForget()`, returning `NotSticky` without stopping the
  service directly. In `BuildNotification`, add a `NotificationCompat.Action` with a
  `PendingIntent.GetService(...)` targeting `ActionStopRequested`. See plan.md §2.3 for exact code
  and the icon-resource fallback note.
- **Depends on**: Slice 1 merged
- **Acceptance**: `dotnet build` green. No unit test (Android platform class, see Known
  Test-Coverage Gap) — validated in T016.

### T015 — Rewrite `NdiNavigationHandoffServiceTests.cs`
- **Layer**: Tests
- **Files**: `tests/MauiApp.Tests/Features/Navigation/NdiNavigationHandoffServiceTests.cs`
- **Depends on**: T013
- **Test list** (full replacement — see plan.md §2.2 for the exact file):
  1. `HandlePrimaryDestinationChangeAsync_LeavingView_StopsReceiver` — `(View → Home)`; verify
     `_viewerBridgeMock.Verify(b => b.StopReceiver(), Times.Once)`.
  2. `HandlePrimaryDestinationChangeAsync_LeavingStream_DoesNotStopReceiver` — `(Stream → Home)`;
     verify `StopReceiver()` `Times.Never`.
  3. `HandlePrimaryDestinationChangeAsync_SameDestination_IsNoOp` — `(View → View)`; verify
     `StopReceiver()` `Times.Never`.
- **Acceptance**: All 3 pass; `dotnet test` green.

### T016 — Device verification
- **Layer**: Manual/Device (use the `android-build-install-run` skill)
- **Depends on**: T014, T015
- **Checklist**:
  - [ ] Start output (screen capture); switch to View tab, then Home, then back to Stream tab —
    output keeps streaming throughout (no `NdiNavigationHandoffService` interruption).
  - [ ] With output active, press the Home button to background the app; confirm on a separate NDI
    receiving client that frames keep arriving and the persistent notification stays visible.
  - [ ] Foreground the app again; confirm `OutputViewModel.IsOutputActive`/`HomeViewModel.OutputStatus`
    still read "active" (no false "Tap Start to resume output").
  - [ ] Tap the notification's Stop action while backgrounded; confirm the sender stops (receiving
    client sees the source disappear) and the notification is dismissed.
  - [ ] Foreground the app after a notification-triggered stop; confirm `OutputViewModel`/
    `HomeViewModel` show the idle/stopped state (proves the `OutputStatusChanged`/`IsActive`
    correction path from Slice 1 covers the notification-stop case too).
  - [ ] Repeat the #326/#334 scenarios from Slice 1 on-device: revoke screen-capture permission
    from the system status bar while output is active — confirm `IsActive` flips to `false` and
    both ViewModels correct their status without requiring the user to tap Stop.
