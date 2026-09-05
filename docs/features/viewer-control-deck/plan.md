# Technical Plan — Viewer Control Deck (fixed layout, no scrolling)

**Issue:** #342
**Branch:** `feature/342-viewer-control-deck`
**Companion spec:** `docs/features/viewer-control-deck/spec.md`
**Status:** Ready for architect validation, then `feature.breakdown`

---

## 1. Overview — file split

`ViewerView.xaml` currently hosts one `ScrollView` with everything stacked vertically
(status → quality → audio → `PtzPanelView` → reconnect UI → Stop), shown identically in normal
and full-screen mode (only padding/spacing differ via `IsFullScreen` triggers). This plan splits
that into **five new `ContentView`s** under `src/MauiApp/Features/Viewer/Views/`, all bound
implicitly to the inherited `ViewerViewModel` (`x:DataType="vm:ViewerViewModel"`, no
`BindingContext` of their own — same pattern as the existing `PtzPanelView`):

| View | Role | Used by |
|---|---|---|
| `PlaybackControlsView.xaml(.cs)` | Status, quality segmented buttons, audio, full-screen toggle, Stop, reconnect UI | `ViewerControlDeck`, `ViewerControlSheet` (Weergave tab) |
| `CameraControlsView.xaml(.cs)` | Endpoint chip, d-pad, zoom rocker, preset grid (tap/long-press) | `ViewerControlDeck`, `ViewerControlSheet` (PTZ tab) |
| `ViewerControlDeck.xaml(.cs)` | Fixed-height (200) two-column Grid host | `ViewerView` (Medium/Expanded, normal) |
| `ViewerControlSheet.xaml(.cs)` | Draggable bottom-sheet overlay with 2 tabs | `ViewerView` (Compact, normal) |
| `FullScreenControlsOverlay.xaml(.cs)` | Wireframe-A overlay (d-pad/zoom/presets/toolbar) | `ViewerView` (full screen) |

`PtzPanelView.xaml(.cs)` and `PtzEndpointFormViewModel`'s consumer wiring are **deleted**
(superseded by `CameraControlsView` + the unchanged `PtzEndpointPanel`, which is still reused
as-is — the issue explicitly keeps that dialog).

`ViewerView.xaml` keeps: the video `Border`/`SKCanvasView` (**unchanged** — still a fixed 240 dp
canvas; this feature does not touch video sizing), and the existing `PtzEndpointPanel` overlay
(unchanged, stays topmost). It gains a root-level 3-way switch between `ViewerControlDeck` /
`ViewerControlSheet` / `FullScreenControlsOverlay`, computed in code-behind from
`IWindowSizeClassService.Current` + `ViewerViewModel.IsFullScreen` (§2).

No changes to `INdiViewerBridge`, `IPtzController`, VISCA transport, `AppShell.xaml.cs`,
`ViewerPage.xaml(.cs)`, or `SourceListPage.xaml(.cs)`.

---

## 2. `ViewerView.xaml(.cs)` — layout switch

### 2.1 XAML skeleton (root `Grid`, replaces current body)

```xml
<ContentView ... x:Name="Root">
    <Grid x:Name="RootGrid" RowDefinitions="Auto,Auto">
        <!-- existing Grid.Style triggers for Padding/RowSpacing on IsFullScreen: UNCHANGED -->

        <!-- 1. Video border + SKCanvasView: UNCHANGED from current file (Grid.Row=0,
             RowSpan trigger to 2 on IsFullScreen, tally-red Stroke trigger, double/single tap
             gestures). Do not modify. -->
        <Border Grid.Row="0"> ... </Border>

        <!-- 2. Deck: Medium/Expanded, normal mode -->
        <views:ViewerControlDeck Grid.Row="1"
            IsVisible="{Binding IsDeckVisible, Source={x:Reference Root}}" />

        <!-- 3. Sheet: Compact, normal mode. Overlays from the bottom, spans both rows so it can
             ride up over the video when expanded. -->
        <views:ViewerControlSheet Grid.RowSpan="2" VerticalOptions="End"
            IsVisible="{Binding IsSheetVisible, Source={x:Reference Root}}" />

        <!-- 4. Full-screen overlay: wireframe A -->
        <views:FullScreenControlsOverlay Grid.RowSpan="2"
            IsVisible="{Binding IsFullScreenOverlayVisible, Source={x:Reference Root}}" />

        <!-- 5. Endpoint dialog: UNCHANGED, stays topmost -->
        <views:PtzEndpointPanel Grid.RowSpan="2" />
    </Grid>
</ContentView>
```

Remove entirely: the old floating full-screen toggle `Button` (its job moves into
`PlaybackControlsView`'s Row 2 and `FullScreenControlsOverlay`'s toolbar), and the old
`ScrollView` + `VerticalStackLayout` controls stack (replaced by items 2–4 above).

