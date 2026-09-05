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
