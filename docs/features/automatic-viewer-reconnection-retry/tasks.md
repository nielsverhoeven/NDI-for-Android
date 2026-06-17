# Tasks: Automatic Viewer Reconnection with Retry Window

## Summary
- Total tasks: 17
- Parent feature issue: **#233** (`feat: implement automatic viewer reconnection with retry window`)
- Branch: `feature/233-automatic-viewer-reconnection-retry`
- Source plan: `docs/features/automatic-viewer-reconnection-retry/plan.md`
- Spec: `docs/features/automatic-viewer-reconnection-retry/spec.md`
- Architecture validation: `docs/features/automatic-viewer-reconnection-retry/architecture.md`
- Layers covered: NDI Bridge, Core Services, ViewModel, View (XAML), DI root, Tests, Docs

## Dependency Graph

```
T001 → T002
T002 → T003, T005
T004 → T005, T006
T005 → T006, T007
T007 → T008, T015
T008 → T009, T010, T013
T009 → T011, T012
T010 → T011
T011 → T014
T013 → T014
T008,T009,T010,T011,T012,T013,T014 → T016
T001..T016 → T017 (final)
```

Ready immediately (no deps): **T001**, **T004**.

## Task List

| T-ID | Issue | Title | Depends on | Layer |
|------|-------|-------|------------|-------|
| T001 | #243 | Add ConnectionState enum to NdiBridgeModels | none | NDI Bridge |
| T002 | #244 | Add GetConnectionState() to INdiViewerBridge | T001 (#243) | NDI Bridge |
| T003 | #245 | Implement stub GetConnectionState() in NdiViewerBridge | T002 (#244) | NDI Bridge |
| T004 | #246 | Add IMainThreadDispatcher abstraction + MAUI implementation + DI | none | Core/Platform/DI |
| T005 | #247 | Inject TimeProvider and IMainThreadDispatcher into ViewerViewModel ctor | T002 (#244), T004 (#246) | ViewModel |
| T006 | #248 | Register TimeProvider.System in MauiProgram and verify ctor resolution | T004 (#246), T005 (#247) | DI root |
| T007 | #249 | Add observable reconnect state/fields/constants; marshal timer callbacks | T005 (#247) | ViewModel |
| T008 | #250 | Drop detection + BeginReconnectWindow() (FR1/FR2) | T007 (#249) | ViewModel |
| T009 | #251 | 2s attempt loop RunAttempt() + CompleteReconnect() (FR3) | T008 (#250) | ViewModel |
| T010 | #252 | 1s countdown TickCountdown() + RetryStatusMessage (FR4) | T008 (#250) | ViewModel |
| T011 | #253 | Expiry FailReconnect() -> terminal state + CanReconnect (FR5) | T009 (#251), T010 (#252) | ViewModel |
| T012 | #254 | CancelRetryCommand + DisposeTimers() (FR6) | T009 (#251) | ViewModel |
| T013 | #255 | Update Stop()/Start() for _userInitiatedStop/_lastSourceId (FR8) | T008 (#250) | ViewModel |
| T014 | #256 | ReconnectCommand (FR7) | T011 (#253), T013 (#255) | ViewModel |
| T015 | #257 | Add XAML bindings in ViewerPage.xaml | T007 (#249) | View (XAML) |
| T016 | #258 | Unit tests with fake TimeProvider + fake IMainThreadDispatcher | T008–T014 (#250–#256) | Tests |
| T017 | #259 | Run full dotnet test green; update plan checkboxes | T001–T016 (#243–#258) | Docs/Verification |

## Detailed Tasks

### T001 — #243: Add ConnectionState enum to NdiBridgeModels
- **Layer**: NDI Bridge
- **Files**: `src/Core/NdiBridge/NdiBridgeModels.cs`
- **Description**: Add a plain C# `ConnectionState` enum (Connected/Disconnected/etc.) to report viewer bridge connection status without leaking NDI types.
- **Depends on**: none
- **Acceptance**: Enum compiles; no NDI types cross the boundary; `dotnet build` green.

