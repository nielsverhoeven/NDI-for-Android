# Tasks: Viewer Control Deck (fixed layout, no scrolling)

## Status

**Done.** PR #357 merged 2026-09-05. Source plan: `docs/features/viewer-control-deck/plan.md`.
Spec: `docs/features/viewer-control-deck/spec.md`.

## Summary

- Total tasks: 14 (T1 is a review gate, T11–T14 are verification/docs, not new production code)
- Parent feature issue: **#342** (`Viewer control deck without scrolling (wireframe B) and overlay controls in full-screen (wireframe A)`)
- Branch: `feature/342-viewer-control-deck`
- Layers covered: Docs (gate), Core, MauiApp, Tests, Docs (release)
- **Shared file**: `ViewerView.xaml(.cs)` is touched once, in T10, only after every new
  `ContentView` it references (T5–T9) and the removal in T2 already exist — this avoids a
  half-wired root Grid referencing types that don't compile yet.

## Dependency Graph

```
T1 (architect gate)
 ├─> T2 (delete PtzPanelView)
 ├─> T3 (ViewerViewModel.Ptz.cs) ─> T4 (VM tests)
 └─> T5 (PlaybackControlsView)
T3 ─> T6 (CameraControlsView — needs the new parameterized preset commands)
T5, T6 ─> T7 (ViewerControlDeck)
T5, T6 ─> T8 (ViewerControlSheet)
T3 ─> T9 (FullScreenControlsOverlay — needs PtzRecallPresetCommand for chips)
T2, T7, T8, T9 ─> T10 (ViewerView.xaml(.cs) rework — SHARED)
T4, T10 ─> T11 (build + unit tests green)
T11 ─> T12 (on-device verification)
T12 ─> T13 (docs update)
T13 ─> T14 (final build/test + checkbox tick)
```

Ready immediately (no deps): **T1**.

## Task List

| T-ID | Title | Depends on | Layer |
|------|-------|------------|-------|
| T1 | Architect validation of the deck/sheet/overlay split | none | Docs (gate) |
| T2 | Delete `PtzPanelView.xaml(.cs)` | T1 | MauiApp |
| T3 | `ViewerViewModel.Ptz.cs` — parameterized preset commands, preset status message | T1 | Core |
| T4 | Unit tests for T3 | T3 | Tests |
| T5 | New `PlaybackControlsView.xaml(.cs)` | T1 | MauiApp |
| T6 | New `CameraControlsView.xaml(.cs)` (incl. long-press wiring) | T3 | MauiApp |
| T7 | New `ViewerControlDeck.xaml(.cs)` | T5, T6 | MauiApp |
| T8 | New `ViewerControlSheet.xaml(.cs)` (hand-built bottom sheet) | T5, T6 | MauiApp |
| T9 | New `FullScreenControlsOverlay.xaml(.cs)` | T3 | MauiApp |
| T10 | **[SHARED]** `ViewerView.xaml(.cs)` rework — root layout switch | T2, T7, T8, T9 | MauiApp |
| T11 | `dotnet build` + `dotnet test tests/MauiApp.Tests` green | T4, T10 | Tests |
| T12 | On-device verification (`android-build-install-run` skill): Galaxy Tab A9+ landscape + portrait against `tools/ViscaMockCamera` | T11 | Android (verification) |
| T13 | Update `docs/architecture.md` (Viewer section) + `.github/KNOWLEDGE-BASE.md` (Key File Paths) | T12 | Docs |
| T14 | Final `dotnet build` + test check; tick spec.md/tasks.md checkboxes | T13 | Docs/Verification |

## Detailed Tasks

### T1 — Architect validation
- **Layer**: Docs (gate, not code)
- **Files**: none produced; verdict recorded before T2/T3/T5 start.
- **Description**: Per CLAUDE.md's mandatory architecture gate, run `solution-architect` against
  `plan.md` before implementation. Confirm: (a) the five-`ContentView` split (§1) is an
  acceptable elaboration of the existing "`ViewerView` shared by three hosts" framing in
  `docs/architecture.md` — the hosts are unchanged (`ViewerPage`, embedded pane,
  `FullScreenViewerPage`), only `ViewerView`'s *internal* composition grows; (b) the
  deck-vs-sheet rule living in `ViewerView.xaml.cs` off `ViewerControlLayout.Choose(widthDp,
  heightDp)` (plan.md §9), not in `ViewerViewModel`, fits Dependency Rule 1 (Views depend on
  ViewModels/bindings — reusing a pure Core policy directly in a View's code-behind is the same
  pattern `SourceListPage.xaml.cs` already uses for its own size-driven layout switch, not a new
  precedent); (c) the hand-built bottom sheet (plan.md §6, no new NuGet package) is acceptable.