### 2.2 Code-behind additions (`ViewerView.xaml.cs`)

Three new read-only bindable properties, computed together whenever either input changes:

```csharp
public static readonly BindableProperty IsDeckVisibleProperty =
    BindableProperty.Create(nameof(IsDeckVisible), typeof(bool), typeof(ViewerView), false);
public bool IsDeckVisible { get => (bool)GetValue(IsDeckVisibleProperty); private set => SetValue(IsDeckVisibleProperty, value); }

public static readonly BindableProperty IsSheetVisibleProperty =
    BindableProperty.Create(nameof(IsSheetVisible), typeof(bool), typeof(ViewerView), false);
public bool IsSheetVisible { get => (bool)GetValue(IsSheetVisibleProperty); private set => SetValue(IsSheetVisibleProperty, value); }

public static readonly BindableProperty IsFullScreenOverlayVisibleProperty =
    BindableProperty.Create(nameof(IsFullScreenOverlayVisible), typeof(bool), typeof(ViewerView), false);
public bool IsFullScreenOverlayVisible { get => (bool)GetValue(IsFullScreenOverlayVisibleProperty); private set => SetValue(IsFullScreenOverlayVisibleProperty, value); }

private IWindowSizeClassService? _windowSizeClassService;
```

- Constructor: after `InitializeComponent()`, resolve
  `_windowSizeClassService = IPlatformApplication.Current?.Services.GetService<IWindowSizeClassService>();`
  then `if (_windowSizeClassService is not null) _windowSizeClassService.Changed += OnWindowSizeClassChanged;`
  then call `UpdateLayoutVisibility();` (same DI-resolution pattern already used for
  `Func<FullScreenViewerPage>` in `PresentFullScreenAsync`).
- `OnWindowSizeClassChanged(object?, WindowSizeClass) => UpdateLayoutVisibility();`
- `OnBindingContextChanged()`: after the existing subscribe/unsubscribe logic, call
  `UpdateLayoutVisibility();`.
- `OnViewModelPropertyChanged`: **extend** the existing `if (e.PropertyName !=
  nameof(ViewerViewModel.IsFullScreen)) return;` early-return block — before that early return,
  add `if (e.PropertyName == nameof(ViewerViewModel.IsFullScreen)) UpdateLayoutVisibility();` (do
  this as a separate check at the top of the method, not inside the existing full-screen-modal
  branch, since `IsModalHost` instances also need their overlay flag updated but must *skip* the
  modal-presenting logic below).
- New private method:
  ```csharp
  private void UpdateLayoutVisibility()
  {
      var isFullScreen = _boundViewModel?.IsFullScreen ?? false;
      var sizeClass = _windowSizeClassService?.Current ?? WindowSizeClass.Compact;
      IsFullScreenOverlayVisible = isFullScreen;
      IsDeckVisible = !isFullScreen && sizeClass != WindowSizeClass.Compact;
      IsSheetVisible = !isFullScreen && sizeClass == WindowSizeClass.Compact;
  }
  ```
- `Teardown()`: add `if (_windowSizeClassService is not null) _windowSizeClassService.Changed -= OnWindowSizeClassChanged;`
  (mirrors the existing VM-unsubscribe there — the modal-host instance must not leak this
  subscription either).

This keeps the Compact/Medium/Expanded decision entirely in the existing, already-tested
`WindowSizeClassService` (Core, MAUI-free) — **no new ViewModel property**, and no new
width-measurement plumbing. `ViewerViewModel` and its tests are unaffected by this rule.

---

## 3. `PlaybackControlsView.xaml` (new)

