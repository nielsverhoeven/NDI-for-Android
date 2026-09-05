# Tasks: Full-Screen Mode for the NDI Viewer

## Status

**Not started.** Ready for `feature.breakdown` to create the child issues below under parent
issue #338. Source plan: `docs/features/viewer-fullscreen/plan.md`. Spec:
`docs/features/viewer-fullscreen/spec.md`.

## Summary

- Total tasks: 13 (T1 is a review gate, not code; T2–T13 are ordered implementation/verification/
  docs tasks)
- Parent feature issue: **#338** (`Add full-screen mode for the NDI viewer`)
- Branch: `feature/338-viewer-fullscreen`
- Layers covered: Docs (gate), Core, Android, MauiApp, Tests, Docs (release)
- **Shared/parallel-feature files** — `src/Core/Features/Viewer/ViewModels/ViewerViewModel.cs`
  and `src/MauiApp/Features/Viewer/Views/ViewerView.xaml(.cs)` are touched by another
  in-flight feature branch too. T6 and T9 (the tasks that touch them) are placed **last** among
  the code tasks that can be, kept as isolated, minimal, additive diffs (new members/regions
  only — no reformatting of unrelated existing code), and should be implemented/merged by an
  integrator who resolves the merge against the parallel branch, not run blind in parallel with
  it.

## Dependency Graph

```
T1 (architect gate)
 └─> T2 → T3, T4
       T3, T4 → T4 done (both register in MauiProgram)
 T2 → T6 (ViewerViewModel — SHARED)
 T4 → T6 (needs IImmersiveModeService registered)
 T6 → T7 (FullScreenViewerPage needs the new VM members)
 T7 → T8 (DI factory registration needs the page class)
 T6, T8 → T9 (ViewerView — SHARED; needs both the VM members and the page factory)
 T2, T5 → T11 (tests need InternalsVisibleTo + the mock interface)
 T6 → T11
 T9, T7 → T12 (on-device verification needs the full wiring)
 T12 → T13 (docs updated after verified behavior)
```

Ready immediately (no deps): **T1**, **T5**.

## Task List

| T-ID | Title | Depends on | Layer |
|------|-------|------------|-------|
| T1 | Architect validation of Option B / third `ViewerView` host | none | Docs (gate) |
| T2 | Add `IImmersiveModeService` to `src/Core/Services/` | T1 | Core |
| T3 | Implement `AndroidImmersiveModeService` | T2 | Android |
| T4 | Implement `NoopImmersiveModeService`; register both in `MauiProgram.cs` | T2, T3 | MauiApp |
| T5 | Add `InternalsVisibleTo` to `NdiForAndroid.Core.csproj` | none | Core |
| T6 | **[SHARED]** `ViewerViewModel.cs` — full-screen state, commands, overlay timer, `KeepScreenOn`/`Stop` wiring | T2, T4 | Core |
| T7 | New `FullScreenViewerPage.xaml(.cs)` | T6 | MauiApp |
| T8 | DI: register `FullScreenViewerPage` + `Func<FullScreenViewerPage>` factory | T7 | MauiApp |
| T9 | **[SHARED]** `ViewerView.xaml(.cs)` — layout rework, gestures, toggle button, modal present/dismiss | T6, T8 | MauiApp |
| T10 | Unit tests: `ViewerViewModelTests` full-screen/overlay/`KeepScreenOn` coverage | T2, T5, T6 | Tests |
| T11 | `dotnet build` + `dotnet test tests/MauiApp.Tests` green | T3, T4, T7, T8, T9, T10 | Tests |
| T12 | On-device verification (`android-build-install-run`): Galaxy Tab A9+ landscape two-pane + phone | T9, T7, T11 | Android (verification) |
| T13 | Update `docs/architecture.md` (Viewer section) + `.github/KNOWLEDGE-BASE.md` (Shell Routes / Key File Paths) | T12 | Docs |

## Detailed Tasks

### T1 — Architect validation of Option B / third `ViewerView` host
- **Layer**: Docs (gate, not code)
- **Files**: none produced directly; verdict recorded as a new entry appended to
  `docs/features/viewer-fullscreen/spec.md` (or the architect's own working notes) before T2
  starts.
- **Description**: Per CLAUDE.md's mandatory architecture gate, run `solution-architect` against
  `plan.md` before any implementation. Confirm: (a) a third `ViewerView` host is an acceptable
  deviation from `docs/architecture.md`'s "two hosts" framing; (b) the modal-push handoff-safety
  argument in plan.md §3 holds; (c) `IImmersiveModeService`'s shape (Enter/Exit + `KeepScreenOn`
  as three members on one interface) fits Rule 5.
- **Depends on**: none.
- **Acceptance**: Recorded verdict, no unresolved architect objection blocking T2.

