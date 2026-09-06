# Feature Spec — Full-Screen Mode for the NDI Viewer

**Issue:** #338
**Branch:** `feature/338-viewer-fullscreen`
**Status:** Decided (owner comment 2026-09-04) — ready for implementation planning

---

## User Story

As someone watching an NDI stream — on the pushed Viewer page (phone) or the embedded pane
(tablet two-pane Sources layout) — I want a full-screen toggle (button or double-tap on the
video) that expands the video to fill the entire screen and hides system/app chrome and
playback controls, so I can watch without distraction, and exit cleanly (same toggle, or the
Android back button/gesture) without stopping the receiver or navigating away from the View
destination.

---

## Resolved Decisions

Binding decisions from the issue owner's comment (2026-09-04), plus a small number of
implementer defaults called out explicitly where the owner comment is silent.

### D1 — Host mechanism: chromeless modal reusing `ViewerView` + `ViewerViewModel` *(owner decision)*

**Decision:** Design Option B. A new chromeless page, `FullScreenViewerPage`, is pushed via
`Navigation.PushModalAsync` (not a Shell route) and hosts a **second** `ViewerView` control
instance bound to the **same, already-playing** `ViewerViewModel` instance handed over by the
donor host (the tablet pane's `PaneViewer`, or the phone `ViewerPage`'s own VM). Same behavior
on phone and tablet.

**Rationale (owner):** a modal push never raises `Shell.Navigating`/`Shell.Navigated`, so
`AppShell.OnNavigating`/`OnShellNavigated` never run and `NdiNavigationHandoffService` cannot be
invoked by entering/exiting full screen — this is a structural guarantee, not a
convention every back-handler must honor correctly. It also sidesteps the
`Shell.NavBarIsVisible`/tab-bar/rail chrome-visibility surface that caused #296.

**Verified against code** (`src/MauiApp/AppShell.xaml.cs`): `OnNavigating`/`OnShellNavigated`
are Shell-only events tied to `GoToAsync`/Shell's own back handling; `Navigation.PushModalAsync`
operates on the separate **modal stack** and does not raise them. No code in this repo currently
calls `PushModalAsync` anywhere (greenfield), so there is no existing counter-example to check.
See plan.md §3 for the concrete guard analysis.

### D2 — No orientation lock *(owner decision)*

