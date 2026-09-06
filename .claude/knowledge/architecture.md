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

### 2026-09-06 — #384 slice 2 plan ("retire the modal; full screen in place")

**REVISE — design unchanged and correctly implemented in shape; six required changes, four blocking.**
Do not re-open (a)/(d)/(e)/(g).2. The mechanism swap is faithful: `ViewerView.xaml.cs`,
`AdaptiveShellStateViewModel.cs`, `AndroidImmersiveModeService.cs`, `MauiProgram.cs:149-153`, both
XAML id additions and the `docs/architecture.md:137-138` correction were checked against the live
files and every "replace whole file" snippet carries namespace, usings and surviving XML docs — the
2026-09-06 self-containment rule is met. `ApplyPlacement(ensureDestination)` is behaviour-identical
when unsuppressed, and the slice-1 interaction is clean in both directions: a suppression toggle
raises no Shell navigation (so the `Navigated` hook cannot fire from it) and the hook calls
`EnsurePrimaryDestinationVisibleAsync`, never `ApplyPlacement`, so it cannot un-suppress chrome.

**Blocking.** (1) `Detach()` must force `_viewModel.IsFullScreen = false`. The plan deletes
`FullScreenViewerPage.OnAppPaused` (`:72-77`, "never restored on resume") and replaces it with
nothing, then books the loss as a decision-log entry — that is a behaviour regression inside a
"no new behaviour" slice, and design (b) requires the force-exit. Put it in `Detach`, not in an
`AppPaused` subscription: the ordering of `OnDisappearing` vs `AppPaused` is not guaranteed.
(2) `PrimaryTabBar` is the `<TabBar>` Shell item (`AppShell.xaml:73`); suppression sets
`IsVisible=false` on `Shell.CurrentItem` in the *primary* phone-portrait path while four rail
`FlyoutItem`s stay visible. The slice-1 verdict recorded this as device-verify when it only hit the
rotation path; it is now the default path, and a `CurrentItem` re-point destroys the pushed
`ViewerPage`. Add the page-scoped `Shell.SetTabBarIsVisible(_page, !isFullScreen)` in `ApplyChrome`
(+ restore in `Detach`) as the tab-bar mechanism; `IsChromeSuppressed` keeps driving the rail, which
the attached property cannot reach. (3) The tablet pane does not go full *window*:
`SourceListPage.xaml:25-33` is a `ColumnSpan=2` header row above the pane, so collapsing `ListColumn`
alone leaves a band over the video — needs an `x:Name` + `IsVisible` toggle, i.e. the XAML edit the
plan rules out. (4) The e2e races the 3 s auto-hide (`ViewerViewModel.FullScreen.cs:10,35-41`;
`FullScreenControlsOverlay.xaml:7`): `IsFullScreen => IsPresent(ViewerFullScreenExit)` reports false
while still full screen, so `ExitFullScreen()` times out and every `Assert.False(... IsFullScreen)`
passes vacuously. Read full screen from the absence of `viewer.stop`, and re-show the overlay
(`TapVideo()`) before touching it.

**Required, not design-blocking.** (5) `PressKeyCode(4)` must be shown to compile against
`Appium.WebDriver 5.*`; prefer `_driver.Navigate().Back()`. (6) Drop the proposed new
`.claude/knowledge/decision-log.md` — `.claude/knowledge/` is agent-owned; its item 1 disappears with
required change 1 and its item 2 is recorded here.

**Recorded, verified, no change needed:** the exit-button defect is genuinely fixed by construction
(overlay and command are now the same VM instance; `ToggleFullScreen` guards only the enter
direction). No `NotifyControlInteraction()` replacement is needed for the deleted `Loaded` hook —
`OnIsFullScreenChanged(true)` already arms the timer, and in-place entry has no modal-construction
latency. The render timer now runs continuously (`StopRendering()` dies with `PresentFullScreenAsync`).
`AppPaused` keeps a subscriber (`DiscoveryRefreshService.cs:57`). `Shell.SetNavBarIsVisible(page,true)`
on Detach is safe — neither host page sets `Shell.NavBarIsVisible`. The "leave Expanded while
pane-full-screen" case converges with no extra code via `SourceListViewModel.cs:76-77` →
`ViewerViewModel.cs:271`. With change 1 the two-live-`ViewerViewModel` race is closed by
construction rather than by lifecycle luck. Rules 1-6 hold; no slice-3 leakage.

### 2026-09-06 — #386 slice 1 plan, revision 3 (addendum to the revision-2 verdict below)

**REVISE — design APPROVED, plan text not yet developer-ready.** Four of the five required changes
land correctly and the code shape is now right; do **not** re-open the design.

