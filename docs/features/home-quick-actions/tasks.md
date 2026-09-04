# Tasks: Home Quick Actions (Issue #328)

## Summary
- Total tasks: 9
- Layers covered: ViewModel, View/XAML, Unit tests, Documentation, Issue update
- GitHub issue: #328 (parent). No child task issues created — this is a small, single-PR change;
  `tasks.md` here is the local execution checklist for `dotnet-developer` + `tester`.
- Related (not implemented by this feature): #326 (`INdiOutputBridge.IsActive`), #327 (background
  streaming) — see `plan.md` "Interim rule for the #326 dependency".

## Dependency Graph
```
T001 → T002, T003
T002 → T004
T003 → T004
T005 → T007
T006 → T005, T008
T004, T007, T008 → T009 (docs)
T009 → T010 (issue/PR close)
```

## Task List

### T001: Add gating fields to HomeViewModel and populate them in RefreshAsync
- **Layer**: ViewModel
- **File**: `src/Core/Features/Home/ViewModels/HomeViewModel.cs`
- **Description**: Add `using NdiForAndroid.Features.Navigation.Models;`. Add four
  `[ObservableProperty]` fields — `_lastViewerSourceId` (string?), `_hasLastViewerSource` (bool,
  with `[NotifyCanExecuteChangedFor(nameof(StartViewingLastSourceCommand))]`),
  `_lastOutputStreamName` (string?), `_canResumeOutput` (bool, with
  `[NotifyCanExecuteChangedFor(nameof(ResumeOutputCommand))]`). In `RefreshAsync`, inside the
  existing `_dispatcher.BeginInvokeOnMainThread(...)` block, set all four from the restored
  `AppStateSnapshot` per `plan.md` section 1.
- **Depends on**: none
- **Acceptance**: after `RefreshCommand` runs with a mocked snapshot where
  `LastViewerSourceId = "src-1"`, `sut.HasLastViewerSource == true` and
  `sut.LastViewerSourceId == "src-1"`; with `AppStateSnapshot.Empty`, both are false/null.

### T002: Fix StartViewingLastSourceCommand navigation and gating
- **Layer**: ViewModel
- **File**: `src/Core/Features/Home/ViewModels/HomeViewModel.cs`
- **Description**: Replace the `StartViewingLastSource` method body exactly as shown in `plan.md`
  section 1 — add `[RelayCommand(CanExecute = nameof(HasLastViewerSource))]`, navigate via
  `_navigationService.NavigateToAsync($"viewer?sourceId={Uri.EscapeDataString(LastViewerSourceId)}")`,
  remove the old `view-tab?sourceId=` route and the old `RestoreStateAsync()` call.
- **Depends on**: T001
- **Acceptance**: with a mocked snapshot `LastViewerSourceId = "src-1"`, executing the command
  calls `INavigationService.NavigateToAsync("viewer?sourceId=src-1")` exactly once; with no last
  viewer source, `sut.StartViewingLastSourceCommand.CanExecute(null) == false` and executing it
  (forced) makes zero navigation calls.

### T003: Fix ResumeOutputCommand navigation and gating
- **Layer**: ViewModel
- **File**: `src/Core/Features/Home/ViewModels/HomeViewModel.cs`
- **Description**: Replace the `ResumeOutput` method body exactly as shown in `plan.md` section 1
  — add `[RelayCommand(CanExecute = nameof(CanResumeOutput))]`, navigate via
  `_navigationService.NavigateToPrimaryAsync(PrimaryNavDestination.Stream, "resume=true")`, remove
  the old `stream-tab?streamName=` route and the old `RestoreStateAsync()` call.
- **Depends on**: T001
- **Acceptance**: with a mocked snapshot `IsOutputActive = true, StreamName = "MyStream"`,
  executing the command calls
  `INavigationService.NavigateToPrimaryAsync(PrimaryNavDestination.Stream, "resume=true")` exactly
  once; with `IsOutputActive = false` (or empty `StreamName`),
  `sut.ResumeOutputCommand.CanExecute(null) == false`.

### T004: Bind IsEnabled on both quick-action buttons in HomePage.xaml
- **Layer**: View/XAML
- **File**: `src/MauiApp/Features/Home/Views/HomePage.xaml`
- **Description**: Add `IsEnabled="{Binding HasLastViewerSource}"` to the "Start Viewing Last
  Source" `Button` and `IsEnabled="{Binding CanResumeOutput}"` to the "Resume Output" `Button`, per
  `plan.md` section 2. No color/DynamicResource changes needed.
- **Depends on**: T002, T003
- **Acceptance**: `dotnet build NdiForAndroid.sln` succeeds (XAML compiles); manual/device check
  via the `android-build-install-run` skill shows both buttons visibly disabled on first app launch
  (no persisted state) and enabled after visiting Viewer/Output at least once.

