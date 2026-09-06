# Technical Plan — Full-Screen Mode for the NDI Viewer

**Issue:** #338
**Branch:** `feature/338-viewer-fullscreen`
**Companion spec:** `docs/features/viewer-fullscreen/spec.md`
**Status:** Ready for architect validation, then `feature.breakdown`

---

## 1. Overview

Add a full-screen toggle to `ViewerView` (shared by the phone's pushed `ViewerPage` and the
tablet's embedded `SourceListPage` pane). Entering full screen presents a new chromeless modal
page, `FullScreenViewerPage`, via `Navigation.PushModalAsync`, hosting a **second** `ViewerView`
instance bound to the **same** `ViewerViewModel` object the donor host was already using — never
a freshly DI-resolved one. The donor's `ViewerView.StopRendering()`/`StartRendering()` are called
explicitly around the modal push/pop (not left to page lifecycle). System-bar hiding and
keep-screen-on go through a new Core interface, `IImmersiveModeService` (Android impl + Noop
twin, Rule 5). **No changes to `AppShell.xaml.cs`, `ViewerPage.xaml(.cs)`, or
`SourceListPage.xaml(.cs)`** — see §3.

---

## 2. Requirements

- **FR1** — `ViewerViewModel` gains `IsFullScreen` (bool) + `ToggleFullScreenCommand`; turns
  full screen **on** only while `IsPlaying` (no-op otherwise); always allowed to turn off.
- **FR2** — `ViewerView` presents `FullScreenViewerPage` via `PushModalAsync` when the bound VM's
  `IsFullScreen` becomes `true`, calling its own `StopRendering()` first.
- **FR3** — `FullScreenViewerPage` hosts its own `ViewerView` (`IsModalHost="True"`, same VM),
  drives its render loop from `OnAppearing`/`OnDisappearing`, and on close invokes a
  caller-supplied callback that resumes the donor's `StartRendering()`.
- **FR4** — `FullScreenViewerPage.OnBackButtonPressed` sets `IsFullScreen = false`, returns
  `true` (never falls through to Shell/pop-donor).
- **FR5** — `ViewerView`'s canvas/border go full-bleed and its controls `ScrollView` becomes a
  translucent auto-hiding overlay while `IsFullScreen`; unchanged in normal mode.
- **FR6** — `IsControlsOverlayVisible` starts `true` whenever `IsFullScreen` turns on; a 3 s
  `TimeProvider`-driven single-shot timer sets it `false` unless reset. `NotifyControlInteraction()`
  (called by every control command + `ShowControlsOverlayCommand`) resets the timer while full
  screen; no-ops otherwise.
- **FR7** — `IImmersiveModeService.KeepScreenOn(bool)` is called from `ViewerViewModel`'s
  existing `OnIsPlayingChanged` partial — active whenever `IsPlaying`, independent of
  `IsFullScreen` (spec D3).
- **FR8** — `FullScreenViewerPage` calls `EnterImmersive()`/`ExitImmersive()` from
  `OnAppearing`/`OnDisappearing`.
- **FR9** — `FullScreenViewerPage` subscribes to `IAppLifecycleService.AppPaused`, sets
  `IsFullScreen = false` — never restored after resume.
- **FR10** — `Stop()` also sets `IsFullScreen = false` (spec D6, implementer default).
- **FR11** — No changes anywhere to `INdiViewerBridge`, `NdiNavigationHandoffService`,
  `AppShell.xaml.cs`, `ViewerPage.xaml(.cs)`, or `SourceListPage.xaml(.cs)`.

**NFRs**: all new state stays in Core/`ViewerViewModel` (MAUI-free, testable); zero NDI types
added; `dotnet build` stays green per task; new Android APIs isolated behind
`IImmersiveModeService`; the overlay timer is disposed on exit and on `Dispose()`.

---

## 3. Architecture Fit — why the modal is structurally handoff-safe

Verified against `src/MauiApp/AppShell.xaml.cs`: `OnNavigating` (override of `Shell.OnNavigating`)
and `OnShellNavigated` (subscribed to `Shell.Navigated`) are the **only** places
`INavigationHandoffService.HandlePrimaryDestinationChangeAsync` is invoked (lines ~210–265), and
both fire only for **Shell route changes** (`GoToAsync`, tab/rail switches, Shell's own
back-stack handling). `Navigation.PushModalAsync`/`PopModalAsync` operate on `INavigation`'s
separate **modal stack** — pushing/popping a modal does not change `Shell.CurrentState` and does
not raise `Shell.Navigating`/`Shell.Navigated`. `ParseDestination` never runs for the full-screen
modal; nothing to guard against, **no change to `AppShell.xaml.cs` needed**.
`NdiNavigationHandoffService` only calls `_viewerBridge.StopReceiver()` `if (from ==
PrimaryNavDestination.View)` inside that handoff call — never invoked by the modal, so
`StopReceiver()` cannot be triggered by toggling full screen (unit test, §6).

**Why the donor pages need no changes:** every concern is owned by `ViewerView`
(present/dismiss, toggle button/gestures), `ViewerViewModel` (all state, incl. `KeepScreenOn`
keyed off `IsPlaying`), or `FullScreenViewerPage` (system bars, back button, resume handling).
The donor's `OnAppearing`/`OnDisappearing` are **not** relied on for the render-loop hand-off — a
donor's `OnDisappearing` is **not guaranteed to fire** when a modal is pushed over it, so
`ViewerView.xaml.cs` calls `StopRendering()`/`StartRendering()` explicitly (§4.5).

**Architect gate:** third `ViewerView` host — a structural change. Run `solution-architect`
against this plan before implementation (tasks.md T1); confirm the deviation from
`docs/architecture.md`'s "two hosts" framing.

---

## 4. Technical Approach

### 4.1 `IImmersiveModeService` (Core contract, Android impl, Noop twin)

```csharp
// src/Core/Services/IImmersiveModeService.cs (NEW)
namespace NdiForAndroid.Services;
public interface IImmersiveModeService
{
    void EnterImmersive();     // hide system bars (swipe-to-reveal remains)
    void ExitImmersive();      // restore system bars
    void KeepScreenOn(bool enabled);
}
```

`src/MauiApp/Platforms/Android/Services/AndroidImmersiveModeService.cs` (NEW):
- `EnterImmersive()` / `ExitImmersive()`: resolve `Microsoft.Maui.ApplicationModel.Platform.
  CurrentActivity`; no-op if `activity?.Window is null` or
  `!OperatingSystem.IsAndroidVersionAtLeast(30)` (pre-30 fallback: chromeless layout +
  keep-screen-on still apply, bars stay visible). Otherwise
  `var controller = AndroidX.Core.View.WindowCompat.GetInsetsController(activity.Window,
  activity.Window.DecorView);` then `controller.SystemBarsBehavior =
  (int)WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe; controller.Hide(
  WindowInsetsCompat.Type.SystemBars());` (Enter) / `controller.Show(WindowInsetsCompat.Type.
  SystemBars());` (Exit). This mirrors the exact `WindowCompat.GetInsetsController` +
  API-30 guard precedent in `MauiAppearanceService.UpdateAndroidStatusBar`
  (`src/MauiApp/Features/Settings/Services/MauiAppearanceService.cs:217`).
- `KeepScreenOn(bool enabled)`: `Microsoft.Maui.Devices.DeviceDisplay.Current.KeepScreenOn =
  enabled;` — cross-platform MAUI Essentials API, no raw `Window` flags needed.

`src/MauiApp/Services/NoopImmersiveModeService.cs` (NEW) — all three members empty bodies,
mirrors `NoopMulticastLockService.cs` exactly.

### 4.2 DI registrations (`src/MauiApp/MauiProgram.cs`)

In the existing `#if ANDROID / #else` block (~line 104–120), alongside `IMulticastLockService`:

```csharp
#if ANDROID
builder.Services.AddSingleton<IImmersiveModeService, AndroidImmersiveModeService>();
#else
builder.Services.AddSingleton<IImmersiveModeService, NoopImmersiveModeService>();
#endif
```

Near the existing `Func<ViewerViewModel>` factory (~line 129) and Views section (~line 137), the
same lazy-factory pattern (`ViewerView` cannot receive it via constructor DI — see §4.5):

```csharp
builder.Services.AddTransient<Features.Viewer.Views.FullScreenViewerPage>();
builder.Services.AddSingleton<Func<Features.Viewer.Views.FullScreenViewerPage>>(
    sp => () => sp.GetRequiredService<Features.Viewer.Views.FullScreenViewerPage>());
```

### 4.3 `ViewerViewModel` changes (`src/Core/Features/Viewer/ViewModels/ViewerViewModel.cs`)

Constructor gains an 8th parameter, `IImmersiveModeService immersiveMode` → `_immersiveMode`.

```csharp
private const int OverlayAutoHideSeconds = 3;
private ITimer? _overlayAutoHideTimer;

[ObservableProperty][NotifyPropertyChangedFor(nameof(AreControlsVisible))]
private bool _isFullScreen;

[ObservableProperty][NotifyPropertyChangedFor(nameof(AreControlsVisible))]
private bool _isControlsOverlayVisible = true;

public bool AreControlsVisible => !IsFullScreen || IsControlsOverlayVisible;

partial void OnIsFullScreenChanged(bool value)
{
    if (value) { IsControlsOverlayVisible = true; ResetOverlayAutoHideTimer(); }
    else { _overlayAutoHideTimer?.Dispose(); _overlayAutoHideTimer = null; IsControlsOverlayVisible = true; }
}

[RelayCommand]
private void ToggleFullScreen()
{
    if (!IsPlaying && !IsFullScreen) return; // nothing to show full-screen
    IsFullScreen = !IsFullScreen;
}

[RelayCommand]
private void ShowControlsOverlay() => NotifyControlInteraction();

private void NotifyControlInteraction()
{
    if (IsFullScreen) ResetOverlayAutoHideTimer();
}

private void ResetOverlayAutoHideTimer()
{
    IsControlsOverlayVisible = true;
    var due = TimeSpan.FromSeconds(OverlayAutoHideSeconds);
    if (_overlayAutoHideTimer is null)
        _overlayAutoHideTimer = _timeProvider.CreateTimer(
            _ => _dispatcher.BeginInvokeOnMainThread(HideControlsOverlay), null, due, Timeout.InfiniteTimeSpan);
    else
        _overlayAutoHideTimer.Change(due, Timeout.InfiniteTimeSpan);
}

// internal (not private): direct testable seam — spec.md "Known Testing Limitation".
// Guarded so a stale callback (fullscreen already exited/re-entered) can't wrongly hide controls.
internal void HideControlsOverlay()
{
    if (IsFullScreen) IsControlsOverlayVisible = false;
}
```

Modify existing members:
- `partial void OnIsPlayingChanged(bool value)` — add `_immersiveMode.KeepScreenOn(value);`
  (keep the existing `_wasPlayingBeforeResume = value;`).
- `Stop()` — add `IsFullScreen = false;` (FR10; right after `_userInitiatedStop = true;`).
- `Dispose()` — add `_overlayAutoHideTimer?.Dispose();` alongside `DisposeTimers()`.
- Add `NotifyControlInteraction();` as the **first statement** in: `PtzNudge`, `PtzZoomNudge`,
  `PtzAutoFocus`, `ChangeQualityProfileAsync`, `CancelRetry`, `Reconnect`, and
  `OnIsAudioEnabledChanged`. (`Stop` already exits full screen via FR10 — no separate call.)

**`ITimer` vs `Timer`**: `TimeProvider.CreateTimer(...)`'s declared return type is
`System.Threading.ITimer`. The existing `_countdownTimer`/`_attemptTimer` fields are typed
`Timer` only because they're constructed via `new Timer(...)` directly, **not**
`_timeProvider.CreateTimer(...)` — a pre-existing deviation from "all timing via `TimeProvider`."
Don't copy that; use `_timeProvider.CreateTimer` + `ITimer` as above (spec D5 explicitly calls
for a "TimeProvider-driven timer").

