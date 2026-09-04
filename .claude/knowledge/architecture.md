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

- **PR #299 / this merge base does not contain the A+B navigation work** recorded in the
  2026-09-04 placement-aware verdict above. That work lives on `bugfix/336-device-test-review-fixes`
  and its descendants, which are **not** ancestors of af82cc1. So the merged `AppShell.xaml.cs`
  legitimately still has inline route dictionaries (`:18-34`), no `OnNavigating` deferral, and the
  ordered-`Contains` `ParseDestination` (`:268-277`) — including the `//view-rail?reStreamSourceId=…`
  mis-parse. **This is not merge drift, but the two lines must be reconciled**: whichever of #299 or
  bugfix/336 merges second has to re-apply the other's `AppShell.xaml.cs` changes by hand.
- Assumed the soak-test instrumentation is **permanent, low-cost diagnostics** rather than throwaway;
  if it is throwaway, it should be reverted after the soak rather than documented in
  `docs/architecture.md`.
