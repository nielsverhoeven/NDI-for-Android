# Tasks: PTZ Control over VISCA-over-IP (TCP) for NDI Sources

## Summary
- Parent feature issue: **#339**
- Branch: `feature/339-visca-ptz-endpoint`
- Source plan: `docs/features/ptz-visca-endpoint/plan.md`
- Spec: `docs/features/ptz-visca-endpoint/spec.md`
- Layers covered: Core (Models/Services/ViewModels), Data (SQLite migration), DI root, ViewModel,
  View (XAML), Tests, Docs
- **Shared-file tasks** (touch `src/Core/Features/Viewer/ViewModels/ViewerViewModel.cs` and/or
  `src/MauiApp/Features/Viewer/Views/ViewerView.xaml(.cs)` — integrated last by the parallel-feature
  integrator, keep isolated): **T18, T19, T20, T24** — flagged below.

## Dependency Graph

```
T1  → T2, T5, T7, T9, T11
T2  → T6
T5  → T6
T6  → T16
T7  → T8
T9  → T10
T11 → T12, T14
T3  → T4                       (independent DB-model track)
T12 → T13
T14 → T15
T6,T12,T14 → T16
T16 → T17
T1,T6 → T18 (PtzEndpointFormViewModel)
T18 → T19 (tests)
T3,T4,T16,T18 → T20 (DI root)
T20 → T21 (ViewerViewModel ctor + wiring — SHARED FILE)
T21 → T22 (ViewerViewModel behavior — SHARED FILE)
T22 → T23 (ViewerViewModelTests rework/extend)
T22 → T24 (ViewerView.xaml — SHARED FILE)
T21,T22,T24 → T25 (docs)
T1..T24 → T26 (build/test green, final)
```

Ready immediately (no deps): **T1**, **T3**.

## Task List

| T-ID | Title | Depends on | Layer |
|------|-------|------------|-------|
| T1 | Add `PtzModels.cs` (PtzConnectionStatus, PtzEndpoint, PtzEndpointSavedEventArgs) | none | Core |
| T2 | Add `IViscaTransport` + `IViscaTransportFactory` interfaces | T1 | Core |
| T3 | Add `NdiSource.PtzOverrideHost`/`PtzOverridePort` (SourceModels.cs) | none | Core |
| T4 | `NdiDatabase` migration: SourceEntity columns, `EnsureSourceColumnsAsync`, `UpsertSourceAsync`, `GetSourcesAsync` | T3 | Core (Data) |
| T5 | Implement `ViscaTcpTransport` + `ViscaTransportFactory` | T2 | Core |
| T6 | Implement `IViscaTransportFactory` DI-ready (verify T5 compiles standalone) | T5 | Core |
| T7 | Implement `ViscaCommandEncoder` (pan/tilt, zoom, autofocus, preset store/recall) | T1 | Core |
| T8 | `ViscaCommandEncoderTests` (exact byte-array assertions) | T7 | Tests |
| T9 | Implement `ViscaResponseParser` | T1 | Core |
| T10 | `ViscaResponseParserTests` (Ack/Completion/Error/Unknown) | T9 | Tests |
| T11 | Add `IPtzController` interface | T1 | Core |
| T12 | Implement `NdiPtzController` (wraps `INdiViewerBridge`) | T11 | Core |
| T13 | `NdiPtzControllerTests` | T12 | Tests |
| T14 | Implement `ViscaPtzController` (connect/reconnect/timeout state machine) | T5, T7, T9, T11 | Core |
| T15 | `ViscaPtzControllerTests` (FakeViscaTransport double) | T14 | Tests |
| T16 | Add `IPtzControllerFactory` + `PtzControllerFactory` | T6, T12, T14 | Core |
| T17 | `PtzControllerFactoryTests` | T16 | Tests |
| T18 | Implement `PtzEndpointFormViewModel` (Open/Save/Cancel/Test) | T1, T6 | Core |
| T19 | `PtzEndpointFormViewModelTests` | T18 | Tests |
| T20 | DI registrations in `MauiProgram.cs` (`IViscaTransportFactory`, `IPtzControllerFactory`, `PtzEndpointFormViewModel`) | T3, T4, T16, T18 | MauiApp (DI root) |
| T21 | **[SHARED FILE]** `ViewerViewModel.cs`: new ctor params (`IPtzControllerFactory`, `PtzEndpointFormViewModel`), new fields/observable properties/computed property + change hooks | T20 | Core (ViewModel) |
| T22 | **[SHARED FILE]** `ViewerViewModel.cs`: `Start()`/`Stop()` PTZ-controller lifecycle, `AttachPtzController`/status helpers, rewritten `PtzNudge`/`PtzZoomNudge`/`PtzAutoFocus`, new preset + open-editor commands, `OnPtzEndpointSaveRequested`, `Dispose()` cleanup | T21 | Core (ViewModel) |
| T23 | `ViewerViewModelTests.cs`: update `CreateSut()`, rework 2 existing PTZ tests, add 4 new tests (§5 of plan.md) | T22 | Tests |
| T24 | **[SHARED FILE]** `ViewerView.xaml`: header row + gated pad/zoom/preset section + endpoint modal overlay | T22 | MauiApp (View, XAML) |
| T25 | Update `docs/ndi-sdk-coverage.md` (PTZ row note) + `docs/architecture.md` (module map) | T21, T22, T24 | Docs |
| T26 | `dotnet build NdiForAndroid.sln` + `dotnet test tests/MauiApp.Tests` green; tick plan/task checkboxes | all | Docs/Verification |

