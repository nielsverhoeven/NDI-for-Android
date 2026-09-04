using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.Features.Navigation.Services;
using NdiForAndroid.Features.Navigation.ViewModels;
using NdiForAndroid.Features.Settings.Services;
using NdiForAndroid.Features.Viewer.Views;
using NdiForAndroid.Services;

namespace NdiForAndroid;

public partial class AppShell : Shell
{
    private readonly AdaptiveShellStateViewModel _stateViewModel;
    private readonly IAndroidOrientationBridge _orientationBridge;
    private readonly INavigationHandoffService _handoffService;
    private readonly IWindowSizeClassService _windowSizeClassService;
    private readonly IAppearanceService _appearanceService;
    private readonly ShellNavigationService _navigationService;

    private PrimaryNavDestination _currentPrimaryDestination = PrimaryNavDestination.Home;
    private bool _handoffInProgress;

    private readonly Dictionary<PrimaryNavDestination, (Border Container, Label Label, Image Icon)> _railButtons = [];

    // Rail text colors come from the shared theme palette (Colors.xaml) so the rail
    // matches the tab bar; resolved per-use so appearance/theme changes are honored.
    private static Color InactiveText => ResolveColor("ShellTabUnselected", Color.FromArgb("#8E8E93"));
    private static Color ActiveText   => ResolveColor("ShellTabSelected", Colors.White);

    private static Color ResolveColor(string key, Color fallback) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : fallback;

    public AppShell(
        AdaptiveShellStateViewModel stateViewModel,
        IAndroidOrientationBridge orientationBridge,
        INavigationHandoffService handoffService,
        IWindowSizeClassService windowSizeClassService,
        IAppearanceService appearanceService,
        ShellNavigationService navigationService)
    {
        InitializeComponent();

        _stateViewModel   = stateViewModel;
        _orientationBridge = orientationBridge;
        _handoffService   = handoffService;
        _windowSizeClassService = windowSizeClassService;
        _appearanceService = appearanceService;
        _navigationService = navigationService;

        Routing.RegisterRoute("viewer", typeof(ViewerPage));
        Routing.RegisterRoute("diagnostic-log", typeof(Features.DiagOverlay.Views.DiagnosticLogPage));
        // OutputPage is a top-level tab — no route registration needed for push navigation.

        BuildRailItems();

        _stateViewModel.PropertyChanged += OnStatePropertyChanged;
        _stateViewModel.RailItemSelected += OnRailItemSelected;
        Navigated += OnShellNavigated;

        _orientationBridge.SyncFromDisplayInfo();
        ApplyPlacement();
    }