- **Depends on**: none.
- **Acceptance**: Recorded verdict, no unresolved architect objection blocking T2/T3/T5.

### T2 — Delete `PtzPanelView.xaml(.cs)`
- **Layer**: MauiApp
- **Files**: `src/MauiApp/Features/Viewer/Views/PtzPanelView.xaml` (DELETE),
  `PtzPanelView.xaml.cs` (DELETE)
- **Description**: Fully superseded by `CameraControlsView` (T6) — no other file references it
  once T10 removes its usage from `ViewerView.xaml`. Delete in this task; T10 removes the
  `<views:PtzPanelView />` XAML reference as part of its own rework (do not leave a dangling
  reference between T2 and T10 landing — coordinate as one PR or land T2 together with T10).
- **Depends on**: T1.
- **Acceptance**: No remaining reference to `PtzPanelView` anywhere in `src/`.

### T3 — `ViewerViewModel.Ptz.cs` changes
- **Layer**: Core
- **Files**: `src/Core/Features/Viewer/ViewModels/ViewerViewModel.Ptz.cs`
- **Description**: Per plan.md §8 — remove `PtzPresetNumber` `[ObservableProperty]` and the
  parameterless `PtzStorePreset`/`PtzRecallPreset` methods (verified no test references either
  symbol). Add `public static IReadOnlyList<int> PresetNumbers` (1–8), `PtzPresetStatusMessage`
  observable string, a `_presetStatusTimer` field (`ITimer?`), and two new `[RelayCommand]`
  methods `PtzStorePreset(int presetNumber)` / `PtzRecallPreset(int presetNumber)` calling
  `GetOrCreatePtzController().StorePresetAsync(presetNumber)` /
  `.RecallPresetAsync(presetNumber)` respectively. `PtzStorePreset` also sets
  `PtzPresetStatusMessage` and arms a 2 s `_timeProvider.CreateTimer` clearing it (same pattern as
  `ResetOverlayAutoHideTimer` in `ViewerViewModel.FullScreen.cs`). Dispose `_presetStatusTimer` in
  `DisposePtz()`.
- **Depends on**: T1.
- **Acceptance**: `dotnet build NdiForAndroid.sln` succeeds; no remaining reference to
  `PtzPresetNumber`.

### T4 — Unit tests for T3
- **Layer**: Tests
- **Files**: `tests/MauiApp.Tests/Features/Viewer/ViewerViewModelTests.cs` (edit — add tests, do
  not restructure `CreateSut()`, which needs no new parameters for this feature)
- **Description**: Add the four tests specified in plan.md §10 (`PtzStorePresetCommand_...`,
  `PtzRecallPresetCommand_...`, `IsPtzControlActive_...` — only if not already covered, grep
  first — and `PresetNumbers_IsOneToEight`). Verify exact `IPtzController` signatures against
  `src/Core/Features/Ptz/Services/IPtzController.cs` before writing `Verify(...)` calls —
  `RecallPresetAsync` takes `(int, float speed = 1f, CancellationToken)`, `StorePresetAsync`
  takes `(int, CancellationToken)`.
- **Depends on**: T3.
- **Acceptance**: `dotnet test tests/MauiApp.Tests` — all new tests pass, no existing test
  regresses.

### T5 — New `PlaybackControlsView.xaml(.cs)`
- **Layer**: MauiApp
- **Files**: `src/MauiApp/Features/Viewer/Views/PlaybackControlsView.xaml` (NEW),
  `PlaybackControlsView.xaml.cs` (NEW, code-behind is `InitializeComponent()` only)
- **Description**: Build exactly the structure in plan.md §3 — status label, a 3-way Row 1
  (quality segmented Smooth/Balanced/High with per-button "filled when selected" `DataTrigger`s
  on `QualityProfile`, XOR the `IsReconnecting` retry stack + Cancel button, XOR the lone
  `CanReconnect` Reconnect button), and Row 2 (48×48 audio `Switch`, 48×48 full-screen toggle
  `Button` with the existing ⛶/⤢ glyph-swap trigger, full-width Stop `Button`). `x:DataType`
  `NdiForAndroid.Features.Viewer.ViewModels.ViewerViewModel` (`vm:` namespace alias, matching
  every other view in this folder), no explicit `BindingContext` — inherited from the parent.
- **Depends on**: T1 (no VM dependency beyond members that already exist —
  `ChangeQualityProfileCommand`, `QualityProfile`, `IsPlaying`, `IsReconnecting`,
  `RetryStatusMessage`, `CancelRetryCommand`, `CanReconnect`, `ReconnectCommand`,
  `IsAudioEnabled`, `ToggleFullScreenCommand`, `IsFullScreen`, `StopCommand` — all pre-existing).