Rotation stays fully sensor-driven, in and out of full screen. No `IOrientationLockService` is
introduced (drops the issue body's optional task 9).

### D3 — Keep-screen-on scope *(owner decision)*

The device's keep-screen-on flag is active whenever **any** stream is playing (`IsPlaying`),
**not** only while full screen — applies identically to the tablet pane and the pushed phone
page.

### D4 — Overlay auto-hide timing and full-screen persistence *(owner defaults, confirmed accepted)*

- Overlay controls auto-hide after **3 seconds** idle, and reappear on a single tap on the video.
- **Any control interaction resets the timer** (PTZ, quality, audio, Stop, Cancel/Reconnect, or
  tapping the video itself).
- Full-screen state is **not** restored after backgrounding/resume — resuming the app always
  shows the normal (non-full-screen) layout.
- Sending side (Stream tab / `OutputPage`) is **out of scope**.
- Exit via the same toggle, double-tap, or the Android back gesture — all three must only close
  the modal, never pop the donor `ViewerPage` or navigate away from View.

### D5 — `IImmersiveModeService` Core abstraction *(owner decision)*

Android immersive mode (system-bar hide/show) and keep-screen-on go behind a new Core interface,
`IImmersiveModeService`, with an Android implementation and a Noop twin, per Architecture Rule 5
— mirrors the existing `IMulticastLockService` pattern exactly.

### D6 — Implementer default: `Stop` also exits full screen *(new, not in the owner comment)*

Not addressed by the owner's decisions. Because the full-screen toggle button and the Stop
button are both gated on `IsPlaying`, a user who stops playback while full screen would
otherwise be stranded on a black chromeless page with no visible toggle (only the back
gesture would still work, since it is unconditional). Default: `Stop()` also sets
`IsFullScreen = false`. Flag to the product owner if undesired — trivial to drop (see
plan.md §4.3, `ViewerViewModel.Stop()`).

---

## Interface Changes

```csharp
// src/Core/Services/IImmersiveModeService.cs (NEW)
public interface IImmersiveModeService
{
    void EnterImmersive();      // hide system bars (swipe-to-reveal remains available)
    void ExitImmersive();       // restore system bars
    void KeepScreenOn(bool enabled);
}
```

No changes to `INdiViewerBridge`, `INdiBridges.cs`, or any bridge model. No NDI types are
touched by this feature.

## ViewModel Changes (`ViewerViewModel`, Core)

- New observable state: `IsFullScreen` (bool), `IsControlsOverlayVisible` (bool, default
  `true`), computed `AreControlsVisible` (`!IsFullScreen || IsControlsOverlayVisible`).
- New commands: `ToggleFullScreenCommand`, `ShowControlsOverlayCommand`.
- New constructor dependency: `IImmersiveModeService immersiveMode` (8th parameter).
- `KeepScreenOn` is driven by the existing `IsPlaying` observable (via the generated
  `OnIsPlayingChanged` partial method), not by `IsFullScreen` (D3).
- A new `TimeProvider`-driven, single-shot 3 s auto-hide timer for
  `IsControlsOverlayVisible`, reset by `NotifyControlInteraction()` — called from every
  existing control-affecting command (`Stop`, `PtzNudge`, `PtzZoomNudge`, `PtzAutoFocus`,
  `ChangeQualityProfileAsync`, `CancelRetry`, `Reconnect`, `OnIsAudioEnabledChanged`) and from
  the new `ShowControlsOverlayCommand`.

## View Changes (`ViewerView.xaml(.cs)`)

- Canvas/border becomes full-bleed (`Grid.RowSpan` trigger + unconstrained `HeightRequest`)
  when `IsFullScreen`; controls `ScrollView` becomes a translucent, auto-hiding overlay
  (`Grid.RowSpan` + `BackgroundColor` trigger, `IsVisible` bound to `AreControlsVisible`).
- New full-screen toggle button (always present, `IsVisible={Binding IsPlaying}`), a
  single-tap gesture on the video (reveal controls) and a double-tap gesture (toggle full
  screen).
- `ViewerView.xaml.cs` owns presenting/dismissing `FullScreenViewerPage` by reacting to its
  bound `ViewerViewModel.IsFullScreen` — **no changes are needed in `ViewerPage.xaml(.cs)` or
  `SourceListPage.xaml(.cs)`** (see plan.md §3 for why).

## New Host (`FullScreenViewerPage.xaml(.cs)`, MauiApp)

Chromeless `ContentPage`, pushed modally, hosting a second `ViewerView` bound to the donor's
`ViewerViewModel` instance. Drives `IImmersiveModeService.EnterImmersive/ExitImmersive` from its
own `OnAppearing`/`OnDisappearing`; overrides `OnBackButtonPressed` to close itself only;
subscribes to `IAppLifecycleService.AppPaused` to force-exit full screen when backgrounded (D4).

---

## Known Testing Limitation — `FakeTimeProvider` does not simulate timers

`src/Core/Services/FakeTimeProvider.cs` (this repo's own hand-rolled fake, **not** the
`Microsoft.Extensions.TimeProvider.Testing` package referenced in
`tests/MauiApp.Tests/NdiForAndroid.Tests.csproj` but never actually used anywhere) overrides
`GetUtcNow()`/`GetTimestamp()` to return a controllable value, but its `CreateTimer(...)`
override **schedules a real `System.Threading.Timer` on real wall-clock time** — `Advance()`/
`AdvanceSeconds()` have **no effect** on when a timer callback actually fires. This is a
pre-existing gap: none of `ViewerViewModel`'s existing reconnect timers (`_countdownTimer`,
`_attemptTimer`) are tested for their real timer-firing behavior either — `ViewerViewModelTests`
only asserts the synchronous state set by calling `BeginReconnectWindow()`/`CancelRetryCommand`
directly, never by advancing the clock and waiting for a callback.