### T005: Wire ResumeRequested query property into OutputPage
- **Layer**: View
- **File**: `src/MauiApp/Features/Output/Views/OutputPage.xaml.cs`
- **Description**: Add `[QueryProperty(nameof(ResumeRequested), "resume")]` and a
  `public string? ResumeRequested { get; set; }` property. In `OnAppearing`, after the existing
  `LoadCommand.Execute(null)` call, keep the existing `ReStreamSourceId` branch and add
  `else if (bool.TryParse(ResumeRequested, out var resume) && resume) _viewModel.ApplyResumeRequestCommand.Execute(null);`
  per `plan.md` section 3.
- **Depends on**: T006
- **Acceptance**: navigating to `//stream-tab?resume=true` (or via
  `NavigateToPrimaryAsync(PrimaryNavDestination.Stream, "resume=true")`) calls
  `ApplyResumeRequestCommand` once; navigating with `reStreamSourceId` set still calls
  `ApplyReStreamRequest` and does not also call `ApplyResumeRequestCommand`.

### T006: Add ApplyResumeRequestAsync command to OutputViewModel
- **Layer**: ViewModel
- **File**: `src/Core/Features/Output/ViewModels/OutputViewModel.cs`
- **Description**: Add the `[RelayCommand] private async Task ApplyResumeRequestAsync()` method
  exactly as shown in `plan.md` section 4 — reads `_appStateRepo.RestoreStateAsync()`, and when
  `IsOutputActive && StreamName` non-empty, sets `StreamName` and
  `StatusMessage = "Tap Start to resume output."`. Must **not** set `IsOutputActive = true` and
  must **not** call `_bridge.StartOutputAsync`.
- **Depends on**: none
- **Acceptance**: with a mocked snapshot `IsOutputActive = true, StreamName = "MyStream"`, after
  `await sut.ApplyResumeRequestCommand.ExecuteAsync(null)`: `sut.StreamName == "MyStream"`,
  `sut.StatusMessage == "Tap Start to resume output."`, `sut.IsOutputActive == false`, and
  `_bridgeMock.Verify(b => b.StartOutputAsync(It.IsAny<string>(), It.IsAny<VideoInputKind>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never)`.

### T007: Add HomeViewModelTests coverage for both quick actions
- **Layer**: Unit tests
- **File**: `tests/MauiApp.Tests/Features/Home/HomeViewModelTests.cs`
- **Description**: Add test methods covering the acceptance criteria of T002 and T003 (four cases:
  navigate-when-present / disabled-when-absent, for each command). Use the existing
  `_appStateRepoMock`, `_navigationServiceMock` fixtures already in the file; set up
  `_appStateRepoMock.Setup(r => r.RestoreStateAsync()).ReturnsAsync(new AppStateSnapshot(...))`
  (constructor order: `lastViewerSourceId, streamName, isOutputActive, lastSelectedSourceId`)
  **before** calling `CreateSut()` — matching `Constructed_WithCachedSources_...` above it —
  because `FakeMainThreadDispatcher` runs synchronously, so the gating fields are already populated
  by the constructor's own `RefreshCommand.Execute(null)` call and no extra refresh is needed.
- **Depends on**: T002, T003
- **Acceptance**: `dotnet test tests/MauiApp.Tests --filter FullyQualifiedName~HomeViewModelTests`
  passes, including the four new cases.

### T008: Add OutputViewModelTests coverage for ApplyResumeRequestAsync
- **Layer**: Unit tests
- **File**: `tests/MauiApp.Tests/Features/Output/OutputViewModelTests.cs`
- **Description**: Add the test described in T006's acceptance criteria, following the existing
  `_appStateRepoMock`/`_bridgeMock` fixture style already in the file (see constructor around line
  20-40).
- **Depends on**: T006
- **Acceptance**: `dotnet test tests/MauiApp.Tests --filter FullyQualifiedName~OutputViewModelTests`
  passes, including the new case.

### T009: Documentation pass (documenter stage)
- **Layer**: Documentation
- **Description**: Add a short "Home Quick Actions (Issue #328)" entry to
  `.github/KNOWLEDGE-BASE.md` once merged (route choices + the #326 interim rule), matching the
  style of existing entries (e.g. "Automatic Viewer Reconnection" section). Update
  `docs/features/home-quick-actions/spec.md` "Open Questions" only if anything changed during
  implementation review.
- **Depends on**: T004, T007, T008
- **Acceptance**: KNOWLEDGE-BASE.md entry exists and cross-references #326/#327.

### T010: Close the loop on GitHub
- **Layer**: Issue update
- **Description**: Open a PR from `feature/328-home-quick-actions`. Sibling issue #326's scheduling
  note targets its PR at `integration/watch-and-discovery` (both branches exist in this repo,
  layered on `bugfix/336-device-test-review-fixes`) — use the same base unless the user says
  otherwise. PR body includes `Closes #328`. Run `github-action-manager` to confirm CI is green
  before merge.
- **Depends on**: T009
- **Acceptance**: PR merged, issue #328 closed by the merge commit.