- **Acceptance**: Compiles; not yet referenced by anything (dangling until T7/T8) — that's fine.

### T6 — New `CameraControlsView.xaml(.cs)`
- **Layer**: MauiApp
- **Files**: `src/MauiApp/Features/Viewer/Views/CameraControlsView.xaml` (NEW),
  `CameraControlsView.xaml.cs` (NEW)
- **Description**: Build exactly the structure in plan.md §4.1 (endpoint chip with the existing
  `PtzLinkState` color triggers ported from the deleted `PtzPanelView`, d-pad 3×3 grid unchanged
  from `PtzPanelView`, zoom rocker as two 48×48 T/W buttons, preset grid of 8 explicitly-named
  48×48 buttons with **no** `Command`/`CommandParameter` in XAML, transient
  `PtzPresetStatusMessage` label using the existing `IsNotNullConverter`). Code-behind per §4.2 —
  `WirePreset(Button, int)` helper called 8 times in the constructor, `Pressed`/`Released` +
  `System.Threading.Timer` at `LongPressThresholdMs = 600`, dispatching through
  `Dispatcher.Dispatch`. Root `IsVisible="{Binding IsPtzControlActive}"`.
- **Depends on**: T3 (needs `PtzStorePresetCommand`/`PtzRecallPresetCommand(int)` and
  `PtzPresetStatusMessage` to exist).
- **Acceptance**: Compiles; manual on-device check deferred to T12 (tap → recall, hold ≥600 ms →
  store + confirmation label appears then clears after ~2 s).

### T7 — New `ViewerControlDeck.xaml(.cs)`
- **Layer**: MauiApp
- **Files**: `src/MauiApp/Features/Viewer/Views/ViewerControlDeck.xaml` (NEW),
  `ViewerControlDeck.xaml.cs` (NEW, `InitializeComponent()` only)
- **Description**: Per plan.md §5 — fixed `HeightRequest="200"` two-column `Grid`
  (`ColumnDefinitions="*,Auto"`), `PlaybackControlsView` in column 0 with the `ColumnSpan=2`
  `DataTrigger` on `IsPtzControlActive=False`, `CameraControlsView` in column 1.
- **Depends on**: T5, T6.
- **Acceptance**: Compiles; dangling until T10.

### T8 — New `ViewerControlSheet.xaml(.cs)`
- **Layer**: MauiApp
- **Files**: `src/MauiApp/Features/Viewer/Views/ViewerControlSheet.xaml` (NEW),
  `ViewerControlSheet.xaml.cs` (NEW)
- **Description**: Per plan.md §6 exactly — `HalfHeight = 320`, `ExpandedHeight = 440`,
  `TranslationY`-based two-state overlay, drag handle `BoxView`, two MD3-style tab `Button`s
  ("Playback"/"PTZ", PTZ tab `IsVisible="{Binding IsPtzControlActive}"`), tab content hosts
  `PlaybackControlsView`/`CameraControlsView` by reference (`x:Name`, visibility toggled
  imperatively in `SelectTab`), `PanGestureRecognizer` + tap-to-toggle on the handle area,
  stranded-tab guard subscribing to `PropertyChanged` for `IsPtzControlActive`. Default state:
  Half, tab: Playback.
- **Depends on**: T5, T6.
- **Acceptance**: Compiles; dangling until T10. On-device drag/tap behavior verified in T12.

### T9 — New `FullScreenControlsOverlay.xaml(.cs)`
- **Layer**: MauiApp
- **Files**: `src/MauiApp/Features/Viewer/Views/FullScreenControlsOverlay.xaml` (NEW),
  `FullScreenControlsOverlay.xaml.cs` (NEW, `InitializeComponent()` only)
- **Description**: Per plan.md §7 — preset chips top-left (`Command="{Binding
  PtzRecallPresetCommand}"`, `CommandParameter` a literal `x:Int32` 1–8 per chip, tap-only, no
  long-press), d-pad card bottom-left, zoom rocker card bottom-right (both duplicating the
  relevant `CameraControlsView` markup, deliberately not shared — see plan.md §11), slim 48 dp
  bottom toolbar (endpoint chip, quality segmented trio, audio `Switch`, full-screen toggle,
  Stop, `⋮` opening `OpenPtzEndpointFormCommand`). Root `IsVisible="{Binding
  AreControlsVisible}"` — reuses existing #338 auto-hide state, no new ViewModel members.
- **Depends on**: T3 (chip `CommandParameter`s need `PtzRecallPresetCommand(int)` to exist; the
  toolbar itself only needs pre-existing members).