### T002 — #244: Add GetConnectionState() to INdiViewerBridge
- **Layer**: NDI Bridge
- **Files**: `src/Core/NdiBridge/INdiBridges.cs`
- **Description**: Extend `INdiViewerBridge` with `ConnectionState GetConnectionState()`, consistent with existing `Get*()` getters.
- **Depends on**: T001 (#243)
- **Acceptance**: Interface compiles; `ConnectionState` referenced; `dotnet build` green.

### T003 — #245: Implement stub GetConnectionState() in NdiViewerBridge
- **Layer**: NDI Bridge
- **Files**: NdiViewerBridge implementation file
- **Description**: Provide a stub implementation of `GetConnectionState()` so the implementation satisfies the interface.
- **Depends on**: T002 (#244)
- **Acceptance**: NdiViewerBridge compiles against updated interface; `dotnet build` green.

### T004 — #246: Add IMainThreadDispatcher abstraction + MAUI implementation + DI
- **Layer**: Core Services / Platform / DI
- **Files**: `src/Core/Services/IMainThreadDispatcher.cs`, `src/MauiApp/Services/MauiMainThreadDispatcher.cs`, `src/MauiApp/MauiProgram.cs`
- **Description**: Add `IMainThreadDispatcher` (Core), `MauiMainThreadDispatcher` wrapping `MainThread.BeginInvokeOnMainThread` (MauiApp), and register in `MauiProgram.cs`. Core cannot reference MAUI.
- **Depends on**: none
- **Acceptance**: Abstraction registered in DI and resolves at runtime; `dotnet build` green.

### T005 — #247: Inject TimeProvider and IMainThreadDispatcher into ViewerViewModel ctor
- **Layer**: ViewModel
- **Files**: `ViewerViewModel.cs`, `ViewerViewModelTests.cs`
- **Description**: Add constructor params for `TimeProvider` and `IMainThreadDispatcher`, add backing fields, update existing tests to pass fakes.
- **Depends on**: T002 (#244), T004 (#246)
- **Acceptance**: Ctor accepts both deps; existing tests updated with fakes; `dotnet test` green.

### T006 — #248: Register TimeProvider.System in MauiProgram and verify ctor resolution
- **Layer**: DI root
- **Files**: `src/MauiApp/MauiProgram.cs`
- **Description**: Register `TimeProvider.System` and confirm both `ViewerViewModel` ctor params resolve at runtime.
- **Depends on**: T004 (#246), T005 (#247)
- **Acceptance**: `TimeProvider.System` registered; `ViewerViewModel` resolves; `dotnet build` green.

### T007 — #249: Add observable reconnect state/fields/constants; marshal timer callbacks
- **Layer**: ViewModel
- **Files**: `ViewerViewModel.cs`
- **Description**: Add observable state (`IsReconnecting`, `RetryStatusMessage`, etc.), private fields and timing constants; route all timer callbacks through `_mainThread.Invoke(...)`.
- **Depends on**: T005 (#247)
- **Acceptance**: Observable props notify; timer callbacks marshaled via `IMainThreadDispatcher`; `dotnet build` green.

### T008 — #250: Drop detection + BeginReconnectWindow() (FR1/FR2)
- **Layer**: ViewModel
- **Files**: `ViewerViewModel.cs`
- **Description**: Detect unexpected drop while `IsPlaying` via `GetConnectionState()==Disconnected` and enter the 15s reconnect window via `BeginReconnectWindow()`.
- **Depends on**: T007 (#249)
- **Acceptance**: Drop while playing enters reconnecting state; FR1/FR2 honored; `dotnet build` green.

### T009 — #251: 2s attempt loop RunAttempt() + CompleteReconnect() (FR3)
- **Layer**: ViewModel
- **Files**: `ViewerViewModel.cs`
- **Description**: Implement ~2s attempt loop (`RunAttempt`) performing `StopReceiver()`→`StartReceiver(SourceId)`; first Connected result calls `CompleteReconnect()` resuming playback.
- **Depends on**: T008 (#250)
- **Acceptance**: Loop runs ~7 attempts; first Connected resumes playback; FR3 honored; `dotnet test` green.

### T010 — #252: 1s countdown TickCountdown() + RetryStatusMessage (FR4)
- **Layer**: ViewModel
- **Files**: `ViewerViewModel.cs`
- **Description**: Implement per-second countdown via injected `TimeProvider` updating `RetryStatusMessage` to `"Reconnecting... {n}s remaining"`.
- **Depends on**: T008 (#250)
- **Acceptance**: Countdown decrements each second; message format correct; FR4 honored; `dotnet test` green.

### T011 — #253: Expiry FailReconnect() -> terminal state + CanReconnect (FR5)
- **Layer**: ViewModel
- **Files**: `ViewerViewModel.cs`
- **Description**: On window expiry with no Connected result, transition to terminal stopped/error state (`StatusMessage = "Connection lost. Reconnection failed."`, `IsPlaying=false`, `IsReconnecting=false`) and enable `CanReconnect`.
- **Depends on**: T009 (#251), T010 (#252)
- **Acceptance**: Expiry sets error state; `CanReconnect` true; FR5 honored; `dotnet test` green.

### T012 — #254: CancelRetryCommand + DisposeTimers() (FR6)
- **Layer**: ViewModel
- **Files**: `ViewerViewModel.cs`
- **Description**: Add `CancelRetry` command that aborts the window, stops the receiver, transitions to stopped state without re-triggering retry; `DisposeTimers()` disposes active timers.
- **Depends on**: T009 (#251)
- **Acceptance**: Cancel aborts window and disposes timers; no overlapping loops; FR6 honored; `dotnet test` green.

### T013 — #255: Update Stop()/Start() for _userInitiatedStop/_lastSourceId (FR8)
- **Layer**: ViewModel
- **Files**: `ViewerViewModel.cs`
- **Description**: Update `Stop()`/`Start()` to set `_userInitiatedStop` flag and record `_lastSourceId` so the drop handler distinguishes intentional stop from unexpected drop.
- **Depends on**: T008 (#250)
- **Acceptance**: User Stop does not trigger retry; `_lastSourceId` tracked; FR8 honored; `dotnet test` green.

### T014 — #256: ReconnectCommand (FR7)
- **Layer**: ViewModel
- **Files**: `ViewerViewModel.cs`
- **Description**: Add `Reconnect` command available in stopped/error state that restarts the full reconnect flow using the last `SourceId`.
- **Depends on**: T011 (#253), T013 (#255)
- **Acceptance**: Reconnect from error state restarts flow with last SourceId; FR7 honored; `dotnet test` green.

### T015 — #257: Add XAML bindings in ViewerPage.xaml
- **Layer**: View (XAML)
- **Files**: `ViewerPage.xaml`
- **Description**: Add bindings for reconnect state (`RetryStatusMessage`, `IsReconnecting`, Cancel/Reconnect commands); code-behind stays a pure `BindingContext` shim.
- **Depends on**: T007 (#249)
- **Acceptance**: Bindings present; no business logic in view; `dotnet build` green.

### T016 — #258: Unit tests with fake TimeProvider + fake IMainThreadDispatcher
- **Layer**: Tests
- **Files**: `tests/MauiApp.Tests/Features/Viewer/ViewerViewModelTests.cs`
- **Description**: Add xUnit + Moq tests covering drop/reconnect/countdown/expiry/cancel/stop/reconnect using a fake `TimeProvider` and a synchronous fake `IMainThreadDispatcher` (no hardware, no real delays).
- **Depends on**: T008–T014 (#250–#256)
- **Acceptance**: All AC-mapped tests present and deterministic; `dotnet test` green.

### T017 — #259: Run full dotnet test green; update plan checkboxes
- **Layer**: Docs / Verification
- **Files**: `docs/features/automatic-viewer-reconnection-retry/plan.md`, `tasks.md`
- **Description**: Run the full `dotnet test` suite to green and tick the plan/task checkboxes.
- **Depends on**: T001–T016 (#243–#258)
- **Acceptance**: `dotnet test` fully green; checkboxes updated.
