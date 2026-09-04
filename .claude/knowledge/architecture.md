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