### T2 — Add `IImmersiveModeService`
- **Layer**: Core
- **Files**: `src/Core/Services/IImmersiveModeService.cs` (NEW)
- **Description**: `EnterImmersive()`, `ExitImmersive()`, `KeepScreenOn(bool enabled)` — see
  plan.md §4.1.
- **Depends on**: T1.
- **Acceptance**: Interface compiles; no MAUI/Android types referenced (Core stays MAUI-free);
  `dotnet build` green.

### T3 — Implement `AndroidImmersiveModeService`
- **Layer**: Android
- **Files**: `src/MauiApp/Platforms/Android/Services/AndroidImmersiveModeService.cs` (NEW)
- **Description**: `WindowInsetsControllerCompat` hide/show + `BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE`,
  guarded by `OperatingSystem.IsAndroidVersionAtLeast(30)`; `KeepScreenOn` via
  `DeviceDisplay.Current.KeepScreenOn`. See plan.md §4.1 for exact API calls.
- **Depends on**: T2.
- **Acceptance**: Compiles against the Android target; `dotnet build` green (Android build only
  — this file is `#if ANDROID`-only by location, no explicit guard needed inside it).

### T4 — Implement `NoopImmersiveModeService`; register both in `MauiProgram.cs`
- **Layer**: MauiApp
- **Files**: `src/MauiApp/Services/NoopImmersiveModeService.cs` (NEW), `src/MauiApp/MauiProgram.cs`
- **Description**: Three empty-body methods (mirrors `NoopMulticastLockService.cs`). Register
  both implementations behind the existing `#if ANDROID / #else` block (plan.md §4.2).
- **Depends on**: T2, T3.
- **Acceptance**: `IImmersiveModeService` resolves at runtime on both build configurations;
  `dotnet build` green.

### T5 — Add `InternalsVisibleTo` for the test assembly
- **Layer**: Core
- **Files**: `src/Core/NdiForAndroid.Core.csproj`
- **Description**: `<InternalsVisibleTo Include="NdiForAndroid.Tests" />` (plan.md §4.4) — the
  seam that lets tests call `ViewerViewModel.HideControlsOverlay()` directly.
- **Depends on**: none.
- **Acceptance**: `dotnet build` green; no other assembly gains unintended internals access.

### T6 — `ViewerViewModel.cs` full-screen state *(SHARED FILE)*
- **Layer**: Core
- **Files**: `src/Core/Features/Viewer/ViewModels/ViewerViewModel.cs`
- **Description**: Per plan.md §4.3 — 8th ctor parameter `IImmersiveModeService immersiveMode`;
  `IsFullScreen`/`IsControlsOverlayVisible`/`AreControlsVisible`; `OnIsFullScreenChanged` partial;
  `ToggleFullScreenCommand`/`ShowControlsOverlayCommand`; `NotifyControlInteraction()`/
  `ResetOverlayAutoHideTimer()`/`internal HideControlsOverlay()`; `_immersiveMode.KeepScreenOn(value)`
  added to `OnIsPlayingChanged`; `Stop()` sets `IsFullScreen = false`; `Dispose()` disposes the
  new timer; `NotifyControlInteraction();` added as the first statement of `PtzNudge`,
  `PtzZoomNudge`, `PtzAutoFocus`, `ChangeQualityProfileAsync`, `CancelRetry`, `Reconnect`,
  `OnIsAudioEnabledChanged`. Additive only — do not reformat unrelated existing members (this
  file is shared with a parallel feature branch; keep the diff isolated for merge review).
- **Depends on**: T2, T4.
- **Acceptance**: Compiles; existing `ViewerViewModelTests` construction updated for the new
  8th ctor arg (see T10); `dotnet build` green.

### T7 — New `FullScreenViewerPage.xaml(.cs)`
- **Layer**: MauiApp
- **Files**: `src/MauiApp/Features/Viewer/Views/FullScreenViewerPage.xaml`,
  `FullScreenViewerPage.xaml.cs` (NEW)
- **Description**: Chromeless `ContentPage` per plan.md §4.6 — constructor takes
  `IImmersiveModeService`, `IAppLifecycleService`; `Initialize(ViewerViewModel, Action onClosed)`;
  `OnAppearing`/`OnDisappearing` drive its own embedded `ViewerView`'s render loop +
  `EnterImmersive`/`ExitImmersive`; `OnBackButtonPressed` sets `IsFullScreen = false` and returns
  `true`; `AppPaused` handler does the same; `OnViewModelPropertyChanged` → `CloseAsync()` pops
  the modal and invokes `onClosed`. Its embedded `<views:ViewerView IsModalHost="True">` requires
  T9's `IsModalHost` property to exist on `ViewerView` — if T9 is not yet merged, stub
  `IsModalHost` as a no-op bindable property in this task and let T9 wire its actual guard logic.
- **Depends on**: T6.
- **Acceptance**: Compiles; page is a valid `ContentPage`; `dotnet build` green.