**Testable seam for this feature:** the new overlay auto-hide callback method
(`HideControlsOverlay()`) is declared `internal` (not `private`) specifically so tests can invoke
it directly — exactly mirroring how `BeginReconnectWindow()`/`CheckForUnexpectedDrop()` are
`public` today for the same reason. `src/Core/NdiForAndroid.Core.csproj` gains an
`InternalsVisibleTo` entry for the test assembly (`NdiForAndroid.Tests`). Unit tests verify the
**state transition** (`ResetOverlayAutoHideTimer()` → `HideControlsOverlay()` → visibility
false, guarded correctly after exiting full screen) but **do not and cannot verify** that the
timer fires after a real 3 seconds — that remains a manual/on-device check (tasks.md T12).

---

## Out of Scope

- Sending side (Stream tab / `OutputPage`) full-screen treatment.
- Orientation lock.
- Restoring full-screen state across app backgrounding/resume.
- Any change to `INdiViewerBridge` / bridge models.
- A real, timer-simulating `FakeTimeProvider` overhaul (documented as a known gap, not fixed
  here — it would affect the existing reconnect feature too and is a separate concern).

---

## Acceptance Criteria (from issue #338)

- [ ] Full-screen toggle button visible on the `ViewerView` overlay whenever a source is
      playing (phone `ViewerPage` and the tablet's embedded pane); double-tapping the video also
      toggles full screen.
- [ ] Entering full screen expands the video to fill the entire screen — including the tablet
      rail / phone Shell top+tab bars and the Android system bars — while preserving aspect-fit
      letterboxing and the tally-red border.
- [ ] While full screen, PTZ/quality/audio/stop controls are hidden by default, reappear on a
      single tap on the video, and auto-hide again after ~3 s idle.
- [ ] Exiting full screen (toggle, double-tap, or Android back) restores the exact prior layout
      without stopping playback, disposing the ViewModel, or calling
      `INdiViewerBridge.StopReceiver()`.
- [ ] Toggling full screen never invokes `INavigationHandoffService.HandlePrimaryDestinationChangeAsync`
      (unit test asserting no `bridge.StopReceiver()` call across an on→off cycle).
- [ ] Navigating away from View while full screen is active still stops the receiver exactly
      once via the existing handoff path.
- [ ] Android system bars stay hidden but reappear on an edge swipe without exiting full screen;
      the device does not sleep for the duration of playback (D3).
- [ ] On the Galaxy Tab A9+ landscape two-pane layout, engaging full screen on the embedded pane
      hides the list, the rail, and any Shell top bar, leaving only video + auto-hiding overlay.
- [ ] `dotnet build NdiForAndroid.sln` succeeds and `dotnet test tests/MauiApp.Tests` passes,
      including new tests for `IsFullScreen`/`ToggleFullScreenCommand` and the overlay
      auto-hide/reset state transitions on `ViewerViewModel`.

---

## Open Questions (not blocking implementation — flag to product owner if they care)

- **Pre-API-30 devices** (minSdk 26): `WindowInsetsControllerCompat` bar-hiding requires API 30+.
  Default: `EnterImmersive()`/`ExitImmersive()` silently no-op below API 30 (chromeless layout
  and keep-screen-on still apply; only the system status/nav bars stay visible). No fallback
  `SYSTEM_UI_FLAG_*` path is implemented.
- **Modal transition animation**: the issue's open questions raised "must feel like one
  continuous view, no page transition animation" as a possible preference. Not resolved in the
  owner's decisions comment. Default: MAUI's normal modal push/pop animation (no special
  handling). Trivial to pass `animated: false` to `PushModalAsync`/`PopModalAsync` later if the
  product owner wants a hard cut instead.
- **D6** (Stop exits full screen) is an implementer default, not an owner decision — confirm or
  reject during review.