```xml
<ContentView ... x:Class="...PlaybackControlsView" x:DataType="vm:ViewerViewModel">
    <Grid RowDefinitions="Auto,Auto,Auto" RowSpacing="6">
        <Label Grid.Row="0" Text="{Binding StatusMessage}" FontSize="13"
               LineBreakMode="TailTruncation" TextColor="{DynamicResource TextPrimary}" />

        <!-- Row 1: quality segmented group (normal) XOR reconnect UI (IsReconnecting) XOR
             a lone Reconnect button (CanReconnect, terminal-failed state) -->
        <Grid Grid.Row="1">
            <Grid ColumnDefinitions="*,*,*" ColumnSpacing="4" HeightRequest="40">
                <Grid.Style>
                    <Style TargetType="Grid">
                        <Setter Property="IsVisible" Value="{Binding IsPlaying}" />
                        <Style.Triggers>
                            <DataTrigger TargetType="Grid" Binding="{Binding IsReconnecting}" Value="True">
                                <Setter Property="IsVisible" Value="False" />
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </Grid.Style>
                <!-- Smooth / Balanced / High: same ChangeQualityProfileCommand as today.
                     "Filled" (selected) look via a DataTrigger per button on QualityProfile. -->
                <Button Grid.Column="0" x:Name="SmoothButton" Text="Smooth"
                        Command="{Binding ChangeQualityProfileCommand}" CommandParameter="Smooth" />
                <Button Grid.Column="1" x:Name="BalancedButton" Text="Balanced"
                        Command="{Binding ChangeQualityProfileCommand}" CommandParameter="Balanced" />
                <Button Grid.Column="2" x:Name="HighButton" Text="High"
                        Command="{Binding ChangeQualityProfileCommand}" CommandParameter="High" />
                <!-- On each button: Style with a DataTrigger Binding="{Binding QualityProfile}"
                     Value="Smooth"/"Balanced"/"High" (matching its own segment) setting
                     BackgroundColor="{DynamicResource Primary}" TextColor="{DynamicResource OnPrimary}";
                     default (unselected) BackgroundColor="{DynamicResource ControlBackground}"
                     TextColor="{DynamicResource TextPrimary}". -->
            </Grid>

            <HorizontalStackLayout Spacing="6" IsVisible="{Binding IsReconnecting}">
                <ActivityIndicator IsRunning="{Binding IsReconnecting}" Color="{DynamicResource TextPrimary}"
                                   WidthRequest="20" HeightRequest="20" />
                <Label Text="{Binding RetryStatusMessage}" VerticalOptions="Center" FontSize="12" />
                <Button Text="Cancel" Command="{Binding CancelRetryCommand}" HeightRequest="32" FontSize="12" />
            </HorizontalStackLayout>

            <Button Text="Reconnect" Command="{Binding ReconnectCommand}"
                    IsVisible="{Binding CanReconnect}" HeightRequest="40" />
        </Grid>

        <!-- Row 2: audio, full-screen toggle, Stop -->
        <Grid Grid.Row="2" ColumnDefinitions="48,48,*" ColumnSpacing="6" HeightRequest="48"
              IsVisible="{Binding IsPlaying}">
            <Switch Grid.Column="0" IsToggled="{Binding IsAudioEnabled}"
                    WidthRequest="48" HeightRequest="48" HorizontalOptions="Center" />
            <Button Grid.Column="1" Text="⛶" Command="{Binding ToggleFullScreenCommand}"
                    WidthRequest="48" HeightRequest="48">
                <!-- Style.Triggers: Binding IsFullScreen=True -> Text="⤢" (same glyph swap as
                     today's floating button, now living here instead). -->
            </Button>
            <Button Grid.Column="2" Text="Stop" Command="{Binding StopCommand}"
                    HorizontalOptions="Fill" HeightRequest="48"
                    BackgroundColor="{DynamicResource ErrorRed}" TextColor="{DynamicResource White}" />
        </Grid>
    </Grid>
</ContentView>
```

No code-behind logic beyond `InitializeComponent()`.

---

## 4. `CameraControlsView.xaml(.cs)` (new)

Only rendered content when `IsPtzControlActive` is true (callers gate visibility of the *column*
or *tab*; the view itself does not need an outer `IsVisible` guard, but add one defensively:
root `IsVisible="{Binding IsPtzControlActive}"`).

### 4.1 XAML skeleton