## Detailed Tasks

### T1 — Add `PtzModels.cs`
- **Layer**: Core · **Files**: `src/Core/Features/Ptz/Models/PtzModels.cs` (new)
- `PtzConnectionStatus` enum, `PtzEndpoint` record (+ `DefaultViscaPort = 5678`), `PtzEndpointSavedEventArgs` record. See plan.md §2.1.
- **Depends on**: none · **Acceptance**: compiles; `dotnet build` green.

### T2 — Add `IViscaTransport` + `IViscaTransportFactory`
- **Layer**: Core · **Files**: `src/Core/Features/Ptz/Services/IViscaTransport.cs` (new)
- Plan.md §2.2. **Depends on**: T1 · **Acceptance**: compiles.

### T3 — `NdiSource` PTZ override columns
- **Layer**: Core · **Files**: `src/Core/Features/Sources/Models/SourceModels.cs`
- Two trailing nullable params, additive (plan.md §3). **Depends on**: none
- **Acceptance**: existing positional call sites still compile (defaults apply); `dotnet build` green.

### T4 — `NdiDatabase` migration
- **Layer**: Core (Data) · **Files**: `src/Core/Data/NdiDatabase.cs`
- `SourceEntity` columns, `EnsureSourceColumnsAsync` ALTER TABLE statements, `UpsertSourceAsync`/`GetSourcesAsync` mapping (plan.md §3).
- **Depends on**: T3 · **Acceptance**: `NdiDatabaseSchemaTests.InitAsync_CreatesEveryTable_WithoutThrowing` still passes; new round-trip test (T4b, folded into T4) passes against a temp SQLite file.

### T5 — Implement `ViscaTcpTransport`
- **Layer**: Core · **Files**: `src/Core/Features/Ptz/Services/ViscaTcpTransport.cs` (new)
- Raw VISCA/TCP, byte-at-a-time frame read until `0xFF` (plan.md §2.3). **Depends on**: T2
- **Acceptance**: compiles; not unit-tested against a real socket (per plan.md §5 rationale).

### T6 — `ViscaTransportFactory` DI-readiness check
- **Layer**: Core · **Files**: `src/Core/Features/Ptz/Services/ViscaTcpTransport.cs` (same file, `ViscaTransportFactory` class)
- Confirms `ViscaTransportFactory : IViscaTransportFactory` is registrable/mockable downstream.
- **Depends on**: T5 · **Acceptance**: `dotnet build` green.

### T7 — Implement `ViscaCommandEncoder`
- **Layer**: Core · **Files**: `src/Core/Features/Ptz/Services/ViscaCommandEncoder.cs` (new)
- Pan/tilt drive, zoom speed, auto-focus, preset store/recall — exact byte layouts in plan.md §2.4.
- **Depends on**: T1 · **Acceptance**: compiles.