**Satisfied.** (1) `LastSegment(string?)` + `route.Trim('/')` comparison — verified it converges in
**both** families and in one hop either way the ancestor-route question resolves on device: rail
`//view-rail-item/view-rail` → `view-rail` == `"//view-rail".Trim('/')`, tab `//view-tab` →
`view-tab`. The first-landscape-launch case (`CurrentItem` = `HomeRailItem`, `AppShell.xaml:32`) now
short-circuits instead of looping. (2) `try`/`catch` + `Debug.WriteLine` placed **inside**
`EnsurePrimaryDestinationVisibleAsync` around `GoToAsync`, so it also covers the pre-existing
`ApplyPlacement:225` dispatch — the preferred option, and nothing else in the method can throw, so the
`async void` lambda is de-fanged in practice. (3) `if (Navigation?.ModalStack?.Count > 0) return;`
inside the method, mirroring `OnNavigating:242`. (5) Device check A now asserts the app **settles** at
a section root in landscape (no repeating `OnAppearing`, no flicker) before the rail→push→rotate→Back
sequence. Guard polarity re-checked at both sites: `Count <= 1` at the hook is `false` when
`Navigation` is null (don't dispatch), `Count > 1` inside `Ensure` is `false` when null (do reconcile)
— correct in both directions. `ApplyPlacement` (`:212-226`) still byte-identical; still exactly the
5 (g).1 files; no slice 2/3 leakage.

**Not satisfied — required change 4.** The `IAppLifecycleService.cs` snippet is still marked *"replace
whole file"* and **still omits** the `<summary>` docs on `AppResumed`/`AppPaused`
(`IAppLifecycleService.cs:9,12`) — and it now omits the `namespace NdiForAndroid.Services;` line too.
The plan's prose claims the docs were preserved, which is worse than silence: a reviewer will believe
it is done and a Sonnet developer will apply the snippet literally. Also: `SyncNavigationOrientation`
is described as *"unchanged from prior revision"* but its body is not restated, and it does **not**
call `NotifyConfigurationChanged` today at all (`MainActivity.cs:151-158`) — that is a **new** call
site, and it is the piece that makes `IsLandscape`/`SmallestWidthDp` correct at startup and after a
backgrounded rotation. Cross-revision references are not actionable for a stateless developer.
`ParseDestination`'s rewrite is likewise described but not given, and it has a real trap: the current
body relies on `?? string.Empty` (`:313`), so `LastSegment(location)!.ToLowerInvariant()` would NRE
and violates the no-bare-`!` rule; the load-bearing comment at `:309-311` must survive.

**Rule for this repo, recorded:** a plan handed to a Sonnet developer must be **self-contained** —
every "replace whole file" snippet carries its namespace, its usings and its existing XML docs, and
every method the plan says it modifies appears verbatim. "Unchanged from the prior revision" is not a
snippet.

### 2026-09-06 — #386 slice 1 plan, revision 2 (adds the `OnShellNavigated` reconciliation)

**REVISE.** The revision closes the blocking stranding gap from revision 1 in the right place and in
the right shape — reconcile on `Navigated` when the section stack is back at its root, dispatched the
same way `ApplyPlacement` already does (`AppShell.xaml.cs:225`). Slice boundary still clean (the same
5 files from (g).1), `ApplyPlacement` (`:212-226`) still byte-identical so the `FlyoutBehavior` /
`PrimaryTabBar` swap still runs unguarded, `NotifyConfigurationChanged` fed from **both**
`OnConfigurationChanged` (`MainActivity.cs:142-149`) and `SyncNavigationOrientation` (`:151-158`,
reached from `OnCreate:63` + `OnResume:132`), `OrientationChanged` raised only on an actual change
with `SmallestWidthDp`/`IsLandscape` written first. **But the new call site turns a comparison that
was previously only a cheap optimisation into the loop guard for a hot path, and that comparison is
wrong for the rail family.** Three blocking changes.

1. **`string.Equals(currentLocation, route)` (`:334`) cannot converge on the `-rail` family →
   unbounded navigation loop.** Every rail `FlyoutItem` carries an **explicit** `Route="*-rail-item"`
   (`AppShell.xaml:33,42,51,60`); the `TabBar` (`:73`) carries none. Shell builds
   `CurrentState.Location` from shellItem/shellSection/shellContent routes and strips only implicit
   (`IMPL_`) and default (`D_FAULT_`) segments, so the rail location is `//view-rail-item/view-rail`
   while the route table (`ShellNavigationService.cs:20-24`) holds `//view-rail` — never equal. The
   file's own `ParseDestination` comment (`:309-311`) already documents multi-segment locations.
   Today that costs one redundant `GoToAsync` per placement change; with the new hook it is
   Navigated → Ensure → `GoToAsync` → Navigated → … forever, and it fires on the **first landscape
   launch** (Shell's default `CurrentItem` is `HomeRailItem`, `AppShell.xaml:32`), i.e. it breaks
   `AdaptiveNavigation_InLandscape_PlacesNavigationInTheLeftRail`. Required: compare the **last path
   segment**, reusing the idiom already in `ParseDestination` (extract a private
   `static string? LastSegment(string?)` and use it in both). Correct in both families whichever way
   the ancestor-route question resolves on device. Do **not** instead strip the `*-rail-item` routes —
   that is a fifth file and Shell needs unique item routes.
2. **The dispatched lambda is `async void` with no guard.** An exception out of `GoToAsync` kills the
   process. Same defect class as the 2026-09-04 follow-up item 3, which this file now honours at
   `:292-295`. Required: `try`/`catch` + `Debug.WriteLine` inside the lambda, or around the
   `GoToAsync` in `EnsurePrimaryDestinationVisibleAsync` (which also covers `:225`).
3. **Add a `ModalStack` guard to `EnsurePrimaryDestinationVisibleAsync`.** In slice 1
   `FullScreenViewerPage` is still live and is pushed through `Shell.Current.Navigation.PushModalAsync`
   (`ViewerView.xaml.cs:167`); Shell routes modal pushes through `GoToAsync`, so `Navigated` fires,
   and `Shell.Navigation.NavigationStack` reports the **section** stack. On the tablet two-pane path
   (full screen entered from `SourceListPage`, section stack == 1) both the new hook and the existing
   `:225` dispatch can `GoToAsync` while a modal is up and pop it — the same "rotation destroys the
   page" class this slice exists to remove. One line, protects both call sites, mirrors
   `OnNavigating:242-243`, the invariant decision (a) says must survive.

**Confirmed, no change needed:** the escape route from the chrome-less window is real — only
`FullScreenViewerPage` overrides `OnBackButtonPressed` (`:65`) and it is a modal, so a pushed
`ViewerPage`/`DiagnosticLogPage` always pops and the new hook then reconciles in one hop;
`ParseDestination` resolves both `//view-rail-item/view-rail` and `//view-rail-item/view-rail/viewer`
to `View`, so the pop raises **no** handoff and the reconciling `GoToAsync` is a no-op in
`OnNavigating` (`to == _currentPrimaryDestination`) — no `StopReceiver` on a family swap;
`Navigation?.NavigationStack?.Count <= 1` is null-safe in the correct direction at both sites;
`Configuration.SmallestScreenWidthDp` is `int` and widens, and `ConfigChanges.SmallestScreenSize` is
already declared (`MainActivity.cs:24-25`); exactly one `IAppLifecycleService` implementation and five
`Mock<>` fixtures that compile unchanged; no slice 2/3 leakage (`OnStatePropertyChanged` still listens
only to `PlacementMode`, `ApplyPlacement` gains no `ensureDestination` parameter, nothing touches
`ViewerViewModel`/`ViewerControlLayout`/`ViewerView`). `OrientationChanged` having no subscriber until
slice 3 is the accepted (g).1 boundary, not dead-code drift.

**Recorded, accepted with a known cosmetic gap:** while a detail page is pushed on the `-rail` family
in portrait the app has **no** navigation chrome at all (a single-`ShellContent` `FlyoutItem` renders
no bottom bar and the flyout is `Disabled`) — not merely a "stale route family". Escapable with one
Back, reconciled there. Device check A must assert exactly that.

**Non-blocking:** the interface replacement snippet silently drops the existing `<summary>` docs on
`AppResumed`/`AppPaused` (`IAppLifecycleService.cs:9,12`) — keep them; the new hook's `<= 1` condition
is a *reconciliation trigger*, not a visibility check, and deserves one terse comment (owner's style
call).

### 2026-09-06 — #386 slice 1 implementation plan (rotation must not destroy the pushed page)

**REVISE.** The plan is faithful to the (g)-slice-1 boundary — 4 production files + 1 test file, no
slice 2/3 leakage, `ApplyPlacement` (`AppShell.xaml.cs:212-226`) left byte-identical so the
`FlyoutBehavior`/`PrimaryTabBar` chrome swap still runs on every placement change,
`NotifyConfigurationChanged` fed from **both** `OnConfigurationChanged` and
`SyncNavigationOrientation` (the latter reached from `OnCreate:63` and `OnResume:132`, so startup
and backgrounded-rotation are both covered), and `OrientationChanged` raised only on an actual
change with `IsLandscape`/`SmallestWidthDp` written before the invoke. One blocking gap.

**Blocking — the guard's early return can strand the app with no navigation chrome.** The `-rail`
family is four separate single-`ShellContent` `FlyoutItem`s (`AppShell.xaml:32-66`); the `-tab`
family is one `TabBar` (`:73-94`). Sequence: landscape → `//view-rail` → push `viewer` → rotate to
portrait. `ApplyPlacement` sets `FlyoutBehavior = Disabled` + `PrimaryTabBar.IsVisible = true`, the
new guard suppresses `GoToAsync("//view-tab")`, but `Shell.CurrentItem` is still `ViewRailItem` — a
single-section `ShellItem` renders **no** bottom bar, and the flyout is now disabled, so there is no
rail either. Pressing Back pops to `//view-rail` and **nothing re-runs
`EnsurePrimaryDestinationVisibleAsync`** — its only caller is `ApplyPlacement`
(`AppShell.xaml.cs:225`), which only fires on a `PlacementMode` change. The user is left on a
chrome-less `SourceListPage` with no route to Home/Stream/Settings until they rotate to landscape and
back. The 2026-09-06 (g)/slice-1 verdict accepted a stale route family only *"until the user pops
back to a section root"*; the plan never implements that reconciliation. Required: re-run the check
when the section stack returns to its root (dispatched, e.g. at the end of `OnShellNavigated` when
`Navigation?.NavigationStack?.Count <= 1`). It converges in one hop — the second pass finds
`currentLocation == route` and returns — and `ParseDestination` already resolves `//view-rail` →
`View` from the last segment (`:313-317`).

**Device-verify, not code-fixable: `PrimaryTabBar.IsVisible = false` becomes load-bearing.** Today
it is always followed by a `GoToAsync` that moves `CurrentItem` off the `TabBar`, so its real effect
is masked. With the guard it is the *only* mechanism hiding the bottom bar in landscape while the
`TabBar` is still `CurrentItem`. If MAUI instead re-points `CurrentItem` at another visible item, the
pushed page is popped and the slice fails at its own goal. Contingency if the device check fails:
`Shell.SetTabBarIsVisible(currentPage, false)`, not reverting the guard.

**Confirmed, no change needed:** exactly one `IAppLifecycleService` implementation and no `Noop` twin
(it holds in-memory state, not an Android API); `FullScreenViewerPage.xaml.cs:41,92` uses only
`AppPaused`; all five `Mock<IAppLifecycleService>` fixtures compile unchanged against the extended
interface; `Configuration.SmallestScreenWidthDp` is read in `Platforms/Android` and crosses into Core
as a `double`, so Rule 5 holds; Rules 1–4 and 6 untouched; `Shell.Navigation.NavigationStack`
`Count > 1` is the established in-repo idiom for "a detail page is pushed"
(`ViewerPage.xaml.cs:44`).

**Recorded invariant for slice 3:** `OnResume` calls `SyncNavigationOrientation()` **before**
`NotifyResumed()` (`MainActivity.cs:132-133`), so `OrientationChanged` fires while `IsInForeground`
is still `false`. Keep that order — it is what makes `IsLandscape` correct at resume — and make
slice 3's handler tolerate it. Also: `SmallestWidthDp` defaults to `0` before the first
configuration report, and `IsCompactDevice(0)` would classify as compact; slice 3 must decide
whether `0` means "unknown".

### 2026-09-06 — #384/#383 YouTube-style in-place full screen (up-front design consult)

**APPROVE-B-WITH-CONSTRAINTS.** Approach B (retire `FullScreenViewerPage`; full screen becomes an
in-place state of the single `ViewerView`) is approved and **supersedes the 2026-09-04 #338 verdict's
third-host decision**. The #338 verdict chose the modal page explicitly to avoid a new
`AppShell`/`AdaptiveShellStateViewModel` seam; that seam is now opened deliberately, because the
modal design cannot express "landscape *is* the viewer layout" (#383) and cannot avoid a page
transition (#384). Approach A is rejected as a fallback: it keeps two `ViewerView` instances, two
render timers and the per-entry teardown discipline, and leaves #383 routed through the same
push/pop machinery.

**Findings that change the plan (verified in code, not taken from the research map):**

- **BLOCKER, and it is a prerequisite, not a consequence: rotation currently destroys the pushed
  `ViewerPage`.** On a phone, rotating to landscape flips `NavigationPolicyService.ResolvePlacement`
  (`src/Core/Features/Navigation/Services/NavigationPolicyService.cs:25-28`) Bottom→LeftRail →
  `AdaptiveShellStateViewModel.PlacementMode` → `AppShell.OnStatePropertyChanged` → `ApplyPlacement`
  (`src/MauiApp/AppShell.xaml.cs:212-226`) → `Dispatcher.Dispatch(EnsurePrimaryDestinationVisibleAsync)`
  → `GoToAsync("//view-rail")`. An absolute Shell route **resets the section stack**, so the pushed
  `viewer` page is popped, `ViewerPage.OnDisappearing` (`ViewerPage.xaml.cs:37-49`) sees it off the
  `NavigationStack` and calls `_viewModel.Dispose()` — which does **not** call `StopReceiver()`, so the
  native receiver is left running while its page is gone. This is exactly the "Shell navigation reset
  to the Home tab/root route on every rotation, discarding the live Viewer page and its connection"
  behaviour the device analysis recorded, and it is corroborated by the 2026-09-05 #327 addendum
  (`-tab` and `-rail` are two distinct `ShellContent` instance families). **"Rotate to landscape
  enters full screen in place, no navigation, no reconnect" is unachievable until this is fixed**, and
  no ordering trick between the placement change and the orientation callback is a sound fix (both are
  queued onto the same main-thread dispatcher). Fix in **slice 1**: `EnsurePrimaryDestinationVisibleAsync`
  must return early when a detail page is pushed (`Navigation?.NavigationStack?.Count > 1`).
  `ApplyPlacement`'s chrome assignments still run, so the rail/tab swap still happens; only the
  stack-resetting `GoToAsync` is suppressed. Accepted consequence: the route *family* stays stale
  (`//view-tab/viewer` while the rail is shown) until the user pops back to a section root — visually
  correct, and strictly better than losing the page. This has app-wide effect (Home→viewer,
  Stream→diagnostic-log) and deserves its own reviewable slice + device check.

- **The orientation seam is already in Core and already in the right place — do not add one to
  `INavigationPolicyService`.** `IAppLifecycleService` (`src/Core/Services/IAppLifecycleService.cs:6,17`)
  already carries `bool IsLandscape` and `NotifyConfigurationChanged(bool)`, already called from
  `MainActivity.OnConfigurationChanged` (`MainActivity.cs:147-148`) **after**
  `bridge.UpdateFromConfiguration`, and `ViewerViewModel` already depends on it (`_lifecycle`,
  ctor `:112`, `AppResumed` subscription `:133`, unsubscribe in `Dispose` `:297`). `IsLandscape` is
  currently dead state with no event. Required: add `event Action<bool>? OrientationChanged`
  (raised inside `NotifyConfigurationChanged` **only on an actual change**), and fix
  `MainActivity.SyncNavigationOrientation` (`:151-158`) to feed it too — today it only calls the
  orientation bridge, so `IsLandscape` is wrong at startup and after a rotation performed while
  backgrounded. This gives **zero** new `ViewerViewModel` constructor parameters for the orientation
  signal and avoids a Viewer→Navigation feature coupling. `INavigationPolicyService.OrientationChanged`
  and an `IWindowSizeClassService` height signal are both **rejected** as unnecessary.

- **The phone/tablet discriminator is the device's short edge (sw), not a height class.**
  `IWindowSizeClassService` is width-only, and width misclassifies: a Galaxy Tab A9+ in **portrait**
  is ~600 dp wide = Medium, not Expanded, so "`Current != Expanded` ⇒ phone" would treat a portrait
  tablet as a phone. A *height* class is correct but is only knowable post-rotation, while the decision
  must be taken at the orientation edge. Android's own canonical discriminator —
  `Configuration.SmallestScreenWidthDp` (sw600dp) — is orientation-invariant, available directly in
  `MainActivity.OnConfigurationChanged(newConfig)` (the activity already declares
  `ConfigChanges.SmallestScreenSize`, `MainActivity.cs:24-25`), and gives S21 = 360 (phone) and
  Tab A9+ = 600 (tablet) with a wide margin. Required: `NotifyConfigurationChanged(bool isLandscape,
  double smallestWidthDp)`; the predicate is a pure Core function
  `ViewerControlLayout.IsCompactDevice(smallestWidthDp)` (`< 600`), unit-tested alongside the existing
  layout policy per the standing #342 item 3 / #370 rule. **`MinDeckWidthDp=640` / `MinDeckHeightDp=470`
  and every existing sheet/video formula stay byte-identical** — the new policy is purely additive, and
  the slice must carry an explicit "unchanged" regression test.

**Design decisions (a)–(g), binding:**

**(a) Chrome seam — an override on `AdaptiveShellStateViewModel`, not per-page Shell attached
properties.** `Shell.SetTabBarIsVisible`/`SetNavBarIsVisible` alone is **insufficient and therefore
rejected as the primary mechanism**: the rail is not Shell's TabBar, it is a custom `RailItems`
container rendered through `FlyoutBehavior.Locked` (`AppShell.xaml.cs:212-223`), and the phone-landscape
case — the whole point of #383 — *is* the rail case. Required shape: `AdaptiveShellStateViewModel`
gains `[ObservableProperty] bool _isChromeSuppressed` (name it for chrome, not "immersive" — immersive
is `IImmersiveModeService`'s system-bar concept); `IsBottomNavigationVisible` and
`IsLeftRailNavigationVisible` both `&& !IsChromeSuppressed`; `OnIsChromeSuppressedChanged` re-raises
both. `AppShell.ApplyPlacement` must read those two computed properties instead of `PlacementMode`
directly, `AppShell.OnStatePropertyChanged` must also fire on `IsChromeSuppressed`, and
`ApplyPlacement` gains an `ensureDestination` parameter so a suppression toggle never calls
`EnsurePrimaryDestinationVisibleAsync`. `PlacementMode` itself is never touched, so it snaps back
correctly. The page-local nav bar stays page-local: the host page sets
`Shell.SetNavBarIsVisible(this, !isFullScreen)`. **The `ModalStack` guard in `OnNavigating`
(`:242-243`) and last-segment `ParseDestination` (`:306-320`) must both survive unchanged** — they
protect ordinary Stream/View/Home/Settings handoffs, not just the retired modal, and full screen now
raises no Shell navigation at all, which *strengthens* the invariant rather than replacing it. The
`ModalStack.Count is not > 0` condition in `ViewerPage.OnDisappearing` (`:45`) stays as defence but
its comment must stop referring to the deleted full-screen modal.

**(b) Orientation.** New Core contract `src/Core/Services/IOrientationLockService.cs` mirroring
`IImmersiveModeService`: `void RequestLandscape(); void RequestPortrait(); void Release();`.
`Platforms/Android/Services/AndroidOrientationLockService` sets
`Platform.CurrentActivity.RequestedOrientation` to `SensorLandscape` / `Portrait`, self-marshalling
every member through `MainThread.BeginInvokeOnMainThread` exactly as `AndroidImmersiveModeService`
does; `Services/NoopOrientationLockService` is the twin; both registered in the existing
`#if ANDROID/#else` block (`MauiProgram.cs:111-131`). **`Release()` must set
`ScreenOrientation.Unspecified`, not `FullSensor`** — the research plan's `FullSensor` overrides the
user's system auto-rotate lock, which is a behavioural regression the app has never had (no
`RequestedOrientation` and no manifest `screenOrientation` exist today). **The 400 ms
`Task.Delay`-then-release heuristic is rejected**: release is event-driven — the ViewModel keeps a
`_pendingOrientation` (None/Landscape/Portrait) and calls `Release()` when the matching
`OrientationChanged` arrives, with a `TimeProvider`-driven 3 s timeout as the only fallback (testable
with `FakeTimeProvider`, no wall-clock delay).

Transitions, exhaustive. Let `compact = ViewerControlLayout.IsCompactDevice(_lifecycle.SmallestWidthDp)`:
- rotate → landscape, `compact && IsPlaying` ⇒ `IsFullScreen = true`.
- rotate → portrait, `compact` ⇒ `IsFullScreen = false`.
- full-screen button, not full screen, `compact && !IsLandscape` ⇒ `RequestLandscape()`, pending =
  Landscape; **full screen is entered by the resulting config change, not by the button** (one code
  path). On timeout, enter full screen in portrait anyway — that is YouTube's documented behaviour
  ("in portrait, entering full screen keeps the device in portrait and re-flows the overlay"), so the
  fallback is a feature, not a hack.
- full-screen button, not full screen, `!compact` (tablet) ⇒ `IsFullScreen = true` directly, **no
  orientation request ever** — tablets keep the two-pane layout and free rotation.
- exit button / Back / `Stop()` while full screen, `compact && IsLandscape` ⇒ `RequestPortrait()`,
  pending = Portrait; full screen ends when portrait arrives. Otherwise `IsFullScreen = false`.
- app pause / `Dispose()` ⇒ force `IsFullScreen = false` **and** `Release()` unconditionally; never
  request a rotation while backgrounding, never leave the device pinned.
- The resulting invariant on a compact device: **`IsFullScreen` ⟺ landscape** (while playing). That
  is what makes #383 disappear rather than be patched: `ViewerControlLayout.Choose` is never asked to
  return `Sheet` at 800×360 while playing.
- Handler body wraps its state mutations in `_dispatcher.BeginInvokeOnMainThread` (Rule 4). This is
  safe **only** because slice 1 removed the ordering dependency on `EnsurePrimaryDestinationVisibleAsync`.
- Auto-enter requires `IsPlaying`: a chromeless empty screen with no visible exit is a trap.

**(c) Overlay state machine (Core, `ViewerViewModel.FullScreen.cs`, `TimeProvider`-driven).**
`ToggleControlsOverlayCommand` replaces `ShowControlsOverlayCommand` on the single-tap gesture:
no-op when not full screen; visible ⇒ dispose the timer and hide immediately; hidden ⇒ show and re-arm.
`NotifyControlInteraction()` stays as the reset used by PTZ/quality/audio commands (#342 item 9).
Auto-hide: **2.5 s** for the minimal overlay — measurably faster than today's 3 s and consistent with
the "already hidden by t=2 s" YouTube sample once screenshot latency is accounted for, while staying
above the ~2 s floor where a reaching finger loses the target — and **5 s while the PTZ layer is
open**, because camera aiming is a sustained interaction with visual pauses longer than 2.5 s between
nudges. Both constants live in Core and are asserted with `FakeTimeProvider`. PTZ layer: new
`[ObservableProperty] bool _isPtzLayerVisible` (default **false**) + `TogglePtzLayerCommand` behind a
new camera button; the overlay's preset grid, d-pad and zoom borders rebind from `IsPtzControlActive`
to a computed `IsFullScreenPtzVisible => IsPtzControlActive && IsPtzLayerVisible` (the root Grid's
`AreControlsVisible` binding already gates them for auto-hide, so no third term). Reset to false on
leaving full screen and on `Stop()`. Back: PTZ layer open ⇒ close it, stay full screen, consume;
full screen (any overlay state) ⇒ the exit path in (b), consume; otherwise ⇒ default. A second Back
during a pending portrait request must be swallowed by the pending flag.
**Double-tap-to-toggle-full-screen is removed** (`ViewerView.xaml:46`). Reasons: two tap recognizers on
one element force MAUI to delay the single tap while it disambiguates, which directly fights the
"tap toggles the overlay immediately, no delay" behaviour the owner is asking for; rotation plus an
explicit, now-`AutomationId`'d button make it redundant; and double-tap means seek in the app being
imitated. Owner may veto — it is a user-visible removal.

**(d) The single `ViewerView`.** No new layout policy is needed for the fill: the full-screen path
already exists and is what the modal instance uses — `ChooseVideoHeightDp(..., isFullScreen: true)`
returns `-1` (`ViewerControlLayout.cs:57`), the root `Grid` `DataTrigger` drops padding/row spacing to
0 (`ViewerView.xaml:15-19`) and the video `Border` takes `Grid.RowSpan=2` (`:38-40`). Only
`Overlay.IsVisible = isFullScreen && IsModalHost` (`ViewerView.xaml.cs:119`) becomes
`= isFullScreen`. Deck and Sheet already collapse on `!isFullScreen` (`:120-121`); verify on device
that `ViewerControlSheet` returns to its peek state after an exit (its `TranslationY` survives
hiding). **The tablet pane goes full *window*, not full *pane*** — a chromeless overlay confined to
3/5 of the width is not full screen. `SourceListPage` collapses `ListColumn` to 0 and restores it, and
`ApplySizeClass` (`SourceListPage.xaml.cs:47-65`) must consult the current full-screen state so a
size-class change mid-full-screen cannot restore `2*` underneath the video. **The SkiaSharp render
timer is never stopped or restarted on a full-screen transition**: `StopRendering()` in
`PresentFullScreenAsync` (`ViewerView.xaml.cs:157`) is deleted with the method; `OnPaintSurface`
re-reads `e.Info` every paint and the existing `SizeChanged` → `UpdateLayoutVisibility` change-guard
(`:125-126`) covers the resize. One instance, one timer, one `SKBitmap`, for the whole session.

**(e) Removal plan.** Delete `FullScreenViewerPage.xaml(.cs)`; drop
`AddTransient<FullScreenViewerPage>()` (`MauiProgram.cs:149`) and the `Func<FullScreenViewerPage>`
factory (`:152-153`); delete `IsModalHostProperty`/`IsModalHost` (`ViewerView.xaml.cs:24-31`),
`_presentingFullScreen` (`:42`), `PresentFullScreenAsync` (`:155-168`) and the modal branch of
`OnViewModelPropertyChanged` (`:134-153`), which becomes a synchronous one-liner (drop `async void`).
The page's real responsibilities — immersive enter/exit, back handling, chrome, teardown — move to a
new non-visual **`src/MauiApp/Features/Viewer/Services/ViewerFullScreenChromeController`** (transient),
with `Attach(Page host, ViewerViewModel vm)` / `Detach()` / `bool HandleBackButton()`, so
`ViewerPage` and `SourceListPage` share one correct implementation instead of two symmetric copies —
this is the direct mitigation for the "#296-class chrome-not-reset" risk that doubling the host count
would otherwise create. Attach on `OnAppearing`, Detach on `OnDisappearing`, and Detach must
unconditionally clear `IsChromeSuppressed`, call `ExitImmersive()` and `Release()` — only the visible
host may own global chrome, which also settles the two-live-`ViewerViewModel` case (pane + pushed page).
`Viewer.Teardown()` loses its only caller; instead call it from `ViewerPage.OnDisappearing` in the same
branch that disposes the ViewModel (before `Dispose()`), turning dead code into deterministic
`SKBitmap` release. **Delete `AndroidImmersiveModeService.FindTopModalDialogWindow` and the second
`yield` (`:61-62, :65-81`)** — it exists solely because the full-screen page was a `DialogFragment`,
and once nothing pushes modals it can only mis-target an unrelated dialog. `KeepScreenOn` is
**unchanged**: it stays driven by `IsPlaying` (`ViewerViewModel.FullScreen.cs:29-32`) and released in
`Dispose()` (`ViewerViewModel.cs:295`) — it must not be re-scoped to the full-screen state, or the
screen sleeps during normal playback. `docs/architecture.md:137` ("three hosts … chromeless
`FullScreenViewerPage` modal") and `:138` must be corrected **in the same PR as the deletion**, not
deferred to a documenter pass: it reverses a decision that file currently records.

**(f) Tests.** Unit (`tests/MauiApp.Tests`, Core-only reference): `ViewerControlLayoutTests` gains
`IsCompactDevice` boundaries (0/359/360/599/600/601/800) **plus an explicit "existing Choose /
sheet / video formulas unchanged" regression block**; `ViewerViewModelFullScreenTests` gains
orientation-driven enter/exit gated on `IsCompactDevice`, the not-playing and tablet no-ops, the
button-in-portrait path (`RequestLandscape` exactly once, `IsFullScreen` **not** set synchronously),
event-driven release, the 3 s timeout fallback, exit-requests-portrait, app-pause force-exit +
release, `Dispose` release + unsubscribe, overlay toggle hide/show semantics, 2.5 s / 5 s timings, PTZ
layer toggle + reset, and an extension of the existing `NeverCallsStopReceiver` guard to every new
path; new `AdaptiveShellStateViewModel` tests for `IsChromeSuppressed` forcing both visibility
properties false regardless of `PlacementMode` and restoring on clear. **Known coverage gap:**
`AppShell.EnsurePrimaryDestinationVisibleAsync` and the chrome controller live in `src/MauiApp`,
which `tests/MauiApp.Tests` does not reference — slice 1's guard and the chrome restore are
**device/e2e-verified only** (same gap recorded as item 9 of the 2026-09-04 follow-up verdict).
Appium: the overlay currently has **no `AutomationId` anywhere** (`FullScreenControlsOverlay.xaml`),
so once full screen becomes the only landscape layout on a phone, the suite is blind in landscape.
Required: reuse the existing ids (`viewer.stop`, `viewer.audioToggle`, `viewer.quality.*`,
`viewer.ptz.*`) on the overlay's equivalents — they are never in the tree simultaneously with the
deck/sheet, and duplicate-id-across-hosts is already the accepted precedent (`TestIds.cs:96`) — plus
new `viewer.fullScreenToggle`, `viewer.fullScreen.exit`, `viewer.fullScreen.overlay`,
`viewer.fullScreen.camera`. `Pages/ViewerPage.cs` gains `EnterFullScreen()`, `ExitFullScreen()`,
`IsFullScreen`, `ToggleCameraLayer()`, `TapVideo()`. Tests to re-run on device before any PR into
`main`: `AppLaunchTests.AdaptiveNavigation_InLandscape_PlacesNavigationInTheLeftRail` (must still
pass — proof that chrome suppression is scoped to full screen), `AccessibilityTests` (its
portrait/landscape audit now reaches the overlay's controls), `SystemBarInsetTests`,
`ThemeRegressionTests`, plus one new `[SkippableFact]` `Viewer_RotatedToLandscape_EntersFullScreenInPlace`
(Skip.If no source, mirroring `Navigation_WatchOnASourceRow_OpensTheViewer`).
Galaxy S21 device checklist: (1) on-screen exit button actually exits — the defect the device analysis
found; (2) tap on video hides the overlay immediately, tap again shows it; (3) auto-hide at ~2.5 s,
~5 s with the PTZ layer open, and every control interaction re-arms it; (4) rotate portrait→landscape
enters full screen with **no** page transition, and portrait→landscape→portrait returns to the
embedded viewer; (5) `pidof` identical and logcat free of `onCreate`/`onDestroy` across the whole
cycle (no activity restart); (6) the NDI connection never drops — no `StopReceiver`, no reconnect
banner, frame timestamps continuous; (7) the Shell no longer resets to a tab root on rotation (slice 1);
(8) camera button reveals pad/presets/zoom, pan-down fully tappable at 48 dp, Back closes the layer
before exiting; (9) Back exits full screen, second Back leaves the viewer; (10) chrome (tab bar, rail,
nav bar, system bars) fully restored after every exit path including app pause/resume and tab switch;
(11) with system auto-rotate **off**, the button still forces landscape and exit still returns to
portrait; (12) Tab A9+: the button gives whole-window full screen from the two-pane page, the source
list is restored on exit, and rotation never auto-enters full screen.

**(g) Slices — three, ordered, each independently reviewable and device-verifiable.**
1. *Rotation must not destroy the pushed page* — the prerequisite bugfix (also fixes a live defect on
   `main`). `AppShell.xaml.cs`, `MainActivity.cs`, `IAppLifecycleService.cs`, `AppLifecycleService.cs`
   + unit tests. Could even ship straight to `main` ahead of the feature.
2. *Retire the modal; full screen in place* — mechanism swap, no new behaviour. Deletion +
   `ViewerFullScreenChromeController` + `IsChromeSuppressed` + `AppShell.ApplyPlacement` + host wiring
   + pane collapse + `docs/architecture.md`. Already fixes the "exit button does nothing" defect.
3. *Orientation-driven full screen + overlay* — `IOrientationLockService` (+ impls + DI),
   `ViewerControlLayout.IsCompactDevice`, the ViewModel state machine, toggle-to-hide, PTZ layer +
   camera button, `TestIds`, page objects, new e2e.

**Standing rules re-checked and preserved:** Rules 1/2/6 untouched (no bridge, DB or frame-lifetime
code in scope); Rule 3 holds — all numeric and state logic lands in Core (`ViewerControlLayout`,
`ViewerViewModel.FullScreen.cs`), views keep only `SizeChanged`/`PropertyChanged` plumbing, and the
one new MauiApp class is Shell/Page chrome plumbing that cannot live in Core; Rule 4 holds — the new
orientation callback marshals through `IMainThreadDispatcher`, the Android services self-marshal;
Rule 5 holds — `IOrientationLockService` is a Core contract with an Android impl and a `Noop` twin in
the existing `#if ANDROID` block; #342 item 5 holds — the overlay keeps binding `IsVisible` on inner
elements while the host sets the root from code-behind; theming holds — every new brush must be
`DynamicResource`; the #360 item 4 semantics idiom (description on the tap target) applies to the new
camera and exit buttons.

### 2026-09-05 — #361 fit-check: main e2e failures after PR #299 (run 33954513042)

**APPROVE-WITH-CHANGES.** Both root causes in the diagnosis are correct and correctly *placed*
(one test-layer, one product-layer), and no workflow-file change is warranted. Evidence re-verified
independently from the run artefacts, not taken from the diagnosis.

**Confirmed root cause 1 — test-layer (read-back, not tap, not app).** Every `settings.theme.*`
node reports `checkable="false" checked="false" clickable="false" selected="false"`, and so does
every descendant, in every page-source dump captured
(`Theme_SwitchingLightToDark_ActuallyChangesWhatIsOnScreen.xml:41,50,59` and their subtrees).
`SettingsPage.CheckedState` (`tests/MauiApp.UITests/Pages/SettingsPage.cs:147-148`) reads
`GetAttribute("checked")`, so `IsThemeSelected` can never return `true` — `TapUntilSet`
(`Pages/PageObject.cs:123-154`) therefore always reaches its final throw, with `(0 tried)` because
`FindClickableWithin` finds nothing. The screenshot shows Light *already correctly selected* at the
moment of the throw. **`TapUntilSet`'s remarks are a recorded misdiagnosis** ("Tapping the container
reported `checked='false'` afterwards, every time") — the tap works; the read does not. Fix belongs
in the page object; **do not** add AutomationIds "on the clickable elements", there are none: MAUI's
default RadioButton `ControlTemplate` exposes no clickable/checkable node at all, so that route means
replacing the framework template — not minimal.

**Confirmed root cause 2 — product-layer, and the container is the defect, not the row.**
`SettingsPage.xaml:14` hard-codes `ColumnDefinitions="220,*"` with `Padding="16"` +
`ColumnSpacing="16"`. On the CI device (1440x2560 @ 3.5, i.e. **411dp — Compact**) that leaves the
detail panel **502px = 143dp**, corroborated three ways: `settings.section.*` measured at 770px =
220dp (`accessibility-summary.txt:22-26`), the panel node bounds `[882,336][1384,2140]`
(`…xml:36-37`), and the row's own children. The 7-column row DataTemplate
(`SettingsPage.xaml:101-140`) then overflows: only `serverRow.down` (squeezed to **28px** wide),
the `●` label, `serverRow.edit` and `serverRow.delete` survive; `serverRow.endpoint`,
`serverRow.enabled` and `serverRow.up` collapse to zero area and drop out of the accessibility tree
entirely — hence `Collection: []`. This is a **user-facing defect on every phone in portrait**, not
a test artefact.

**Architectural finding (the actual drift).** The repo's established responsive idiom is a
size-class-aware layout applied as pure plumbing from page code-behind
(`SourceListPage.xaml.cs:43-65`, "Layout plumbing only", Rule 3-compliant). `SettingsPage` opts out
of it with a fixed master column, which is why a Compact window gets a 143dp detail pane. **A fixed
`220` master column is invalid at Compact and must become size-class aware.** Fixing only the row
template makes #361 green while leaving the Enabled switch and Up/Down unreachable on every phone —
the vacuous-green outcome this suite exists to prevent. If the owner defers the container fix it must
be an explicit decision plus a filed issue, not a side effect of the test going green.

**Binding constraint on that fix.** `SettingsPage` is **transient** (`MauiProgram.cs:155`) while
`IWindowSizeClassService` is a **singleton** (`:89`), and the 2026-09-05 `#327` addendum established
that tab-root pages are re-created on every visit. Copying `SourceListPage`'s constructor
subscription would leak one page per Settings visit — the exact shape blocked as item 4 of the #342
verdict. Use the page's own `OnSizeAllocated(width, height)` (already device-independent units; same
source `WindowSizeClassService` is fed from) and reuse the Material thresholds
(`WindowSizeClassService.cs:9-10`). No new ctor parameter, no subscription, no leak.

**Workflow: no change required, and the diagnosis is right about why.** `e2e-tests`' condition
(`ndi-for-android-cicd.yml:192`) fired correctly for PR #299 and went red *before* the merge; the
merge was not blocked because branch protection on `main` requires only `build-and-test`. That is a
repo-settings gap, not a YAML gap — and the workflow's own comment (`:35-43`) already records the
intent that `build-and-test`, `Build Release APK` and `Run Emulator UI Tests` all be required checks,
so restoring it is enforcement of recorded intent rather than a new decision. **Owner call; escalate,
do not bundle.** One minimal, justified CI addition is in scope: `build-and-test` never compiles
`tests/MauiApp.UITests` (it restores only `tests/MauiApp.Tests`, `:65`), and that project is plain
`net10.0` referencing only `src/Core` (`NdiForAndroid.UITests.csproj:3,13`) — so a
`dotnet build tests/MauiApp.UITests/NdiForAndroid.UITests.csproj` step is workload-free and would
have caught the 2026-09-04 blocking item (page object referencing deleted `TestIds`) on every PR.

**Scope note:** the run failed **7** tests, not the 4 named in the issue — the two theme-persistence
tests share the same read-back root cause. The `finally` cleanup in
`AppLaunchTests.cs:169-174` also silently no-oped (`RemoveServer` locates rows through the very
locator that was missing), so `10.255.255.1:45959` stayed persisted for the rest of that run.
Cleanup must not depend on the locator it compensates for.

**Not in scope, filed as follow-ups:** (a) the theme *and* accent radios announce no selection state
to TalkBack (`checkable/checked/selected` all false) — a genuine a11y defect whose fix is a product
+ UX decision (custom `ControlTemplate` or a bound semantic description), explicitly **not** to be
blended into #361's test fix; (b) `settings.accent.*` carries the identical read-back gap, currently
unexercised.

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

#### Addendum 2026-09-05 — #370 phone validation (Galaxy S21 Ultra): #367 / #368 / #369

**APPROVE-WITH-CHANGES.** All three are pure View-layer defects; no Architecture Rule 1–6 or
Dependency Rule is violated today and none is relaxed by the fix. `ViewerControlLayout.Choose`
(`src/Core/Features/Viewer/ViewerControlLayout.cs:18-21`) and its `MinDeckWidthDp=640` /
`MinDeckHeightDp=470` thresholds **stay untouched**; every *new* numeric policy is added to the same
Core class and unit-tested in `tests/MauiApp.Tests/Features/Viewer/ViewerControlLayoutTests.cs`,
which is the standing rule from the 2026-09-04 #342 verdict item 3 ("layout policy moves to a pure
Core helper; the View keeps only the `SizeChanged` wiring"). The deck (≥ 640 × 470 dp) is
geometrically unchanged by all three fixes — verified per fix below.

**#367 — camera cluster: width-triggered two-row wrap, threshold 440 dp.**
Measured from `CameraControlsView.xaml:33-87`: d-pad 3×48 + 2×8 = **160**, zoom **48**, presets
4×48 + 3×8 = **216**, outer `ColumnSpacing=8` ×2 = **16** → **440 dp**, not the ~390 dp in the issue.
Sheet content width at 360 dp = 360 − 32 (`ViewerControlSheet.xaml:34` `Padding="16,0,16,16"`) =
**328** → 112 dp overflow, i.e. presets 3/4/7/8 unreachable. Prescribed: one outer
`Grid RowDefinitions="Auto,Auto" ColumnSpacing="8" RowSpacing="0"`, preset grid named and moved
(`Grid.SetRow/SetColumn/SetColumnSpan` + `Margin="0,8,0,0"`) to row 1 when
`ViewerControlLayout.ShouldStackCameraPresets(Width)` (new Core member, `< 440`). Compact footprint
= 216 × 272 dp (160 pad row + 8 + 104 preset rows) ≤ 328 ✓. **No oscillation:** in a Fill parent
(sheet) `Width` is content-independent; in the deck's `Auto` column the two states are both fixed
points (wide desires 440 ≥ 440 → wide; stacked desires 216 < 440 → stacked) and the XAML ships in
the wide state, so the deck never switches. Deck floor re-checked: 640 − 24 padding = 616, camera
`Auto` = 440 → playback star = 164 dp ≥ the 156 dp the #360 verdict budgeted ✓. A host-set
"compact" flag on the sheet is **rejected**: at 800 × 360 the sheet is ~700 dp wide and the *wide*
row (160 dp tall) is the only one that fits the short host — the trigger must be width, not host.

**#368 — overlay toolbar: one 48 dp row, star-first columns, quality collapsed to 48 dp.**
Current `FullScreenControlsOverlay.xaml:79` `ColumnDefinitions="Auto,152,48,48,Auto,48"` needs
≈ 594 dp (chip ~170 + 152 + 48 + 48 + 72 + 48 + 40 spacing + 16 padding). Prescribed
`ColumnDefinitions="*,48,48,48,Auto,48"` = 264 fixed + 40 spacing + 16 padding = **320 dp**, so at
360 dp every required target keeps its 48 dp and 40 dp is left for a truncating status label; it
still fits at the 320 dp floor (star → 0) and shows the full `PtzStatusText` on a tablet.
Two binding decisions: (a) the endpoint chip (`:81-90`) is **deleted** — same
`OpenPtzEndpointFormCommand` as the ⋮ overflow (`:147`), already flagged as duplication in the #360
addendum; its `PtzStatusText` survives as the star-column label with the Connected/Error
`DynamicResource` triggers, so link state stays glanceable. (b) A **second toolbar row is rejected
by arithmetic**: it forces `Margin="16,16,16,112"` on the pad/zoom borders and the left column then
needs 102 (presets) + 176 (d-pad) = 278 dp inside 360 − 112 − 16 = 232 dp → the ▼ key is clipped in
landscape, which is the mode full screen is actually used in. Today's 48 dp toolbar leaves exactly
280 dp for those 278 dp, so the row height is load-bearing and `Margin="16,16,16,64"` stays valid.
A floating quality cluster is likewise rejected: top-left presets already occupy x 16..226, and a
vertical right-hand cluster (y 16..188) collides with the zoom border (y 176..296) at 360 dp height.

**#369 — landscape sheet: adaptive video height + sheet floor, both as Core policy.**
Diagnosis reproduced exactly: `ViewerPage.xaml:10` has a `Title` and no `Shell.NavBarIsVisible`, so
at 800 × 360 dp the `ViewerView` is ≈ 280 dp and its padded inner area (= `Sheet.Height`, `Grid`
`Padding=16`) ≈ **248 dp**; the fixed `HeightRequest="240"` canvas (`ViewerView.xaml:54`) + 6 dp
stroke leaves row 1 negative, and `ApplySheetHeights` (`ViewerControlSheet.xaml.cs:61-62`) yields
expanded = min(440, 0.8×248) = 198 → content viewport = 198 − 48 − 40 − 16 = **94 dp**, the "~100 dp"
in the issue. Prescribed policy (Core, unit-tested; portrait and deck values provably unchanged):
expanded = `Min(h, Max(Min(440, h·0.8), 312))`, peek = `Clamp(h·0.55, min(136,max), Min(320,
expanded))`, video = `Clamp(h − peek − 6, 96, 240)` for Sheet, `240` for Deck, `-1` for full screen.
Portrait (h ≈ 608) → 440 / 320 / 240 = today's constants exactly; landscape (h = 248) → 248 / 136 /
106, i.e. 106 + 6 + 136 = 248 → the collapsed sheet and the video tile the host with **zero**
overlap. Tablet portrait 800 × 1200 → Deck → 240 and the sheet is `IsVisible=false`; untouched.
Two required mechanics: the `SKCanvasView` `Style`+`DataTrigger` block (`ViewerView.xaml:52-61`)
must be **deleted** — a local `HeightRequest` written from `UpdateLayoutVisibility()` outranks a
style/trigger setter, so leaving both would strand full screen at the clamped height — and the
assignment must be change-guarded (it runs inside `SizeChanged`).

**Escalated, arithmetic-forced:** at 800 × 360 dp the wireframe rule "no scrolling to reach PTZ
controls" is **unsatisfiable by any layout**: 48 (handle) + 40 (tabs) + 22 (chip) + 160 (d-pad, the
floor for 48 dp targets) + 16 (padding) = **286 dp > 248 dp** of host, even with the video removed.
The prescribed baseline is therefore a `ScrollView` around the sheet's tab content (row 2 only, so
the deck is untouched; the pan recognizer lives on the row-0 handle, `ViewerControlSheet.xaml:14-17`,
so there is no gesture conflict), which never scrolls in portrait (318 dp content vs 336 dp
viewport) and absorbs 12–62 dp in landscape. Owner decision, not taken here: setting
`Shell.NavBarIsVisible="False"` on `ViewerPage` in the Sheet layout recovers ~56 dp (host 304 →
viewport 200 ≥ 186) and would make the rule hold in landscape at the cost of the Shell back
affordance; routing landscape phones to the full-screen overlay instead is the larger alternative
(a third `ViewerControlLayoutKind`) and is out of scope for a bugfix.

**P-3 (Settings compact rail) is not actionable on this branch** — the #361 `FlexLayout` rail is not
present; `SettingsPage.xaml:21-38` is still the fixed two-column `Grid`, and the only `FlexLayout`
(`:57-69`) is the accent-colour radio group. Re-run P-3 after main is merged in.

Theming, semantics and layering all hold: no colour literal is introduced (every new brush is
`DynamicResource`), all moved/added controls keep 48 dp and their `SemanticProperties.Description`
on the tap target (never on a `Label`, per the #360 addendum item 4), the overlay has no
`AutomationId`s so no UITest is affected, and the `AutomationId`s on the PTZ pad/zoom
(`CameraControlsView.xaml:37-65`) are preserved by the reflow, so `Pages/ViewerPage.cs:63-69`
keeps working.

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
