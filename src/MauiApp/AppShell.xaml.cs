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
    private readonly AdaptiveShellStateViewModel _stateViewModel;
    private readonly IAndroidOrientationBridge _orientationBridge;
    private readonly INavigationHandoffService _handoffService;
    private readonly IWindowSizeClassService _windowSizeClassService;
    private readonly IWindowInsetsService _windowInsetsService;
    private readonly IAppearanceService _appearanceService;
    private readonly ShellNavigationService _navigationService;

    private PrimaryNavDestination _currentPrimaryDestination = PrimaryNavDestination.Home;
    private bool _handoffInProgress;

    /// <summary>
    /// Set when <see cref="ApplyPlacement"/> was asked to hide <c>PrimaryTabBar</c> while a page was
    /// pushed or a modal was open and therefore skipped the chrome swap (#393). Hiding the current
    /// <c>ShellItem</c> makes Shell re-point to the first visible one, and a pushed page has no
    /// equivalent route under the rail's independent <c>ShellItem</c> family — so the eviction can
    /// never be reconciled afterwards and the swap must be deferred, not undone. Re-applied from
    /// <see cref="OnShellNavigated"/> once the section stack is back at its root.
    /// </summary>
    private bool _placementSwapDeferred;

    private readonly Dictionary<PrimaryNavDestination, (Border Container, Label Label, Path Icon)> _railButtons = [];

    /// <summary>Rendered edge length of a rail icon, in device-independent units.</summary>
    private const double RailIconSize = 28d;

    // Rail text colors come from the shared theme palette (Colors.xaml) so the rail
    // matches the tab bar; resolved per-use so appearance/theme changes are honored.
    private static Color InactiveText => ResolveColor("ShellTabUnselected", Color.FromArgb("#8E8E93"));
    private static Color ActiveText   => ResolveColor("ShellTabSelected", Colors.White);

    // Active rail item's indicator pill (M3 navigation-rail active indicator) — neutral, not
    // accent-tinted, so it matches the tab bar, whose selected item is also neutral (#343 home-nav-05).
    private static Color ActiveIndicator => ResolveColor("ShellRailActiveIndicator", Color.FromArgb("#45455F"));

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
        IAppearanceService appearanceService,
        ShellNavigationService navigationService)
    {
        InitializeComponent();

        _stateViewModel   = stateViewModel;
        _orientationBridge = orientationBridge;
        _handoffService   = handoffService;
        _windowSizeClassService = windowSizeClassService;
        _windowInsetsService = windowInsetsService;
        _appearanceService = appearanceService;
        _navigationService = navigationService;

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
            // has to say it itself — including which destination is currently selected, since a
            // plain Border exposes no native "selected" state (#345 home-nav-06).
            SemanticProperties.SetDescription(container, RailDescription(item.Label, isSelected: false));

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
        var activeText      = ActiveText;
        var inactiveText    = InactiveText;
        var activeIndicator = ActiveIndicator;

        foreach (var kvp in _railButtons)
        {
            bool isActive = kvp.Key == active;
            var foreground = isActive ? activeText : inactiveText;

            // M3 navigation-rail active indicator: a tonal pill behind the selected item. The
            // Border already has the RoundRectangle(12) shape and 64dp height (BuildRailItems).
            // Not the sole state cue — the label's colour and bold weight also carry it — so the
            // pill's own contrast against ShellBackground does not need to clear 3:1 on its own
            // (#343 home-nav-05).
            kvp.Value.Container.BackgroundColor = isActive ? activeIndicator : Colors.Transparent;
            kvp.Value.Label.TextColor      = foreground;
            kvp.Value.Label.FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None;
            kvp.Value.Icon.Fill            = new SolidColorBrush(foreground);

            SemanticProperties.SetDescription(kvp.Value.Container, RailDescription(kvp.Value.Label.Text, isActive));
        }
    }

    /// <summary>
    /// A rail item is a plain Border, so nothing exposes a tab role or a selected state to
    /// TalkBack; both are carried in the accessible name instead (same pattern as
    /// ViewerControlSheet's tab buttons) (#345).
    /// </summary>
    private static string RailDescription(string label, bool isSelected) =>
        isSelected ? $"{label}, selected" : label;

    // ── Orientation / placement ───────────────────────────────────────────────

    private void OnStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AdaptiveShellStateViewModel.PlacementMode))
            ApplyPlacement();
    }

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

    // ── Navigation ───────────────────────────────────────────────────────────

    private async void OnRailItemSelected(object? sender, PrimaryNavDestination destination)
    {
        if (!TryGetRouteForCurrentPlacement(destination, out var route))
            return;

        _navigationService.BeginExplicitNavigation();
        try { await GoToAsync(route); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Rail navigation failed: {ex}"); }
        finally { _navigationService.EndExplicitNavigation(); }
    }

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

    private async Task RunNavigatingHandoffAsync(PrimaryNavDestination to, ShellNavigatingDeferral deferral)
    {
        try
        {
            var from = _currentPrimaryDestination;
            await Task.Run(() => _handoffService.HandlePrimaryDestinationChangeAsync(from, to))
                .WaitAsync(TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation handoff failed: {ex}");
        }
        finally
        {
            _currentPrimaryDestination = to;
            _handoffInProgress = false;
            deferral.Complete();
        }
    }

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

    private static string? LastSegment(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return null;
        return location.Split('?', 2)[0].Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
    }

    private static PrimaryNavDestination? ParseDestination(string? location)
    {
        // Match on the last path segment only — a query value, or an ancestor
        // segment (e.g. "stream-tab" when "viewer" is pushed on top of it),
        // must never influence which destination this resolves to.
        var s = LastSegment(location)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(s)) return null;
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
        if (_handoffInProgress) return;
        if (Navigation?.NavigationStack?.Count > 1) return;
        if (Navigation?.ModalStack?.Count > 0) return;
        if (!TryGetRouteForCurrentPlacement(_stateViewModel.SelectedDestination, out var route)) return;

        var currentSegment = LastSegment(CurrentState?.Location?.OriginalString);
        if (string.Equals(currentSegment, route.Trim('/'), StringComparison.OrdinalIgnoreCase)) return;

        _navigationService.BeginExplicitNavigation();
        try { await GoToAsync(route); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Placement reconciliation failed: {ex}"); }
        finally { _navigationService.EndExplicitNavigation(); }
    }
}
