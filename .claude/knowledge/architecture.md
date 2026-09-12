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

### 2026-09-13 — #384 orphaned View chip after pane full screen (fit-check)

**APPROVE-WITH-CHANGES.** The suspected mechanism is confirmed — and the UI dump disproves the
device report's own hypothesis, which matters for the fix. The fix stays inside
`ViewerFullScreenChromeController`; no new abstraction, no `AppShell` change, no XAML change.

**Mechanism (confirmed from the dump + `AppShell.xaml`, not from the report's wording).**

1. *The rail did not lose "View".* `ui_G05_rail_bug.xml` still contains all four rail entries —
   `nav.home [12,48][96,144]`, `nav.stream [12,150][96,246]`, **`nav.view [12,252][96,348]`,
   `content-desc="View, selected"`**, `nav.settings [12,354][96,450]` — inside the locked flyout
   `ScrollView [0,0][108,1200]`. So "a leftover Medium bottom-tab item that never got removed when
   the Expanded rail was rebuilt" (`logs/20260913-384-device-report.md:90-96`) is wrong: nothing was
   relocated, an **extra** bar was added. Correct that line when the fix lands.
2. *The chip is Shell's own `BottomNavigationView`.* The orphan sits in a `FrameLayout`
   `[108,1093][1920,1200]` — the full content width, x-offset by the 108px rail — and its subtree
   carries the Material ids `navigation_bar_item_icon_container`, `navigation_bar_item_labels_group`,
   `navigation_bar_item_large_label_view` (`text="View"`, `selected="true"`). The sibling `ViewPager`
   was shrunk from the window height to `[108,141][1920,1093]` to make room for it. That is
   `ShellItemRenderer`'s bottom view flipping from `Gone` to `Visible` for the **current
   `ShellItem`** — i.e. `ViewRailItem` — not a stray view.
3. *Why one item.* The rail family is four independent `FlyoutItem`s each holding exactly one
   `ShellContent` (`src/MauiApp/AppShell.xaml:32-66`), so `ViewRailItem` has a single section and its
   bar renders exactly one centred tab. The `TabBar` family is one `ShellItem` with four
   `ShellContent`s (`:73-94`).
4. *Why writing `true` is not the inverse of writing `false`.* Shell resolves tab-bar visibility via
   `ShellItem.ShowTabs`, which takes the **first explicitly set** `Shell.TabBarIsVisible` on the
   displayed page's parent chain and only falls back to "this `ShellItem` has more than one section"
   when **nobody set it** (`IsSet`-based lookup, not `GetValue`). Under the rail that fallback is
   `false`; an explicit `true` overrides it. This is not taken on trust from the framework source —
   the observed defect is only explicable this way: a bar that is absent by default appeared *because*
   the app set the attached property to `true`, on a page whose `ShellItem` has one section.
   `ViewerFullScreenChromeController.cs:101` (`ApplyChrome`) and `:71` (`Detach`) are the only two
   writers of `true` in the repo (grep over `src/`: five hits total, all in that file).
5. *Why only after an enter/exit round trip, and why it never self-heals.* The BindableProperty's own
   default is `true`, so the **first** write on `Attach → ApplyChrome(false)` equals the current value
   → no `PropertyChanged` → the renderer never recomputes → no chip on a plain visit. Entering writes
   `false` (change → recompute → hide; invisible in the rail family, nothing was shown), exiting
   writes `true` (change → recompute → `IsSet` is now `true` → **show**). After that the page-level
   value stays `true` forever: `SourceListPage` is the `ShellContent`-cached page of `ViewRailItem`,
   so Home→View round trips and rotation reuse the same instance, and every later `Attach` re-writes
   `true` with no value change. Exactly the reported "persists across navigation, rotation and
   stop/reconnect", with no exception line anywhere (`report:99-102`, item I clean).
6. *Why the portrait pushed `ViewerPage` is fine.* Same explicit `true`, but under the 4-section
   `TabBar` the default was `true` anyway, so the override is invisible.

**Decision — option (a), with two required extensions.** Never write `true`; track that this
controller took ownership and restore by `ClearValue` back to "unset", so Shell recomputes its own
per-family default (hidden under a 1-section rail `FlyoutItem`, visible under the 4-item `TabBar`).

- **Rejected: gate on `_shellState.IsBottomNavigationVisible`.** Placement can change between enter
  and exit (rotation, an unlocked orientation, background/resume), so a family-conditional write can
  write under one family and restore under the other. The failure mode is worse than today's: a
  leaked `false` permanently hides the *real* bottom bar in the `TabBar` family. The ownership flag is
  invariant to placement changes.
- **Rejected: read the pre-entry value and restore it only if it was explicitly set.** Behaviourally
  identical while this controller is the sole writer (#393), at the cost of extra state and a
  dependency on `BindableObject.IsSet`. Revisit only if a host page ever declares
  `Shell.TabBarIsVisible`/`NavBarIsVisible` in its own XAML — today none does, and the #393 ownership
  rule (recorded in `AdaptiveShellStateViewModel.cs:14-19`) is what keeps `ClearValue` safe.
- **Required extension 1 — only clear what we set.** An unconditional `ClearValue` in `Detach` would
  wipe a value the controller never owned. Guard with the flag; a page that never went full screen
  must end the session with the property untouched.
- **Required extension 2 — `NavBarIsVisible` gets the same treatment.** `:70`/`:100` write `true`
  there too. It is benign *today* only because no page sets it `false` in XAML and the property
  default is `true`. It is the identical latent clobber, it costs nothing to fix symmetrically, and
  leaving it asymmetric invites exactly this bug back on the next page that wants a custom nav bar.
- **Slice-2 gate rule preserved.** Page-scoped `Shell.SetTabBarIsVisible(page, false)` is still the
  one mechanism that hides the bottom bar, applied uniformly in both families;
  `IsChromeSuppressed` still drives only the rail (`AppShell.xaml.cs:272`). **#393 preserved:** the
  controller remains the only writer of both attached properties; `AppShell` still touches neither.

**Verbatim change — `src/MauiApp/Features/Viewer/Services/ViewerFullScreenChromeController.cs`
(four edits, nothing else in the file changes).**

Edit 1 — fields (`:22-23`).

BEFORE
```csharp
    private Page? _page;
    private ViewerViewModel? _viewModel;
```

AFTER
```csharp
    private Page? _page;
    private ViewerViewModel? _viewModel;

    /// <summary>True while this controller has written the page-scoped chrome overrides onto
    /// <see cref="_page"/>. Gates the restore so a page that never went full screen keeps
    /// Shell's own values (this controller is the app's only writer of those two attached
    /// properties — #393).</summary>
    private bool _chromeOverridden;
```

Edit 2 — `Detach`'s doc comment (`:51-56`).

BEFORE
```csharp
    /// <summary>
    /// Unconditionally releases chrome ownership: forces full screen off and abandons any
    /// in-flight orientation request (<see cref="ViewerViewModel.ForceExitFullScreen"/>), clears
    /// the shared suppression flag, exits immersive mode, releases the orientation lock, and
    /// restores the page's own nav bar and tab bar. Safe to call when not attached.
    /// </summary>
```

AFTER
```csharp
    /// <summary>
    /// Unconditionally releases chrome ownership: forces full screen off and abandons any
    /// in-flight orientation request (<see cref="ViewerViewModel.ForceExitFullScreen"/>), clears
    /// the shared suppression flag, exits immersive mode, releases the orientation lock, and
    /// reverts the page's own nav bar and tab bar to Shell's defaults (see
    /// <see cref="RestoreChrome"/>). Safe to call when not attached.
    /// </summary>
```

Edit 3 — `Detach`'s restore block (`:68-72`).

BEFORE
```csharp
        if (_page is not null)
        {
            Shell.SetNavBarIsVisible(_page, true);
            Shell.SetTabBarIsVisible(_page, true);
        }
```

AFTER
```csharp
        RestoreChrome();
```

(`RestoreChrome()` must stay **above** the `_page = null;` line — it needs the page it is clearing.)

Edit 4 — `ApplyChrome` (`:89-103`), replaced in full and followed by the two new helpers.

BEFORE
```csharp
    private void ApplyChrome(bool isFullScreen)
    {
        _shellState.IsChromeSuppressed = isFullScreen;

        if (isFullScreen)
            _immersiveMode.EnterImmersive();
        else
            _immersiveMode.ExitImmersive();

        if (_page is not null)
        {
            Shell.SetNavBarIsVisible(_page, !isFullScreen);
            Shell.SetTabBarIsVisible(_page, !isFullScreen);
        }
    }
```

AFTER
```csharp
    private void ApplyChrome(bool isFullScreen)
    {
        _shellState.IsChromeSuppressed = isFullScreen;

        if (isFullScreen)
        {
            _immersiveMode.EnterImmersive();
            OverrideChrome();
        }
        else
        {
            _immersiveMode.ExitImmersive();
            RestoreChrome();
        }
    }

    /// <summary>Hides the host page's own nav bar and tab bar, page-scoped. This is the one
    /// mechanism that hides the bottom bar; the left rail is driven separately, through
    /// <see cref="AdaptiveShellStateViewModel.IsChromeSuppressed"/>.</summary>
    private void OverrideChrome()
    {
        if (_page is null)
            return;

        Shell.SetNavBarIsVisible(_page, false);
        Shell.SetTabBarIsVisible(_page, false);
        _chromeOverridden = true;
    }

    /// <summary>
    /// Reverts to Shell's own per-page defaults by clearing the attached properties. Never writes
    /// <c>true</c>, and never touches a property this controller did not set.
    /// <para>
    /// Writing <c>true</c> is NOT the inverse of writing <c>false</c> (#384 item G). Shell resolves
    /// tab-bar visibility from the first <em>explicitly set</em> <c>Shell.TabBarIsVisible</c> on the
    /// displayed page's parent chain, and only falls back to "this ShellItem has more than one
    /// section" when nobody set it. Every rail destination is a single-<c>ShellContent</c>
    /// <c>FlyoutItem</c> (`AppShell.xaml:32-66`), so that fallback is <c>false</c> there — an
    /// explicit <c>true</c> overrode it and made Shell render a one-item BottomNavigationView
    /// ("View") under the two-pane page, which never went away again. Clearing restores the correct
    /// default in both families: hidden under the rail's FlyoutItems, visible under the 4-item
    /// <c>TabBar</c>.
    /// </para>
    /// </summary>
    private void RestoreChrome()
    {
        if (_page is not null && _chromeOverridden)
        {
            _page.ClearValue(Shell.NavBarIsVisibleProperty);
            _page.ClearValue(Shell.TabBarIsVisibleProperty);
        }

        _chromeOverridden = false;
    }
```

No other file changes. `ClearValue(BindableProperty)` is public on `BindableObject`; `Shell.NavBarIsVisibleProperty` / `Shell.TabBarIsVisibleProperty` are public statics; no new `using`.

**Device re-check (device-only gate — this controller lives in `src/MauiApp` and
`tests/MauiApp.Tests` references only `src/Core`, so no unit test can cover it).**
Run `dotnet build NdiForAndroid.sln` first; `dotnet test tests/MauiApp.Tests` must stay green (it
cannot regress, but run it).

1. **G, the defect itself.** Galaxy Tab A9+, landscape/Expanded. Watch → pane plays → ⛶ → whole-window
   full screen → exit. Take a UI dump and assert **no** node with a `navigation_bar_item_*`
   resource-id exists anywhere, the rail still lists all four entries, and the content `ViewPager`
   again reaches the window bottom (~y=1177 above the system nav bar) instead of stopping at 1093.
   Repeat enter/exit **three** times, then Home→View, then a full rotation cycle — dump again each
   time. Also dump **before** the first ⛶ of the session: no bottom item there either (that path now
   writes nothing at all).
2. **Portrait pushed `ViewerPage` (TabBar family) regression — the one this change could break.**
   Portrait, Watch from the list → `ViewerPage` → ⛶ → nav bar + bottom tabs gone → exit → the bottom
   tab bar returns with **all four** items (Home/Stream/View/Settings) and the header/nav bar returns.
   Back out to the list and confirm the tabs are still correct. Repeat the enter/exit twice. Also
   check the plain portrait View tab (no pane): bottom tabs present at all times.
3. **Background / relaunch, both families.** (a) Portrait pushed viewer, enter full screen → Home key
   → relaunch: not full screen, chrome fully intact, bottom tabs present (item H's scenario, now
   exercising `Detach → RestoreChrome` on the backgrounding `OnDisappearing` and a no-write `Attach`
   on resume). (b) The same from the landscape pane: enter full screen → Home key → relaunch → rail
   back, **no chip**, and then one more ⛶/exit cycle to prove the flag survived the round trip.
4. **Logcat** for the whole session: no `FATAL`, no `Placement reconciliation failed`, no
   `Navigation handoff failed` (item I baseline was clean, keep it clean).

**Follow-up (not part of this fix).** Nothing in the Appium suite asserts the *absence* of the bottom
bar in Expanded, which is why a page-object run would not have caught this either. A tester-owned
assertion ("in Expanded, no `navigation_bar_item_*` node exists") is cheap and would pin the invariant;
file it separately rather than bundling it here.

---

### 2026-09-12 — #393 rotation lands on Home (gate)

**APPROVE-WITH-CHANGES — nine required changes, five of them blocking. The diagnosis is correct and
device-proven, the chosen design is the right one, and the two rejected alternatives were rejected
for the right reasons. The blocking defect is scope, not shape: the plan brackets only the *two*
`GoToAsync` call sites inside `AppShell` and leaves the *three* in `ShellNavigationService`
unbracketed — which silently classifies every `NavigateToPrimaryAsync` on a rail device as "Shell's
own fallback" and bounces it back.**

Verified line by line against the live `main` checkout (`AppShell.xaml.cs` @ 367 lines,
`AppShell.xaml`, `ShellNavigationService.cs`, `MauiProgram.cs:87-88`, `MainActivity.cs:142-161`,
`NavigationPolicyService.cs:25-28`, `AdaptiveShellStateViewModel.cs:36-40`,
`tests/MauiApp.UITests/AppLaunchTests.cs`, `Pages/NavigationBar.cs:144-157`,
`Pages/ViewerPage.cs:39-52`), and against the slice-2 worktree (`…-wt/yt/src/MauiApp/AppShell.xaml.cs:228-251`)
and the #316 worktree (`…-wt/e2e316/tests/MauiApp.UITests/LifecycleTests.cs:29-46`).

Every "Before" block in the plan matches `main` byte-for-byte (fields `:26-27`, `ApplyPlacement`
`:234-248`, `OnRailItemSelected` `:252-256`, `OnNavigating` `:258-281`, `OnShellNavigated` `:303-329`,
`EnsurePrimaryDestinationVisibleAsync` `:354-366`). Self-containment (the 2026-09-06 rule) is met for
`AppShell.xaml.cs`; it is **not** met for the second file this fix now has to touch — see required
change 1, which supplies it.

---

## Judgement on the four questions put to the gate

**(1) Deferring the chrome swap while a page is pushed / a modal is open — correct, and
chrome-neutral.** The guard is evaluated *before* the flip, so `Navigation` still refers to the
**pre-swap** section — the one the pushed page actually lives in. That is precisely what the #386
guard could not do (it is evaluated after the swap, against the already-re-pointed section, which is
why the researcher found the pushed viewer lost on the emulator). No `Shell.TabBarIsVisible` /
`Shell.NavBarIsVisible` is set anywhere in `src/` (grep: zero hits), so a pushed page renders with the
bottom tab bar today in portrait. The deferral therefore makes rotation **chrome-neutral for a pushed
page**: whatever chrome the user already had in portrait is exactly what they keep in landscape — no
rail appears, nothing disappears, and no page is destroyed. That is coherent and strictly better than
today, and it answers the question directly: yes, the bottom tab bar stays on the pushed page in
landscape, and that is acceptable until pop. (On the 384 branch the landscape viewer is full screen
anyway, so the state is not even reachable there.)
The deferred swap does run on pop: `Shell.Navigated` fires with `Source == Pop` and
`NavigationStack.Count == 1`, which is the existing, proven slice-1 reconciliation trigger. Two
mechanical corrections are required (RC3/RC5): the flag must be cleared wherever the swap *does* run,
and `ApplyPlacement()` must replace — not duplicate — the trailing dispatch.

**(2) The flag held across `await GoToAsync` — sound in shape, under-scoped as written, and it must
be a counter, not a bool.** Shell raises `Navigating` synchronously inside `GoToAsync` and accumulates
`Navigated` until the call unwinds, so both events land while the marker is set — the ordering the
design depends on, and the same ordering the plan's own logcat shows for the *unsolicited* path.
`try/finally` covers exceptions. Two defects: (a) **re-entrancy** — `EnsurePrimaryDestinationVisibleAsync`
is dispatched, so it can run on a UI-thread turn taken while `OnRailItemSelected`'s `await GoToAsync`
is suspended; with a bool the inner `finally` clears the outer's marker and the outer's `Navigated`
is then misclassified. A depth counter removes that by construction. (b) **scope** — see required
change 1. Threading is clean: `OnConfigurationChanged` → `bridge.UpdateFromConfiguration` →
`ApplyPlacement` is an unbroken UI-thread chain (`MainActivity.cs:145-146`), `Dispatcher.Dispatch`
queues to the same thread, and `NotifyConfigurationChanged` (`:148`) — i.e. slice 3's
`OrientationChanged` — is fed **after** the placement bridge, so the entire #393 chain, including the
synchronous re-point, completes before any slice-3 handler sees the rotation. The marker is therefore
never touched cross-thread.

**(3) Ignoring `ShellItemChanged` without the marker — the classification is exhaustive only after
required change 1.** Full census of everything that can produce a cross-`ShellItem` move in this app:
- rail tap → `AdaptiveShellStateViewModel.SelectDestination` (`:36-40`, sets `SelectedDestination`
  **before** raising the event) → `OnRailItemSelected` → `GoToAsync` — bracketed by the plan ✔
- placement reconciliation → `EnsurePrimaryDestinationVisibleAsync` → `GoToAsync` — bracketed ✔
- bottom-tab tap → same `TabBar` item, different `ShellContent` → `ShellSectionChanged`, never
  `ShellItemChanged` ✔ (confirmed live in the plan's capture)
- the Shell flyout menu → cannot navigate: all four `FlyoutItem`s are
  `Shell.FlyoutItemIsVisible="False"` (`AppShell.xaml:34,43,52,61`) and the flyout body is a custom
  `Shell.FlyoutContent` ✔
- **`INavigationService.NavigateToPrimaryAsync` → `ShellNavigationService.cs:64` — NOT bracketed ✘**
- **`INavigationService.NavigateToAsync` → `:45` and `GoBackAsync` → `:77` — NOT bracketed ✘**

The third bullet-pair is the blocking defect. In the **rail** placement the four destinations are four
*separate* `FlyoutItem`s, so `//home-rail` → `//stream-rail` is a `ShellItemChanged`. Every one of
these is a live user path on a rail device (a tablet in either orientation, any phone in landscape):
`SourceListViewModel.cs:145` (re-stream this source), `HomeViewModel.cs:175,185` (both Home quick
actions), `DeepLinkService.cs:95` (`ndi://stream?…`). As written the plan would, for each of them,
(i) skip the handoff in `OnNavigating` — so a View→Stream move no longer calls `StopReceiver()` and a
receiver keeps running while output starts, the exact resource contention the handoff exists to
prevent — and (ii) in `OnShellNavigated` refuse to adopt the new destination and then *reconcile back
to the old one*, i.e. bounce the user out of the page they just asked for. In portrait this is
invisible (all four are `ShellContent`s of one `TabBar` → `ShellSectionChanged`), which is exactly why
the emulator repro did not surface it. `NavigateToAsync("viewer?…")` (`SourceListViewModel.cs:130`,
`HomeViewModel.cs:176`, `DeepLinkService.cs:90`) is a relative push → `Push` → unaffected, and
`GoBackAsync` is `Pop` → unaffected; they are bracketed anyway for uniformity.
One residual, accepted: the first `Navigated` of the process may arrive as `ShellItemChanged` with the
marker clear. It falls into the unsolicited branch, where `SelectedDestination` is already `Home`,
`UpdateRailHighlight(Home)` is what `BuildRailItems` already did, `ReapplyChrome()` still runs, and the
dispatched reconciliation short-circuits on `alreadyOnRoute` — inert.

**(4) The #386 guard now works — for the right reason.** Not because the guard was rewritten, but
because the eviction it was meant to survive no longer happens. The plan's §2 conclusion ("for a
pushed page the only correct fix is to never let Shell evict the current item") is correct and is the
single most important sentence in the document: `view-tab` and `view-rail` are independent
`ShellSection`s with independent stacks, so no route reconciles back to a page that lived in the other
family. Reconciling after the fact cannot work, and the plan does not try to.

---

## Required changes

**1 (BLOCKING) — `src/MauiApp/Services/ShellNavigationService.cs`: the ownership marker belongs here,
and all three of this file's `GoToAsync` calls must carry it.** Reason above. It must live on
`ShellNavigationService` and **not** on `INavigationService`: Core must not learn that Shell has a
navigation-source classification problem (2026-09-04 rule: "`//x-tab`/`//x-rail` are Shell URIs, a
MAUI-layer concern; Core must not learn Shell routing"). `AppShell` already holds the concrete type
(`_navigationService`, `AppShell.xaml.cs:24`) and already delegates route lookup to it
(`:351-352`), and `MauiProgram.cs:87-88` registers the concrete singleton with the interface mapped to
the *same* instance — so one marker is genuinely shared by both layers with no downcast (the
`Shell.Current as AppShell` downcast this log banned stays banned). Delete the plan's
`_explicitNavigationInProgress` field from `AppShell` entirely; `AppShell` reads
`_navigationService.IsExplicitNavigationInProgress`.

Add to `ShellNavigationService`, immediately after the `_portraitRoutes` dictionary (`:33`) and before
the constructor:

```csharp
    private int _explicitNavigationDepth;

    /// <summary>
    /// True while this app is inside a <c>GoToAsync</c> call it issued itself — a rail tap, the
    /// placement reconciliation, or any <see cref="INavigationService"/> call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MAUI Shell re-points <c>CurrentItem</c> to the first visible <c>ShellItem</c> on its own when
    /// the current one is hidden — which is what <c>AppShell.ApplyPlacement</c> does to
    /// <c>PrimaryTabBar</c> on a rotation (#393) — and reports it exactly like a real cross-item move
    /// (<see cref="ShellNavigationSource.ShellItemChanged"/>). The source value alone cannot tell the
    /// two apart, because the app's own rail taps and reconciliations are also cross-item moves.
    /// </para>
    /// <para>
    /// A user has no other way to cause one: a bottom-tab tap is always
    /// <see cref="ShellNavigationSource.ShellSectionChanged"/>, and the four rail
    /// <c>FlyoutItem</c>s are <c>Shell.FlyoutItemIsVisible="False"</c> behind a custom
    /// <c>Shell.FlyoutContent</c>, so the rail can only navigate through
    /// <c>AppShell.OnRailItemSelected</c>. A <c>ShellItemChanged</c> that arrives while this is
    /// <c>false</c> is therefore, by construction, Shell's own unsolicited fallback.
    /// </para>
    /// <para>
    /// Counted rather than a flag: the reconciliation is dispatched, so it can run on a UI-thread
    /// turn taken while another navigation is suspended at its <c>await</c>, and a bool would let the
    /// inner call clear the outer call's marker. Written and read on the UI thread only — every
    /// mutation brackets a <c>GoToAsync</c>, which MAUI requires to be issued from the UI thread.
    /// </para>
    /// </remarks>
    public bool IsExplicitNavigationInProgress => _explicitNavigationDepth > 0;

    /// <summary>Marks the start of a navigation this app issued. Pair with
    /// <see cref="EndExplicitNavigation"/> in a <c>finally</c>.</summary>
    public void BeginExplicitNavigation() => _explicitNavigationDepth++;

    /// <summary>Marks the end of a navigation this app issued. Never drops below zero.</summary>
    public void EndExplicitNavigation()
    {
        if (_explicitNavigationDepth > 0)
            _explicitNavigationDepth--;
    }
```

and replace the three navigating methods verbatim (namespace `NdiForAndroid.Services`, usings
unchanged):

```csharp
    public async Task NavigateToAsync(string route)
    {
        BeginExplicitNavigation();
        try
        {
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation failed for route '{Route}'", route);
            throw;
        }
        finally
        {
            EndExplicitNavigation();
        }
    }

    public async Task NavigateToPrimaryAsync(PrimaryNavDestination destination, string? queryString = null)
    {
        if (!TryGetRouteForCurrentPlacement(destination, out var route))
            throw new ArgumentOutOfRangeException(nameof(destination), destination, "No route registered for this primary destination.");

        if (!string.IsNullOrEmpty(queryString))
            route = $"{route}?{queryString}";

        BeginExplicitNavigation();
        try
        {
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation failed for route '{Route}'", route);
            throw;
        }
        finally
        {
            EndExplicitNavigation();
        }
    }

    public async Task GoBackAsync()
    {
        BeginExplicitNavigation();
        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GoBack navigation failed");
            throw;
        }
        finally
        {
            EndExplicitNavigation();
        }
    }
```

`TryGetRouteForCurrentPlacement` (`:86-90`) and the two route dictionaries are unchanged.

**2 (BLOCKING) — `src/MauiApp/AppShell.xaml.cs`, field block: one new field, not two.** Replace the
plan's Step 1 entirely. After `private bool _handoffInProgress;` (`:27`):

```csharp
    /// <summary>
    /// Set when <see cref="ApplyPlacement"/> was asked to hide <c>PrimaryTabBar</c> while a page was
    /// pushed or a modal was open and therefore skipped the chrome swap (#393). Hiding the current
    /// <c>ShellItem</c> makes Shell re-point to the first visible one, and a pushed page has no
    /// equivalent route under the rail's independent <c>ShellItem</c> family — so the eviction can
    /// never be reconciled afterwards and the swap must be deferred, not undone. Re-applied from
    /// <see cref="OnShellNavigated"/> once the section stack is back at its root.
    /// </summary>
    private bool _placementSwapDeferred;
```

**3 (BLOCKING) — `ApplyPlacement`: defer only a genuine `true → false` flip, and clear the flag
before the flip, not after.** The plan's version defers on the whole rail branch, which (a) makes the
flag sticky across a rotate-back and (b) would, after the 384 rebase, skip the
`FlyoutBehavior` assignment on a chrome-suppression toggle — leaving the rail `Locked` and visible over
a full-screen viewer on a pushed page, the primary slice-3 scenario. Gating on
`PrimaryTabBar.IsVisible` fixes both: only a `true → false` transition can evict anything, so a
repeated call while the bar is already hidden falls through and still updates `FlyoutBehavior`.
Clearing before the flip matters because the flip can re-enter `OnShellNavigated` synchronously (the
plan's own Settings-tab capture shows exactly that ordering). Replace `:234-248` with:

```csharp
    private void ApplyPlacement()
    {
        // Cleared before the flip below, never after: hiding PrimaryTabBar can drive Shell's
        // fallback navigation to completion synchronously, re-entering OnShellNavigated before
        // this method returns.
        _placementSwapDeferred = false;

        if (_stateViewModel.IsLeftRailNavigationVisible)
        {
            // Hiding PrimaryTabBar while it is still Shell.CurrentItem makes Shell fall back to the
            // first visible ShellItem (always HomeRailItem) — a navigation that cannot be stopped
            // once IsVisible flips. A pushed page, or an open #338 modal, has no equivalent route
            // under the rail's independent ShellItem family, so that eviction cannot be reconciled
            // afterwards: defer the whole swap and let OnShellNavigated re-apply it once the page is
            // popped. Only a true -> false transition can evict anything, so this is evaluated
            // against the bar's current state, and the guard reads the *pre-swap* section — the one
            // the pushed page actually lives in.
            if (PrimaryTabBar.IsVisible
                && (Navigation?.NavigationStack?.Count > 1 || Navigation?.ModalStack?.Count > 0))
            {
                _placementSwapDeferred = true;
                return;
            }

            FlyoutBehavior          = FlyoutBehavior.Locked;
            PrimaryTabBar.IsVisible = false;
        }
        else
        {
            FlyoutBehavior          = FlyoutBehavior.Disabled;
            PrimaryTabBar.IsVisible = true;
        }

        Dispatcher.Dispatch(async () => await EnsurePrimaryDestinationVisibleAsync());
    }
```

**4 (BLOCKING) — `OnNavigating`: same predicate, read from the service.** Replace `:258-281` with the
plan's Step 3, with one line changed:

```csharp
    protected override void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);

        // A modal push/pop (e.g. the full-screen viewer) does not change Shell.CurrentState and
        // must never be misclassified as a primary-destination change by ParseDestination below.
        if (Navigation?.ModalStack?.Count > 0)
            return;

        if (args.Cancelled)
            return;

        // Shell's own unsolicited item fallback (#393) — chrome plumbing, not a destination change:
        // no handoff. See ShellNavigationService.IsExplicitNavigationInProgress.
        if (args.Source == ShellNavigationSource.ShellItemChanged
            && !_navigationService.IsExplicitNavigationInProgress)
            return;

        var to = ParseDestination(args.Target?.Location?.OriginalString);
        if (to is null || to == _currentPrimaryDestination)
            return;

        if (!args.CanCancel)
            return;

        var deferral = args.GetDeferral();
        _handoffInProgress = true;

        _ = RunNavigatingHandoffAsync(to.Value, deferral);
    }
```

**5 (BLOCKING) — `OnShellNavigated`: one dispatch, not two, and the deferred re-apply goes through
`ApplyPlacement` (which dispatches for itself).** Replace `:303-329` with:

```csharp
    private async void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        // Shell's own unsolicited item fallback (#393): reconcile back to the destination that was
        // actually selected instead of adopting wherever Shell fell back to, and never run the
        // handoff or overwrite SelectedDestination for it. The rail keeps highlighting the real
        // selection, so the fallback is never visible as a selection change.
        // See ShellNavigationService.IsExplicitNavigationInProgress.
        if (e.Source == ShellNavigationSource.ShellItemChanged
            && !_navigationService.IsExplicitNavigationInProgress)
        {
            UpdateRailHighlight(_stateViewModel.SelectedDestination);
            _appearanceService.ReapplyChrome();
            Dispatcher.Dispatch(async () => await EnsurePrimaryDestinationVisibleAsync());
            return;
        }

        var to = ParseDestination(e.Current.Location.OriginalString) ?? _currentPrimaryDestination;

        if (to != _currentPrimaryDestination)
        {
            try
            {
                await _handoffService.HandlePrimaryDestinationChangeAsync(_currentPrimaryDestination, to);
                _currentPrimaryDestination = to;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation handoff failed: {ex}");
            }
        }

        _stateViewModel.SelectedDestination = to;
        UpdateRailHighlight(to);

        // MAUI re-applies per-page toolbar appearance on navigation, resetting the
        // AppBarLayout background to template defaults — restore the themed chrome (#296).
        _appearanceService.ReapplyChrome();

        if (Navigation?.NavigationStack?.Count <= 1)
        {
            // A rotation while a page was pushed (or a modal was open) deferred the chrome swap in
            // ApplyPlacement (#393); the guard has just cleared, so apply the placement the device
            // actually has now. ApplyPlacement dispatches the reconciliation itself, so this is an
            // either/or — dispatching both would queue a redundant second pass.
            if (_placementSwapDeferred && Navigation?.ModalStack?.Count is not > 0)
                ApplyPlacement();
            else
                Dispatcher.Dispatch(async () => await EnsurePrimaryDestinationVisibleAsync());
        }
    }
```

**6 (required) — the two `GoToAsync` call sites in `AppShell`: use the service's marker, and stop the
`async void` from being able to kill the process.** `OnRailItemSelected` awaits `GoToAsync` in an
`async void` with no `catch` today; the plan adds a `finally` but still no `catch`, and this log has
already ruled twice (2026-09-04 item 3, #386 revision-2 item 2) that an escaped exception there takes
the process down. Replace `:252-256` with:

```csharp
    private async void OnRailItemSelected(object? sender, PrimaryNavDestination destination)
    {
        if (!TryGetRouteForCurrentPlacement(destination, out var route))
            return;

        _navigationService.BeginExplicitNavigation();
        try { await GoToAsync(route); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Rail navigation failed: {ex}"); }
        finally { _navigationService.EndExplicitNavigation(); }
    }
```

and the tail of `EnsurePrimaryDestinationVisibleAsync` (`:364-365`) with:

```csharp
        _navigationService.BeginExplicitNavigation();
        try { await GoToAsync(route); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Placement reconciliation failed: {ex}"); }
        finally { _navigationService.EndExplicitNavigation(); }
```

The method's four guards (`_handoffInProgress`, `NavigationStack`, `ModalStack`, route lookup) and the
`LastSegment`/`route.Trim('/')` comparison are unchanged.

**7 (BLOCKING for the CI gate) — the e2e suite: fix test 1, delete test 2.**
(a) `Rotating_WhileOnANonHomeTab_KeepsTheSameDestination` as written **fails deterministically on the
CI AVD**: `NavigationBar.AnnouncesSelected` (`Pages/NavigationBar.cs:153-157`) reads `content-desc`
and requires a `", selected"` suffix, which only the **rail** emits (`AppShell.RailDescription`,
`:223-224`) — the bottom `BottomNavigationView` tab carries no such suffix, so the portrait assertion
can never be true. It also inherits the shared session's page and orientation. Replace with:

```csharp
    [SkippableFact]
    public void Rotating_WhileOnANonHomeTab_KeepsTheSameDestination() => Run(app =>
    {
        // The Appium session is shared: start from a known page and orientation rather than
        // inheriting whatever the previous test left behind (including a pushed page).
        app.ResetToHome();

        app.Navigation.GoTo(NavDestination.Stream);
        app.Output.WaitUntilVisible();

        // Landscape is the rail placement on this AVD, and the rail is the only placement that
        // announces its selection (NavigationBar.AnnouncesSelected reads the ", selected" suffix
        // the rail puts in content-desc, #345 home-nav-06); the bottom tab bar has no such suffix,
        // so in portrait the page's own id is the assertion.
        app.Rotate(ScreenOrientation.Landscape);
        app.Output.WaitUntilVisible();
        Assert.True(app.Navigation.AnnouncesSelected(NavDestination.Stream),
            "Rotating away from the Stream tab must not silently switch the selected destination to Home (#393).");

        app.Rotate(ScreenOrientation.Portrait);
        app.Output.WaitUntilVisible();
        Assert.True(app.Output.IsVisible,
            "Rotating back to portrait must keep Stream selected (#393).");
    });
```

Insert immediately after `AdaptiveNavigation_AllFourDestinations_ShowTheirOwnPage`
(`AppLaunchTests.cs:114`). `…-wt/e2e316`'s copy of `AppLaunchTests.cs` is identical to `main`'s (same
14 methods, same line numbers), so this insertion does not conflict with #316.
(b) **Delete `Rotating_WhileViewingASource_KeepsTheViewerOnScreen`.** It `Skip.If`s on
`SourceCount == 0`, i.e. it never executes on CI, and it duplicates a test that already exists and
*does* run there: `LifecycleTests.Rotation_BothWays_KeepsTheViewerAliveAndPlaying`
(`…-wt/e2e316/tests/MauiApp.UITests/LifecycleTests.cs:29-46`), which reaches a pushed `ViewerPage`
through `app.DeepLink("ndi://view?sourceId=…")` and its permanent "Connecting…" hold state — no live
NDI source required — then asserts `Viewer.IsVisible` and `Viewer.IsPlaying` after rotating both
ways. That is the #393 pushed-page regression test, and on today's `main` it must fail. `main` has no
`NdiApp.DeepLink`, no `LifecycleTests`, no `DeviceInterruptionTestBase` and no `CrashBufferGuard`
(all are new on `feature/316-e2e-entry-points-lifecycle`), so #393 cannot write the deep-link version
without importing #316's infrastructure and guaranteeing a merge conflict. The two tests are therefore
**complementary, not redundant**: #393 owns the section-root case, #316 owns the pushed-page case.
Record the dependency in the #393 PR body and run the other branch's test before either reaches
`main` — see the cross-branch section.

**8 (required) — drop plan Step 6 (`.claude/knowledge/decision-log.md`).** Third time this proposal
has come up and third refusal: `.claude/knowledge/` is agent-owned, a developer-written third decision
store will drift, and the rationale belongs in the PR/issue plus this verdicts log (2026-09-04 #327
verdict item 3; 2026-09-06 #384 slice-2 verdict item 6). The terse `(#393)` pointers in the code
comments above are the whole of what goes in the C# file.

**9 (required) — device checklist item 4 is factually wrong about the tablet, and two items are
missing.** `NavigationPolicyService.ResolvePlacement` (`:25-28`) returns `LeftRail` when
**landscape OR Expanded (> 840 dp)**. Either the Tab A9+ is Expanded in portrait — in which case
`PlacementMode` never changes on rotation, `ApplyPlacement` is never called, and the tablet cannot
exercise #393 at all — or it is Medium in portrait (600–840 dp), in which case rotating it flips
Bottom↔LeftRail exactly like a phone and items 1–3 must be run on it too. The plan asserts the former
and then claims it "exercises the *same* orientation-only trigger", which is self-contradictory.
Replace item 4 with: *"First record the device's real width: `adb -s <device> shell wm size` +
`wm density` → dp. If portrait width > 840 dp the placement never changes on rotation and #393 cannot
fire on this device — item 4 is then a no-regression check only (pane keeps playing, no jump to Home).
If portrait width is 600–840 dp the tablet flips Bottom↔LeftRail on rotation and items 1–3 must be
repeated on it, including the pushed-viewer case."* Add two items:
*"(9) On every rotation that lands on the rail from a section root, a brief Home frame may be visible
before the app reconciles back to the original tab. Confirm it is a flash and not a landing — the
destination after settling must be the original tab, the rail highlight must never move to Home, and
no page may be re-created (check `OnAppearing` logging or the discovery/output status cards)."*
*"(10) Rotate to landscape while a page is pushed (viewer or diagnostic log), then press Back: the
chrome must catch up to the rail at that moment (deferred swap re-applied), and the app must land on
the section root of the tab you started from — not Home."*

---

## Confirmed — no change needed

- **Root cause and mechanism.** Confirmed in the live file: `ApplyPlacement:239` flips
  `PrimaryTabBar.IsVisible` while the `TabBar` (`AppShell.xaml:73`) is `CurrentItem`; the four rail
  `FlyoutItem`s (`:32,41,50,59`) carry only `Shell.FlyoutItemIsVisible="False"`, which governs the
  auto-generated flyout listing, not `BaseShellItem.IsVisible`, so `HomeRailItem` is always the first
  remaining visible item; `OnShellNavigated:320` then adopts it unconditionally. The plan's logcat is
  consistent with the code in every detail, including the two different completion orderings that
  explain #321's intermittency.
- **Rejections (A-as-sketched, B, C) are all correct.** A's method-scoped flag cannot bracket an event
  that lands after the method returns — the plan proves this with its own capture. B is the #386 bug
  re-introduced deliberately when a page is pushed. C is a navigation-model change (retiring one route
  family) that would collide head-on with slice 2/3 on the same method and must not ride in a bugfix.
  Worth recording for the future: B is in fact *safe* precisely where the new defer guard does **not**
  apply (section root, no modal), so "reconcile first, then hide the `TabBar`" remains the fallback
  design if device testing ever shows the marker misclassifying — but it is not needed now and must
  not be attempted in this ticket.
- **Invariants preserved.** The #338 `ModalStack` early-return (`:264-265`) is untouched and is now
  mirrored in the new guard; `LastSegment`/`ParseDestination` (`:331-349`) are byte-identical;
  `SelectedDestination` is written only on genuine navigation; `RunNavigatingHandoffAsync` and
  `NdiNavigationHandoffService`'s "only `from == View`" semantics are untouched; `UpdateRailHighlight`,
  `RailDescription`, `BuildRailItems`, `ApplyRailInset`, `OnSizeAllocated`, `OnAppearanceChanged` and
  the constructor are untouched; `ReapplyChrome()` (#296) runs on **both** branches of
  `OnShellNavigated`; slice-1's `NavigationStack.Count <= 1` reconciliation survives and gains the
  deferred re-apply.
- **Rules 1–6.** No DB access from a ViewModel, no NDI type crosses the bridge, no business logic in a
  View (the only changes are Shell/navigation plumbing in `AppShell` + `ShellNavigationService`, both
  `src/MauiApp`), no threading rule touched (single UI thread throughout), no Android API added, no
  frame-lifetime code touched. Core stays MAUI-free: `INavigationService` is unchanged.
- **No unit-test gap.** `tests/MauiApp.Tests` references only `src/Core`, so `AppShell` and
  `ShellNavigationService` are unreachable from it — a recorded, accepted coverage gap (2026-09-04
  follow-up item 9), not new drift. `NavigationPolicyService`/`WindowSizeClassService`/
  `AdaptiveShellStateViewModel` are untouched, so their suites need no edit.
- **Threading/ordering.** `MainActivity.OnConfigurationChanged` (`:142-148`) feeds the placement bridge
  *before* `NotifyConfigurationChanged`, so the whole #393 chain precedes slice 3's
  `OrientationChanged`; `SyncNavigationOrientation` (`:151-161`, from `OnCreate:63` / `OnResume:132`)
  keeps the same order. Nothing in the fix runs off the UI thread.

## Recorded decisions

- **The "did we ask for this?" marker lives on `ShellNavigationService`, never on `INavigationService`
  and never on `AppShell` alone.** `ShellNavigationService` is already the single navigation choke
  point (one route table, 2026-09-04) and is the same singleton instance behind `INavigationService`
  (`MauiProgram.cs:87-88`), so one counter covers every app-issued `GoToAsync` in both layers with no
  `Shell.Current as AppShell` downcast and no Core leakage.
- **New standing rule:** *any new `GoToAsync` call site in `src/MauiApp` must be bracketed with
  `BeginExplicitNavigation`/`EndExplicitNavigation` (or routed through `ShellNavigationService`, which
  does it). An unbracketed cross-`ShellItem` navigation is silently treated as Shell's own fallback and
  reverted.* This belongs in `docs/architecture.md`'s Navigation section when #393 lands.
- **`PrimaryTabBar.IsVisible` may only be flipped `true → false` while the section stack is at its root
  and no modal is open.** The #386 slice-1 verdict's "device-verify, not code-fixable" paragraph is now
  **closed**: the device check failed exactly as that verdict's contingency predicted (MAUI re-points
  `CurrentItem`; the pushed page is lost). The contingency it named — page-scoped
  `Shell.SetTabBarIsVisible(currentPage, false)` — is **not** adopted, because that attached property
  is owned by `ViewerFullScreenChromeController` on the 384 branch and two owners would make an exit
  from full screen re-show the bottom bar in a rail window (slice-3 gate, binding constraint (b)).
  Defer-and-re-apply is the adopted mechanism instead.
- **#321 shares this root cause and is not closed by this fix.** Re-run its own diagnostic afterwards
  and comment the result on #321; the decision to close is the owner's.
- **Known limitation, unchanged by this fix and self-closing:** `Shell.Navigated` does not fire for
  `PopModalAsync`, so a swap deferred while the #338 modal is open is only re-applied on the next
  placement-changing event. There is no such mechanism on `main` today either, so this is not a
  regression — and slice 2 retires `FullScreenViewerPage` entirely, which removes the case. Do not
  expand this fix with a `Window.ModalPopped` hook.
- **Accepted UX consequence:** on a section-root rotation into the rail, Shell's fallback still
  happens; the app reconciles away from it within the same UI turn, so a brief Home frame may be
  visible. Verified as a flash, not a landing, by device checklist item 9.

## Cross-branch instructions

**Sequencing is hard, not advisory.** `bugfix/393-rotation-lands-on-home` is cut from `main` and its
PR targets **`main`** (there is no integration branch in flight for this work; `feature/384-…` is a
feature branch that rebases, and the slice-3 gate already records that slice 3 is not device-verifiable
or mergeable before #393 is in the same tree). Full gate before that merge: `dotnet build
NdiForAndroid.sln`, `dotnet test tests/MauiApp.Tests`, the **full** `tests/MauiApp.UITests` Appium
suite on the CI emulator with the run link on the PR, and the device checklist on both the tablet and
a phone. Never merge with a check pending.

**#393 branch — exactly this, and no more.** Required changes 1–9. `src/MauiApp/AppShell.xaml.cs` and
`src/MauiApp/Services/ShellNavigationService.cs` are the only production files; `AppLaunchTests.cs` is
the only test file. Do **not** add `ensureDestination`, do **not** add or read `IsChromeSuppressed`, do
**not** touch `AdaptiveShellStateViewModel`, and do **not** introduce page-scoped
`Shell.SetTabBarIsVisible` — all four are slice-2/3 concepts and must not leak into a bugfix (the
slice-3 gate's binding constraints (a) and (b)). Slice-3 required change 4 is therefore **not**
expressible on `main` as written: `ApplyPlacement` has no `ensureDestination` parameter and
`IsChromeSuppressed` does not exist, and adding an unused parameter plus a property that no production
code reads would be dead scaffolding. The #393 branch instead lands the *guard structure* the merged
version needs (required change 3), which is what makes the merge mechanical.

**`feature/384-youtube-style-full-screen` — rebase onto `main` after #393 merges and produce exactly
this `ApplyPlacement`.** The two branches touch the same method, so a textual conflict is certain; this
is the required resolution, verbatim (it carries #393's guard *and* slice-2's suppression *and*
slice-3 required change 4):

```csharp
    private void ApplyPlacement(bool ensureDestination = true)
    {
        // Cleared before the flip below, never after: hiding PrimaryTabBar can drive Shell's
        // fallback navigation to completion synchronously, re-entering OnShellNavigated before
        // this method returns.
        _placementSwapDeferred = false;

        if (_stateViewModel.IsLeftRailNavigationVisible)
        {
            // Hiding PrimaryTabBar while it is still Shell.CurrentItem makes Shell fall back to the
            // first visible ShellItem (always HomeRailItem) — a navigation that cannot be stopped
            // once IsVisible flips. A pushed page, or an open modal, has no equivalent route under
            // the rail's independent ShellItem family, so that eviction cannot be reconciled
            // afterwards: defer the whole swap and let OnShellNavigated re-apply it once the page is
            // popped (#393). Only a true -> false transition can evict anything, so a chrome
            // suppression toggle on a rail device still falls through and updates FlyoutBehavior.
            if (PrimaryTabBar.IsVisible
                && (Navigation?.NavigationStack?.Count > 1 || Navigation?.ModalStack?.Count > 0))
            {
                _placementSwapDeferred = true;
                return;
            }

            FlyoutBehavior          = _stateViewModel.IsChromeSuppressed ? FlyoutBehavior.Disabled : FlyoutBehavior.Locked;
            PrimaryTabBar.IsVisible = false;
        }
        else
        {
            FlyoutBehavior          = FlyoutBehavior.Disabled;
            PrimaryTabBar.IsVisible = true;
        }

        // A full-screen viewer owns the whole window; a placement reconciliation must never
        // navigate it away (the section-root/pane case — the pushed-page case is covered by the
        // NavigationStack guard inside EnsurePrimaryDestinationVisibleAsync). Same intent as
        // SourceListPage.ApplySizeClass's _isPaneFullScreen early return.
        if (ensureDestination && !_stateViewModel.IsChromeSuppressed)
            Dispatcher.Dispatch(async () => await EnsurePrimaryDestinationVisibleAsync());
    }
```

Also on the 384 branch after the rebase: `OnStatePropertyChanged` keeps its `IsChromeSuppressed` →
`ApplyPlacement(ensureDestination: false)` arm unchanged; `OnShellNavigated`'s deferred re-apply calls
`ApplyPlacement()` (default `ensureDestination: true`) unchanged; nothing else in slice 2/3 moves.
Note the composition is behaviourally correct without further edits: while a swap is deferred on a
pushed page, `FlyoutBehavior` stays `Disabled` (the portrait value), so entering full screen there
shows no rail, and the page-scoped `Shell.SetTabBarIsVisible(page,false)` still hides the bottom bar —
the full-screen chrome is correct in the deferred state, and the placement catches up on pop.

**#316 (`feature/316-e2e-entry-points-lifecycle`, worktree `…-wt/e2e316`) — complementary, no name or
id collision.** `LifecycleTests.Rotation_BothWays_KeepsTheViewerAliveAndPlaying` is the pushed-page
regression test for #393 and will go green only with #393 in the tree; `AppLaunchTests`'
`Rotating_WhileOnANonHomeTab_KeepsTheSameDestination` is the section-root one. Whichever branch merges
second must run the other's test before merging: if #393 goes first, #316 rebases and its
`LifecycleTests` run becomes the acceptance evidence for both; if #316 goes first, the #393 branch
rebases onto it and must include `LifecycleTests.Rotation_BothWays_KeepsTheViewerAliveAndPlaying` in
its PR's run link. Neither branch may relax or rename the other's test.

## Non-blocking notes

1. **A theoretically concurrent `GoToAsync` window survives.** `ApplyPlacement`'s trailing dispatch can
   in principle run while Shell's own fallback navigation is still in flight (it did not in either
   captured run — the dispatched lambda ran after `Navigated` both times). If device testing shows
   navigation wedging or a double transition, the minimal hardening is a `_shellFallbackInProgress`
   flag set in `OnNavigating`'s unsolicited branch, cleared at the top of every `OnShellNavigated`, and
   checked as a fifth early return in `EnsurePrimaryDestinationVisibleAsync` — mirroring
   `_handoffInProgress`. Do not add it pre-emptively.
2. `_currentPrimaryDestination` and `_stateViewModel.SelectedDestination` are two sources of truth for
   "where are we" (`OnAppearanceChanged` reads the former, the new unsolicited branch the latter). They
   agree in every path traced here, but the duplication is worth collapsing under its own ticket.
3. **Knowledge-base drift, for the teamlead:** `C:\repos-github\NDI-for-Android\.claude\knowledge\architecture.md`
   (main checkout) contains **none** of the #386 slice-1, #384 slice-2 or 2026-09-12 verdicts — those
   exist only in `C:\repos-github\NDI-for-Android-wt\yt\.claude\knowledge\architecture.md`. Two
   divergent copies of the architect's own log are now in flight; whoever merges the 384 branch must
   union them rather than let one overwrite the other, and this #393 entry should be appended to
   whichever copy lands on `main` first.

### 2026-09-12 — #384 slice 3 plan (gate)

**APPROVE-WITH-CHANGES — eight required changes, four of them blocking; no design decision needs
re-opening by the owner, and required change 1 is an amendment to *my own* 2026-09-06 decision (b),
not a rejection of the plan.** The plan (`384-slice3-plan-v2.md`, 2318 lines) was re-verified line by
line against the live slice-2 worktree (`C:\repos-github\NDI-for-Android-wt\yt` @ `b979b93`), not
against its own prose. Every baseline claim in its §0 is true byte-for-byte:
`ViewerViewModel.cs:134-167` (11-arg ctor), `:329-354` (`Stop()` with `IsFullScreen = false` as the
2nd statement), `:356-367` (`Dispose()`), `ViewerViewModel.FullScreen.cs:10,35-57,59-60`,
`ViewerViewModel.Ptz.cs:44-48`, `ViewerControlLayout.cs:15-16,55-62`,
`IAppLifecycleService`/`AppLifecycleService.cs:27-33`, `MainActivity.cs:129-161`,
`ViewerFullScreenChromeController.cs:23,50-68,73-80`, `ViewerPage.xaml.cs:35-63`,
`SourceListPage.xaml.cs:48-58,97-142`, `ViewerView.xaml:44-47,104`, `ViewerView.xaml.cs:100-110`,
`FullScreenControlsOverlay.xaml:81-131` (toolbar `*,48,48,48,Auto,48`; audio `Switch` and Stop with
no `AutomationId`; pad/zoom with no ids), `TestIds.cs:109-121,139`, `MauiProgram.cs:127,139,155,169`,
`Pages/ViewerPage.cs:44,51-52,86,95-96,107-111`, `AppLaunchTests.cs:79-114`, `docs/architecture.md:159`
+ `:161 ## NDI Bridge`. The four `ViewerViewModel` construction sites are exactly the four the plan
lists — `ViewerViewModelFullScreenTests.cs:47`, `ViewerViewModelTests.cs:48`,
`ViewerViewModelConnectionHintTests.cs:42`, `SourceListViewModelTests.cs:53` — grep confirms no fifth.
Self-containment (the 2026-09-06 rule) is met: every "full new file" carries namespace, usings and
XML docs, and every modified method (`Stop`, `Dispose`, the ctor, the two `OnIs*Changed` PTZ partials,
`ChooseVideoHeightDp`, the two gesture recognizers, the two DI lines) is restated verbatim and matches
the live file. The `#342` item-5 idiom holds (inner `IsVisible` bindings only; the overlay root is set
from `ViewerView.xaml.cs:102`), every new brush is `DynamicResource`, every new target is 48 dp, and
every new control has a human-language `SemanticProperties.Description`.

---

## Required changes

**1 (BLOCKING) — `§3a`: hold the orientation lock for the lifetime of full screen; release it when
full screen ends, not when the requested rotation arrives.** This corrects decision (b)'s
"calls `Release()` when the matching `OrientationChanged` arrives", which I recorded on 2026-09-06
and which is wrong on device. `OrientationChanged` is fed from `MainActivity.OnConfigurationChanged`
→ `newConfig.Orientation` (`MainActivity.cs:142-148`), i.e. the **window's** orientation — which
changed because *we* set `RequestedOrientation`, not because the user turned the phone. So on a
compact device the button path is: request `SensorLandscape` → window flips → handler releases to
`Unspecified` → the system immediately resolves `Unspecified` against a device that is still
physically portrait (or against the user's rotation lock, with auto-rotate **off**) → window flips
back to portrait → `OrientationChanged(false)` → the auto-exit branch fires. Net user-visible result:
full screen flashes and exits, and S21 checklist item 10 ("with auto-rotate off the button still
forces landscape") cannot pass. Fix, verbatim — in the new `ViewerViewModel.FullScreen.cs`, replace
the landscape-pending branch of `HandleOrientationChanged`:

```csharp
        if (_pendingOrientation == PendingOrientation.Landscape && isLandscape)
        {
            // The lock is NOT released here: OrientationChanged reports the *window's* orientation,
            // which flipped because this ViewModel asked it to, not because the user turned the
            // device. Releasing now would resolve Unspecified against a device that is still
            // physically portrait (or against the user's rotation lock with auto-rotate off) and
            // snap straight back, taking full screen with it. The lock is held for as long as full
            // screen is on and released the moment it ends (OnIsFullScreenChanged) — the same
            // behaviour YouTube has: button-entered full screen stays landscape until the user
            // exits it, rotation-entered full screen (no lock ever taken) still exits on rotation.
            ClearPendingOrientation();
            IsFullScreen = true;
            return;
        }
```

and replace the `else` branch of `OnIsFullScreenChanged` with:

```csharp
        else
        {
            _overlayAutoHideTimer?.Dispose();
            _overlayAutoHideTimer = null;
            IsControlsOverlayVisible = true;
            IsPtzLayerVisible = false;
            // Single choke point for "full screen ended" — every exit path (button, Back, Stop(),
            // the portrait rotation completing, the 3s fallback, ForceExitFullScreen) converges
            // here, so the device can never be left pinned. Release() is idempotent.
            _orientationLock.Release();
        }
```

`OnPendingOrientationTimeout`, `ForceExitFullScreen` and `BeginExitFullScreen`'s pending-landscape
cancellation keep their explicit `_orientationLock.Release()` calls (they must release even when
`IsFullScreen` does not change). Test changes that follow: rename
`OrientationChanged_AfterButtonRequestedLandscape_EntersFullScreenAndReleasesLock` to
`OrientationChanged_AfterButtonRequestedLandscape_EntersFullScreenAndKeepsTheLandscapeLock` and
replace its verify with `_orientationLockMock.Verify(o => o.Release(), Times.Never);`, and add:

```csharp
    [Fact]
    public void ExitingAfterAButtonEnteredFullScreen_ReleasesTheOrientationLock()
    {
        var sut = CreateSut();
        SetCompactDevice(isLandscape: false);
        sut.IsPlaying = true;
        sut.ToggleFullScreenCommand.Execute(null);          // requests landscape
        _lifecycleMock.Setup(l => l.IsLandscape).Returns(true);
        _lifecycleMock.Raise(l => l.OrientationChanged += null, true);   // enters, lock still held
        _orientationLockMock.Verify(o => o.Release(), Times.Never);

        sut.ToggleFullScreenCommand.Execute(null);          // requests portrait
        _lifecycleMock.Setup(l => l.IsLandscape).Returns(false);
        _lifecycleMock.Raise(l => l.OrientationChanged += null, false);  // exits

        Assert.False(sut.IsFullScreen);
        _orientationLockMock.Verify(o => o.Release(), Times.AtLeastOnce);
    }
```

Accepted consequence, to be written into S21 checklist item 4: full screen entered **by the button**
on a compact device does not exit when the device is rotated to portrait (it cannot — the window is
locked); it exits by the button or Back, which requests portrait and then releases. Full screen
entered **by physically rotating to landscape** takes no lock and still exits on rotating back. That
is YouTube's behaviour and it is the only pair of semantics achievable without an
`OrientationEventListener` (rejected: new platform machinery for a slice-3 nicety).

**2 (BLOCKING) — `§3a`: close the "playback starts while already landscape" hole in the
`IsFullScreen ⟺ landscape (while playing)` invariant.** Decision (b) records that invariant as *the*
reason #383 disappears rather than being patched ("`ViewerControlLayout.Choose` is never asked to
return `Sheet` at 800×360 while playing"). The plan's §1 item 9 covers the *button* in
already-landscape, but nothing covers playback that **starts** in landscape: `OrientationChanged` is
raised only on an actual change (`AppLifecycleService.cs:30`), so tapping Watch from the landscape
rail, a reconnect completing, or `OnAppResumed` restoring playback all leave `IsFullScreen == false`
in landscape — the exact #383 symptom, on the exact device in the checklist (S21 Ultra landscape =
914×411 dp → `WindowSizeClass.Expanded` → the `SourceListPage` pane renders `Sheet` under a 411 dp
height). Fix, verbatim — in the new `ViewerViewModel.FullScreen.cs`, replace the `OnPropertyChanged`
override and add the helper below it:

```csharp
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(IsPlaying))
        {
            _immersiveMode.KeepScreenOn(IsPlaying);
            EnterFullScreenIfPlayingInLandscapeOnCompactDevice();
        }
    }

    /// <summary>Keeps the compact-device invariant "IsFullScreen ⟺ landscape while playing"
    /// (#383/#384 design decision (b)) true on the one path no orientation event covers: playback
    /// that *starts* while the device is already in landscape — Watch tapped from the landscape
    /// rail, a reconnect completing, or a restore on resume. <see cref="IAppLifecycleService.OrientationChanged"/>
    /// only fires on an actual change, so without this the viewer renders the windowed Sheet
    /// layout in landscape, which is the #383 report itself. Never requests a rotation (the device
    /// is already landscape) and never fires while a rotation request is in flight.</summary>
    private void EnterFullScreenIfPlayingInLandscapeOnCompactDevice()
    {
        if (IsPlaying
            && !IsFullScreen
            && _pendingOrientation == PendingOrientation.None
            && _lifecycle.IsLandscape
            && ViewerControlLayout.IsCompactDevice(_lifecycle.SmallestWidthDp))
        {
            IsFullScreen = true;
        }
    }
```

Add to §7b:

```csharp
    [Fact]
    public void PlaybackStartingWhileAlreadyLandscape_OnCompactDevice_EntersFullScreenWithoutRequestingRotation()
    {
        var sut = CreateSut();
        SetCompactDevice(isLandscape: true);

        sut.IsPlaying = true;

        Assert.True(sut.IsFullScreen);
        _orientationLockMock.Verify(o => o.RequestLandscape(), Times.Never);
    }

    [Fact]
    public void PlaybackStartingWhileAlreadyLandscape_OnTablet_StaysWindowed()
    {
        var sut = CreateSut();
        _lifecycleMock.Setup(l => l.SmallestWidthDp).Returns(800);
        _lifecycleMock.Setup(l => l.IsLandscape).Returns(true);

        sut.IsPlaying = true;

        Assert.False(sut.IsFullScreen);
    }
```

Mechanical consequence in §7b, must be applied or six tests silently change meaning: in
`OrientationChanged_ToPortrait_CompactAndFullScreen_ExitsFullScreen`,
`ToggleFullScreenCommand_CompactDeviceInLandscape_RequestsPortraitAndStaysFullScreenUntilOrientationChanges`,
`OrientationChanged_AfterButtonRequestedPortrait_ExitsFullScreenAndReleasesLock`,
`PendingPortraitRequest_TimesOutAfterThreeSeconds_ExitsFullScreenAndReleasesLock`,
`AppPaused_WhileFullScreen_ForcesExitAndReleasesOrientationLock` and
`HandleBackButtonPress_DuringPendingPortrait_SwallowsSecondPress` — each does
`SetCompactDevice(isLandscape: true); sut.IsPlaying = true;` and then one
`sut.ToggleFullScreenCommand.Execute(null); // enters directly`. Delete that now-redundant "enters
directly" line in all six and put `Assert.True(sut.IsFullScreen, "playback starting in landscape on
a compact device auto-enters full screen");` in its place. (Leave
`Stop_WhileFullScreenCompactAndLandscape_StopsReceiverBeforeRequestingPortrait` alone — it sets
`SourceId` *before* `SetCompactDevice`, so `SmallestWidthDp` is still 0 when `IsPlaying` flips and
its explicit toggle is still the entry.)

**3 (BLOCKING) — `§6`, `tests/MauiApp.UITests/Pages/ViewerPage.cs`: `ExitFullScreen()` must stop
blind-tapping the video.** Decision 2 turns the single tap from *show* into *toggle*, which
invalidates the existing method's whole premise ("Taps the video first to guarantee the overlay … is
on screen", `Pages/ViewerPage.cs:102-111`). `AppLaunchTests.cs:94,99` calls `WaitUntilFullScreen()`
and then `ExitFullScreen()` immediately, so the overlay is still up: `TapVideo()` now **hides** it
and the following `Tap(viewer.fullScreenToggle)` waits 10 s and throws. The plan's "the existing test
body needs no text changes" is correct only once this is fixed. Replace the method with:

```csharp
    /// <summary>
    /// Exits full screen. The single tap on the video *toggles* the overlay since #384 slice 3, so
    /// tapping unconditionally would hide the very button this method then needs; tap only when the
    /// 2.5s/5s auto-hide has already taken the toolbar away, and re-check rather than assume.
    /// </summary>
    public void ExitFullScreen()
    {
        if (!IsPresent(TestIds.ViewerFullScreenToggle))
            TapVideo();

        if (!IsPresent(TestIds.ViewerFullScreenToggle))
            TapVideo();   // the auto-hide can fire between the check and the tap; one retry is enough

        ToggleFullScreen();
    }
```

**4 (BLOCKING) — `AppShell.xaml.cs:236-251`: a placement change must not navigate away from a
full-screen viewer.** The plan states "no changes to `AppShell.xaml.cs` … are needed for slice 3";
that is true for the phone (the pushed `ViewerPage` is protected by slice 1's
`NavigationStack.Count > 1` guard, `:360`) and false for the tablet pane, which lives at a **section
root**. Tab A9+ portrait is ~600 dp wide = `Medium` → `ResolvePlacement` returns `Bottom`
(`NavigationPolicyService.cs:25-28`), landscape returns `LeftRail`, so rotating the tablet while the
pane is full screen runs `ApplyPlacement(ensureDestination: true)` → `EnsurePrimaryDestinationVisibleAsync`
→ `GoToAsync` a different route family → `SourceListPage.OnDisappearing` → `Detach()` →
`ForceExitFullScreen()`. Tab A9+ checklist item 3 ("rotating never auto-exits full screen") therefore
cannot pass, and this is independent of #393. This is the navigation-level twin of the guard
`SourceListPage.ApplySizeClass` already has (`:101-102`). One line, in `ApplyPlacement`:

```csharp
        // A full-screen viewer owns the whole window; a placement reconciliation must never
        // navigate it away (the section-root/pane case — the pushed-page case is covered by the
        // NavigationStack guard inside EnsurePrimaryDestinationVisibleAsync). Same intent as
        // SourceListPage.ApplySizeClass's _isPaneFullScreen early return.
        if (ensureDestination && !_stateViewModel.IsChromeSuppressed)
            Dispatcher.Dispatch(async () => await EnsurePrimaryDestinationVisibleAsync());
```

**Coordination, mandatory:** `AppShell.xaml.cs` is currently owned by the #393 bugfix branch. Land
this line in whichever of the two branches merges first and rebase the other onto it; the #393 fix
must preserve it, and it must **not** be re-expressed as folding `IsChromeSuppressed` into
`IsLeftRailNavigationVisible`/`IsBottomNavigationVisible` — the 2026-09-12 slice-2 verdict's
deviation 1 (upheld) forbids that, because `ShellNavigationService.cs:88` reads the same property to
decide the route *family*.

**5 (required) — `§7b`: three test gaps.** (a) Decision (f) asks for the `NeverCallsStopReceiver`
guard "extended to **every** new path"; the plan only covers the button+rotation pair. (b) Decision
(b)'s "tablet ⇒ no orientation request ever" is asserted nowhere. (c)
`Dispose_UnsubscribesFromAppPausedAndOrientationChanged` is vacuous — `Mock.Raise` with zero
subscribers never throws, and with subscribers it would not throw either, so `Record.Exception`
returns `null` regardless of whether the unsubscribe happened. Add/replace:

```csharp
    [Fact]
    public void EveryFullScreenExitPath_NeverCallsStopReceiver()
    {
        var sut = CreateSut();
        SetCompactDevice(isLandscape: true);
        sut.IsPlaying = true;                                   // auto-enters (required change 2)

        sut.HandleBackButtonPress();                            // exit via Back -> pending portrait
        _lifecycleMock.Setup(l => l.IsLandscape).Returns(false);
        _lifecycleMock.Raise(l => l.OrientationChanged += null, false);

        _lifecycleMock.Setup(l => l.IsLandscape).Returns(true);
        _lifecycleMock.Raise(l => l.OrientationChanged += null, true);   // auto-enter again
        sut.ToggleFullScreenCommand.Execute(null);              // exit -> pending portrait
        _timeProvider.Advance(TimeSpan.FromSeconds(3));         // exit via the 3s fallback

        _lifecycleMock.Raise(l => l.AppPaused += null);         // force-exit path
        sut.ForceExitFullScreen();                              // Detach()'s path

        _bridgeMock.Verify(b => b.StopReceiver(), Times.Never);
    }

    [Fact]
    public void ToggleFullScreenCommand_OnATablet_NeverRequestsAnOrientation()
    {
        var sut = CreateSut();
        _lifecycleMock.Setup(l => l.SmallestWidthDp).Returns(800);
        sut.IsPlaying = true;

        sut.ToggleFullScreenCommand.Execute(null);
        sut.ToggleFullScreenCommand.Execute(null);

        Assert.False(sut.IsFullScreen);
        _orientationLockMock.Verify(o => o.RequestLandscape(), Times.Never);
        _orientationLockMock.Verify(o => o.RequestPortrait(), Times.Never);
    }

    [Fact]
    public void Dispose_UnsubscribesFromAppPausedAndOrientationChanged()
    {
        var sut = CreateSut();
        SetCompactDevice(isLandscape: false);
        sut.IsPlaying = true;
        sut.Dispose();

        // A disposed ViewModel must not act on the event. Raising it on a Moq mock never throws,
        // so the assertion has to be behavioural: if the handler were still attached it would set
        // IsFullScreen (compact + playing + landscape).
        _lifecycleMock.Setup(l => l.IsLandscape).Returns(true);
        _lifecycleMock.Raise(l => l.OrientationChanged += null, true);

        Assert.False(sut.IsFullScreen, "OrientationChanged still reached a disposed ViewerViewModel");
    }
```

**6 (required) — `§6`: the new e2e's tablet skip must use a device-reported smallest width.**
`Math.Min(app.WindowSize.Width, app.WindowSize.Height) / app.Metrics.Density` is not
`Configuration.SmallestScreenWidthDp`: `Driver.Manage().Window.Size` is the app window, which on
some devices excludes system decorations, and the Tab A9+ sits **exactly on** the 600 boundary — a
few subtracted pixels flip it to "compact" and the test then asserts phone behaviour on a tablet and
fails for the wrong reason, on the one device the checklist targets. Add to
`tests/MauiApp.UITests/Infrastructure/DeviceMetrics.cs` (same no-silent-default rule as its
siblings):

```csharp
    /// <summary>
    /// The device's short edge in dp — Android's own phone/tablet discriminator
    /// (<c>Configuration.SmallestScreenWidthDp</c>, sw600dp), mirrored for the test side so a
    /// compact-device-only assertion can skip on a tablet. Read from the *real* display size, not
    /// from the app window: the window can exclude system decorations, and the Tab A9+ sits on the
    /// 600 dp boundary where that difference decides the answer.
    /// </summary>
    public double SmallestWidthDp
    {
        get
        {
            var info = Invoke("mobile: deviceInfo");

            if (info is Dictionary<string, object> map &&
                map.TryGetValue("realDisplaySize", out var size) &&
                size?.ToString() is { } text &&
                text.Split('x') is [var first, var second] &&
                int.TryParse(first, out var width) && int.TryParse(second, out var height))
            {
                return Math.Min(width, height) / Density;
            }

            throw new InvalidOperationException(
                $"'mobile: deviceInfo' returned no usable realDisplaySize (got: {Describe(info)}). " +
                "The compact-device skip guard cannot run without it.");
        }
    }
```

and replace the guard in `Viewer_RotatedToLandscape_EntersFullScreenInPlace` with:

```csharp
        Skip.If(app.Metrics.SmallestWidthDp >= 600,
            "Rotation-driven full screen is compact-device-only (#384 slice 3); this device reports " +
            $"sw={app.Metrics.SmallestWidthDp:0}dp, i.e. tablet-class.");
```

**7 (required) — `§6`: `WaitUntilPlaying()`'s second wait must fail loudly.** As written the loop
just expires after 20 s and returns, so a genuine "never left full screen" is reported later by an
unrelated assertion (or, in `FullScreen_EnterViaButton_…`, by a chrome assertion that names the wrong
cause) after a silent 20 s stall. Replace the loop with:

```csharp
        var deadline = DateTime.UtcNow + Timeouts.Navigation;
        while (IsFullScreen && DateTime.UtcNow < deadline)
            Thread.Sleep(100);

        if (IsFullScreen)
            throw new InvalidOperationException(
                $"The viewer reported playback but was still full screen after " +
                $"{Timeouts.Navigation.TotalSeconds:0}s — the windowed Deck/Sheet never re-rendered.");
```

**8 (required) — `§10`: two checklist items, one of them a hard prerequisite for trusting the new
page object.** Add to the S21 list: *"After entering full screen, wait out the 2.5 s auto-hide and
confirm with `adb shell uiautomator dump` (or an Appium page source) that
`com.ndi.android:id/viewer.fullScreen.overlay` is still in the tree. The overlay's only child is the
`Grid` bound to `AreControlsVisible`, so once it collapses the node is an empty `ViewGroup`, which
Android may prune from the accessibility tree — and `PageObject.IsPresent` additionally requires
`Displayed`. If it is pruned, `IsFullScreen` must instead be read from a Deck/Sheet-exclusive id
(`TestIds.ViewerQualitySmooth`, which this slice deliberately does **not** add to the overlay):
`IsFullScreen => HasVideoSurface && !IsPresent(TestIds.ViewerQualitySmooth)`. Do not resolve this by
moving the id onto the inner `Grid` — that reintroduces exactly the auto-hide race decision 6 exists
to remove."* And to both lists: *"After a rotation that lands on Home (#393), confirm chrome is fully
restored — tab bar/rail back, system bars back, no immersive mode — i.e. that `OnDisappearing` →
`Detach()` actually ran for the page that was re-pointed away."*

---

## Confirmed — verified against the live files, no change needed

- **Rule 4 (threading).** `OnOrientationChanged` marshals through `_dispatcher.BeginInvokeOnMainThread`
  before touching any state; both new `TimeProvider` timers wrap their callbacks the same way,
  matching `StartAttemptTimer`/`StartCountdown` (`ViewerViewModel.cs:417,477`). `OnAppPaused` and
  `Dispose` are **not** marshalled and correctly so — `NotifyPaused` is called from
  `MainActivity.OnPause` (`:136-140`) and `Dispose` from page lifecycle, both already the UI thread;
  the existing `AppResumed` subscription sets the precedent. No Android type appears in Core:
  `IOrientationLockService` is three `void` members over no platform type, the Android impl lives in
  `Platforms/Android/Services` and mirrors `AndroidImmersiveModeService`'s
  `MainThread.BeginInvokeOnMainThread` + `Platform.CurrentActivity is AppCompatActivity` idiom
  (`:18,52`) exactly, the `Noop` twin lands in `src/MauiApp/Services` with namespace
  `NdiForAndroid.Services` (same as `NoopImmersiveModeService.cs:1`), and both are registered in the
  existing `#if ANDROID`/`#else` block at `MauiProgram.cs:127`/`:139`. `Release()` is
  `ScreenOrientation.Unspecified` with the "never `FullSensor`" reason in an XML doc. Rule 5 holds.
- **The state machine covers decision (b) exhaustively** apart from the hole in required change 2:
  not-playing no-op (the `ToggleFullScreen` guard is unchanged), tablet direct entry with no
  orientation call, compact-and-already-landscape falling through the same `else`, pending flags,
  the symmetric 3 s fallback in both directions, app-pause/`Dispose` force-exit, and a second Back
  swallowed by `BeginExitFullScreen`'s `_pendingOrientation == Portrait` early return (so
  `HandleBackButtonPress` needs no third branch — correct, and the plan says so).
- **The three gaps the researcher closed are real and correctly closed.** (i)
  `BeginExitFullScreen`'s unconditional cancellation of a pending **Landscape** request before the
  `IsFullScreen` guard is genuinely necessary — without it `Stop()` during the request window leaves
  the platform locked and a late rotation re-enters full screen on a dead stream; the paired test
  raises `OrientationChanged` *after* `Stop()` and asserts it does not resurrect, which is the right
  shape. (ii) `Detach()` calling `ForceExitFullScreen()` instead of assigning
  `_viewModel.IsFullScreen = false` is strictly better than the live slice-2 line
  (`ViewerFullScreenChromeController.cs:55`), for the reason given: a torn-down ViewModel with
  `_pendingOrientation` still set would flip itself back to full screen with nothing on screen. The
  controller keeping its own `Release()` as defence in depth is harmless (idempotent) and matches
  decision 7 literally. (iii) The `WaitUntilPlaying()` redefinition is necessary once `viewer.stop`
  is added to the overlay, and the plan's rejection of `viewer.fullScreenToggle` as the
  disambiguator is correct — that id is already shared with `PlaybackControlsView.xaml` and
  disambiguates nothing.
- **`Stop()` ordering** implements decision 3 exactly: `_bridge.StopReceiver()` keeps its position
  and `BeginExitFullScreen()` follows it, with the call-order test proving it. The lock is never
  left held because every exit converges on `Release()` (and, after required change 1, on a single
  choke point).
- **Overlay.** `ToggleControlsOverlayCommand` has the decision-(c) semantics (no-op when not full
  screen; visible ⇒ dispose the timer and hide; hidden ⇒ show and re-arm);
  `NotifyControlInteraction()` survives untouched and keeps its PTZ/quality/audio callers
  (`ViewerViewModel.cs:178,517,531,564`); `IsFullScreenPtzVisible => IsPtzControlActive &&
  IsPtzLayerVisible` is notified from all three sources (the `[NotifyPropertyChangedFor]` on
  `_isPtzLayerVisible` plus the two widened PTZ partials); the layer resets on leaving full screen;
  Back order is PTZ-layer → full screen → not consumed. The new XAML is a faithful superset of the
  live file: `DynamicResource` everywhere, the toolbar column indices match the widened
  `*,48,48,48,48,Auto,48` definition one-for-one, and the camera button uses the same
  `Button.Triggers`/`DataTrigger` description idiom as the full-screen toggle.
- **Reused ids break nothing.** `AccessibilityTests` audits only Home/Output/Sources/Settings in
  portrait (`:60,204-210`) and never enters full screen, so the budget of 12 is untouched; its
  `AutomationIds_AreNotUsedAsScreenReaderLabels` check is satisfied (every new id'd control has a
  human-language description). `ThemeRegressionTests` and `SystemBarInsetTests` touch navigation
  chrome only. The overlay's `viewer.stop`/`viewer.audioToggle`/`viewer.ptz.*` can never be in the
  tree at the same time as the Deck/Sheet's (`ViewerView.xaml.cs:102-104` makes them mutually
  exclusive), which is the accepted `TestIds.cs:94-98` duplicate-id-across-hosts precedent.
- **`ViewerControlLayout.IsCompactDevice` is purely additive** — every existing constant and formula
  is byte-identical, and the "existing formulas unchanged" regression block is present with pinned
  values.
- **Constructor call sites, DI and `TimeProvider`.** All four test files are covered with correct
  before/after; `ViewerViewModel` and `ViewerFullScreenChromeController` are both `AddTransient<T>()`
  so DI resolves the new parameters with no factory edit; `MsFakeTimeProvider` usage is right
  (`Advance` past 2.5 s / 5 s / 3 s, no wall-clock sleeps), and a timer armed *inside* an advanced
  callback correctly does not fire in the same `Advance`. `AdaptiveNavigationTests` /
  `AdaptiveShellStateViewModel` tests are untouched, as the slice-2 verdict's deviation 1 requires.
- **Docs.** The insertion point is correct (`docs/architecture.md:159` bullet, `:161 ## NDI Bridge`)
  and the new bullet's content matches what the code will do — with one correction owed by required
  change 1: the phrase "released back to `Unspecified` … as soon as the requested orientation
  arrives" must become "held for as long as full screen is on and released the moment it ends (or by
  the 3 s fallback)".

## The #393 decision — sequence, do not redesign

**Slice 3's design does not change because of #393; slice 3 *sequences after* it, and that sequencing
is hard, not advisory.** The mechanism is confirmed in the live file: `ApplyPlacement`'s rail branch
sets `PrimaryTabBar.IsVisible = false` (`AppShell.xaml.cs:241`), Shell re-points `CurrentItem` off the
hidden `TabBar`, and `OnShellNavigated` adopts that as the truth (`:308,323`) before dispatching
`EnsurePrimaryDestinationVisibleAsync` (`:330-331`). Chrome suppression alone cannot trigger it —
`PrimaryTabBar.IsVisible` follows `PlacementMode` only (slice-2 deviation 1), and on a phone in
portrait the `else` branch leaves it `true` — so the trigger is specifically the **rotation** that
slice 3 is built on, and `MainActivity.OnConfigurationChanged` feeds the placement bridge *before*
`NotifyConfigurationChanged` (`:145-148`), so the navigation reset is queued first every time. On a
compact device that means every slice-3 path (button → forced rotation, and physical rotation) runs
through the reset: the app lands on Home, the host page disappears, `Detach()` → `ForceExitFullScreen()`
fires, and full screen never sticks. Slice 3 is therefore **not device-verifiable and not mergeable
to `main` before the #393 fix is in the same tree**; implementation and unit tests can proceed in
parallel, but the S21 checklist (items 4, 5, 6, 7, 9, 10, 11) and the new
`Viewer_RotatedToLandscape_EntersFullScreenInPlace` e2e are blocked on it, as is Tab A9+ items 3, 4
and 6. The binding constraint on the fix is that **no workaround may be added to `ViewerViewModel` or
`ViewerFullScreenChromeController`** — not a suppressed `Detach()`, not a re-enter-after-navigation
retry, not an `IsChromeSuppressed` check inside the ViewModel. #393 is a Shell-layer defect and its
fix belongs in `AppShell.xaml.cs`; encoding it in the viewer would contradict the "only the visible
host owns chrome" invariant that decision (e) exists to protect. Two further constraints for whoever
fixes #393: it must not fold `IsChromeSuppressed` into the two visibility properties (slice-2
deviation 1, upheld), and it must not adopt **page-scoped** `Shell.SetTabBarIsVisible(page, …)` as the
placement mechanism — that attached property is already owned by `ViewerFullScreenChromeController`
(`ApplyChrome`/`Detach`), and two owners writing it would make an exit from full screen re-show the
bottom tab bar in a landscape rail window.

**On the tablet, a #393-induced re-point while the pane is full screen is already handled correctly —
provided `OnDisappearing` fires.** `SourceListPage.OnDisappearing:48-55` calls `Detach()`, which
(with the plan's change) runs `ForceExitFullScreen()`; that raises `IsFullScreen`, which the page's
own still-live subscription turns into `ApplyPaneFullScreen(false)` (`:90-94,125-142`), restoring
`ListHeader` and the 2*/3* columns, while the controller clears `IsChromeSuppressed`, exits immersive
and restores both bars. The pane subscription is never removed on `Detach`, so this holds even though
the controller has let the ViewModel go. The residual risk is exactly "does MAUI raise
`OnDisappearing` for a page whose `ShellItem` was re-pointed away" — hence required change 8's
checklist line. Note that even **after** #393 is fixed, required change 4 is still needed for the
tablet: the placement change itself would otherwise reconcile the route family at a section root and
tear the full-screen pane down.

## Recorded decisions

- Decision (b)'s event-driven release is **amended**: the lock is held for the duration of full
  screen and released when full screen ends. Rationale and the accepted behavioural consequence are
  in required change 1. `docs/architecture.md`'s new bullet must state the amended rule.
- The invariant "`IsFullScreen ⟺ landscape` while playing, on a compact device" is now enforced on
  the playback-start edge as well as the rotation edge (required change 2).
- `TestIds.ViewerFullScreenExit` from design (f) is **superseded**: the exit affordance is the same
  `viewer.fullScreenToggle` button with a state-dependent glyph/description, as slice 2 shipped it.
  Only `viewer.fullScreen.overlay` and `viewer.fullScreen.camera` are added.
- Design (f)'s "reuse `viewer.quality.*` on the overlay" is **deliberately not done**: the overlay
  keeps only `viewer.fullScreen.qualityCycle`, which leaves `viewer.quality.smooth` Deck/Sheet-
  exclusive and available as the fallback full-screen signal in required change 8.
- Camera-button placement (toolbar column 1, between status and quality) and its `IsPtzControlActive`
  visibility gate are the plan's own calls, accepted; escalate only if a visual mock disagrees.
- Button double-press during a pending rotation is accepted as un-deduplicated (idempotent
  `RequestedOrientation` writes); S21 checklist item 12 confirms it on device.
- `.github/KNOWLEDGE-BASE.md` needs no edit in this slice (agreed with the plan's §1 item 11).

## Non-blocking notes

1. The widened toolbar's fixed columns total ~360 dp (4×48 + a 72 dp Stop + 48 + 6×8 spacing), so on
   a 360 dp-wide **portrait** phone the status column is squeezed to zero — reachable only via the
   3 s timeout fallback, but it is the same fixed-column failure mode as #361. One line in the S21
   checklist, or an `IsVisible` on the "⋮" button below some width, would close it.
2. The XML doc for the new policy sits on `CompactDeviceMaxSmallestWidthDp`, leaving
   `IsCompactDevice` itself undocumented; move or duplicate it onto the method.
3. `IsPtzLayerVisible` is reset by `OnIsFullScreenChanged(false)`, so on a compact device `Stop()`
   leaves the camera layer open for the duration of the pending-portrait window. Cosmetic, sub-second.
4. Carried over unchanged from the 2026-09-12 slice-2 gate: `SourceListViewModel.cs:76` only stops
   the pane when `PaneViewer is { IsPlaying: true }`, so a full-screen pane that is momentarily not
   playing does not converge back via `Stop()`. Two escape routes remain; still not this slice's.


### 2026-09-12 — #384 slice 2 refreshed plan (gate)

**APPROVE-WITH-CHANGES — four required changes, none of them design changes.** The refreshed plan
(2026-09-12, against `main` @ `9d294e9`) folds in all six required changes from the 2026-09-06 gate,
and every factual claim it makes about the post-#390/#391 tree was re-verified against the live
files: `ShellNavigationService.cs:88`, `AppShell.xaml.cs:228-248`, `AdaptiveShellStateViewModel.cs:22-24`,
`ViewerView.xaml.cs` (#348 block `:134-144`, `SKSamplingOptions.Default` `:226`), `ViewerPage.xaml.cs`,
`SourceListPage.xaml:25` / `.xaml.cs`, `AndroidImmersiveModeService.cs:61-81`, `MauiProgram.cs:166-172`,
`ViewerViewModel.cs:332` (`Stop()` already clears full screen), `ViewerViewModel.FullScreen.cs:10,29-32`,
`TestIds.cs:101-102,120-121,139`, `PlaybackControlsView.xaml:94,106`,
`FullScreenControlsOverlay.xaml:108,115,126`, `tests/.../Pages/ViewerPage.cs:28,44,73`, `Pages/NdiApp.cs`,
`AppLaunchTests.cs:52-68`, `NdiForAndroid.UITests.csproj:23` (Appium 8.*), `docs/architecture.md:157-158`.
Required changes 1, 2, 3, 5 and 6 are correctly implemented; 4 is implemented in the page object but
**not** in the test body (see required change 1 below). Self-containment (the 2026-09-06 rule) is met:
every "replace whole file" snippet carries namespace, usings and surviving XML docs, and both
`AppShell.xaml.cs` methods are restated verbatim and match the live file byte-for-byte.

**Deviation 1 — UPHELD. Keep the visibility properties as pure `PlacementMode` queries.** Decision (a)
conflated two different questions under one property name. `IsLeftRailNavigationVisible` has exactly two
production consumers: `ShellNavigationService.cs:88` (which *route family* is current — `-rail` vs `-tab`)
and `AppShell.ApplyPlacement:236` (is the rail chrome shown). The first must be invariant to chrome
suppression, and that contract is **already recorded in two committed docs** — `docs/architecture.md:140`
and `.github/KNOWLEDGE-BASE.md:104` both state that placement-adaptive routing reads
`IsLeftRailNavigationVisible` — so folding suppression in would silently contradict them. Concretely it
would break the tablet pane path: with suppression folded in, entering full screen on `SourceListPage`
(section stack == 1, so slice 1's `NavigationStack.Count > 1` guard does not apply) makes route
selection return `//view-tab` while the app sits on `//view-rail`, and the last-segment comparison in
`EnsurePrimaryDestinationVisibleAsync` then fires an absolute `GoToAsync` that swaps the ShellContent
family, runs `OnDisappearing`/`OnAppearing`, and force-exits the full screen the user just entered.
The alternative — changing `ShellNavigationService.cs:88` to read `PlacementMode` directly and folding
suppression into the two properties — is rejected because `ApplyPlacement` would then take its `else`
branch during suppression on a rail device and set `PrimaryTabBar.IsVisible = true`, i.e. re-show the
`TabBar` Shell item in a rail window; that is exactly the `Shell.CurrentItem` re-point hazard required
change 2 exists to avoid, and avoiding it requires splitting the branch by `PlacementMode` anyway — the
plan's inline form, plus one extra production file and a property with two different meanings for its
two consumers. `IsBottomNavigationVisible` has no production consumer at all (tests only), which removes
the last argument for symmetry. **Consequences, binding:** `OnStatePropertyChanged` must still listen for
`IsChromeSuppressed` and call `ApplyPlacement(ensureDestination: false)` — `ensureDestination: false` is
load-bearing, not an optimisation: in the slice-1-accepted stale-route-family state a suppression toggle
would otherwise reach `GoToAsync` and reset the section stack. `PrimaryTabBar.IsVisible` keeps following
`PlacementMode` only; `FlyoutBehavior` is the sole thing suppression touches, and only inside the
existing rail branch (`Disabled` is the proven value the non-rail branch already uses). The section 4
unit tests are therefore **correct as written** — suppression is inert on the ViewModel and
`IsLeftRailNavigationVisible` stays `true` under `LeftRail`+suppressed; do **not** revert them to the
2026-09-06 assertions.

**Deviation 2 — CONFIRMED sound, and not racy with the auto-hide.** `IsPlaying` in the page object is
`IsPresent(TestIds.ViewerStop)` (`Pages/ViewerPage.cs:44`), and `viewer.stop` is carried **only** by
`PlaybackControlsView.xaml:106`; the overlay's own Stop button (`FullScreenControlsOverlay.xaml:126`)
deliberately has no `AutomationId`. In full screen `ViewerView.UpdateLayoutVisibility` sets
`Deck.IsVisible = Sheet.IsVisible = false`, so `viewer.stop` leaves the tree because the **deck/sheet is
collapsed**, not because the overlay auto-hid — the signal is independent of `AreControlsVisible` and of
the 3 s timer. `HasVideoSurface` (`viewer.videoCanvas`) is present in both states. The residual
ambiguity (`IsFullScreen` is also true for "windowed and not playing") is real but is already documented
in the plan's own comment and is excluded by the test's `WaitUntilPlaying()` precondition. The coupling
is load-bearing: if anyone ever adds `viewer.stop` to the overlay's Stop button this property silently
inverts — that is now recorded here.

**Required changes.** (1) The new e2e test still reads three non-waiting properties immediately after a
transition — `NavigationBar.IsPresent` (`Pages/NavigationBar.cs:145,160-187`) and `PageObject.IsPresent`
(`:249-255`) both deliberately do not wait — so required change 4's anti-race measure is applied only
after `PressBackButton()`. Add a `WaitUntilFullScreen()` built on the overlay-exclusive id
`TestIds.ViewerQualityCycle` (`viewer.fullScreen.qualityCycle`, present only in
`FullScreenControlsOverlay.xaml:108`, on screen for the first 3 s) with the 2 s `Timeouts.StateChange`
budget, and a `WaitUntilPlaying()` after each exit, before the assertions. (2)+(3) Lock deviation 1 in
the code and in the canonical doc, not only in a scratch plan: XML-doc the two visibility properties as
placement queries that must never fold in suppression (naming `ShellNavigationService`), and add the
same rule to `docs/architecture.md`. (4) Give the two new unit tests a comment naming
`ShellNavigationService.cs:88` — same reasoning as the #380 verdict's "do not name it `DefaultTimeout`":
a bare assertion invites a future "consistency" edit that reverts the decision.

**Open question (i) — recommendation: accept the interim, do not special-case now.** Slice 3's decision
(b) already says tablets get *no* orientation-driven full-screen behaviour ever ("`!compact` ⇒
`IsFullScreen = true` directly, no orientation request ever"), so a tablet auto-exit added here would be
contradicted by the next slice and removed again; the state is escapable by two independent on-screen
paths (overlay exit button, Back — checklist items 10-11); it is visually coherent (full-window video in
a portrait window); and implementing it means putting orientation logic in `SourceListPage.xaml.cs`,
which decision (b) reserves for Core. **Sharper variant for the owner:** because
`SourceListViewModel.cs:76` only stops the pane when `PaneViewer is { IsPlaying: true }`, a pane that is
full screen but momentarily *not* playing (mid-reconnect) does not get the `Stop()` → `IsFullScreen=false`
convergence the plan relies on, and `ApplySizeClass`'s `_isPaneFullScreen` early return then leaves a
0-width list column in a Medium window until the user exits manually. Same two escape routes; worth one
device-checklist line rather than code in this slice.

**Standing rules re-checked.** Rules 1, 2, 6 untouched. Rule 3 holds — all state stays in Core, the new
`ViewerFullScreenChromeController` is Shell/Page chrome plumbing that cannot live in Core (decision (e)
mandated it), and `SourceListPage.ApplyPaneFullScreen` follows the established "Layout plumbing only"
idiom. Rule 4 holds: after this slice `IsFullScreen` is assigned only from `ToggleFullScreen` (UI
command), `Stop()` (`ViewerViewModel.cs:332`, UI command / UI-thread size-class handler) and the
controller's `Detach`/`HandleBackButton` (page lifecycle), so `ApplyChrome`'s `Shell` writes are always
on the UI thread; `AndroidImmersiveModeService` self-marshals. Rule 5 holds — `IsChromeSuppressed` is a
MAUI-free Core property, Android APIs stay in `Platforms/Android`. Theming holds (the only XAML change
is one `x:Name`). The #338 `ModalStack` guard (`AppShell.xaml.cs:264`), `LastSegment`/`ParseDestination`
(`:331-349`) and `EnsurePrimaryDestinationVisibleAsync` (`:354-366`) all stay byte-identical.
`KeepScreenOn` stays driven by `IsPlaying` (`ViewerViewModel.FullScreen.cs:29-32`). The render timer is
never stopped on a transition — `StopRendering` survives only in page lifecycle and in `ApplySizeClass`'s
non-Expanded branch, which the `_isPaneFullScreen` early return now guards. No slice-3 leakage: no
`IOrientationLockService`, `OverlayAutoHideSeconds` still 3, no PTZ layer, and `ViewerView.xaml:46`'s
double-tap recognizer is untouched. CI emulator: the new test `Skip.If`s on `SourceCount == 0` before any
full-screen interaction; `AdaptiveNavigation_InLandscape_PlacesNavigationInTheLeftRail` is unaffected
because `IsChromeSuppressed` can never be true on a sourceless emulator (`FlyoutBehavior.Locked` as
today); the accessibility audit sees no id or control changes; `tests/` has zero references to any
deleted symbol.

**Recorded, verified, no change needed.** `Detach()` unsubscribes *before* forcing `IsFullScreen = false`,
so `ApplyChrome` is not re-entered while the manual chrome clear runs — and the View's and
`SourceListPage`'s own separate subscriptions still fire, so the overlay, deck/sheet, `ListHeader` and the
column widths all restore. `PaneViewer` is assigned with `??=` (`SourceListViewModel.cs:124`), so it can
never be replaced and `AttachPaneIfReady`'s single-shot `!ReferenceEquals` subscription cannot leak a
stale handler. `ViewerFullScreenChromeController` registered `AddTransient` into a Singleton
`SourceListPage` and a Transient `ViewerPage` is correct — it depends only on singletons, so there is no
captive dependency, and registering it concretely (no interface) matches the `ShellNavigationService`
precedent. `Shell.SetNavBarIsVisible(page, true)` on Detach is safe: neither `ViewerPage.xaml` nor
`SourceListPage.xaml` sets `Shell.NavBarIsVisible`. Push/pop ordering between the two hosts is
order-independent because both paths converge on "not suppressed". `docs/features/viewer-fullscreen/*`
and `viewer-control-deck/*` correctly stay as point-in-time records; `.github/KNOWLEDGE-BASE.md` contains
no claim about `FullScreenViewerPage`, so it needs no correction (an added chrome-suppression line there
would be welcome but is optional).

**Non-blocking notes.** (i) `ViewerView.Teardown()` gains its first real caller but does not clear
`_pendingFrame`/`_lastRenderedTimestamp`, so the `BindingContext = null` → `UpdateLayoutVisibility` →
`HeightRequest` path can trigger one more paint that reallocates an `SKBitmap` nobody disposes — a
one-off leak per page pop, pre-existing in shape, worth two lines under its own ticket. (ii) On a Compact
window the live layout is `ViewerControlSheet`, whose peek state can leave the ⛶ toggle partially below
the fold; if `ToggleFullScreen()` misbehaves on the S21 the sheet must be expanded first — a page-object
concern, not a product defect. (iii) Add one phone checklist step: rotate landscape→portrait *while full
screen* and confirm the bottom tab bar stays hidden — that is the one path where Shell-wide
`PrimaryTabBar.IsVisible = true` and the page-scoped `Shell.SetTabBarIsVisible(page, false)` disagree and
the page-scoped value must win.

### 2026-09-06 — #380 flaky `ViscaPtzControllerLoopbackTests` (per-test timeout budgets)

**APPROVE-WITH-CHANGES.** Test-project-only change; no production code, no fake change. The plan is
the correct idiom and is a direct application of a rule this log already recorded: *"Timeouts must be
constructor-injected, not `static readonly` — required for deterministic loopback timeout tests"*
(#339 verdict, later in this log). `CreateSut()` currently applies the **timeout test's** budget to
**every** test in the class (`ViscaPtzControllerLoopbackTests.cs:11,19-20`); giving each test the
budget its own intent requires is what that rule was for.

**(a) Budgets, not `TimeProvider` — `TimeProvider` is the wrong tool here.**
`docs/architecture.md:238`'s "no wall-clock in testable logic, tests advance a `FakeTimeProvider`"
rule is scoped to the #233 viewer reconnection **state machine** — pure logic, no I/O.
`ViscaPtzController` is recorded at `docs/architecture.md:20` as owning a real
"connect/reconnect/timeout state machine" over `System.Net.Sockets` (Dependency Rule 7), and the
loopback suites are, by recorded decision (#339 verdict, later in this log), **real-socket integration tests**.
Four concrete reasons against a virtual clock: (1) it needs a new `TimeProvider` ctor parameter on
`ViscaPtzController` — a production change to fix a test flake; (2) `CancelAfter` would then never
fire on its own, so the non-timeout tests get an *unbounded* budget with no fail-fast guard (there is
no `xunit.runner.json` and no `[Fact(Timeout=)]` in this project) — strictly worse than a finite
generous budget; (3) `PanTiltAsync_SilentMode_TimesOutWithoutHanging` would have to `Advance()` from
the test thread with no signal for "the receive is now pending", and advancing before the linked CTS
is created (`ViscaPtzController.cs:111-112`) means nothing ever cancels — a new, worse race, and its
`Stopwatch` assertion measures real wall-clock anyway; (4) the repo's own
`src/Core/Services/FakeTimeProvider.cs:38-43` creates a **real** `System.Threading.Timer`, so it
could not drive a `TimeProvider`-backed CTS deterministically regardless. Rejected alternatives:
disabling xUnit parallelisation (slows the whole suite, does not address cold-JIT), a retry
attribute (hides flakes), bulk reads in the fake (reduces continuations, does not remove the race).

**(b) Intent is preserved, including the retry test.** The retry in
`SendCommandAsync` (`ViscaPtzController.cs:87-91`) is gated on `wasAlreadyConnected` and on attempt 1
failing, and attempt 1 fails because the fake closed the socket (`LoopbackViscaCamera.cs:88-93`
→ EOF/RST → `ViscaTcpTransport.cs:63`), **not** because a timer fired. Widening the budget cannot
change which branch runs; it only stops an unrelated clock from pre-empting it. Note the deterministic
proof of the reconnect already lives in the mock suite —
`ViscaPtzControllerTests.PanTiltAsync_FailsOnOpenConnection_RetriesOnceAndReconnects:92-105` asserts
`ConnectCount == 2` / `DisconnectCount == 1`, and `:108-118` covers the no-retry-on-fresh-connection
case — so the loopback test's unique job is proving the same thing against a real peer close. That
job survives the change intact.

**(c) No `LoopbackViscaCamera` change is proposed, and that is the right call.** Behaviour-preservation
is trivially satisfied. The byte-at-a-time reads (`:114-137`) are an efficiency smell, not a defect.

**(d) No deviation from `docs/architecture.md`, `docs/constitution.md` or this file.** Core stays
MAUI-free; the change is confined to `tests/MauiApp.Tests`, which references only `src/Core`.

**Required changes:**

1. **Do not name the new constant `DefaultTimeout`.** `ViscaPtzController.cs:12-13` already defines
   the *actual* defaults (3 s connect / 2 s command) and they are what production
   (`PtzControllerFactory.cs:23`) and the mock suite (`ViscaPtzControllerTests.cs:16`) use. A 5 s
   test constant called `DefaultTimeout` misstates that and invites a future "consistency" edit that
   reintroduces the flake. Use `GenerousTimeout` (or similar) with a one-line comment: *deliberately
   larger than the production defaults; these tests do not test timing.*
2. **`PanTiltAsync_SilentMode_TimesOutWithoutHanging` must keep the short budget on the *command*
   only: `CreateSut(GenerousTimeout, ShortTimeout)`.** The `Stopwatch` window spans connect **and**
   command, so with a 300 ms connect budget a starved runner can fail in `ConnectAsync` while all
   three assertions (`false`, `Error`, `< 1500 ms`) still pass — a vacuous green in the one test that
   exists to prove the command timeout fires. Tightening only the budget under test removes that path
   and shrinks the residual flake the plan itself lists.
3. **Verification must include unfiltered repeat runs.** `--filter "FullyQualifiedName~Loopback"`
   removes the cross-collection parallelism that is the hypothesised trigger (no `CollectionBehavior`
   attribute exists, so every class is its own collection and runs concurrently up to
   `ProcessorCount`). Run at least part of the repeat loop on the full suite, and state plainly that
   N green local runs is a smoke check — the acceptance signal is CI `build-and-test` staying green
   over subsequent runs.

**Mechanism note (do not over-index on the plan's decomposition of the 894 ms).** Attempt 1 of the
second command almost always fails *fast* on EOF/RST, not by consuming its 300 ms. The dominant
exposure is attempt 2: reconnect (300 ms) plus a fresh command (300 ms) racing the fake's accept-loop
continuation and ~12 byte-wise continuations. Same conclusion, different arithmetic — worth knowing
if the flake recurs.

**Non-blocking / out of scope for #380:** (i) the loopback drop test asserts only "the second call
returned true", not "a reconnect happened" — an optional connection counter on `LoopbackViscaCamera`
would close that, though the mock suite already proves it; (ii) `LoopbackViscaCamera.Mode:32` is a
plain auto-property written on the test thread and read on pool threads with no barrier (benign on
x64 CI); (iii) neither loopback class carries `[Trait("Category","Integration")]` although
`.claude/knowledge/testing.md:25` defines a stage by that filter; (iv) **drift**:
`src/Core/Services/FakeTimeProvider.cs` is a hand-rolled `TimeProvider` sitting in **production
Core** whose `CreateTimer` ignores its own fake clock, while the #338 verdict (item 6) already
directs new work at `Microsoft.Extensions.Time.Testing.FakeTimeProvider` — it should eventually leave
`src/Core` or be deleted, under its own ticket.
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

- **[SUPERSEDED 2026-09-07 — see the #352/#359 verdict below]** **FIX-02 (OutputPage half): REJECTED.** `OutputPage` is a `ShellContent` `ContentTemplate` target
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
   lifetime. **Resolved 2026-09-07 by #352/#359 (Singleton pair).** Use
   `_ = RefreshCommand.ExecuteAsync(null)` (AsyncRelayCommand suppresses concurrent
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

### 2026-09-07 — #352/#359 tab-root ViewModel lifetime (amends FIX-02)

**DECIDED: option 1, Singleton pair.** `HomeViewModel`+`HomePage` and `OutputViewModel`+`OutputPage`
are registered Singleton (`src/MauiApp/MauiProgram.cs`), matching `SourceListViewModel`/`SourceListPage`
(C1). Rationale: the 2026-09-05 addendum proved re-creation per tab entry (so the FIX-02 rule 'tab roots
must not dispose' was right for the wrong reason and left an unbounded leak); an OnAppearing/OnDisappearing
attach/detach (option 2) would unsubscribe `OutputViewModel` from `AppResumed` exactly while the app is
backgrounded (OnDisappearing fires on sleep and on modal push — `ViewerPage.xaml.cs:40-46`) and would
need catch-up logic for Home's discovery status; the singleton page is already proven to re-parent
across `-tab`/`-rail` ShellContents by the e2e orientation loops (`AccessibilityTests.cs:91-99`,
`ThemeRegressionTests.cs:56-64`). No captive dependency: both pages depend only on their ViewModel and
every ViewModel dependency is a Singleton.

**Rule (replaces FIX-02):** a ShellContent-hosted tab root whose ViewModel subscribes to a singleton event
is a Singleton pair (page + ViewModel); `Dispose()` on such a ViewModel is container-owned and is never
called from page lifecycle; per-visit work goes through the appearance command (`RefreshCommand` /
`LoadCommand`), which must re-corroborate against the bridge; query intents on a singleton page are
consumed one-shot (`OutputPage.ApplyEntryStateAsync` nulls them in `finally`). Push-navigated pages
(`ViewerPage`) keep the Transient + dispose-after-leaving-the-stack pattern. `SettingsViewModel` stays
Transient (no singleton subscriptions). Documented in `docs/architecture.md` Dependency Rule 8 and
`KNOWLEDGE-BASE.md` Architecture Rule 7; guarded by `TabRootLifetimeRegistrationTests`.

Accepted behaviour change: Stream-tab state (typed name, input kind, mic, re-stream mode, status text)
now persists across tab visits/rotation — the symptom the #327 fit-check observed is gone by design.

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