### T8 — `ViscaCommandEncoderTests`
- **Layer**: Tests · **Files**: `tests/MauiApp.Tests/Features/Ptz/ViscaCommandEncoderTests.cs` (new)
- Exact byte-array cases from plan.md §5. **Depends on**: T7 · **Acceptance**: `dotnet test` green.

### T9 — Implement `ViscaResponseParser`
- **Layer**: Core · **Files**: `src/Core/Features/Ptz/Services/ViscaResponseParser.cs` (new)
- Ack/Completion/Error/Unknown parsing (plan.md §2.5). **Depends on**: T1 · **Acceptance**: compiles.

### T10 — `ViscaResponseParserTests`
- **Layer**: Tests · **Files**: `tests/MauiApp.Tests/Features/Ptz/ViscaResponseParserTests.cs` (new)
- **Depends on**: T9 · **Acceptance**: `dotnet test` green.

### T11 — Add `IPtzController`
- **Layer**: Core · **Files**: `src/Core/Features/Ptz/Services/IPtzController.cs` (new)
- Plan.md §2.6. **Depends on**: T1 · **Acceptance**: compiles.

### T12 — Implement `NdiPtzController`
- **Layer**: Core · **Files**: `src/Core/Features/Ptz/Services/NdiPtzController.cs` (new)
- Thin wrapper over `INdiViewerBridge`, no NDI PTZ behavior change (plan.md §2.7).
- **Depends on**: T11 · **Acceptance**: compiles.

### T13 — `NdiPtzControllerTests`
- **Layer**: Tests · **Files**: `tests/MauiApp.Tests/Features/Ptz/NdiPtzControllerTests.cs` (new)
- **Depends on**: T12 · **Acceptance**: `dotnet test` green.

### T14 — Implement `ViscaPtzController`
- **Layer**: Core · **Files**: `src/Core/Features/Ptz/Services/ViscaPtzController.cs` (new)
- Persistent-connection state machine, lazy connect, reconnect-on-failure, ACK/Completion/Error handling, 3s connect / 2s command timeouts (plan.md §2.8).
- **Depends on**: T5, T7, T9, T11 · **Acceptance**: compiles.

### T15 — `ViscaPtzControllerTests`
- **Layer**: Tests · **Files**: `tests/MauiApp.Tests/Features/Ptz/ViscaPtzControllerTests.cs` (new)
- Hand-rolled `FakeViscaTransport` double; cases in plan.md §5. **Depends on**: T14
- **Acceptance**: `dotnet test` green, including reconnect-after-failure and status-transition cases.

### T16 — Add `IPtzControllerFactory` + `PtzControllerFactory`
- **Layer**: Core · **Files**: `src/Core/Features/Ptz/Services/IPtzControllerFactory.cs`, `PtzControllerFactory.cs` (new)
- Selects NDI vs VISCA per `NdiSource` (plan.md §2.9). **Depends on**: T6, T12, T14
- **Acceptance**: compiles.

### T17 — `PtzControllerFactoryTests`
- **Layer**: Tests · **Files**: `tests/MauiApp.Tests/Features/Ptz/PtzControllerFactoryTests.cs` (new)
- **Depends on**: T16 · **Acceptance**: `dotnet test` green.

### T18 — Implement `PtzEndpointFormViewModel`
- **Layer**: Core · **Files**: `src/Core/Features/Ptz/ViewModels/PtzEndpointFormViewModel.cs` (new)
- Open/Save/Cancel/Test commands, validation, `SaveRequested` event (plan.md §2.10).
- **Depends on**: T1, T6 · **Acceptance**: compiles.

### T19 — `PtzEndpointFormViewModelTests`
- **Layer**: Tests · **Files**: `tests/MauiApp.Tests/Features/Ptz/PtzEndpointFormViewModelTests.cs` (new)
- **Depends on**: T18 · **Acceptance**: `dotnet test` green.

### T20 — DI registrations
- **Layer**: MauiApp (DI root) · **Files**: `src/MauiApp/MauiProgram.cs`
- Register `IViscaTransportFactory`, `IPtzControllerFactory`, `PtzEndpointFormViewModel` (plan.md §4.1).
- **Depends on**: T3, T4, T16, T18 · **Acceptance**: app resolves all new types at startup; `dotnet build` green.