- **Acceptance**: Compiles; dangling until T10.

### T10 — **[SHARED]** `ViewerView.xaml(.cs)` rework
- **Layer**: MauiApp
- **Files**: `src/MauiApp/Features/Viewer/Views/ViewerView.xaml`,
  `ViewerView.xaml.cs`
- **Description**: Per plan.md §2 exactly.
  - XAML: keep the video `Border`/`SKCanvasView` and `PtzEndpointPanel` untouched; remove the old
    `ScrollView`/`VerticalStackLayout` stack, the `<views:PtzPanelView />` reference, and the old
    floating full-screen toggle `Button`; add named `<views:ViewerControlDeck x:Name="Deck">`,
    `<views:ViewerControlSheet x:Name="Sheet">`, `<views:FullScreenControlsOverlay x:Name="Overlay">`
    with no `IsVisible` binding of their own (visibility is set directly in code-behind — see
    plan.md §2.2 for why not a `BindableProperty` + `{x:Reference Root}` binding).
  - Code-behind: add the `UpdateLayoutVisibility()` helper calling
    `ViewerControlLayout.Choose(Width, Height)` and setting `Overlay`/`Deck`/`Sheet.IsVisible`
    directly; invoke it from `SizeChanged`, `OnBindingContextChanged`, and from
    `OnViewModelPropertyChanged` on `IsFullScreen` changes. No `IWindowSizeClassService`
    dependency, no new `BindableProperty`.
- **Depends on**: T2 (dangling reference removed), T7, T8, T9 (all three new hosts must exist to
  compile the XAML references).
- **Acceptance**: `dotnet build NdiForAndroid.sln` succeeds; `Overlay`/`Deck`/`Sheet.IsVisible`
  toggle correctly for the `IsFullScreen` / measured-size combinations described in
  plan.md §9 — spot-checked on-device in T12, not unit-testable without MAUI runtime.

### T11 — `dotnet build` + `dotnet test` green
- **Layer**: Tests
- **Files**: none (verification only)
- **Description**: `dotnet build NdiForAndroid.sln` then `dotnet test tests/MauiApp.Tests`. Fix
  any compile or test regression before proceeding — do not defer to T12.
- **Depends on**: T4, T10.
- **Acceptance**: Both commands exit 0; no test regressions vs. the pre-feature baseline.

### T12 — On-device verification
- **Layer**: Android (verification)
- **Files**: none produced (screenshots/notes only, per the `android-build-install-run` skill's
  own conventions)
- **Description**: Use the `android-build-install-run` skill against a Galaxy Tab A9+ (or
  matching emulator profile at 1280×800 dp) with `tools/ViscaMockCamera` running as the PTZ
  endpoint. Walk every acceptance criterion in spec.md: landscape two-pane deck (no scrolling,
  no overlap), no-PTZ source (camera column absent, playback spans full width), portrait
  bottom-sheet-vs-deck per the ViewerControlLayout.Choose 640×470 dp measured-size rule (plan.md
  §9), full-screen overlay (auto-hide/tap/double-tap, d-pad/zoom operable, tally border visible),
  preset tap-vs-long-press
  (600 ms) with confirmation text and auto-clear.
- **Depends on**: T11.
- **Acceptance**: Every checkbox in spec.md's Acceptance Criteria section verified true; any
  spacing/sizing deviation found is corrected in the relevant view file from T5–T9 (numeric
  tuning only, per plan.md §11) and re-verified.

### T13 — Documentation updates
- **Layer**: Docs
- **Files**: `docs/architecture.md` (Viewer row in the Module Map — note the deck/sheet/overlay
  composition, no structural host-count change), `.github/KNOWLEDGE-BASE.md` (Key File Paths —
  add the five new view files; note `PtzPanelView` removal)
- **Description**: Mirror the update style of the #338/#339 features' own doc-update tasks —
  additive, factual, no re-derivation of unrelated sections.
- **Depends on**: T12.
- **Acceptance**: Docs reflect the shipped file layout; no stale reference to `PtzPanelView`
  remains in either doc.

### T14 — Final build/test check + checkbox tick
- **Layer**: Docs/Verification
- **Files**: `docs/features/viewer-control-deck/spec.md`, `tasks.md` (tick checkboxes)
- **Description**: Re-run `dotnet build NdiForAndroid.sln` + `dotnet test tests/MauiApp.Tests`
  once more after the T13 doc edits (docs-only changes should not affect this, but confirm), then
  mark every spec.md acceptance-criteria checkbox and this file's Task List as complete.
- **Depends on**: T13.
- **Acceptance**: Green build/test; PR ready to open per CLAUDE.md's workflow rules (linked
  branch, PR referencing #342, `Closes #342`).