```xml
<ContentView ... x:Class="...CameraControlsView" x:DataType="vm:ViewerViewModel"
             IsVisible="{Binding IsPtzControlActive}">
    <VerticalStackLayout Spacing="4">
        <!-- Endpoint status chip -->
        <Border Padding="8,3" StrokeShape="RoundRectangle 10"
                BackgroundColor="{DynamicResource CardBackground}"
                Stroke="{DynamicResource BorderColor}">
            <Border.GestureRecognizers>
                <TapGestureRecognizer Command="{Binding OpenPtzEndpointFormCommand}" />
            </Border.GestureRecognizers>
            <Label Text="{Binding PtzStatusText}" FontSize="11">
                <!-- Same TextColor DataTriggers on PtzLinkState as the old PtzPanelView:
                     Connected -> SuccessGreen, Error -> ErrorRed, default TextSecondary. -->
            </Label>
        </Border>

        <Grid ColumnDefinitions="Auto,Auto,Auto" ColumnSpacing="8">
            <!-- Col 0: d-pad, 3x3 grid of 48dp buttons, corners empty (same layout as the old
                 PtzPanelView d-pad, unchanged commands). -->
            <Grid Grid.Column="0" RowDefinitions="48,48,48" ColumnDefinitions="48,48,48"
                  RowSpacing="8" ColumnSpacing="8">
                <Button Grid.Row="0" Grid.Column="1" Text="▲" WidthRequest="48" HeightRequest="48"
                        Command="{Binding PtzNudgeCommand}" CommandParameter="up" />
                <Button Grid.Row="1" Grid.Column="0" Text="◄" WidthRequest="48" HeightRequest="48"
                        Command="{Binding PtzNudgeCommand}" CommandParameter="left" />
                <Button Grid.Row="1" Grid.Column="1" Text="AF" WidthRequest="48" HeightRequest="48"
                        Command="{Binding PtzAutoFocusCommand}" />
                <Button Grid.Row="1" Grid.Column="2" Text="►" WidthRequest="48" HeightRequest="48"
                        Command="{Binding PtzNudgeCommand}" CommandParameter="right" />
                <Button Grid.Row="2" Grid.Column="1" Text="▼" WidthRequest="48" HeightRequest="48"
                        Command="{Binding PtzNudgeCommand}" CommandParameter="down" />
            </Grid>

            <!-- Col 1: zoom rocker, two 48x48 buttons stacked, vertically centered against the
                 160dp-tall d-pad column. -->
            <VerticalStackLayout Grid.Column="1" Spacing="8" VerticalOptions="Center">
                <Button Text="T" WidthRequest="48" HeightRequest="48"
                        Command="{Binding PtzZoomNudgeCommand}" CommandParameter="in" />
                <Button Text="W" WidthRequest="48" HeightRequest="48"
                        Command="{Binding PtzZoomNudgeCommand}" CommandParameter="out" />
            </VerticalStackLayout>

            <!-- Col 2: preset grid, 2 rows x 4 columns of 48dp buttons, numbered 1-8.
                 Named individually (no BindableLayout) so code-behind can wire long-press
                 explicitly per button — see §4.2. None of these buttons set Command/
                 CommandParameter in XAML; Pressed/Released are wired entirely in code-behind. -->
            <Grid Grid.Column="2" RowDefinitions="48,48" ColumnDefinitions="48,48,48,48"
                  RowSpacing="8" ColumnSpacing="8" VerticalOptions="Center">
                <Button x:Name="Preset1Button" Grid.Row="0" Grid.Column="0" Text="1" WidthRequest="48" HeightRequest="48" />
                <Button x:Name="Preset2Button" Grid.Row="0" Grid.Column="1" Text="2" WidthRequest="48" HeightRequest="48" />
                <Button x:Name="Preset3Button" Grid.Row="0" Grid.Column="2" Text="3" WidthRequest="48" HeightRequest="48" />
                <Button x:Name="Preset4Button" Grid.Row="0" Grid.Column="3" Text="4" WidthRequest="48" HeightRequest="48" />
                <Button x:Name="Preset5Button" Grid.Row="1" Grid.Column="0" Text="5" WidthRequest="48" HeightRequest="48" />
                <Button x:Name="Preset6Button" Grid.Row="1" Grid.Column="1" Text="6" WidthRequest="48" HeightRequest="48" />
                <Button x:Name="Preset7Button" Grid.Row="1" Grid.Column="2" Text="7" WidthRequest="48" HeightRequest="48" />
                <Button x:Name="Preset8Button" Grid.Row="1" Grid.Column="3" Text="8" WidthRequest="48" HeightRequest="48" />
            </Grid>
        </Grid>

        <!-- Transient store confirmation, cleared automatically by the ViewModel -->
        <Label Text="{Binding PtzPresetStatusMessage}" FontSize="11" HorizontalOptions="Center"
               TextColor="{DynamicResource TextSecondary}"
               IsVisible="{Binding PtzPresetStatusMessage, Converter={StaticResource IsNotNullConverter}}" />
    </VerticalStackLayout>
</ContentView>
```

`IsNotNullConverter` already exists (used by `SourceListPage.xaml` for `ErrorMessage`) — reuse
it, no new converter.

### 4.2 Code-behind — tap vs. long-press (`CameraControlsView.xaml.cs`)

No new MAUI package. `Button` already exposes `Pressed`/`Released` events (fire for touch and
mouse alike) — use those plus a plain `System.Threading.Timer` for the 600 ms threshold. This
is view-layer gesture plumbing only (same category as the render loop / modal-presenting code
already in `ViewerView.xaml.cs`); the actual recall/store behavior is entirely in the ViewModel
commands.

```csharp
public partial class CameraControlsView : ContentView
{
    private const int LongPressThresholdMs = 600;

    public CameraControlsView()
    {
        InitializeComponent();
        WirePreset(Preset1Button, 1);
        WirePreset(Preset2Button, 2);
        WirePreset(Preset3Button, 3);
        WirePreset(Preset4Button, 4);
        WirePreset(Preset5Button, 5);
        WirePreset(Preset6Button, 6);
        WirePreset(Preset7Button, 7);
        WirePreset(Preset8Button, 8);
    }

    private void WirePreset(Button button, int presetNumber)
    {
        Timer? longPressTimer = null;
        var longPressFired = false;

        button.Pressed += (_, _) =>
        {
            longPressFired = false;
            longPressTimer = new Timer(_ =>
            {
                longPressFired = true;
                Dispatcher.Dispatch(() =>
                {
                    if (BindingContext is ViewerViewModel vm && vm.PtzStorePresetCommand.CanExecute(presetNumber))
                        vm.PtzStorePresetCommand.Execute(presetNumber);
                });
            }, null, LongPressThresholdMs, Timeout.Infinite);
        };

        button.Released += (_, _) =>
        {
            longPressTimer?.Dispose();
            longPressTimer = null;
            if (longPressFired)
                return; // already handled as a long-press store

            if (BindingContext is ViewerViewModel vm && vm.PtzRecallPresetCommand.CanExecute(presetNumber))
                vm.PtzRecallPresetCommand.Execute(presetNumber);
        };
    }
}
```