    /// <summary>
    /// Feeds the window width (device-independent units) into the size-class service.
    /// Runs on the UI thread; the service only raises Changed on class transitions.
    /// </summary>
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width > 0)
            _windowSizeClassService.UpdateFromWidth(width);
    }

    // ── Rail construction ────────────────────────────────────────────────────

    private void BuildRailItems()
    {
        foreach (var item in PrimaryNavigationMetadata.Items)
        {
            var icon = new Image
            {
                Source = item.IconKey,
                HeightRequest = 28,
                WidthRequest  = 28,
                HorizontalOptions = LayoutOptions.Center,
            };

            var label = new Label
            {
                Text = item.Label,
                FontSize = 10,
                HorizontalOptions = LayoutOptions.Center,
                TextColor = InactiveText,
            };

            var stack = new VerticalStackLayout
            {
                Spacing = 4,
                Padding = new Thickness(0, 10),
                HorizontalOptions = LayoutOptions.Fill,
                Children = { icon, label },
            };

            var container = new Border
            {
                BackgroundColor = Colors.Transparent,
                StrokeShape     = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Stroke          = Colors.Transparent,
                Padding         = 0,
                Margin          = new Thickness(8, 2),
                Content         = stack,
                HeightRequest   = 64,
            };

            var destination = item.Destination;
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => _stateViewModel.SelectDestination(destination);
            container.GestureRecognizers.Add(tap);

            _railButtons[destination] = (container, label, icon);
            RailItems.Children.Add(container);
        }

        UpdateRailHighlight(PrimaryNavDestination.Home);
    }

    private void UpdateRailHighlight(PrimaryNavDestination active)
    {
        foreach (var kvp in _railButtons)
        {
            bool isActive = kvp.Key == active;
            kvp.Value.Container.BackgroundColor = Colors.Transparent;
            kvp.Value.Label.TextColor = isActive ? ActiveText : InactiveText;
            kvp.Value.Icon.Opacity    = isActive ? 1.0 : 0.62;
        }
    }

    /// <summary>
    /// Re-themes the custom rail after a light/dark switch (#294). The rail icons are
    /// plain Images whose SVG fill is baked in at build time (no runtime tint), so a
    /// light theme needs the dark icon variants; labels re-resolve the current palette.
    /// Called by MauiAppearanceService.UpdateShell on every theme/accent apply.
    /// </summary>
    public void ApplyThemePalette(bool isLight)
    {
        foreach (var item in PrimaryNavigationMetadata.Items)
        {
            if (!_railButtons.TryGetValue(item.Destination, out var entry))
                continue;

            entry.Icon.Source = isLight ? ToDarkIconKey(item.IconKey) : item.IconKey;
        }

        UpdateRailHighlight(_currentPrimaryDestination);
    }

    private static string ToDarkIconKey(string iconKey) =>
        iconKey.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
            ? iconKey[..^4] + "_dark.svg"
            : iconKey + "_dark";

    /// <summary>Base top padding of the rail item stack (matches the XAML initial value).</summary>
    private const double RailBaseTopPadding = 24;

    /// <summary>
    /// Pushes the rail items below the status-bar inset (#296). With edge-to-edge enforced
    /// (API 35+) the flyout drawer is drawn from y=0, so without this the first rail item
    /// interleaves with the system clock. Idempotent; called on every theme apply.
    /// </summary>
    public void SetRailTopInset(double insetDp)
    {
        RailItems.Padding = new Thickness(0, RailBaseTopPadding + Math.Max(0, insetDp), 0, 0);
    }

    // ── Orientation / placement ───────────────────────────────────────────────

    private void OnStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AdaptiveShellStateViewModel.PlacementMode))
            ApplyPlacement();
    }

    private void ApplyPlacement()
    {
        if (_stateViewModel.IsLeftRailNavigationVisible)
        {
            FlyoutBehavior         = FlyoutBehavior.Locked;
            PrimaryTabBar.IsVisible = false;
        }
        else
        {
            FlyoutBehavior         = FlyoutBehavior.Disabled;
            PrimaryTabBar.IsVisible = true;
        }

        Dispatcher.Dispatch(async () => await EnsurePrimaryDestinationVisibleAsync());
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    private async void OnRailItemSelected(object? sender, PrimaryNavDestination destination)
    {
        if (TryGetRouteForCurrentPlacement(destination, out var route))
            await GoToAsync(route);
    }

    protected override void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);

        if (args.Cancelled)
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

    private async Task RunNavigatingHandoffAsync(PrimaryNavDestination to, ShellNavigatingDeferral deferral)
    {
        try
        {
            await _handoffService.HandlePrimaryDestinationChangeAsync(_currentPrimaryDestination, to)
                .WaitAsync(TimeSpan.FromSeconds(3));
            _currentPrimaryDestination = to;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation handoff failed: {ex}");
        }
        finally
        {
            _handoffInProgress = false;
            deferral.Complete();
        }
    }

    private async void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        var to = ParseDestination(e.Current.Location.OriginalString) ?? _currentPrimaryDestination;

        if (to != _currentPrimaryDestination)
        {
            await _handoffService.HandlePrimaryDestinationChangeAsync(_currentPrimaryDestination, to);
            _currentPrimaryDestination = to;
        }

        _stateViewModel.SelectedDestination = to;
        UpdateRailHighlight(to);

        // MAUI re-applies per-page toolbar appearance on navigation, resetting the
        // AppBarLayout background to template defaults — restore the themed chrome (#296).
        _appearanceService.ReapplyChrome();
    }

    private static PrimaryNavDestination? ParseDestination(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return null;
        // Match on the path only — a query value (e.g. reStreamSourceId containing
        // "stream") must never influence which destination this resolves to.
        var path = location.Split('?', 2)[0];
        var s = path.ToLowerInvariant();
        if (s.Contains("home")     || s.Contains("sources")) return PrimaryNavDestination.Home;
        if (s.Contains("stream")   || s.Contains("output"))  return PrimaryNavDestination.Stream;
        if (s.Contains("view")     || s.Contains("viewer"))  return PrimaryNavDestination.View;
        if (s.Contains("settings"))                          return PrimaryNavDestination.Settings;
        return null;
    }

    private bool TryGetRouteForCurrentPlacement(PrimaryNavDestination destination, out string route) =>
        _navigationService.TryGetRouteForCurrentPlacement(destination, out route);

    private async Task EnsurePrimaryDestinationVisibleAsync()
    {
        if (_handoffInProgress)
            return;

        if (!TryGetRouteForCurrentPlacement(_stateViewModel.SelectedDestination, out var route))
            return;

        var currentLocation = CurrentState?.Location?.OriginalString;
        if (string.Equals(currentLocation, route, StringComparison.OrdinalIgnoreCase))
            return;

        await GoToAsync(route);
    }
}