### 4.4 Test seam (`src/Core/NdiForAndroid.Core.csproj`)

```xml
<ItemGroup>
  <InternalsVisibleTo Include="NdiForAndroid.Tests" />
</ItemGroup>
```

Lets `tests/MauiApp.Tests` (assembly `NdiForAndroid.Tests`) call `HideControlsOverlay()` directly.

### 4.5 `ViewerView` changes (`src/MauiApp/Features/Viewer/Views/ViewerView.xaml(.cs)`)

**XAML** — existing `StatusMessage`/quality/audio/PTZ/retry/Stop content stays as-is. Changes:

1. Root `Grid`: add a `Style` with `Padding=16`/`RowSpacing=16` defaults and a `DataTrigger` on
   `IsFullScreen=True` setting both to `0`.
2. Canvas `Border` (`Grid.Row="0"`): its existing `Style` (which already has the `IsTallyProgram`
   `DataTrigger` for `Stroke`) gains a base `Setter Property="Grid.RowSpan" Value="1"` and a
   second `DataTrigger` on `IsFullScreen=True` setting `Grid.RowSpan="2"`. Add two
   `GestureRecognizers` to the `Border`: `TapGestureRecognizer NumberOfTapsRequired="1"
   Command="{Binding ShowControlsOverlayCommand}"` and `NumberOfTapsRequired="2"
   Command="{Binding ToggleFullScreenCommand}"`.
