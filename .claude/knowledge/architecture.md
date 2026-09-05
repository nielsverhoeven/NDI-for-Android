<!-- Architect knowledge index. Created 2026-09-04. -->

# Architecture knowledge (index)

**This file is deliberately thin.** The canonical, committed architecture sources for this repo are:

| Source | Role |
|---|---|
| `docs/architecture.md` | Canonical module map, dependency rules, threading rules, bridge layout |
| `docs/constitution.md` | Authoritative tech/architecture decisions |
| `.github/KNOWLEDGE-BASE.md` | Consolidated agent reference (Architecture Rules section) |
| `CLAUDE.md` | Quick-start summary + the 6 "must not violate" rules |

Do **not** duplicate those here — update them (via the repo's `architect`/`documenter` flow) instead.
This file only records architect verdicts and open questions that do not yet belong in `docs/`.

## Standing rules used in fit-checks (pointers, not restatements)

1. Repository-mediated persistence; no SQLite from ViewModels.
2. No NDI SDK types cross the bridge boundary; `[DllImport("ndi")]` only in `src/MauiApp/NdiBridge/Interop/`.
3. No business logic in Views.
4. Bridge events raised on pump threads; marshal via `IMainThreadDispatcher` (Core) / `MainThread` (MauiApp).
5. Android APIs isolated in `Platforms/Android` behind Core interfaces; `Noop*` elsewhere.
6. Every captured NDI frame must be freed; pump threads must never throw out.

Note on rule 5 scope: `src/MauiApp` targets **only** `net10.0-android`
(`src/MauiApp/NdiForAndroid.csproj:4`), so `Android.*` type usage inside `src/MauiApp` compiles
unconditionally. Rule 5's intent is to keep Android APIs out of **Core** and out of
**testable/portable logic** — not to ban every `Android.Util.Log` call in the MAUI app layer
(precedent: `src/MauiApp/NdiBridge/NdiRuntime.cs:174`).

## Verdicts log

### 2026-09-04 — #338 Full-screen viewer (chromeless modal, 3rd `ViewerView` host)

**APPROVE-WITH-CHANGES.** Option B (chromeless `FullScreenViewerPage` pushed via
`Navigation.PushModalAsync`, hosting a 2nd `ViewerView` bound to the donor's live
`ViewerViewModel`) is accepted; a **third `ViewerView` host** is a sanctioned extension of
`docs/architecture.md:131` ("shared by `ViewerPage` and the embedded pane") and must be
documented in T13. `IImmersiveModeService` (Enter/Exit/KeepScreenOn) in `src/Core/Services/`
with `Platforms/Android/Services/AndroidImmersiveModeService` + `Services/NoopImmersiveModeService`
fits Rule 5 and mirrors `IMulticastLockService`. Blocking changes:

1. **plan.md §3 / FR11 "no `AppShell.xaml.cs` change" is REJECTED as an unverified claim.**
   MAUI 10 (`Microsoft.Maui.Controls` `10.*`) Shell integrates modal pages into its navigation
   state; `AppShell.OnNavigating` (`AppShell.xaml.cs:210`) + `ParseDestination` (`:267`) use
   substring `Contains`, so an implicit modal route containing "view" would misclassify. Required:
   guard at the top of `OnNavigating` (after `base`) — `if (Navigation?.ModalStack?.Count > 0)
   return;` (plus `ShellNavigationSource.PushModal/PopModal` if those values exist on the target
   MAUI version). New task T2a.
2. **`ViewerPage.OnDisappearing` (`ViewerPage.xaml.cs:35-48`) must be hardened.** Its
   `NavigationStack.Contains(this)` guard is the only thing between a modal push and
   `_viewModel.Dispose()`. Add `Shell.Current?.Navigation?.ModalStack?.Count > 0` as an
   additional skip condition. FR11's "no donor page changes" is therefore amended.
3. **Keep-screen-on must be released on teardown**: `ViewerViewModel.Dispose()` must call
   `_immersiveMode.KeepScreenOn(false)` — `Dispose()` does not set `IsPlaying = false`, so the
   flag would otherwise leak for the process lifetime.
4. **`AndroidImmersiveModeService` marshals to the UI thread itself** (`MainThread.
   BeginInvokeOnMainThread`) for all three members; Core must not learn about threading here.
5. **Modal `ViewerView` teardown is mandatory** (transient page + `SKBitmap` + un-unsubscribed
   `PropertyChanged` = ~8 MB leaked per full-screen entry).
6. **Test seam: use `Microsoft.Extensions.Time.Testing.FakeTimeProvider`** (already referenced by
   `tests/MauiApp.Tests`), not `InternalsVisibleTo`. T5 is dropped.
7. `#AA000000` → `{DynamicResource ScrimBackground}` (`Resources/Styles/Colors.xaml:31`).
8. Full-screen members live in `ViewerViewModel.FullScreen.cs` (partial); PTZ
   `NotifyControlInteraction()` wiring is **deferred to the #338+#339 integrator**
   (those methods move to `ViewerViewModel.Ptz.cs` on the parallel branch).

### 2026-09-04 — Soak-test logcat instrumentation (NdiViewerBridge stats + LogBridgeEvent mirror)

**APPROVE-WITH-CHANGES.** Runtime `IsDiagnosticOverlayService.IsDeveloperMode` gate accepted as the
debug flag (a `#if DEBUG` gate would not work for the intended Release-build device soak).
Direct `Android.Util.Log` in `src/MauiApp` accepted (app layer, Android-only TFM, existing
precedent) — no new Core abstraction warranted for temporary instrumentation.
Required change: `DiagnosticOverlayService._isDeveloperMode`
(`src/MauiApp/Features/DiagOverlay/DiagnosticOverlayService.cs:13`) is read from NDI pump threads
and must be `volatile` (or accessed via `Volatile.Read`) once cross-thread reads are introduced.
Constraints: 1 Hz only, nothing in the per-frame path, max-gap field stays pump-thread-confined,
logging must never throw into the pump loop.

### 2026-09-04 — Fase-4 fixplan (FIX-01..FIX-17 + D8 option 1)

**APPROVE-WITH-CHANGES overall.** Verdicts per fix in the session report. Binding decisions:

- **FIX-02 (OutputPage half): REJECTED.** `OutputPage` is a `ShellContent` `ContentTemplate` target
  (`src/MauiApp/AppShell.xaml:43,72`), i.e. Shell-cached for the section lifetime — identical to the
  `HomePage` disposal bug FIX-03 removes. Disposing its transient `OutputViewModel` in
  `OnDisappearing` would permanently kill `OutputStatusChanged` + `AppResumed` after the first tab
  switch. Rule: **only push-navigated pages may dispose their ViewModel from page lifecycle;
  ShellContent-hosted tab roots must not.**
- **FIX-04 route strings:** `//stream-tab` hardcoded in Core (`SourceListViewModel`) and in
  `DeepLinkService` conflicts with `docs/architecture.md` Navigation rule 5 (placement-adaptive
  `-tab` vs `-rail` routes). Accepted **only** as an interim with a `TODO` + follow-up issue for a
  placement-aware route resolver; `docs/architecture.md` Navigation rule 4 ("OutputPage … does not
  accept a query parameter") must be amended to allow `reStreamSourceId`/`isReStreamMode`.
- **FIX-09:** public/Core split must use `ConfigureAwait(false)` on every `SemaphoreSlim.WaitAsync`
  (`NdiOutputBridge.Dispose` blocks synchronously on these methods) and must convert **both**
  internal `StopOutputAsync` call sites (`NdiOutputBridge.cs:80` and the catch at `:134`).
- **FIX-11:** `VideoInputKind` already lives in Core (`src/Core/Services/ICaptureSources.cs:4`), so
  the signature change stays Core-only — approved. The `captureMicrophone` parameter is **not**
  derivable in the video path (`IVideoCaptureSource.StartAsync` has no mic flag), so the FGS keeps
  permission-gating `TypeMicrophone`; only `TypeMediaProjection`/`TypeCamera` become kind-gated.
- **D8 option 1: APPROVED** — `IDiagnosticOverlayService` is a Core contract
  (`src/Core/Features/DiagOverlay/Services/IDiagnosticOverlayService.cs`), so calling it from Core's
  `DiscoveryRefreshService` respects layering. Inject as a trailing **optional** ctor parameter.

### 2026-09-04 — Placement-aware primary navigation (A) + handoff timing (B)

**APPROVE-WITH-CHANGES (both).** Follow-up to the FIX-04 interim; confirmed by tablet rail-mode testing.

**A — `INavigationService.NavigateToPrimaryAsync(PrimaryNavDestination, string? queryString)`.**
Approved; retires the `TODO(nav-rule-5)` markers in `SourceListViewModel.NavigateToOutputAsync` and
`DeepLinkService.NavigateToOutputForReStreamAsync`. **No Core `IPrimaryRouteResolver`** — `//x-tab`
/`//x-rail` are Shell URIs, a MAUI-layer concern; Core must not learn Shell routing.
Required change: **no `Shell.Current as AppShell` downcast.** Move the two route dictionaries
(`AppShell.xaml.cs:12-28`) into `ShellNavigationService`, injecting the singleton
`AdaptiveShellStateViewModel` (`MauiProgram.cs:122`) for `IsLeftRailNavigationVisible`;
`AppShell.TryGetRouteForCurrentPlacement` then delegates to it. One route table, no duplicated
fallback. Still owed from the fase-4 verdict: amend `docs/architecture.md` Navigation rule 4
(`OutputPage` may accept `reStreamSourceId`/`isReStreamMode`) and rule 2/5 for the new method.

**B — handoff moved to `OnNavigating` with a deferral.** Approved in principle; five blocking changes:
1. `ParseDestination` (`AppShell.xaml.cs:242-251`) must match on the **path only** (split on `?`).
   Ordered `Contains` puts `stream` before `view`, so `//view-rail?reStreamSourceId=…` parses as
   **Stream**. Live bug today: `viewer?sourceId=<name containing "stream">` fires a View→Stream
   handoff whose `StopReceiver()` kills the viewer just opened.
2. `GetDeferral()` is unavailable when `args.CanCancel` is false — null-check and fall back to the
   current post-navigation handoff; keep `OnShellNavigated` as the reconciliation point for
   `_currentPrimaryDestination`, guarded so the handoff cannot double-fire.
3. `base.OnNavigating(args)` first; bail if `args.Cancelled`.
4. `try/finally` around `Complete()` and swallow+log handoff exceptions — an escaped exception
   wedges *all* Shell navigation permanently.
5. Latency: the deferral holds the old page while `StopOutputCoreAsync` tears down
   MediaProjection/camera (`NdiOutputBridge.cs:165-193`). Reorder the handoff so
   `SaveAsync(IsOutputActive=false)` precedes `StopOutputAsync`, and cap the deferral with a
   timeout so a wedged stop cannot freeze navigation (ANR).

Placement-change path is safe: `EnsurePrimaryDestinationVisibleAsync` keeps the same destination, so
`from == to` short-circuits (`NdiNavigationHandoffService.cs:29`). Guard `ApplyPlacement`'s dispatched
`GoToAsync` against landing while a deferral is pending.

### 2026-09-04 — B: Home quick actions (#328)

**APPROVE-WITH-CHANGES.** T001 AWC · T002 REJECT · T003 AWC · T004 AWC · T005 A · T006 AWC ·
T007–T010 AWC. #328 lands **after** #326/#334 (worktree `…-wt/output`), on top of that branch's
`HomeViewModel` (which gains an `INdiOutputBridge outputBridge` 5th ctor parameter, a
`_outputBridge` field, an `OutputStatusChanged` subscription that re-runs `RefreshCommand`, and a
corroborated `OutputStatus` condition). Full A verdict lives in the output worktree's copy of this
file.

1. **T002 REJECT — `NavigateToAsync("viewer?sourceId=…")` from Home pushes under `//home-tab`.**
   `AppShell.ParseDestination` matches `home` first (`AppShell.xaml.cs:274`), so the location
   resolves to **Home**, `NdiNavigationHandoffService`'s `View` branch never fires, and
   `ViewerViewModel.Dispose()` does **not** call `StopReceiver()` — the NDI receiver keeps running
   after the user leaves (docs/architecture.md NDI Bridge rule 4: stop native sessions on route
   transitions). Fix: `await NavigateToPrimaryAsync(PrimaryNavDestination.View);` then
   `await NavigateToAsync($"viewer?sourceId={Uri.EscapeDataString(...)}")`, so the push lands under
   `//view-tab`/`//view-rail`. Alternative (larger, viewer-owned): make `ViewerViewModel.Dispose()`
   stop the receiver.
2. **T001/T003 seam over A (exact).** No 7th ctor parameter — reuse A's `_outputBridge`. In
   `RefreshAsync`'s existing `_dispatcher.BeginInvokeOnMainThread` block compute once:
   `var outputActive = state.IsOutputActive && _outputBridge.IsActive;` → `OutputStatus` from
   `outputActive`; `LastOutputStreamName = state.StreamName;`
   `CanResumeOutput = !outputActive && !string.IsNullOrWhiteSpace(state.StreamName);`.
   plan.md's persisted-only interim rule and its "prefer a read model over a direct bridge
   dependency" follow-up note are **superseded** — the direct `INdiOutputBridge` dependency is
   approved and already present after A. A's `OnOutputStatusChanged` → `RefreshCommand` makes
   `[NotifyCanExecuteChangedFor(nameof(CanResumeOutput))]` update live with no extra wiring.
   Note `StopOutputCommand` persists `StreamName = null`, so Resume stays disabled after a
   deliberate stop — intended semantics.
3. **T006 REQUIRED — drop `state.IsOutputActive` from `ApplyResumeRequestAsync`'s gate.** After A,
   that flag is cleared whenever the bridge does not corroborate, so the command would be dead code
   in exactly the resume scenario. Gate on `!string.IsNullOrWhiteSpace(state.StreamName)` only.
   Use A's exact string `"Tap Start to resume output"` (**no** trailing period) in both ViewModels
   and in T008's assertion — plan.md currently has both spellings.
4. **T004 — disabled-not-hidden APPROVED.** Verify on device that the explicit
   `BackgroundColor="{DynamicResource Primary}"` / `SuccessGreen` still yields a visibly disabled
   button; if not, add a `Disabled` VisualState using `DynamicResource` only.
5. **T005 APPROVED** (`resume` `[QueryProperty]`, `else if` after the `reStreamSourceId` branch —
   mutually exclusive entry points, lifecycle wiring only, Rule 3 respected). Doc debt:
   `docs/architecture.md` Navigation rule 4 must be amended to list `resume` alongside
   `reStreamSourceId`/`isReStreamMode`.
6. Not starting capture from `ApplyResumeRequestAsync` is the correct invariant — no silent
   MediaProjection re-consent. All tests remain reachable from `tests/MauiApp.Tests`; only T004's
   visual disabled state needs device verification.
### 2026-09-04 — A: Output session lifecycle (#326 + #334 slice 1; #327 slice 2)

**APPROVE-WITH-CHANGES (slice 1). APPROVE-WITH-CHANGES + HOLD (slice 2).**
Per-task: T001 A · T002 A · T003 A · T004 AWC · T005 A · T006 AWC · T007 AWC · T008 AWC ·
T009–T012 A · T013 AWC(hold) · T014 AWC · T015 A · T016 AWC.

Binding changes:
1. **T006 — `IsActive` must not take `_sendLock`.** `_sendLock` is held across the synchronous
   `NDIlib_send_send_video_v2` at 30 fps (`NdiOutputBridge.cs:256-263`); both ViewModels read
   `IsActive` on the UI thread. Use `Volatile.Read(ref _send) != IntPtr.Zero || _reStreamRunning`
   (IntPtr read is atomic on both packaged ABIs). **`IsActive` must never acquire `_outputLock`** —
   `RaiseOutputStatusChanged` is invoked while `_outputLock` is held.
2. **T004 — guard `RaiseStopped` with `if (!_isActive) return;`.** The "never on a caller-requested
   stop" invariant currently rests only on `UnregisterCallback` preceding `projection.Stop()`
   (`AndroidVideoCaptureSource.cs:122,125`). Mirror the mic's `_running` guard.
3. **T007 — wrap `OnAppResumed`'s post-`await` mutations in `_dispatcher.BeginInvokeOnMainThread`**
   (Rule 4; `async void` + `await` continuation).
4. **T008 — `INdiOutputBridge` in `HomeViewModel` is APPROVED** (Core interface, Dependency Rule 2;
   Home's charter is the output/viewer status summary). Known debt: `HomeViewModel` is transient
   (`MauiProgram.cs:125`) and `HomePage` must not dispose it (ShellContent tab root — FIX-02 rule),
   so the `Dispose()` unsubscribe is dead code and ≤2 stale VMs stay subscribed to the singleton
   bridge. Same shape as `OutputViewModel` today; accepted, follow-up issue owed for tab-root VM
   lifetime. Use `_ = RefreshCommand.ExecuteAsync(null)` (AsyncRelayCommand suppresses concurrent
   executions).
5. **T013 must not merge before slice 1 is device-verified.** It removes the only code that clears
   persisted `IsOutputActive` on leaving Stream; the corroboration path replaces it.
6. **T016 — add a check for `StartCommandResult.Sticky`** (`ScreenShareForegroundService.cs:52`): a
   sticky restart delivers a null intent, falling through to `StartForeground(TypeMediaProjection)`
   with no live projection (API 34+ SecurityException). Likely surfaced by background testing;
   treat a fix as new scope.

Confirmed real: mic `_running` never reset on autonomous loop exit
(`AndroidMicrophoneCaptureSource.cs:96,104` vs `IsActive => _running` at `:26`). The
`|| wasActive` addition to `StopOutputCoreAsync`'s `statusChanged` is not a pre-existing bug — it
is a required consequence of adding `IsActive` to the `OutputStatusChanged` contract.
No deadlock in the `Stopped` → `StopOutputAsync` path (all waits are async `SemaphoreSlim`).
`IPlatformApplication.Current.Services.GetService` in the FGS is accepted (OS-constructed `Service`,
`MainActivity` precedent) — confine it to the `ActionStopRequested` branch.
Doc debt: `docs/architecture.md` Navigation rule 4 must gain the `resume` query parameter.

### 2026-09-04 — B: Home quick actions (#328)

**APPROVE-WITH-CHANGES.** T001 AWC · T002 REJECT · T003 AWC · T004 AWC · T005 A · T006 AWC ·
T007–T010 AWC. #328 lands **after** A, on top of A's `HomeViewModel`.

1. **T002 REJECT — `NavigateToAsync("viewer?sourceId=…")` from Home pushes under `//home-tab`.**
   `AppShell.ParseDestination` matches `home` first (`AppShell.xaml.cs:274`), so the location
   resolves to **Home**, the handoff's `View` branch never fires, and `ViewerViewModel.Dispose()`
   does **not** call `StopReceiver()` — the NDI receiver leaks (docs/architecture.md NDI Bridge
   rule 4). Fix: `await NavigateToPrimaryAsync(PrimaryNavDestination.View);` then
   `await NavigateToAsync($"viewer?sourceId={Uri.EscapeDataString(...)}")`. Alternative (larger):
   make `ViewerViewModel.Dispose()` stop the receiver.
2. **T001/T003 seam over A (exact).** No 7th ctor param — reuse A's `_outputBridge`. In
   `RefreshAsync`'s dispatcher block: `var outputActive = state.IsOutputActive && _outputBridge.IsActive;`
   → `OutputStatus` from `outputActive`; `LastOutputStreamName = state.StreamName;`
   `CanResumeOutput = !outputActive && !string.IsNullOrWhiteSpace(state.StreamName);`. B's
   persisted-only interim rule is **replaced**, not kept. A's `OnOutputStatusChanged` → Refresh
   makes `[NotifyCanExecuteChangedFor]` live for free.
3. **T006 REQUIRED — drop `state.IsOutputActive` from `ApplyResumeRequestAsync`'s gate.** After A
   the flag is cleared on non-corroborated resume, so the command would be dead. Gate on
   `!string.IsNullOrWhiteSpace(state.StreamName)` only, and use A's exact string
   `"Tap Start to resume output"` (no trailing period) in both ViewModels and both test sets.
4. **T004 — disabled-not-hidden APPROVED**; verify on device that the explicit
   `BackgroundColor="{DynamicResource Primary}"` still yields a visibly disabled state (add a
   `Disabled` VisualState using `DynamicResource` if not).
5. Not starting capture from `ApplyResumeRequestAsync` is the correct invariant — no silent
   MediaProjection re-consent.

#### Addendum 2026-09-04 — device fit-check (2 deviations found on Galaxy Tab A9+)

**1. Handoff clearing `StreamName` — APPROVE (interim).** Persist
`new AppStateSnapshot(state.LastViewerSourceId, state.StreamName, false, state.LastSelectedSourceId)`
at `NdiNavigationHandoffService.cs:38`. This does **not** introduce new semantics — it removes an
outlier. The "stopped but resumable" encoding `StreamName != null && !IsOutputActive` is already the
established idiom, written by `OutputViewModel.OnAppResumed`'s non-corroborated branch
(`OutputViewModel.cs:119-120`) and by `ToggleReStreamModeAsync` (`:191-195`); line 38 was the only
writer that also erased the name. Resulting invariant, now consistent:
**`AppState.StreamName` = the name of the current or most recent *unterminated* output session;
`null` only after a deliberate Stop.** The long-lived preferred name lives separately in
`OutputConfiguration.PreferredStreamName`, so nothing is lost. `SaveAsync` must stay **before**
`StopOutputAsync` (deferral-latency rule from the handoff-timing verdict). Interaction with the
slice-1 corroborated gate is benign: `outputActive = state.IsOutputActive && IsActive` is `false`,
so `CanResumeOutput` is `true` — the intended result. #327 deletes this whole `from == Stream`
branch (spec.md D4), so keep the edit to the single argument and delete the paired test with the
branch. Known consequence: `ToggleReStreamModeAsync` persists a name without a session, so Home can
offer Resume for a never-started re-stream — bounded by the "resume only pre-fills, never starts"
invariant; do not widen the gate to compensate.

**2. Disabled quick-action buttons — APPROVE-WITH-CHANGES.** A `Disabled` VisualState is the right
mechanism (explicit `BackgroundColor` overrides the native Android disabled `ColorStateList`), but
**not inline in `HomePage.xaml`**. Put it in the implicit `Style TargetType="Button"` in
`src/MauiApp/Resources/Styles/Styles.xaml:21-28`: theming is centralized there (every style in that
file is implicit and `DynamicResource`-based), a per-page VSM block duplicates a global rule in a
View, and any `Command`-disabled button app-wide gets the fix for free. Must include an explicit
`Normal` state resetting `Opacity` to `1`. `Opacity` is not set locally on either button, so the
setters apply cleanly; no colour keys added, `DynamicResource` untouched → theming rules respected.
Acceptable narrower alternative if blast radius must stay inside #328: an `x:Key`'d style
`BasedOn` the implicit one, applied to the two buttons.

**Housekeeping:** the "B: Home quick actions (#328)" verdict is duplicated in this file (also at the
earlier heading); collapse to one on the next edit.
### 2026-09-04 — #342 Viewer control deck (wireframe B) + full-screen overlay (A) — T1–T14

**APPROVE-WITH-CHANGES overall (T1 gate).** No violation of Architecture Rules 1, 2, 5, 6 or
`docs/architecture.md` Dependency Rules 1–7. The five-`ContentView` split is an accepted
*internal* elaboration of `ViewerView`; the three hosts (`ViewerPage`, embedded pane,
`FullScreenViewerPage`) are unchanged, so `docs/architecture.md:133` needs no structural edit
(T13 stays additive). Binding decisions:

1. **BLOCKER — the deck is height-constrained, and the rule only measures width.** Deck
   minimum = 240 dp video + 200 dp deck + 32 dp padding + 16 dp row spacing = **472 dp of host
   height**, and the plan deletes the `ScrollView` (`ViewerView.xaml:79`) that absorbs overflow
   today. A phone in landscape is Medium/Expanded by width (e.g. 800×360 dp) → deck → the Stop
   button is clipped off-screen with no scroll. Required: `UpdateLayoutVisibility()` must also
   require height ≥ ~470 dp and the host's own width ≥ ~640 dp (camera cluster is a hard 440 dp);
   below either, fall back to the sheet.
2. **BLOCKER — narrow Expanded panes make Stop unreachable, not merely tight.** `SourceListPage`
   gives the pane 3/5 (`SourceListPage.xaml.cs:51-52`); at 841 dp window the pane is ~500 dp,
   camera column takes 440 dp, `ColumnDefinitions="*,Auto"` starves the `*` column to ~0. Same at
   the Medium floor (600 dp). "Accepted limitation" is not acceptable when the *stop control*
   disappears. The §1 guard resolves this.
3. **Layout policy moves to a pure Core helper.** `ViewerControlLayout.Choose(widthDp, heightDp)`
   → `Deck|Sheet` in `src/Core/Features/Viewer/`, unit-tested in `tests/MauiApp.Tests` (which
   references Core only). The View keeps `SizeChanged`/`Changed` wiring; the numeric policy is
   tested. This satisfies Rule 3 with the smallest possible code-behind.
4. **BLOCKER — `IWindowSizeClassService` is a singleton (`MauiProgram.cs:89`); `ViewerPage` and
   `FullScreenViewerPage` are transient (`:146,:147`).** Subscribing `Changed` in the `ViewerView`
   ctor and unsubscribing only in `Teardown()` (called on the modal path only) leaks every pushed
   `ViewerPage`'s `ViewerView` + `SKBitmap`. Subscribe/unsubscribe on `Loaded`/`Unloaded`, or drop
   the service in favour of the view's own `SizeChanged` (preferred, and it removes the leak).
5. **New standing rule — a reusable `ContentView` must not bind its own root `IsVisible`.** The
   host owns root visibility; the view binds visibility on an *inner* element. Precedent:
   `PtzPanelView.xaml:7,28`. MAUI's public `SetValue` clears one-way bindings, so the plan's
   `CameraControlsView` root `IsPtzControlActive` binding dies the first time
   `ViewerControlSheet.SelectTab` assigns `IsVisible`, and the host-level `IsVisible` in
   `ViewerView.xaml` silently replaces `FullScreenControlsOverlay`'s `AreControlsVisible` binding
   (auto-hide would never fire). Drop the three `BindableProperty` declarations and the
   `{x:Reference Root}` bindings; name the three hosts and set `.IsVisible` in
   `UpdateLayoutVisibility()`. This also avoids compiled-binding `x:DataType` breakage.
6. **`ViewerControlSheet` must unsubscribe.** `OnBindingContextChanged` subscribes
   `vm.PropertyChanged` with no `-=`; mirror `ViewerView.xaml.cs:95-106`.
7. **Sheet gestures belong on a 48 dp handle row, not on `SheetContainer`.** The plan attaches
   pan + tap to the whole container, so any tap on sheet chrome toggles half/expanded. Row 0
   becomes `48`, the 4 dp pill sits inside it, both recognizers move there.
8. **Explicit `<Button.Style>` without `BasedOn` drops the implicit themed style**
   (`Resources/Styles/Styles.xaml:21-27`, no keyed Button styles exist), i.e. loses
   `{DynamicResource Primary}`/`OnPrimary` — a theming-rule regression. Use element-level
   `<Button.Triggers>` or add keyed styles with `BasedOn`.
9. **PTZ interaction must reset the full-screen auto-hide timer.** The #338 verdict deferred
   `NotifyControlInteraction()` wiring to this integration branch; without it the overlay hides
   3 s into a pan. Wire it from the PTZ commands in `ViewerViewModel.Ptz.cs` (Core, testable).
10. **Preset long-press timers must be cancel-safe.** Dispose all 8 on `Unloaded` and re-check
    `BindingContext` inside `Dispatcher.Dispatch`; a stray callback after teardown would call
    `GetOrCreatePtzController()` on a disposed VM and open a new VISCA socket.

Confirmed by inspection: no test references `PtzPresetNumber` or the parameterless preset
commands (safe removal); `IPtzController.cs:30,33` signatures match plan §10; `_timeProvider`
/`_dispatcher` exist (`ViewerViewModel.cs:36-37`); every `DynamicResource` key in the plan exists
in `Resources/Styles/Colors.xaml`; `IsNotNullConverter` is app-scoped (`Styles.xaml:116`).

### 2026-09-04 — #339 PTZ over VISCA-over-IP (raw TCP) — plan.md/tasks.md T1–T26

**APPROVE-WITH-CHANGES overall.** No violation of Architecture Rules 1–6 or `docs/architecture.md`
Dependency Rules 1–6. Binding decisions:

- **VISCA stack in `src/Core/Features/Ptz/`: APPROVED, and it is mandatory, not preferential.**
  `tests/MauiApp.Tests/NdiForAndroid.Tests.csproj:27` references **only** `src/Core`, so a transport
  in `src/MauiApp` cannot be covered by the PR CI unit-test job. Rule 2 / Dependency Rules 4–5 are
  about **NDI SDK types and `[DllImport("ndi")]`**, not all networking. `NetworkReachability.cs`
  sits in `src/MauiApp/NdiBridge/` because it serves the NDI discovery bridge — not a precedent
  against Core-hosted BCL sockets. New standing rule to record in `docs/architecture.md`:
  *Core may use BCL networking (`System.Net.Sockets`) for non-NDI device-control protocols; NDI
  native interop stays bridge-only.*
- **BLOCKER — discovery clobbers the persisted override.** `SourceRepository.DiscoverAsync`
  (`src/MauiApp/Features/Sources/Repositories/SourceRepository.cs:28-34`) rebuilds `NdiSource` from
  bridge entries with defaults and calls `NdiDatabase.UpsertSourceAsync` →
  `InsertOrReplaceAsync` (`src/Core/Data/NdiDatabase.cs:197`), which **replaces the whole row**.
  Every discovery poll would null `PtzOverrideHost`/`PtzOverridePort` (and already resets
  `QualityProfile`/`PreviouslyConnected` — pre-existing bug, separate issue). The feature must add a
  targeted `ISourceRepository.SavePtzOverrideAsync(sourceId, host, port)` +
  `NdiDatabase` `UPDATE sources SET ...` and make `UpsertSourceAsync` carry forward the existing
  override columns; the ViewModel persists through the repository only (Rule 1 preserved).
- **`ViscaPtzController` must serialize commands** (`SemaphoreSlim(1,1)` over connect+send+receive,
  `ConfigureAwait(false)` everywhere). One `NetworkStream` + concurrent `AsyncRelayCommand`
  invocations (button mashing; two live `ViewerViewModel` instances via
  `Func<ViewerViewModel>`, `MauiProgram.cs:129`) would interleave frames.
- **Timeouts must be constructor-injected**, not `static readonly` — required for deterministic
  loopback timeout tests.
- **Per-source selection in `ViewerViewModel` (not a new service): APPROVED** — same place the
  existing per-source `QualityProfile` restore lives (`ViewerViewModel.cs:212-222`). Reuse the
  existing `GetCachedSourcesAsync()` fetch; do **not** add a third round-trip.
- **Shared-file shaping (parallel #338): REQUIRED.** `ViewerViewModel` is already `partial`
  (`src/Core/Features/Viewer/ViewModels/ViewerViewModel.cs:24`) — all PTZ members go in
  `ViewerViewModel.Ptz.cs`, with `partial void StartPtz(NdiSource?)/StopPtz()/DisposePtz()` hooks so
  the shared file's diff is ~6 lines. XAML extracted to `PtzPanelView.xaml` +
  `PtzEndpointPanel.xaml` (ContentViews).
- **Loopback integration tests are required for CI** (product-owner gate): PR job is
  `windows-latest` (`.github/workflows/ndi-for-android-cicd.yml:21,48`); `TcpListener` on
  `IPAddress.Loopback` port 0 is CI-safe on both hosted Windows and Ubuntu runners.

## Open questions / assumptions

- Assumed this instrumentation is **permanent, low-cost diagnostics** rather than throwaway; if it
  is throwaway, it should be reverted after the soak rather than documented in `docs/architecture.md`.