`Dispatcher` here is `ContentView.Dispatcher` (MAUI's `IDispatcher`, always available on a
`BindableObject`) — the timer callback runs on a thread-pool thread, so the command execution is
marshaled back with `Dispatcher.Dispatch`, consistent with how the rest of the app marshals
bridge-thread callbacks (`IMainThreadDispatcher` in the ViewModel layer; this is the View-layer
equivalent for a bare `System.Threading.Timer`).

---

## 5. `ViewerControlDeck.xaml(.cs)` (new)

```xml
<ContentView ... x:Class="...ViewerControlDeck" x:DataType="vm:ViewerViewModel">
    <Grid HeightRequest="200" Padding="12,6" ColumnDefinitions="*,Auto" ColumnSpacing="12"
          BackgroundColor="{DynamicResource CardBackground}">
        <views:PlaybackControlsView Grid.Column="0">
            <!-- Style.Triggers: Binding IsPtzControlActive=False -> Setter Grid.ColumnSpan=2
                 (spans the full deck width when there is no camera column — literal reading of
                 the acceptance criterion "no empty camera column"). -->
        </views:PlaybackControlsView>
        <views:CameraControlsView Grid.Column="1" />
    </Grid>
</ContentView>
```

`CameraControlsView`'s own root `IsVisible="{Binding IsPtzControlActive}"` (§4.1) collapses the
Auto column to 0 width when there's no PTZ; the `ColumnSpan=2` trigger on
`PlaybackControlsView` (via a `Style` with a `DataTrigger`, same technique used throughout this
codebase, e.g. `ViewerView.xaml`'s existing Border/ScrollView `Grid.RowSpan` triggers) makes it
visually fill the freed space rather than leaving a gap. No code-behind beyond
`InitializeComponent()`.

**Sizing budget** (validated against the 1280×800 dp Galaxy Tab A9+ reference, ~690 dp pane
width per the wireframe): Camera column content = d-pad (3×48+2×8=160) + 8 + zoom (48) + 8 +
presets (4×48+3×8=216) = **440 dp wide**, chip (~24 dp) + 4 dp spacing + 160 dp control row =
**188 dp tall**, fitting the deck's 200−12=188 dp interior height exactly. Playback column then
gets ≈690−440−12=**238 dp** — enough for the quality segmented row and the audio/full-screen/Stop
row (see §3), but tight; **on-device verification (tasks.md, device task) may require ±4–8 dp
tuning of `Spacing`/`Padding` values above** — this is expected tolerance, not a redesign.

---

## 6. `ViewerControlSheet.xaml(.cs)` (new — hand-built bottom sheet)

No `BottomSheet` control exists in `Microsoft.Maui.Controls` 10, and `NdiForAndroid.csproj` does
not reference `CommunityToolkit.Maui` (verified: only `Microsoft.Maui.Controls`, `sqlite-net-pcl`,
`Microsoft.Extensions.Logging.Debug`, `SkiaSharp.Views.Maui.Controls` are listed) — per the issue,
no new NuGet packages. Build it as a `Grid` overlay anchored to the bottom, sized to the
**Expanded** height always, positioned via `TranslationY` so only the **Half** portion is visible
by default (the standard bottom-sheet trick — avoids animating `HeightRequest`, which MAUI
handles poorly).

Constants: `HalfHeight = 320`, `ExpandedHeight = 440` (both `double`, `const` fields).

### 6.1 XAML skeleton

```xml
<ContentView ... x:Class="...ViewerControlSheet" x:DataType="vm:ViewerViewModel">
    <Grid x:Name="SheetContainer" HeightRequest="440" VerticalOptions="End"
          BackgroundColor="{DynamicResource CardBackground}"
          RowDefinitions="16,32,*">
        <!-- Row 0: drag handle -->
        <BoxView Grid.Row="0" WidthRequest="36" HeightRequest="4" CornerRadius="2"
                 Color="{DynamicResource DividerColor}" HorizontalOptions="Center" VerticalOptions="Center" />

        <!-- Row 1: two MD3 secondary tabs -->
        <Grid Grid.Row="1" ColumnDefinitions="*,*">
            <Button x:Name="PlaybackTabButton" Grid.Column="0" Text="Weergave"
                    BackgroundColor="Transparent" />
            <Button x:Name="PtzTabButton" Grid.Column="1" Text="PTZ"
                    BackgroundColor="Transparent" IsVisible="{Binding IsPtzControlActive}" />
            <!-- Selected-tab indicator: a thin BoxView under the active tab, moved in code-behind
                 (no data binding needed — internal UI-only state). -->
        </Grid>

        <!-- Row 2: tab content, no scrolling -->
        <Grid Grid.Row="2" Padding="16,0,16,16">
            <views:PlaybackControlsView x:Name="PlaybackTabContent" />
            <views:CameraControlsView x:Name="PtzTabContent" IsVisible="False" />
        </Grid>

        <Grid.GestureRecognizers>
            <PanGestureRecognizer PanUpdated="OnSheetPanUpdated" />
        </Grid.GestureRecognizers>
    </Grid>
</ContentView>
```

### 6.2 Code-behind (`ViewerControlSheet.xaml.cs`)

```csharp
public partial class ViewerControlSheet : ContentView
{
    private const double HalfHeight = 320;
    private const double ExpandedHeight = 440;
    private const uint AnimationDurationMs = 200;

    private bool _isExpanded;
    private bool _isPtzTabSelected;
    private double _panStartTranslationY;

    public ViewerControlSheet()
    {
        InitializeComponent();
        SheetContainer.TranslationY = ExpandedHeight - HalfHeight; // start Half
        PlaybackTabButton.Clicked += (_, _) => SelectTab(isPtz: false);
        PtzTabButton.Clicked += (_, _) => SelectTab(isPtz: true);

        var tapHandle = new TapGestureRecognizer();
        tapHandle.Tapped += (_, _) => _ = ToggleStateAsync();
        SheetContainer.GestureRecognizers.Add(tapHandle);
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (BindingContext is ViewerViewModel vm)
            vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewerViewModel.IsPtzControlActive)
            && BindingContext is ViewerViewModel { IsPtzControlActive: false } && _isPtzTabSelected)
            SelectTab(isPtz: false); // stranded-tab guard: PTZ disappeared, fall back to Weergave
    }

    private void SelectTab(bool isPtz)
    {
        _isPtzTabSelected = isPtz;
        PlaybackTabContent.IsVisible = !isPtz;
        PtzTabContent.IsVisible = isPtz;
        // Move the selected-tab indicator BoxView (omitted above) to under the active button.
    }

    private async Task ToggleStateAsync()
    {
        _isExpanded = !_isExpanded;
        var targetY = _isExpanded ? 0 : ExpandedHeight - HalfHeight;
        await SheetContainer.TranslateTo(0, targetY, AnimationDurationMs, Easing.CubicOut);
    }

    private void OnSheetPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panStartTranslationY = SheetContainer.TranslationY;
                break;
            case GestureStatus.Running:
                var collapsedY = ExpandedHeight - HalfHeight;
                var proposed = _panStartTranslationY + e.TotalY;
                SheetContainer.TranslationY = Math.Clamp(proposed, 0, collapsedY);
                break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                var collapsed = ExpandedHeight - HalfHeight;
                _isExpanded = SheetContainer.TranslationY < collapsed / 2;
                _ = SheetContainer.TranslateTo(0, _isExpanded ? 0 : collapsed, AnimationDurationMs, Easing.CubicOut);
                break;
        }
    }
}
```

Default tab on open: **Weergave** (`_isPtzTabSelected = false`, matches field defaults — no
explicit call needed). Default state: **Half** (constructor sets `TranslationY` directly, no
animation on first show). `PtzTabContent`'s own `IsVisible="{Binding IsPtzControlActive}"` (§4.1)
still applies underneath the tab-selection `IsVisible` set here — both must be true to render, but
since `SelectTab` also drives `PtzTabButton.IsVisible` via its own XAML binding, the tab button and
its content go invisible/inaccessible together.

---

## 7. `FullScreenControlsOverlay.xaml(.cs)` (new — wireframe A)

Rendered when `IsFullScreen`; reuses all the existing #338 auto-hide machinery — no ViewModel
changes needed here at all.

```xml
<ContentView ... x:Class="...FullScreenControlsOverlay" x:DataType="vm:ViewerViewModel"
             IsVisible="{Binding AreControlsVisible}" BackgroundColor="Transparent">
    <Grid>
        <!-- Preset chips, top-left: tap = recall only (no long-press/store in full screen) -->
        <HorizontalStackLayout Grid.Row="0" Margin="16" Spacing="6" VerticalOptions="Start" HorizontalOptions="Start">
            <!-- 8 chip-style Buttons ("1".."8"), each Command="{Binding PtzRecallPresetCommand}"
                 CommandParameter bound as an int literal via x:Static or a simple
                 <Button.CommandParameter><x:Int32>1</x:Int32></Button.CommandParameter> per chip
                 (RelayCommand<int> accepts a boxed int CommandParameter directly - no converter
                 needed). IsVisible="{Binding IsPtzControlActive}" on the whole StackLayout. -->
        </HorizontalStackLayout>

        <!-- D-pad, bottom-left: semi-transparent card, 16dp from edges -->
        <Border Margin="16" HorizontalOptions="Start" VerticalOptions="End"
                BackgroundColor="{DynamicResource ScrimBackground}" StrokeShape="RoundRectangle 8"
                IsVisible="{Binding IsPtzControlActive}">
            <!-- same 3x3 d-pad Grid as CameraControlsView §4.1, Col 0 only (duplicated markup;
                 not reusing CameraControlsView here since the overlay's composition — chips
                 top-left instead of a preset grid beside the pad — differs enough that sharing
                 would need extra visibility params; the d-pad/zoom Grids are short enough to
                 duplicate directly, consistent with the issue's overlay-specific description). -->
        </Border>

        <!-- Zoom rocker, bottom-right -->
        <Border Margin="16" HorizontalOptions="End" VerticalOptions="End"
                BackgroundColor="{DynamicResource ScrimBackground}" StrokeShape="RoundRectangle 8"
                IsVisible="{Binding IsPtzControlActive}">
            <!-- same T/W VerticalStackLayout as CameraControlsView §4.1, Col 1 -->
        </Border>

        <!-- Slim 48dp bottom toolbar -->
        <Grid VerticalOptions="End" HeightRequest="48" ColumnDefinitions="Auto,Auto,40,40,Auto,40"
              ColumnSpacing="8" Padding="8,0" BackgroundColor="{DynamicResource ScrimBackground}">
            <Border Grid.Column="0" Padding="8,3" StrokeShape="RoundRectangle 10"
                    IsVisible="{Binding IsPtzControlActive}">
                <Border.GestureRecognizers>
                    <TapGestureRecognizer Command="{Binding OpenPtzEndpointFormCommand}" />
                </Border.GestureRecognizers>
                <Label Text="{Binding PtzStatusText}" FontSize="11" />
            </Border>
            <Grid Grid.Column="1" ColumnDefinitions="*,*,*" ColumnSpacing="2" WidthRequest="150">
                <!-- Smooth/Balanced/High, same as PlaybackControlsView Row 1's segmented group -->
            </Grid>
            <Switch Grid.Column="2" IsToggled="{Binding IsAudioEnabled}" />
            <Button Grid.Column="3" Text="⛶" Command="{Binding ToggleFullScreenCommand}" />
            <Button Grid.Column="4" Text="Stop" Command="{Binding StopCommand}"
                    BackgroundColor="{DynamicResource ErrorRed}" />
            <Button Grid.Column="5" Text="⋮" Command="{Binding OpenPtzEndpointFormCommand}" />
        </Grid>
    </Grid>
</ContentView>
```

No code-behind beyond `InitializeComponent()` — tapping the video (existing
`ShowControlsOverlayCommand` on the `Border`'s `TapGestureRecognizer` in `ViewerView.xaml`,
unchanged) and the 3 s auto-hide timer (unchanged, `ViewerViewModel.FullScreen.cs`) already drive
`AreControlsVisible`.

---

## 8. `ViewerViewModel.Ptz.cs` changes

Remove: `PtzPresetNumber` observable property, the parameterless `PtzStorePreset`/
`PtzRecallPreset` methods. No test references either symbol (verified via repo-wide grep) so
this is a clean removal, not a deprecation.

Add:

```csharp
public static IReadOnlyList<int> PresetNumbers { get; } = Enumerable.Range(1, 8).ToArray();

[ObservableProperty]
private string? _ptzPresetStatusMessage;

private ITimer? _presetStatusTimer;
private static readonly TimeSpan PresetStatusDisplayDuration = TimeSpan.FromSeconds(2);

[RelayCommand]
private async Task PtzStorePreset(int presetNumber)
{
    await GetOrCreatePtzController().StorePresetAsync(presetNumber);
    PtzPresetStatusMessage = $"Preset {presetNumber} stored";
    _presetStatusTimer?.Dispose();
    _presetStatusTimer = _timeProvider.CreateTimer(
        _ => _dispatcher.BeginInvokeOnMainThread(() => PtzPresetStatusMessage = null),
        null, PresetStatusDisplayDuration, Timeout.InfiniteTimeSpan);
}

[RelayCommand]
private async Task PtzRecallPreset(int presetNumber) =>
    await GetOrCreatePtzController().RecallPresetAsync(presetNumber);
```

`_timeProvider`/`_dispatcher` are the existing injected fields on the parent partial
(`ViewerViewModel.cs`) — same pattern as `ResetOverlayAutoHideTimer` in `ViewerViewModel.FullScreen.cs`.
Dispose `_presetStatusTimer` in `DisposePtz()` (add `_presetStatusTimer?.Dispose();` there,
alongside the existing `DetachPtzController()` / event-unsubscribe lines).

`PtzNudgeCommand`, `PtzZoomNudgeCommand`, `PtzAutoFocusCommand`, `OpenPtzEndpointFormCommand`,
`IsPtzControlActive`, `PtzStatusText`, `PtzLinkState` — **all unchanged**.

---

## 9. Deck-vs-sheet rule — exact statement (for spec/tests traceability)

> `IWindowSizeClassService.Current == WindowSizeClass.Compact` → bottom sheet.
> `Medium` or `Expanded` → fixed deck.

This lives in `ViewerView.xaml.cs` (`UpdateLayoutVisibility`, §2.2), **not** in
`ViewerViewModel` — it is a pure view-layer layout decision reusing an existing, already
unit-tested Core service (`WindowSizeClassServiceTests`, pre-existing), exactly like
`SourceListPage.xaml.cs`'s `ApplySizeClass` already does for the two-pane split. No new
ViewModel test is needed for this rule; `tasks.md` includes a manual/device verification step
instead (both breakpoints, both orientations, per issue acceptance criteria).

---

## 10. Testing Strategy (`tests/MauiApp.Tests/Features/Viewer/`)

All in `ViewerViewModelTests.cs` (existing file) unless noted; `CreateSut()` is unchanged (no new
constructor parameters — this feature adds no new ViewModel dependencies).

1. `PtzStorePresetCommand_StoresAndSetsConfirmation` — `await sut.PtzStorePresetCommand.ExecuteAsync(3);`
   → `_ptzControllerMock.Verify(c => c.StorePresetAsync(3, It.IsAny<CancellationToken>()), Times.Once);`
   and `Assert.Equal("Preset 3 stored", sut.PtzPresetStatusMessage);`
2. `PtzRecallPresetCommand_Recalls` — `await sut.PtzRecallPresetCommand.ExecuteAsync(5);` →
   `_ptzControllerMock.Verify(c => c.RecallPresetAsync(5, It.IsAny<float>(), It.IsAny<CancellationToken>()), Times.Once);`
   (`IPtzController.RecallPresetAsync` signature is `(int presetNumber, float speed = 1f,
   CancellationToken cancellationToken = default)` — confirmed in
   `src/Core/Features/Ptz/Services/IPtzController.cs:33`; `StorePresetAsync` is
   `(int presetNumber, CancellationToken cancellationToken = default)` — no `speed` param, so
   test 1's `Verify` above only needs the `CancellationToken` overload match.)
3. `IsPtzControlActive_TrueWhenPtzSupported_FalseOtherwise` — set `sut.IsPtzSupported = true`
   (already an observable property) with no override configured → assert
   `sut.IsPtzControlActive` true; set both `IsPtzSupported` and `HasPtzOverride` (via `Start()`
   with no `PtzOverrideHost`) false → assert false. (This already indirectly exists via
   `OnIsPtzSupportedChanged`/`OnHasPtzOverrideChanged`; add an explicit assertion-only test if
   none currently targets `IsPtzControlActive` directly — grep first, don't duplicate.)
4. `PresetNumbers_IsOneToEight` — `Assert.Equal(Enumerable.Range(1, 8), ViewerViewModel.PresetNumbers);`
   (static, no `CreateSut()` needed).

No `IPtzController` interface change is required — both methods already existed with these exact
signatures (the deleted `PtzStorePreset()`/`PtzRecallPreset()` already called them with an int).

XAML-level behavior (deck/sheet/overlay switching, drag gestures, long-press timing, exact
spacing) is **not** unit-testable without MAUI runtime — verified on-device per tasks.md.

---

## 11. Risks / Known Limitations

- **Narrow Expanded panes** (§ spec.md "Out of scope") — the deck's two-column layout could be
  tight just above the 840 dp Expanded threshold on the embedded pane. Accepted per the issue's
  explicit `IWindowSizeClassService`-based rule; not fixed in this feature.
- **Sizing budget is exact, not generous** (§5) — expect minor `Spacing`/`Padding` tuning during
  on-device verification; keep changes local to the numeric literals called out, not a structural
  rework.
- **`FullScreenControlsOverlay` duplicates the d-pad/zoom markup** rather than reusing
  `CameraControlsView` (§7) — deliberate, since the overlay's composition (chips separate from
  the pad, no preset-grid-beside-zoom arrangement) differs from the deck/sheet's camera column;
  forcing one shared view would need extra visibility parameters for marginal reuse benefit.
