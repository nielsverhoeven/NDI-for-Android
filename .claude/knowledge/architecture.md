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

<!-- Paste each entry into `.claude/knowledge/architecture.md`'s Verdicts log on that item's own branch. -->

### 2026-09-12 — #343 OUT-08 re-stream source picker (gate)

**APPROVE-WITH-CHANGES.** The data-source choice, the lifetime model and the converter are all
correct and are the *existing* idioms, not new ones. Three blocking defects, all in
`ApplyReStreamSources` / `SelectReStreamSourceById` / the Appium smoke test, where the plan's code
contradicts the plan's own design decisions.

**Verified against the live tree (not taken from the plan):**

- **Lifetime is right and cannot leak or double-fire.** `OutputViewModel` is
  `AddSingleton` (`MauiProgram.cs:159`), `OutputPage` is `AddSingleton` (`:172`), and both
  `ISourceRepository` (`:72`) and `IDiscoveryRefreshService` (`:96`) are singletons — **no DI change
  is needed**. Subscribing once in the constructor and unsubscribing only in the container-owned
  `Dispose()` is exactly the rule recorded in the 2026-09-07 #352/#359 verdict and restated in the
  `MauiProgram.cs:147-153` comment. The ViewModel is resolved once per process and
  `OutputPage.OnAppearing` runs `LoadCommand` (`OutputPage.xaml.cs:34-37`), never re-subscribes, so
  there is exactly one handler for the process lifetime.
- **The "already polling continuously" claim is TRUE.** `DiscoveryRefreshService` starts and stops
  itself from `IAppLifecycleService.AppResumed`/`AppPaused` (`DiscoveryRefreshService.cs:56-57`),
  not from the Sources page — `SourceListViewModel` only ever calls `Stop()` (`:106`) and
  `RequestRefresh()` (`:99`). Poll interval is **5 s** (`DiscoveryRefreshService.cs:15`). That
  number is what makes required change 1 blocking.
- **Rule 4 holds.** `OnReStreamSourcesSnapshotReady` marshals through
  `_dispatcher.BeginInvokeOnMainThread`, mirroring `SourceListViewModel.OnSnapshotReady`
  (`SourceListViewModel.cs:80-91`) and the interface's own "raised on a background thread"
  contract (`IDiscoveryRefreshService.cs:11-15`).
- **Rule 1 holds** (repository interface, no SQLite in the ViewModel) and **Rule 3 holds**.
- **The converter is the right place — do not move display strings into the ViewModel.**
  `NdiSourceDisplayConverter` is a byte-for-byte parallel of `VideoInputKindDisplayConverter`
  (`ValueConverters.cs:81`), which the sibling Video Input Picker already uses for
  `ItemDisplayBinding` (`OutputPage.xaml:61`). This log already records that "a value converter …
  is the correct usage" for `StaticResource` in a view. A ViewModel-side
  `ObservableCollection<string>` would need a second collection kept in sync with `SelectedItem`
  and would break the `SelectedItem`→`NdiSource` round-trip. Registration in `Styles.xaml:148`
  next to `VideoInputKindDisplayConverter` is correct.
- **Cross-feature Core dependency (Output → Sources) is established precedent**, not a new
  boundary: `HomeViewModel` already takes `ISourceRepository` and `IDiscoveryRefreshService`
  (`HomeViewModel.cs:24,72`). No layering deviation.
- Baseline references all check out: `OutputViewModel.cs:84` (`_reStreamSourceId`), `:93-114`
  (ctor), `:231-240` (`ApplyReStreamRequest`), `:283-341` (`StartOutputAsync`, including the silent
  fall-through to capture mode at `:295-317`), `:370-374` (`Dispose`); `OutputPage.xaml:90-98`;
  `TestIds.cs:153` + reflection-based `TestIds.All` (`:47`); `A11Y_MAX_VIOLATIONS` budget 12 /
  measured 10 (`AccessibilityTests.cs:47-50`); `OutputPage.xaml.cs:40-61` awaits `LoadCommand`
  **before** `ApplyReStreamRequest`, so the preselection ordering the plan depends on is real;
  `SourceListViewModel.NavigateToOutputAsync:133-150` unchanged.

**Decision on the new validation (design decision 7): KEEP.** `StartOutputAsync` today silently
falls through to the **capture** branch when `IsReStreamMode && ReStreamSourceId` is empty
(`OutputViewModel.cs:295,304-317`) — it starts a screen capture and raises the MediaProjection
consent dialog for a user who asked to re-stream. That is a defect, not a feature, and a Picker
makes "no selection" a first-class visible state that reaches it far more often. Four lines,
mirrors the stream-name guard immediately above, Core-testable. Keep item 7 and its test.

**Required changes, ordered:**