### T8 — DI: register `FullScreenViewerPage` + factory
- **Layer**: MauiApp
- **Files**: `src/MauiApp/MauiProgram.cs`
- **Description**: `AddTransient<FullScreenViewerPage>()` +
  `AddSingleton<Func<FullScreenViewerPage>>(sp => () => sp.GetRequiredService<FullScreenViewerPage>())`
  (plan.md §4.2).
- **Depends on**: T7.
- **Acceptance**: `sp.GetRequiredService<Func<FullScreenViewerPage>>()` resolves at runtime;
  `dotnet build` green.

### T9 — `ViewerView.xaml(.cs)` layout rework + modal hand-off *(SHARED FILE)*
- **Layer**: MauiApp
- **Files**: `src/MauiApp/Features/Viewer/Views/ViewerView.xaml`, `ViewerView.xaml.cs`
- **Description**: Per plan.md §4.5 — root `Grid` `Padding`/`RowSpacing` trigger; canvas `Border`
  `Grid.RowSpan` trigger + tap/double-tap gestures; `VideoCanvas` unconstrained-height trigger;
  new full-screen toggle `Button`; controls `ScrollView` `Grid.RowSpan`/`BackgroundColor` trigger
  + `IsVisible` rebind to `AreControlsVisible`. Code-behind: real `IsModalHost` bindable
  property (guards nested-modal re-entry), `OnBindingContextChanged` subscribing to
  `PropertyChanged`, `OnViewModelPropertyChanged`/`PresentFullScreenAsync` (resolves
  `Func<FullScreenViewerPage>` via `IPlatformApplication.Current.Services`, calls
  `StopRendering()`, pushes the modal). Existing `StatusMessage`/quality/audio/PTZ/retry/Stop
  XAML content is unchanged. Additive/targeted only — this file is shared with a parallel
  feature branch; keep the diff isolated for merge review.
- **Depends on**: T6, T8.
- **Acceptance**: Normal (non-full-screen) layout is pixel-identical to today's; full-screen
  layout goes full-bleed; `dotnet build` green.

### T10 — Unit tests: `ViewerViewModelTests` full-screen coverage
- **Layer**: Tests
- **Files**: `tests/MauiApp.Tests/Features/Viewer/ViewerViewModelTests.cs`
- **Description**: Add `Mock<IImmersiveModeService>`, update the single `CreateSut()` call site
  for the 8th ctor arg, and add the 12 tests listed in plan.md §6 (`ToggleFullScreenCommand_*`,
  `HideControlsOverlay_*`, `ShowControlsOverlayCommand_*`, `NotifyControlInteraction_*`,
  `Stop_WhileFullScreen_ExitsFullScreen`, `Dispose_WhileFullScreenTimerPending_DoesNotThrow`,
  `OnIsPlayingChanged_*_CallsKeepScreenOn*`, `ToggleFullScreen_OnAndOff_NeverCallsStopReceiver`).
- **Depends on**: T2, T5, T6.
- **Acceptance**: All 12 new tests pass deterministically (no real-time waits); no existing test
  regresses; `dotnet test tests/MauiApp.Tests` green.

### T11 — Full solution build + test green
- **Layer**: Tests
- **Files**: none (verification task)
- **Description**: `dotnet build NdiForAndroid.sln` and `dotnet test tests/MauiApp.Tests` both
  green with every prior task's changes integrated together.
- **Depends on**: T3, T4, T7, T8, T9, T10.
- **Acceptance**: Zero build errors; zero test failures.

### T12 — On-device verification
- **Layer**: Android (verification)
- **Files**: none (uses the `android-build-install-run` skill)
- **Description**: Install and exercise on the Galaxy Tab A9+ (landscape, two-pane Sources page)
  and a phone form factor (pushed `ViewerPage`): toggle button + double-tap enter/exit, single
  tap reveal + 3 s auto-hide + reset-on-interaction, Android back gesture exit (never pops the
  donor page or stops the receiver), system bars hidden but swipe-revealable, screen stays on
  while playing (both pane and pushed page, not just full screen), backgrounding while full
  screen resumes to normal layout. Check the single-/double-tap coexistence risk (plan.md §7)
  for any objectionable visual flash.
- **Depends on**: T9, T7, T11.
- **Acceptance**: All items above observed correct; any regression found is fixed before T13
  (loop back to T6/T9 as needed).

### T13 — Documentation update
- **Layer**: Docs
- **Files**: `docs/architecture.md` (Viewer / "two hosts" framing → three hosts),
  `.github/KNOWLEDGE-BASE.md` (Shell Routes table stays unchanged — no new route; add a short
  Viewer full-screen entry near the existing Viewer reconnection section; Key File Paths table
  gains `IImmersiveModeService`/`FullScreenViewerPage` rows)
- **Description**: Run the `documenter` stage once T12 confirms on-device behavior.
- **Depends on**: T12.
- **Acceptance**: Docs reflect the shipped design; no stale "two hosts" claim remains
  unqualified.
