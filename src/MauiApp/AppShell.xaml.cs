using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.Features.Navigation.Services;
using NdiForAndroid.Features.Navigation.ViewModels;
using NdiForAndroid.Features.Settings.Services;
using NdiForAndroid.Features.Viewer.Views;
using NdiForAndroid.Services;

// Aliased rather than importing Microsoft.Maui.Controls.Shapes wholesale: its Path would
// collide with System.IO.Path, which the SDK's implicit usings already bring in.
using Geometry = Microsoft.Maui.Controls.Shapes.Geometry;
using Path = Microsoft.Maui.Controls.Shapes.Path;
using PathGeometryConverter = Microsoft.Maui.Controls.Shapes.PathGeometryConverter;

namespace NdiForAndroid;

public partial class AppShell : Shell
{
    private readonly IReadOnlyDictionary<PrimaryNavDestination, string> _landscapeRoutes =
        new Dictionary<PrimaryNavDestination, string>
        {
            [PrimaryNavDestination.Home]     = "//home-rail",
            [PrimaryNavDestination.Stream]   = "//stream-rail",
            [PrimaryNavDestination.View]     = "//view-rail",
            [PrimaryNavDestination.Settings] = "//settings-rail",
        };

    private readonly IReadOnlyDictionary<PrimaryNavDestination, string> _portraitRoutes =
        new Dictionary<PrimaryNavDestination, string>
        {
            [PrimaryNavDestination.Home]     = "//home-tab",
            [PrimaryNavDestination.Stream]   = "//stream-tab",
            [PrimaryNavDestination.View]     = "//view-tab",
            [PrimaryNavDestination.Settings] = "//settings-tab",
        };

    private readonly AdaptiveShellStateViewModel _stateViewModel;
    private readonly IAndroidOrientationBridge _orientationBridge;
    private readonly INavigationHandoffService _handoffService;
    private readonly IWindowSizeClassService _windowSizeClassService;
    private readonly IWindowInsetsService _windowInsetsService;
    private readonly IAppearanceService _appearanceService;

    private PrimaryNavDestination _currentPrimaryDestination = PrimaryNavDestination.Home;

    private readonly Dictionary<PrimaryNavDestination, (Border Container, Label Label, Path Icon)> _railButtons = [];

    /// <summary>Rendered edge length of a rail icon, in device-independent units.</summary>
    private const double RailIconSize = 28d;

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
        IWindowInsetsService windowInsetsService,
        IAppearanceService appearanceService)
    {
        InitializeComponent();

        _stateViewModel   = stateViewModel;
        _orientationBridge = orientationBridge;
        _handoffService   = handoffService;
        _windowSizeClassService = windowSizeClassService;
        _windowInsetsService = windowInsetsService;
        _appearanceService = appearanceService;

        Routing.RegisterRoute("viewer", typeof(ViewerPage));
        Routing.RegisterRoute("diagnostic-log", typeof(Features.DiagOverlay.Views.DiagnosticLogPage));
        // OutputPage is a top-level tab — no route registration needed for push navigation.

        BuildRailItems();

        _stateViewModel.PropertyChanged += OnStatePropertyChanged;
        _stateViewModel.RailItemSelected += OnRailItemSelected;
        Navigated += OnShellNavigated;

        // The rail is built in code, so DynamicResource cannot reach it — re-tint on every
        // palette change instead, otherwise the icons keep the previous theme's color (#294).
        _appearanceService.AppearanceChanged += OnAppearanceChanged;

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

        // Insets resolve only once the window is laid out, and change on rotation or when a
        // cutout enters/leaves the top edge — so re-read them here rather than at construction.
        ApplyRailInset();
    }

    private void OnAppearanceChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        UpdateRailHighlight(_currentPrimaryDestination);
    }

    /// <summary>
    /// Pushes the rail's first item below the status bar. The window is drawn edge-to-edge, so
    /// without this the rail's background — and its topmost item — sit under the clock (#296).
    /// </summary>
    private void ApplyRailInset()
    {
        var topInset = _windowInsetsService.GetStatusBarInset();
        if (topInset < 0)
            topInset = 0;

        var padding = new Thickness(0, topInset, 0, 0);
        if (RailItems.Padding != padding)
            RailItems.Padding = padding;
    }

    // ── Rail construction ────────────────────────────────────────────────────

    private void BuildRailItems()
    {
        foreach (var item in PrimaryNavigationMetadata.Items)
        {
            // A vector Path rather than an Image: the bundled SVGs bake in a white fill, and
            // MAUI's Image has no tint, so an icon built from one cannot follow the theme (#294).
            var icon = new Path
            {
                Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(item.IconGeometry)!,
                Aspect = Microsoft.Maui.Controls.Stretch.Uniform,
                HeightRequest = RailIconSize,
                WidthRequest  = RailIconSize,
                HorizontalOptions = LayoutOptions.Center,
                Fill = new SolidColorBrush(InactiveText),
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

            // The rail item is a plain Border with a tap gesture, so nothing describes it to
            // accessibility services — a screen reader reaches it as an unlabelled container,
            // and it surfaces in the Android view tree as a bare TextView with no
            // contentDescription. The bottom tab bar gets this for free from Shell; the rail
            // has to say it itself.
            SemanticProperties.SetDescription(container, item.Label);

            // Same destination, same id as the matching bottom tab — the two placements are
            // never in the tree at once, so a test asking for the id gets whichever is live.
            container.AutomationId = item.TestId;

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
        // Resolved once per pass so a theme change picks up the new palette.
        var activeText   = ActiveText;
        var inactiveText = InactiveText;

        foreach (var kvp in _railButtons)
        {
            bool isActive = kvp.Key == active;
            var foreground = isActive ? activeText : inactiveText;

            kvp.Value.Container.BackgroundColor = Colors.Transparent;
            kvp.Value.Label.TextColor = foreground;
            kvp.Value.Icon.Fill       = new SolidColorBrush(foreground);
        }
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
    }

    private static PrimaryNavDestination? ParseDestination(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return null;
        var s = location.ToLowerInvariant();
        if (s.Contains("home")     || s.Contains("sources")) return PrimaryNavDestination.Home;
        if (s.Contains("stream")   || s.Contains("output"))  return PrimaryNavDestination.Stream;
        if (s.Contains("view")     || s.Contains("viewer"))  return PrimaryNavDestination.View;
        if (s.Contains("settings"))                          return PrimaryNavDestination.Settings;
        return null;
    }

    private bool TryGetRouteForCurrentPlacement(PrimaryNavDestination destination, out string route)
    {
        var routes = _stateViewModel.IsLeftRailNavigationVisible ? _landscapeRoutes : _portraitRoutes;
        return routes.TryGetValue(destination, out route!);
    }

    private async Task EnsurePrimaryDestinationVisibleAsync()
    {
        if (!TryGetRouteForCurrentPlacement(_stateViewModel.SelectedDestination, out var route))
            return;

        var currentLocation = CurrentState?.Location?.OriginalString;
        if (string.Equals(currentLocation, route, StringComparison.OrdinalIgnoreCase))
            return;

        await GoToAsync(route);
    }
}