1. **`ApplyReStreamSources` must merge, never `Clear()` — and must ignore failure/empty snapshots.**
   The plan's design decision 2 commits to "cached registry + live discovery, **unioned by
   identity**", but the code is a wholesale replace. Three consequences, all real:
   (a) with a 5 s poll the Picker's bound `ItemsSource` is emptied and refilled **every 5 seconds**,
   which resets the native `SelectedIndex` to −1, pushes `SelectedItem = null` back into the
   ViewModel and transiently nulls `ReStreamSourceId`; (b) `HasReStreamSources` flips false→true on
   every poll, so the Picker unloads and the fallback `Entry` flashes in; (c) a single **failed**
   poll raises `SnapshotReady` with `Sources: Array.Empty` (`DiscoveryRefreshService.cs:164-171`),
   which would wipe every cached source — precisely the "live-only was rejected" regression the plan
   says it is avoiding. Replace the whole of `OnReStreamSourcesSnapshotReady` /
   `ApplyReStreamSources` with:

   ```csharp
    /// <summary>Raised on a background/pump thread whenever a discovery poll completes — marshal to
    /// the UI thread before touching <see cref="AvailableReStreamSources"/> (Architecture Rule 4).
    /// A failed poll carries an empty source list (DiscoveryRefreshService raises a Failure snapshot
    /// with Array.Empty) and must never be allowed to empty the picker.</summary>
    private void OnReStreamSourcesSnapshotReady(object? sender, DiscoverySnapshot snapshot)
    {
        if (snapshot.Status == DiscoveryStatus.Failure)
            return;

        _dispatcher.BeginInvokeOnMainThread(() => ApplyReStreamSources(snapshot.Sources));
    }

    /// <summary>
    /// Merges <paramref name="sources"/> into <see cref="AvailableReStreamSources"/> by SourceId.
    /// Deliberately additive: discovery polls every 5 seconds, and clearing a Picker's bound
    /// ItemsSource resets its SelectedIndex to -1 — which would null SelectedReStreamSource (and
    /// with it ReStreamSourceId) on every poll. A source that did not answer one poll (weak Wi-Fi,
    /// source briefly busy) also stays selectable, which is the whole reason this list is a union
    /// of the cached registry and live discovery rather than the latest snapshot.
    /// </summary>
    private void ApplyReStreamSources(IReadOnlyList<NdiSource> sources)
    {
        foreach (var source in sources)
        {
            var index = IndexOfReStreamSource(source.SourceId);
            if (index < 0)
            {
                AvailableReStreamSources.Add(source);
            }
            else if (AvailableReStreamSources[index].LastSeenAtEpochMillis == 0)
            {
                // A synthesized raw-id placeholder (LastSeenAtEpochMillis == 0 is only ever set by
                // SelectReStreamSourceById) has now been discovered for real — swap in the real
                // entry so the Picker shows its friendly name instead of the raw id.
                AvailableReStreamSources[index] = source;
                if (string.Equals(SelectedReStreamSource?.SourceId, source.SourceId, StringComparison.Ordinal))
                    SelectedReStreamSource = source;
            }
        }

        HasReStreamSources = AvailableReStreamSources.Any(s => s.LastSeenAtEpochMillis != 0);

        // A restored/preselected id with no Picker selection yet (first LoadAsync) — point the
        // selection at it now that the list is populated.
        if (SelectedReStreamSource is null && !string.IsNullOrWhiteSpace(ReStreamSourceId))
            SelectReStreamSourceById(ReStreamSourceId);
    }

    private int IndexOfReStreamSource(string sourceId)
    {
        for (var i = 0; i < AvailableReStreamSources.Count; i++)
        {
            if (string.Equals(AvailableReStreamSources[i].SourceId, sourceId, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }
   ```

