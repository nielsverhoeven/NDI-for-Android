using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.Features.Navigation.Services;
using NdiForAndroid.Features.Navigation.ViewModels;
using NdiForAndroid.Features.Viewer.Views;
using NdiForAndroid.Services;

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

    private PrimaryNavDestination _currentPrimaryDestination = PrimaryNavDestination.Home;

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
        INavigationHandoffService handoffService)
    {
        InitializeComponent();

        _stateViewModel   = stateViewModel;
        _orientationBridge = orientationBridge;
        _handoffService   = handoffService;

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
