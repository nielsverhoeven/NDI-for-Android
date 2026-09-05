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

### 2026-09-04 — PR #299 merge resolution (af82cc1: main → integration/watch-and-discovery)

**APPROVE-WITH-CHANGES.** The five resolved files follow the agreed recipe exactly: main's f267cd2
design survives (Path rail icons, `IWindowInsetsService`, `IAppearanceService.AppearanceChanged`,
restore-based teardown guard), `ReapplyChrome()` is back and wired in `OnShellNavigated`, the #292
auto-save redesign is layered on top, and 891081f's manual inset/retint code is gone with no
dangling callers. One blocking item and six cleanups.

**Verified survivals (code paths traced, not taken from the resolver's summary):**

- **Rail inset is still applied, by exactly one mechanism.** `AppShell.xaml:26-28` declares
  `RailItems` with no literal padding (the comment at `:20-23` states padding comes from code);
  `AppShell.xaml.cs:108` → `ApplyRailInset()` (`:122-131`) sets it from
  `IWindowInsetsService.GetStatusBarInset()`, which returns **dp** (`AndroidWindowInsetsService.cs:23`,
  `insetPixels / density`) so it matches `Thickness`. The `status_bar_height` dimen fallback
  (`:43-44`) covers the first layout pass before `ViewCompat.GetRootWindowInsets` resolves.
  `SetRailTopInset` / `RailBaseTopPadding` / `GetStatusBarHeightPx` have zero hits repo-wide.
- **Deliberate behaviour delta vs integration:** 891081f applied `24dp base + inset`; the merged
  code applies the inset only, so the first rail item now sits flush under the status bar. This is
  main's design and therefore per the recipe, but it is device-visible — confirm on hardware.
- **Icon theming is correct in both themes with Path icons.** `UpdateRailHighlight`
  (`AppShell.xaml.cs:201-216`) sets `Icon.Fill` from `ActiveText`/`InactiveText` (`:52-53`), which
  re-resolve `ShellTabSelected`/`ShellTabUnselected` per pass. `MauiAppearanceService.UpdateResources`
  (`:166-167`) rewrites those keys **before** `AppearanceChanged` fires (`:82`), and
  `OnAppearanceChanged` (`AppShell.xaml.cs:111-116`) re-tints. Ordering is right. The light-theme
  contrast fix (`MauiAppearanceService.cs:133`, `#6E6E73`) is preserved.
- **`ReapplyChrome` still repaints after navigation** (`AppShell.xaml.cs:265`;
  `MauiAppearanceService.cs:34-52`), and ce41c8e's DrawerLayout/AppBarLayout repaint is kept
  (`:243-244`).
- **Auto-save is still guarded during teardown** (`SettingsViewModel.cs:457-492`), with the
  `_suppressAutoSave` save/restore (`:465-468`, `:482-485`) preventing re-entrant `PersistAsync`,
  and `_committedTheme`/`_committedAccent` (not the bound strings) feeding the snapshot (`:544-545`).

**Blocking change (must land before PR #299 merges):**

1. **The e2e page object still drives an Apply button that #292 deleted.**
   `tests/MauiApp.UITests/Pages/SettingsPage.cs:102,106,108` target `TestIds.SettingsApply`
   (`settings.apply`) and `TestIds.SettingsAppliedNotice` (`settings.appliedNotice`); neither id
   exists anywhere in `src/` after the merge. `ThemeRegressionTests.ApplyTheme` (`:173-174`) — the
   helper **every** test in that file uses — and `AppLaunchTests.cs:141-142` therefore fail on
   device. The unit suite is green because UITests are a separate project. Needs a teamlead
   decision: (a) adapt the page objects to auto-save and delete the two constants, or (b) add a
   transient "saved" notice bound to `TestIds.SettingsAppliedNotice` so the suite keeps a positive
   save signal instead of a timing race.

**Non-blocking, ordered:**

2. Discovery-row `Enabled` switch has **no** teardown guard: `SettingsPage.xaml:119` binds
   `IsToggled` TwoWay inside a `CollectionView` template and
   `SettingsViewModel.OnDiscoveryServerItemPropertyChanged` (`:517-528`) persists unconditionally.
   Same failure class as #300, in the one place main's guard does not reach.
3. `MauiAppearanceService.ReapplyChrome` (`:47-51`) captures `palette`/`isLight` at call time; the
   250 ms delayed pass can repaint with a stale palette if the theme changes inside that window.
   Re-read `_lastPalette`/`_lastIsLight` inside the delayed lambda.
4. Redundant state at the merge seam: `_lastValidThemeOption`/`_lastValidAccentColor`
   (`SettingsViewModel.cs:81-82`) duplicate `_committedTheme`/`_committedAccent` (`:48-49`).
5. Dead assets/constants: `Resources/Images/nav_*_dark.svg` (4 files, unreferenced since
   `ToDarkIconKey` was removed); `TestIds.SettingsDiscoveryServerEndpoint` (`:163`) and
   `TestIds.SettingsValidationError` (`:181`) bind to no element.
6. `PersistAsync` reports every failure into `DiscoveryServersValidationMessage` (`:550`, `:560`),
   which renders only inside the Discovery panel (`SettingsPage.xaml:94-96`).
7. Hygiene in `MauiAppearanceService.cs`: self-referencing using (`:2` vs namespace `:9`), unused
   `using Android.Views;` (`:5`), and `static` `_lastPalette`/`_lastIsLight` (`:23-24`) on a DI
   singleton whose `AppearanceChanged` is an instance event.

**Recorded architectural note (interim, not a blocker).** The merge leaves **two idioms for Android
chrome access** side by side: the new, rule-5-correct `IWindowInsetsService` Core contract with an
`AndroidWindowInsetsService` / `NoopWindowInsetsService` pair, and direct `#if ANDROID` view-tree
walking inside a feature service (`MauiAppearanceService.UpdateAndroidStatusBar:199-246`,
`FindView<DrawerLayout>` / `FindView<AppBarLayout>` at `:243-244`). Accepted as interim under the
rule-5 scope note above. Follow-up: move status-bar / AppBarLayout painting behind a
`Platforms/Android` service so `IWindowInsetsService` is the single pattern.

Rules 1–6, Core-stays-MAUI-free, DynamicResource-only and no-logic-in-views all hold across the six
reviewed files. The single `StaticResource` in `SettingsPage.xaml:106` is a value converter, which
is the correct usage.

### 2026-09-04 — Follow-up on the merge branch (02f600a + 0164a92 + 77593f8, chore/299-merge-main-into-integration)

**APPROVE-WITH-CHANGES.** The structural reconciliation my earlier item 1f warned about landed
cleanly: the AppShell constructor union is wired and resolvable, main's chrome and #337's navigation
handoff coexist, there is exactly **one** route table, and no `Shell.Current as AppShell` downcast
survives anywhere in the repo. The blocking UITest item (option (a)) and non-blocking item 3 are
resolved. Five new findings, one of them blocking.

**Verified (traced, not taken from the resolver's summary):**

- **Constructor union is correct and resolvable.** `AppShell(..., ShellNavigationService)`
  (`AppShell.xaml.cs:44-51`); every parameter has a registration — `AdaptiveShellStateViewModel`
  (`MauiProgram.cs:125`), `IAndroidOrientationBridge` (`:85`), `INavigationHandoffService` (`:84`),
  `IWindowSizeClassService` (`:82`), `IWindowInsetsService` (`:112`/`:121`), `IAppearanceService`
  (`:70`), `ShellNavigationService` (`:78`). `AppShell` itself is a singleton (`:136`) injected into
  `App` (`App.xaml.cs:7`). `ShellNavigationService` needs `ILogger<T>` (supplied by the MAUI host;
  precedent `DiscoveryRefreshService.cs:42`) and the singleton `AdaptiveShellStateViewModel` — no
  captive dependency, both singletons. `INavigationService` is mapped by factory to the *same*
  instance (`MauiProgram.cs:79`), not a second registration.
- **One route table.** `_landscapeRoutes`/`_portraitRoutes` live only in
  `ShellNavigationService.cs:17-33`; `AppShell.TryGetRouteForCurrentPlacement` (`:307-308`) is pure
  delegation, and repo-wide grep finds no other `//x-tab`/`//x-rail` literal in `src/` except
  `HomeViewModel` (see required change 5). Core callers use `NavigateToPrimaryAsync`
  (`SourceListViewModel.cs:145`, `DeepLinkService.cs:110`).
- **Both designs survive in AppShell.** Chrome: Path rail icons (`:127-135`), `ApplyRailInset`
  (`:108-117`) driven from `IWindowInsetsService`, `AppearanceChanged` re-tint (`:75`, `:97-102`),
  `ReapplyChrome()` in `OnShellNavigated` (`:290`). Navigation: `OnNavigating` with `base` first,
  `args.Cancelled` bail, `!args.CanCancel` fallback, deferral + 3 s cap, `try/finally` around
  `Complete()` (`:236-273`), path-only `ParseDestination` (`:293-305`),
  `EnsurePrimaryDestinationVisibleAsync` guarded by `_handoffInProgress` (`:312-313`).
  There is **no** `ModalStack` early-return in the merged file and none anywhere in the repo; the app
  never pushes modals through Shell, so nothing is missing — but confirm with the #337 author that
  none was dropped.
- **Item 3 fixed.** `MauiAppearanceService.ReapplyChrome`'s delayed pass re-reads `_lastPalette`
  (`:47-48`) at execution time. Unused usings gone (`:1-5`; `Android.Views.View` now fully qualified
  at `:248`).
- **Item 1 fixed via option (a).** `TestIds.SettingsApply`/`SettingsAppliedNotice` are gone from
  `src/Core/Testing/TestIds.cs`, `Pages/SettingsPage.cs` no longer has `Apply`/`WaitForApplied`/
  `IsApplied`, and `ThemeRegressionTests.ApplyTheme` (`:167-175`) ends at `SelectTheme`.

**Required changes, ordered:**

1. **`Settings_DiscoveryHost_SurvivesAnAppRestart` cannot pass on device — rewrite it.**
   `tests/MauiApp.UITests/AppLaunchTests.cs:140` writes into `TestIds.SettingsDiscoveryHost`, which
   after the #292 redesign is the *add-server form* Entry bound to `NewServerHost`
   (`SettingsPage.xaml:78-81`). `NewServerHost` has no `OnNewServerHostChanged` partial and is never
   persisted; `AddDiscoveryServerAsync` is the only writer and it *clears* the field
   (`SettingsViewModel.cs:222`) before `PersistAsync()` (`:225`). `TryRestart` really force-stops and
   relaunches the process (`Pages/NdiApp.cs:112-122`), so the fresh ViewModel has an empty field and
   the assertion at `AppLaunchTests.cs:149` fails. The pre-restart assertion at `:141` only echoes
   the text just typed, so the test proves nothing either way.
   Minimal correct rewrite (test-project only, no production change): after `OpenSection(Discovery)`
   set `DiscoveryHost` **and** `DiscoveryPort`, tap `TestIds.SettingsDiscoveryServerAction`
   ("Add Server"), then assert the persisted row — `EndpointDisplay` is `"{Host}:{Port}"`
   (`DiscoveryServerItem.cs:26`), surfaced on `TestIds.SettingsServerRowEndpoint`
   (`SettingsPage.xaml:110-111`); a blank port defaults to 5959 (`SettingsViewModel.cs:565,577`).
   Restart, reopen Discovery, assert the row is still listed. Add `AddDiscoveryServer(host, port)`
   and a `HasDiscoveryServer(endpoint)`/`ServerEndpoints` member to `Pages/SettingsPage.cs`.
   Two constraints on the rewrite: (a) the row appears in the CollectionView *before*
   `await PersistAsync()` (`SettingsViewModel.cs:219` vs `:225`), so "the row is visible" is not a
   save barrier — navigate away and back first (`SettingsPage.OnAppearing` re-runs `LoadCommand`,
   `SettingsPage.xaml.cs:19-20`, which reloads from the repository) and assert there; (b) the test
   must delete the row it added (`TestIds.SettingsServerRowDelete`) in a finally — a persisted bogus
   discovery server survives the app restart *and* the test session and changes NDI discovery
   behaviour for every later test.
2. **Move the handoff off the UI thread — the 3 s cap does not bound it.**
   `AppShell.RunNavigatingHandoffAsync` (`:256-261`) calls
   `HandlePrimaryDestinationChangeAsync(...).WaitAsync(3s)`; the method body runs **synchronously on
   the UI thread until its first incomplete await**, and for `from == View` the first statement is
   `_viewerBridge.StopReceiver()` (`NdiNavigationHandoffService.cs:32-33`), which does unbounded
   `Thread.Join()` on both pump threads (`NdiViewerBridge.cs:205,207`) whose capture timeouts are
   1000 ms / 500 ms (`:19-20`). So leaving the View destination blocks the main thread for up to
   ~1 s+ *while a Shell navigating deferral is held* — the exact latency/ANR risk item B5 was meant
   to close, now with a frozen navigation on top. Fix: wrap the call in `Task.Run(...)` before
   `.WaitAsync(...)`. `StopReceiver` is thread-safe by design (state lock + self-join guard) and
   bridge events already marshal via `IMainThreadDispatcher`, so this is safe. (The `SaveAsync`-
   before-`StopOutputAsync` half of B5 *is* satisfied: `NdiNavigationHandoffService.cs:37-39`.)
3. **`OnShellNavigated`'s fallback handoff is unprotected.** `AppShell.xaml.cs:281` awaits
   `HandlePrimaryDestinationChangeAsync` inside an `async void` handler with no `try/catch` — an
   exception there (SQLite, bridge) crashes the process. Item B4's guard was applied only to the
   deferral path (`:264-267`). Wrap `:279-283` in the same swallow-and-log.
4. **A timed-out handoff re-fires.** On `TimeoutException` `_currentPrimaryDestination` is left
   unchanged (`:262` is skipped), so `OnShellNavigated` (`:279`) starts a **second**, uncapped
   handoff while the first is still running. Move `_currentPrimaryDestination = to;` into the
   `finally` at `:269-272` (or set an explicit "already attempted" marker) so the reconciliation
   point cannot double-fire.
5. **`ParseDestination` still mis-parses multi-segment paths.** The query-string bleed is fixed
   (`:296-299`), but the ordered `Contains` runs over the **whole path**, so `//home-tab/viewer`
   → Home and `//stream-tab/viewer` → Stream. Deep-link `ndi://view?...` while on the Stream tab
   (`DeepLinkService.cs:105` pushes the relative `viewer` route) therefore performs **no** handoff:
   the output keeps running while a receiver starts — the resource-contention case the handoff
   exists to prevent. Fix: match the **last** path segment
   (`path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()`); `diagnostic-log` then
   correctly returns `null` and pushed pages inherit the section's destination. If the teamlead wants
   the merge PR kept minimal this can become a follow-up issue, but it is a live behavioural defect,
   not a cosmetic one — that call is theirs.

**Non-blocking, ordered:**

6. `Theme_SelectedInSettings_SurvivesAnAppRestart` (`ThemeRegressionTests.cs:112-124`) now
   force-stops the process immediately after `SelectTheme`, with no barrier against the
   fire-and-forget `_ = PersistAsync()` (`SettingsViewModel.cs:474`). Almost certainly wide enough in
   practice (a radio-button verify round-trip is ~10²–10³ ms, the SQLite write ~ms), but it is a
   genuine race where option (b) would have had a signal. Cheap fix: navigate away and back before
   restarting and assert `IsThemeSelected` — `OnAppearing` re-runs `LoadCommand`, so the read-back
   is a real repository barrier.
7. `HomeViewModel.StartViewingLastSource` (`:111`) and `ResumeOutput` (`:123`) call
   `NavigateToAsync("view-tab?sourceId=…")` / `"stream-tab?streamName=…"` — hard-coded,
   placement-unaware, and **not even absolute**: only `viewer` and `diagnostic-log` are registered as
   relative routes (`AppShell.xaml.cs:63-64`), so Shell will not resolve these and
   `ShellNavigationService.NavigateToAsync` rethrows (`:49-50`) out of an `AsyncRelayCommand`. This
   is pre-existing on both lines (not merge drift) but it violates the `docs/architecture.md`
   Navigation rule 4 sentence that these commits' own work added, and the two Home quick-action
   buttons are user-visible. Convert both to `NavigateToPrimaryAsync` and device-test.
8. `docs/architecture.md` Navigation rule 5 (`:124`) still says placement-adaptive routing "is
   handled by `AppShell` reading `AdaptiveShellStateViewModel`" — it is now `ShellNavigationService`
   (`:86-90`). Rules 2 and 4 are correctly updated; only rule 5's owner name is stale.
9. The placement→route mapping has **no** automated coverage: `tests/MauiApp.Tests` references only
   `src/Core` (`NdiForAndroid.Tests.csproj:27`), so `ShellNavigationService` is unreachable from the
   unit suite, and the UITests navigate by tapping, never by route. Keeping the table in the MAUI
   layer is the right call (Shell URIs are not a Core concern) — but note the coverage gap; if it
   ever needs tests, the mapping (not the `GoToAsync` call) is what would move to a Core-testable
   resolver.
10. Still open from the af82cc1 verdict, unchanged by these three commits: item 2 (discovery-row
    `Enabled` switch persists with no teardown guard — `SettingsViewModel.cs:517-528`,
    `SettingsPage.xaml:119`), item 4 (`_lastValidThemeOption`/`_committedTheme` duplication),
    item 5 (dead `nav_*_dark.svg`; `TestIds.SettingsDiscoveryServerEndpoint:163` and
    `TestIds.SettingsValidationError:181` bind to nothing), item 6 (`PersistAsync` reports every
    failure into `DiscoveryServersValidationMessage`, visible only in the Discovery panel), item 7
    (`MauiAppearanceService._lastPalette/_lastIsLight` are `static` on a DI singleton — now
    load-bearing for the fix, so leave them, but instance fields would be equivalent and cleaner).
    New hygiene: `ShellNavigationService.cs:4` is a self-referencing using (namespace at `:6`); and
    `Pages/SettingsPage.cs:61-67` (`DiscoveryPort`, `ValidationError`) is now referenced by no test.

Rules 1–6 hold across all files touched by the three commits: no DB access from ViewModels, no NDI
types across the bridge boundary, `SourceListPage.xaml.cs` code-behind is layout/render-loop
plumbing only, Core stays MAUI-free, all Android contracts sit behind Core interfaces with `Noop*`
counterparts, and no frame-lifetime code was touched. `DiagnosticLogPage.xaml` combines the
AutomationIds and the `Category` label with `DynamicResource` throughout (`:18-23`);
`SourceListPage.xaml`'s single `StaticResource` (`:32`) is a converter, the correct usage.

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

#### Addendum 2026-09-05 — #360 Viewer control deck polish (6 changes, post-PR #357)

**APPROVE-WITH-CHANGES.** No violation of Architecture Rules 1–6, Dependency Rules 1–7, or the
`DynamicResource`-only theming rule (no colour keys are touched). `ViewerControlLayout`
(`src/Core/Features/Viewer/ViewerControlLayout.cs:15-16`) **must not change**: the playback column
grows 148→156 dp against a 188 dp deck budget (`ViewerControlDeck.xaml:8`, 200 − 12 padding), so
`MinDeckWidthDp=640` / `MinDeckHeightDp=470` stay valid. Binding decisions:

1. **BLOCKER — eight 48 dp presets cannot fit one row in a 400 dp window.**
   `FullScreenControlsOverlay.xaml:8-34`: 8×48 + 7×spacing + 2×margin ≥ 8 + 384 + 28 = **420 dp**
   at the issue's own 4 dp floors. Required: a **2 rows × 4 columns** `Grid` (`RowDefinitions="48,48"
   ColumnDefinitions="48,48,48,48"`, Row/ColumnSpacing 6, `Margin="16"`) = 210×102 dp, mirroring the
   existing deck preset grid (`CameraControlsView.xaml:53-63`) and spec.md:33-35.
2. **Overlay presets are recall-only** (`FullScreenControlsOverlay.xaml:10-33` bind
   `PtzRecallPresetCommand`; long-press storing exists only in `CameraControlsView.xaml.cs:36-71`).
   Accessibility text must not promise long-press storing there.
3. **`SemanticProperties` (not `AutomationProperties`) is the standing repo idiom** — MAUI-native,
   sole precedent `ViewerControlSheet.xaml:23,27` + `.xaml.cs:79-80`. `AutomationProperties` appears
   only in stale doc text (`docs/features/ndi-integration-rework/plan.md:326`). No file uses
   `SemanticProperties.Hint` yet; introducing one is a deliberate new idiom, not a drift.
4. Descriptions belong on the tap target, never on a `Label` (Android maps Description →
   `ContentDescription` and suppresses the label text). The endpoint chips are tap-gesture
   `Border`s (`CameraControlsView.xaml:8-13`, `FullScreenControlsOverlay.xaml:66-74`); if device
   TalkBack shows them unfocusable, convert to a `Button` per the precedent at
   `PlaybackControlsView.xaml:12-17` rather than nesting more semantics.
5. Removing `ViewerViewModel.IsFullScreenToggleVisible` is safe (4 in-repo references, all inside
   `ViewerViewModel.FullScreen.cs`); the `[NotifyPropertyChangedFor(nameof(AreControlsVisible))]`
   attributes and the `_immersiveMode.KeepScreenOn(IsPlaying)` side effect must survive.
6. Doc drift is wider than the issue lists: the `IWindowSizeClassService` rule is stale in
   **spec.md:30,39,72-74,92 + plan.md:96-124,626-634,674**, and the "Weergave" tab name is stale in
   **docs/architecture.md:135, spec.md:41,43, plan.md:21,408,465,506, tasks.md:168,172**.
   `docs/architecture.md:135` already describes `ViewerControlLayout.Choose` correctly.

**Pre-existing defects found (not caused by #360, escalated for a scope decision):**
- `FullScreenControlsOverlay.xaml:36-62` — the d-pad and zoom `Border`s are `VerticalOptions="End"
  Margin="16"`, so their bottom 24-32 dp sits under the always-visible 48 dp toolbar (`:64`).
  The ▼ and W buttons lose half their target. Fix: `Margin="16,16,16,64"` on both borders.
- `FullScreenControlsOverlay.xaml:64` — `ColumnDefinitions="Auto,152,48,48,Auto,48"` + spacing +
  padding needs ≥ ~420 dp; the toolbar overflows a 400 dp portrait window and the chip (`:66`)
  duplicates the ⋮ overflow's `OpenPtzEndpointFormCommand` (`:126`).
- Preset **storing is unreachable with TalkBack** (long-press only) — follow-up issue owed.
- `CameraControlsView` overflows the 188 dp deck budget by ~18 dp while
  `PtzPresetStatusMessage` is shown (`:66-68`).

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

### 2026-09-04 — A slice 2 (#327) refreshed implementation plan + #351 guard

**APPROVE-WITH-CHANGES.** T013 A (hold from the slice-1 verdict is **lifted** — slice 1 is
device-verified) · T014 AWC · T015 A · #351 guard A · docs-1 A · docs-2 AWC · docs-3 A ·
decision-log.md REJECT (out of scope).

Re-verified against the worktree code: `NdiNavigationHandoffService.cs:29-40` still has the Stream
branch; `AppShell.RunNavigatingHandoffAsync` (`AppShell.xaml.cs:230-247`) caps the handoff at 3 s and
completes the deferral in `finally` — after T013 the handoff returns `Task.CompletedTask`, so the
deferral completes synchronously and the ANR/latency risk from the handoff-timing verdict disappears.
Nothing outside DI constructs the service (`MauiProgram.cs:84`, `AppShell.xaml.cs:14` interface-typed).
`HomeViewModel.CanResumeOutput` (`HomeViewModel.cs:91-100`) depends on `state.StreamName` +
`_outputBridge.IsActive`, not on the removed branch. Teardown path re-checked: `StopOutputCoreAsync`
(`NdiOutputBridge.cs:188-198`) unsubscribes `Stopped` before `StopAsync()`, `AndroidVideoCaptureSource
.StopAsync` (`:147-155`) sends `ActionStop` only when `_startedForegroundSession` — no double-fire, no
deadlock (all waits async, `Context.StartService` does not block on `OnStartCommand`).

Binding changes:
1. **T014 — the `ActionStopRequested` branch needs a self-stop fallback.** If
   `IPlatformApplication.Current?.Services.GetService<INdiOutputBridge>()` returns `null`, or the
   bridge is already inactive (double tap; a tap that lands after `ActionStop` destroyed and Android
   re-created the service), nothing ever sends `ActionStop` — `AndroidVideoCaptureSource.StopAsync`
   short-circuits on `_startedForegroundSession == false` — leaving an undismissable notification or a
   started, non-foreground, never-stopped service. Required:
   `if (bridge is null || !bridge.IsActive) { StopForeground(StopForegroundFlags.Remove); StopSelf(); }
   else bridge.StopOutputAsync().FireAndForget();`. This does not weaken the "never stop the service
   directly while capture is live" rule — the direct stop happens only when nothing is live.
2. **docs-2 — factually wrong as written.** No code clears `AppState.StreamName` on a
   notification-triggered stop: only `OutputViewModel.StopOutputCommand` (`OutputViewModel.cs:274-279`)
   writes `StreamName = null`; the notification path reaches the ViewModel only through
   `OnOutputStatusChanged` (`:132-147`), which mutates in-memory state and persists nothing (the
   persisted `IsOutputActive` is corrected later by `OnAppResumed`, `:119-120`). Document the actual
   behaviour: after a notification Stop the session stays **resumable** (`StreamName` kept,
   `IsOutputActive` corroborated `false`), unlike the in-app Stop button.
3. **decision-log.md is not #327's to create.** Feature rationale belongs in
   `docs/features/output-session-state/` + the PR/issue; architect verdicts belong in this file. A
   third, developer-written decision store in `.claude/knowledge/` will drift. Drop the step.

Accepted as-is: `IPlatformApplication.Current.Services.GetService` confined to the
`ActionStopRequested` branch (rule 5 — `MainActivity.cs:104,145` precedent, `src/MauiApp` is
Android-only); `INdiOutputBridge` is a Core contract so no NDI type crosses the boundary (rule 2);
`PendingIntent.GetService` + `Immutable | UpdateCurrent` is correct for API 26-35 (a running FGS keeps
the app out of the background-start restriction, and a notification action is allowlisted anyway); no
manifest change (the `[Service]` attribute already declares the component, `POST_NOTIFICATIONS` and
all four `FOREGROUND_SERVICE*` permissions are present). The #351 null-intent guard is minimal and
correct — a null `Intent` only ever arrives via sticky restart after process death, when neither the
MediaProjection consent nor the sender survives, and a stickily restarted service is not foreground so
there is no `startForeground` deadline to miss.

Non-blocking observations (do not expand #327 scope): (a) returning `StartCommandResult.NotSticky`
from the *start* path would remove the pointless restart entirely — the service can never resume
anything — and is the cleaner long-term shape; (b) `StopOutputAsync().FireAndForget()` from
`OnStartCommand` runs the teardown inline on the main thread until the first real await, exactly as the
in-app Stop button does on the UI thread — acceptable precedent, `Task.Run(...)` if device testing ever
shows a stall; (c) a concurrent re-stream session is not stopped by the notification action
(`StopOutputAsync` ≠ `StopReStreamAsync`) — unreachable from today's `OutputViewModel`, note only.

#### Addendum 2026-09-05 — #327 device fit-check: Stream tab does not reflect a live sender

**APPROVE-WITH-CHANGES** on "extract `CorroborateWithBridgeAsync` and call it from `LoadAsync` too".

**Tab-root page/VM lifetime — determined: assume RE-CREATION, never caching.** The observed
"Tap Start to begin broadcasting from this device." is written by exactly one line, the
`OutputViewModel` constructor (`OutputViewModel.cs:77`); nothing else in the repo writes it. Seeing
it after Stream→View→Home→Stream therefore proves a **new** `OutputViewModel` (transient,
`MauiProgram.cs:130`) inside a **new** `OutputPage` (transient, `:138`) was bound. Mechanism: MAUI's
Android Shell destroys the non-current section fragment on a tab switch, which recycles the
`ContentTemplate` cache of the `ShellContent`, so the next entry re-resolves both from DI.
Independently, `stream-rail` (`AppShell.xaml:43`) and `stream-tab` (`:72`) are two distinct
`ShellContent`s, i.e. two instance families across a rotation. **This falsifies the premise of the
2026-09-04 FIX-02 verdict** ("ShellContent `ContentTemplate` target … Shell-cached for the section
lifetime"). FIX-02's *conclusion* (don't dispose the VM from page lifecycle) is now unsupported and
must be re-decided in the tab-root VM lifetime follow-up already owed from the slice-1 verdict
(item 4) — the leak is **unbounded** (one live VM per Stream visit, each still subscribed to the
singleton bridge and to `AppResumed`, each writing `SaveAsync` from `OnAppResumed`), not ≤2. Repo
precedent for the alternative: `SourceListPage`/`SourceListViewModel` are both singletons
(`MauiProgram.cs:124,136`, "Singleton: matches ViewModel lifetime (C1)"). Out of #327 scope.

Binding changes:
1. `LoadAsync`'s `if (config is null) return;` (`OutputViewModel.cs:88-89`) must not skip
   corroboration — guard only the three config assignments, then always await the shared method.
2. Resumable predicate must match Home. Replace the `!state.IsOutputActive ||` guard
   (`:105`) with `StreamName`-only; `state.IsOutputActive` decides only whether the corrective
   `SaveAsync` runs. Otherwise, after the first corroboration clears the flag, Output falls back to
   the ctor default while `HomeViewModel.CanResumeOutput` (`HomeViewModel.cs:100`) still offers
   Resume. Same rule the #328 T006 verdict already imposed on `ApplyResumeRequestAsync`.
3. Corroborated-active branch must set `IsReStreamMode = _bridge.IsReStreamActive`
   (`INdiBridges.cs:135`, Core contract). Without it a fresh VM over a live re-stream routes Stop to
   `StopOutputAsync` (`OutputViewModel.cs:265`), which does not touch the re-stream path
   (`NdiOutputBridge.cs:175-186` vs `:498-509`) — UI says stopped, sender keeps sending.
4. **Ordering (blocking).** `OutputPage.OnAppearing` (`OutputPage.xaml.cs:26-37`) fires
   `LoadCommand` fire-and-forget, so its post-await continuation races/overwrites
   `ApplyReStreamRequest` and `ApplyResumeRequestCommand`. Already a live bug (`LoadAsync:92`
   overwrites the re-stream name with `PreferredStreamName`); adding `StatusMessage` writes makes it
   visible. Sequence them in an `async Task ApplyEntryStateAsync()` (await Load, *then* apply the
   query intent) and null the three `[QueryProperty]` fields in `finally` — one-shot consumption is
   what makes the fix correct under **both** lifetimes. Ordering/lifecycle wiring only; Rule 3 holds.
5. Keep `_dispatcher.BeginInvokeOnMainThread` **inside** the extracted method, not at call sites
   (Rule 4 for the `AppResumed` caller; free for the UI-thread `LoadAsync` caller).
6. Message split: `LoadAsync` passes `"Output active"` (identical to `StartOutputCommand`, `:228`),
   `OnAppResumed` keeps `"Output session restored."`. A page appearance renders state; only a resume
   narrates a transition.

## Open questions / assumptions

- ~~**PR #299 / this merge base does not contain the A+B navigation work**~~ — **resolved by
  02f600a** (merge of #337 into `chore/299-merge-main-into-integration`). The two lines are now
  reconciled in one `AppShell.xaml.cs`: constructor union, single route table in
  `ShellNavigationService`, `OnNavigating` deferral, path-only `ParseDestination`. See the
  2026-09-04 follow-up verdict above for the residual defects (the multi-segment `ParseDestination`
  mis-parse and the UI-thread handoff block).
- Open question for the #337 author: the merged `OnNavigating` has **no `ModalStack` early-return**
  and none exists anywhere in the repo. Confirm nothing was dropped in the resolution — the app
  currently pushes no Shell modals, so today it is a no-op either way.
- Assumed the soak-test instrumentation is **permanent, low-cost diagnostics** rather than throwaway;
  if it is throwaway, it should be reverted after the soak rather than documented in
  `docs/architecture.md`.
- Assumed this instrumentation is **permanent, low-cost diagnostics** rather than throwaway; if it
  is throwaway, it should be reverted after the soak rather than documented in `docs/architecture.md`.
- **#327 — is a notification Stop a "deliberate stop" for `AppState.StreamName`?** The owner's
  decision names the in-app Stop button and the notification action as the two deliberate stops, but
  only the former clears `StreamName`, so Home keeps offering **Resume** after a notification stop and
  `OutputViewModel` settles on "Tap Start to resume output" rather than the idle text. Harmless under
  the "resume only pre-fills, never starts" invariant, so #327 ships as-is with docs describing the
  real behaviour. If parity is wanted, the seam is the ViewModel/AppState layer (a user-requested vs.
  autonomous stop distinction on `OutputStatusChanged`) — **not** `IAppStateRepository` writes from the
  Android foreground service. Owner decision needed; follow-up issue if yes.