2. **A synthesized placeholder must not count as "there are sources to pick from".** As written,
   `SelectReStreamSourceById` sets `HasReStreamSources = true` when it inserts a placeholder. On a
   **singleton** ViewModel that is permanent: one deep link or one Sources-page "Output" tap, and
   `ShowReStreamManualEntry` can never be true again for the rest of the process — the free-text
   `Entry` that design decision 3 exists to preserve becomes unreachable. That matters because
   typing a bare `host:port` is a *supported* path: `NdiOutputBridge.LooksLikeUrlAddress` routes it
   to `p_url_address`, and the raw `192.168.0.25:5961` in the ticket's own screenshot is exactly
   that case — it is how you re-stream a source mDNS cannot see. Replace `SelectReStreamSourceById`
   with:

   ```csharp
    /// <summary>
    /// Points the Picker at the entry matching <paramref name="sourceId"/>. When the picker is the
    /// visible control but the id is not in it yet — e.g. a source preselected from the Sources
    /// page's Output button that this view model's cache/snapshot has not reconciled — a
    /// placeholder showing the raw id is synthesized and selected, so the Picker is never blank for
    /// a source the user just chose (Nielsen #6). With an empty registry the free-text Entry is the
    /// visible control instead, so only ReStreamSourceId is set: synthesizing there would flip
    /// HasReStreamSources and permanently hide the manual-entry fallback on this Singleton VM.
    /// </summary>
    private void SelectReStreamSourceById(string? sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            SelectedReStreamSource = null;
            ReStreamSourceId = null;
            return;
        }

        var index = IndexOfReStreamSource(sourceId);
        if (index >= 0)
        {
            SelectedReStreamSource = AvailableReStreamSources[index];
            return;
        }

        if (HasReStreamSources)
        {
            var placeholder = new NdiSource(sourceId, sourceId, null, IsAvailable: false, LastSeenAtEpochMillis: 0);
            AvailableReStreamSources.Insert(0, placeholder);
            SelectedReStreamSource = placeholder;
            return;
        }

        // Empty registry: the manual-entry Entry is what is on screen — set the id it binds to.
        SelectedReStreamSource = null;
        ReStreamSourceId = sourceId;
    }
   ```

   Two unit tests in §4 must change with it, because their expectations encoded the old behaviour:
   - `ApplyReStreamRequest_SourceNotInList_SynthesizesPlaceholderAndSelectsIt` — with **no** cached
     sources the expected outcome is now the manual-entry fallback. Rename to
     `ApplyReStreamRequest_WithNoCachedSources_FallsBackToManualEntryWithTheRawId` and assert:
     `Assert.Null(sut.SelectedReStreamSource); Assert.Equal("192.168.0.25:5961", sut.ReStreamSourceId);
     Assert.False(sut.ShowReStreamSourcePicker); Assert.True(sut.ShowReStreamManualEntry);`
   - Add a new test for the placeholder path proper: seed `GetCachedSourcesAsync` with one source,
     `await sut.LoadCommand.ExecuteAsync(null)`, then `sut.ApplyReStreamRequest("192.168.0.25:5961", true)`
     and assert `sut.SelectedReStreamSource?.SourceId == "192.168.0.25:5961"`,
     `sut.AvailableReStreamSources.Count == 2`, `sut.ShowReStreamSourcePicker`.
   - `SelectedReStreamSource_SetToNull_ClearsReStreamSourceId` still passes (the
     `OnSelectedReStreamSourceChanged` partial still nulls the id).

3. **The Appium smoke test does not compile — there is no `WaitUntil` helper.** `UiTestBase`
   exposes only `Run(Action<NdiApp>, [CallerMemberName] string)` (`UiTestBase.cs:41`); `PageObject`
   exposes `WaitFor`/`WaitUntilVisible`, not a predicate `WaitUntil`. Replace the block in §5's
   smoke test with an explicit deadline loop:

   ```csharp
                app.Output.ToggleReStreamMode();

                var deadline = DateTime.UtcNow + Timeouts.StateChange;
                while (DateTime.UtcNow < deadline && !app.Output.IsReStreamMode)
                    Thread.Sleep(250);

                Assert.True(app.Output.IsReStreamMode, "The mode switch did not reach re-stream mode");
   ```

   Keep the `finally` that restores the previous mode — it is load-bearing, not tidiness: since
   #352/#359 the Stream tab's state persists for the process lifetime on the Singleton ViewModel,
   so leaving re-stream mode on would change what every later test in the shared `"AppiumSession"`
   collection sees on the Stream tab, including `AccessibilityTests`' budget sweep.

4. **Doc edits: locate the anchors by content, not by line number.** §6.1's "after line 236" and
   §6.2's "after line 112" will have drifted; anchor on the `IOutputConfigurationRepository`
   persistence paragraph in `docs/architecture.md` and on the "Lifetime (#352/#359)" bullet in
   `.github/KNOWLEDGE-BASE.md`. Also amend `docs/architecture.md` Navigation rule 4's
   `reStreamSourceId` sentence to note the id now resolves to a Picker selection.

**Non-blocking:**

5. Give the fallback `Entry` a `SemanticProperties.Description="Re-stream source id"`. It is
   `Clickable`+`Focusable`, so `AccessibilityAudit.IsInteractive` (`AccessibilityAudit.cs:47`) picks
   it up whenever re-stream mode happens to be on during the sweep — and per item 3 that state now
   survives across tests. Cheap insurance against a 13th violation against a budget of 12.
6. `MinimumHeightRequest="48"` on both new controls is correct and must not be dropped; it is also
   the right call *not* to fix the two pre-existing touch-target violations on the Stream Name
   `Entry` / Video Input `Picker` here — those want their own ticket before the budget is ratcheted
   from 12 down to 10.
7. Latent, out of scope, do **not** fix here: `ToggleReStreamModeAsync` dereferences
   `ReStreamSourceId!` (`OutputViewModel.cs:250`) and would NRE with no selection. Unreachable
   today — `ToggleReStreamModeCommand` is bound nowhere in XAML (`OutputPage.xaml` binds
   `ToggleOutputModeCommand` and a two-way `IsReStreamMode` Switch); the only callers are
   `OutputViewModelTests.cs:347,370`. Its own class remarks (`:267-272`) already record why.
8. The plan's `NdiSource`-record-equality note is accurate and stays true under the merge rewrite.

**Open question for the owner:** the ticket cell is truncated at source ("kee[p]…"). This verdict
ratifies the plan's reading — free text is the *empty-state fallback*, not a permanently available
alternate path — and required change 2 is what actually makes that reading true in code. If the
owner meant "always keep a manual-override toggle even when sources exist", say so before
implementation; that is a different control, not a tweak.

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