3. `VideoCanvas` (`skia:SKCanvasView`): remove the static `HeightRequest="240"`; add
   `VerticalOptions="Fill" HorizontalOptions="Fill"` and a `Style` with base
   `HeightRequest="240"`, `DataTrigger` on `IsFullScreen=True` setting `HeightRequest="-1"`
   (MAUI's "unconstrained" sentinel).
4. New `Button` (full-screen toggle), placed as a sibling after the `Border`:
   `Grid.Row="0" Grid.RowSpan="2"`, `Command="{Binding ToggleFullScreenCommand}"`,
   `IsVisible="{Binding IsPlaying}"`, `HorizontalOptions="End" VerticalOptions="Start"`,
   `Margin="8"`. `Style` with base `Text="⛶"`, `DataTrigger` on `IsFullScreen=True` →
   `Text="⤢"`.
5. Controls `ScrollView` (`Grid.Row="1"`): `IsVisible="{Binding AreControlsVisible}"` (replaces
   any prior binding); `Style` with base `Grid.RowSpan="1"`, `BackgroundColor="Transparent"`,
   `DataTrigger` on `IsFullScreen=True` → `Grid.RowSpan="2"`, `BackgroundColor="#AA000000"`. Its
   inner `VerticalStackLayout` gains `VerticalOptions="End"` (controls cluster to the bottom
   when overlaying full-bleed video).

**Code-behind** — a bindable `IsModalHost` property (so the modal's own embedded `ViewerView`
never presents a *nested* modal), and reacting to the bound VM's `IsFullScreen`:

```csharp
public static readonly BindableProperty IsModalHostProperty =
    BindableProperty.Create(nameof(IsModalHost), typeof(bool), typeof(ViewerView), false);

public bool IsModalHost { get => (bool)GetValue(IsModalHostProperty); set => SetValue(IsModalHostProperty, value); }

private ViewerViewModel? _boundViewModel;

protected override void OnBindingContextChanged()
{
    base.OnBindingContextChanged();
    if (_boundViewModel is not null) _boundViewModel.PropertyChanged -= OnViewModelPropertyChanged;
    _boundViewModel = BindingContext as ViewerViewModel;
    if (_boundViewModel is not null) _boundViewModel.PropertyChanged += OnViewModelPropertyChanged;
}

private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    if (IsModalHost) return;
    if (e.PropertyName != nameof(ViewerViewModel.IsFullScreen)) return;
    if (BindingContext is not ViewerViewModel vm || !vm.IsFullScreen) return;
    await PresentFullScreenAsync(vm);
}

private async Task PresentFullScreenAsync(ViewerViewModel vm)
{
    StopRendering(); // explicit hand-off — page lifecycle is not reliable under a modal push
    var factory = IPlatformApplication.Current?.Services.GetService<Func<FullScreenViewerPage>>();
    if (factory is null || Shell.Current is null) { StartRendering(); return; }
    var page = factory();
    page.Initialize(vm, onClosed: StartRendering);
    await Shell.Current.Navigation.PushModalAsync(page);
}
```

`IPlatformApplication.Current?.Services.GetService<T>()` matches the DI-resolution idiom already
used in `MainActivity.HandleDeepLinkAsync` — `ViewerView` is XAML-instantiated, not
DI-constructed, so this is the established seam to reach the container. Needs
`using Microsoft.Extensions.DependencyInjection;` and `using System.ComponentModel;`.

### 4.6 New page: `FullScreenViewerPage.xaml(.cs)` (`src/MauiApp/Features/Viewer/Views/`)

XAML: a plain `ContentPage` (`BackgroundColor="Black"`, `x:DataType="vm:ViewerViewModel"`)
whose only child is `<views:ViewerView x:Name="FullScreenViewer" IsModalHost="True" />`.
Chromeless by construction — a modally-pushed `ContentPage` has no Shell nav/tab bar, so no
`Shell.NavBarIsVisible` overrides are needed.

```csharp
public partial class FullScreenViewerPage : ContentPage
{
    private readonly IImmersiveModeService _immersiveMode;
    private readonly IAppLifecycleService _lifecycle;
    private ViewerViewModel? _viewModel;
    private Action? _onClosed;

    public FullScreenViewerPage(IImmersiveModeService immersiveMode, IAppLifecycleService lifecycle)
    {
        InitializeComponent();
        _immersiveMode = immersiveMode;
        _lifecycle = lifecycle;
    }

    public void Initialize(ViewerViewModel viewModel, Action onClosed)
    {
        _viewModel = viewModel;
        _onClosed = onClosed;
        BindingContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _lifecycle.AppPaused += OnAppPaused;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        FullScreenViewer.StartRendering();
        _immersiveMode.EnterImmersive();
    }

    protected override void OnDisappearing()
    {
        FullScreenViewer.StopRendering();
        _immersiveMode.ExitImmersive();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel is not null) _viewModel.IsFullScreen = false;
        return true; // swallow — never reaches Shell / the donor page's own back handling
    }

    private void OnAppPaused()
    {
        if (_viewModel is not null) _viewModel.IsFullScreen = false; // never restored on resume
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewerViewModel.IsFullScreen) && _viewModel?.IsFullScreen == false)
            _ = CloseAsync();
    }

    private async Task CloseAsync()
    {
        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _lifecycle.AppPaused -= OnAppPaused;
        if (Shell.Current?.Navigation.ModalStack.Contains(this) == true)
            await Shell.Current.Navigation.PopModalAsync();
        _onClosed?.Invoke();
    }
}
```

Both `ViewerView` instances (donor + this page's `FullScreenViewer`) share one
`ViewerViewModel`; toggling `IsFullScreen` off from either the modal's own toggle button or its
back button routes through the same `OnViewModelPropertyChanged` → `CloseAsync` path.

---

## 5. Data Layer

None — full-screen state is transient ViewModel/UI state, no SQLite/repository changes.

---

## 6. Testing Strategy

`tests/MauiApp.Tests/Features/Viewer/ViewerViewModelTests.cs` — extend. Add
`Mock<IImmersiveModeService> _immersiveModeMock = new();`, pass `.Object` as the 8th `CreateSut()`
arg (the only call site).

1. `ToggleFullScreenCommand_WhilePlaying_SetsIsFullScreenTrue`
2. `ToggleFullScreenCommand_WhileNotPlaying_DoesNothing`
3. `ToggleFullScreenCommand_Twice_ReturnsToFalse`
4. `EnteringFullScreen_SetsIsControlsOverlayVisibleTrue`
5. `HideControlsOverlay_WhileFullScreen_HidesOverlay` — call `sut.HideControlsOverlay()` directly
   (the testable seam, §4.4).
6. `HideControlsOverlay_AfterExitingFullScreen_IsGuardedNoOp`
7. `ShowControlsOverlayCommand_WhileFullScreen_RevealsControls`
8. `NotifyControlInteraction_ViaPtzAutoFocusCommand_RevealsControls`
9. `Stop_WhileFullScreen_ExitsFullScreen` (FR10)
10. `Dispose_WhileFullScreenTimerPending_DoesNotThrow`
11. `OnIsPlayingChanged_True_CallsKeepScreenOnTrue` / `_False_CallsKeepScreenOnFalse`
12. `ToggleFullScreen_OnAndOff_NeverCallsStopReceiver` — maps to the acceptance criterion;
    `_bridgeMock.Verify(b => b.StopReceiver(), Times.Never)` across a full on→off cycle.

**Not tested** (documented limitation, spec.md): the auto-hide timer actually firing after a
real 3 s. `AndroidImmersiveModeService`/`NoopImmersiveModeService` and
`FullScreenViewerPage`/`ViewerView` code-behind changes are UI/platform plumbing, verified via
`android-build-install-run` (tasks.md T12), not xUnit — no existing precedent unit-tests
`AndroidMulticastLockService`/`NoopMulticastLockService` either.

---

## 7. Risks & Edge Cases

| Risk / Edge case | Mitigation |
|---|---|
| Donor `OnDisappearing` unreliable under a modal push | `ViewerView.PresentFullScreenAsync` calls `StopRendering()`/`StartRendering()` explicitly (§4.5). |
| Two concurrent playing `ViewerViewModel`s both calling `KeepScreenOn` | Not possible today — `SourceListViewModel.OnWindowSizeClassChanged` already stops pane playback before a size-class transition could overlap with a pushed `ViewerPage`. Direct set/clear (no ref-count) is safe under that invariant; flag if it changes. |
| MAUI doesn't disambiguate a `NumberOfTapsRequired="1"` vs `="2"` `TapGestureRecognizer` on the same element | A genuine double-tap may fire both handlers (brief "reveal controls" flash before collapsing to full screen). Cosmetic only. Verify on-device via `android-build-install-run` (tasks.md T12); `ShowControlsOverlayCommand` already only acts while `IsFullScreen`, no further guard expected. |
| Pre-API-30 devices never get bars hidden | Documented default (spec.md Open Questions); chromeless layout + keep-screen-on still work. |
| `FakeTimeProvider.CreateTimer` doesn't simulate — real firing untestable | Testable seam via `internal HideControlsOverlay()` (§4.4), documented not silently ignored. |
| Nested re-entry via the modal's own `ViewerView` | Guarded by `IsModalHost="True"` — its handler early-returns. |
| App killed (not just backgrounded) while full screen | Process restart recreates the VM graph fresh (`IsFullScreen` defaults `false`). |

---

## 8. Constitution / Architecture Compliance

| Rule | How this plan satisfies it |
|---|---|
| Rule 1 — no direct DB access from ViewModels | Not touched. |
| Rule 2 — no NDI types cross the bridge | Zero bridge members added. |
| Rule 3 — no business logic in Views | `ViewerView.xaml.cs`'s modal present/dismiss is navigation/rendering plumbing (same category as its existing `StartRendering`/`StopRendering`); all state lives in `ViewerViewModel`. |
| Rule 4 — timer callbacks marshal to the UI thread | New timer wraps its mutation in `_dispatcher.BeginInvokeOnMainThread(...)`, matching existing countdown/attempt timers. |
| Rule 5 — Android APIs isolated behind interfaces | `IImmersiveModeService` + Android/Noop pair, registered conditionally, mirroring `IMulticastLockService`. |
| Rule 6 — every captured frame freed | Untouched — no bridge/frame-lifecycle code here. |

---

## 9. Open Questions

Carried from spec.md: pre-API-30 fallback, modal transition animation, and D6 (`Stop` exits full
screen) are implementer defaults pending product-owner confirmation — none block starting
implementation.