### T21 — **[SHARED FILE]** `ViewerViewModel.cs` constructor/fields/properties
- **Layer**: Core (ViewModel) · **Files**: `src/Core/Features/Viewer/ViewModels/ViewerViewModel.cs`
- Two new trailing ctor params, 3 new fields, `PtzEndpointForm` property, 3 new observable properties, `IsPtzControlActive` + 2 partial change hooks, constructor-body wiring (plan.md §4.2).
- **Depends on**: T20 · **Acceptance**: `dotnet build` green (existing `ViewerViewModelTests` call sites will not yet compile until T23 — acceptable intermediate state within this task sequence, do not merge without T23).
- **Isolation note**: confine to the exact additions in plan.md §4.2 — no other existing member signatures change here.

### T22 — **[SHARED FILE]** `ViewerViewModel.cs` behavior wiring
- **Layer**: Core (ViewModel) · **Files**: `src/Core/Features/Viewer/ViewModels/ViewerViewModel.cs`
- `Start()`/`Stop()` PTZ lifecycle, `AttachPtzController`/`OnPtzConnectionStatusChanged`/`DescribePtzStatus` helpers, rewritten `PtzNudge`/`PtzZoomNudge`/`PtzAutoFocus` bodies, new `PtzStorePreset`/`PtzRecallPreset`/`OpenPtzEndpointEditor` commands, `OnPtzEndpointSaveRequested` handler, `Dispose()` cleanup (plan.md §4.3).
- **Depends on**: T21 · **Acceptance**: `dotnet build` green.
- **Isolation note**: only the 3 named existing methods (`Start`, `Stop`, `PtzNudge`/`PtzZoomNudge`/`PtzAutoFocus`) have their bodies edited; no signature changes besides `PtzAutoFocus` becoming `async Task`.

### T23 — Extend `ViewerViewModelTests.cs`
- **Layer**: Tests · **Files**: `tests/MauiApp.Tests/Features/Viewer/ViewerViewModelTests.cs`
- Update `CreateSut()` with the two new mocks/args; rework `PtzNudge_BurstsThenStops` / `PtzZoomNudge_In_BurstsThenStops`; add the 4 new tests listed in plan.md §5.
- **Depends on**: T22 · **Acceptance**: `dotnet test tests/MauiApp.Tests` green, no regressions.

### T24 — **[SHARED FILE]** `ViewerView.xaml` PTZ panel
- **Layer**: MauiApp (View, XAML) · **Files**: `src/MauiApp/Features/Viewer/Views/ViewerView.xaml`
- Replace lines 49-69 per plan.md §4.4; add the endpoint-edit modal as the last child of the root `Grid`. No code-behind changes.
- **Depends on**: T22 · **Acceptance**: `dotnet build` green; visually verify via `android-build-install-run` skill once merged with the parallel feature's integration pass (not blocking this task in isolation).
- **Isolation note**: XAML-only; `ViewerView.xaml.cs` untouched.

### T25 — Documentation updates
- **Layer**: Docs · **Files**: `docs/ndi-sdk-coverage.md`, `docs/architecture.md`
- One-sentence PTZ-row note + `IPtzController` module-map addition (plan.md §6).
- **Depends on**: T21, T22, T24 · **Acceptance**: docs reviewed for accuracy against the merged code.

### T26 — Full verification
- **Layer**: Docs/Verification · **Files**: none (process task)
- Run `dotnet build NdiForAndroid.sln` and `dotnet test tests/MauiApp.Tests` to green; tick spec.md/plan.md checkboxes.
- **Depends on**: T1–T25 · **Acceptance**: both commands succeed with zero failures.
- **Note**: `tools/ViscaMockCamera/` (a standalone raw-VISCA-over-TCP camera emulator, currently
  untracked in this worktree — see plan.md §5) already exists and byte-matches this plan's
  protocol; commit it as part of this feature's PR and optionally use it for a manual smoke check
  (`dotnet run --project tools/ViscaMockCamera -- --port 5678 --verbose`, then configure the
  Viewer's PTZ endpoint to `127.0.0.1:5678` via `adb reverse tcp:5678 tcp:5678`) before opening
  the PR. Not required for `dotnet test` to pass.
