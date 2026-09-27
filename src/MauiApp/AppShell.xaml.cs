using NdiForAndroid.Features.DiagOverlay.Services;
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
    private readonly IDiagnosticOverlayService? _diagnostics;

    private PrimaryNavDestination _currentPrimaryDestination = PrimaryNavDestination.Home;
    private bool _handoffInProgress;

    /// <summary>Navigation sequence number and start tick, for the NDI-Nav trace.</summary>
    private long _navSeq;
    private long _navStartedAtTicks;

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
        ShellNavigationService navigationService,
        IDiagnosticOverlayService? diagnostics = null)
    {
        _stateViewModel   = stateViewModel;
        _orientationBridge = orientationBridge;
        _handoffService   = handoffService;
        _windowSizeClassService = windowSizeClassService;
        _windowInsetsService = windowInsetsService;
        _appearanceService = appearanceService;
        _navigationService = navigationService;
        _diagnostics = diagnostics;

        // Shell raises Navigating synchronously while this sets the initial CurrentItem, so every
        // field OnNavigating/OnShellNavigated can read must already be assigned above this call.
        InitializeComponent();

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

        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "shell.sizeallocated", $"w={width} h={height}");

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
        else if (e.PropertyName is nameof(AdaptiveShellStateViewModel.IsChromeSuppressed))
            ApplyPlacement(ensureDestination: false);
    }

    private void ApplyPlacement(bool ensureDestination = true)
    {
        var placementStartedAt = Environment.TickCount64;
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "placement.begin",
            $"rail={_stateViewModel.IsLeftRailNavigationVisible} suppressed={_stateViewModel.IsChromeSuppressed}");

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
                _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "placement.end",
                    $"deferred={_placementSwapDeferred} ms={Environment.TickCount64 - placementStartedAt}");
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

        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "placement.end",
            $"deferred={_placementSwapDeferred} ms={Environment.TickCount64 - placementStartedAt}");
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    private async void OnRailItemSelected(object? sender, PrimaryNavDestination destination)
    {
        if (!TryGetRouteForCurrentPlacement(destination, out var route))
            return;

        _navigationService.BeginExplicitNavigation();
        var railGotoStartedAt = Environment.TickCount64;
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "nav.goto.begin", $"route={route}");
        try { await GoToAsync(route); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Rail navigation failed: {ex}"); }
        finally
        {
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "nav.goto.end",
                $"ms={Environment.TickCount64 - railGotoStartedAt}");
            _navigationService.EndExplicitNavigation();
        }
    }

    protected override void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);

        _navSeq++;
        _navStartedAtTicks = Environment.TickCount64;
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "shell.navigating.begin",
            $"nav={_navSeq} src={args.Source} cancel={args.CanCancel} target={args.Target?.Location?.OriginalString}");

        // A modal push/pop (e.g. the full-screen viewer) does not change Shell.CurrentState and
        // must never be misclassified as a primary-destination change by ParseDestination below.
        if (Navigation?.ModalStack?.Count > 0)
        {
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "shell.navigating.skip", $"nav={_navSeq} reason=modal");
            return;
        }

        if (args.Cancelled)
        {
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "shell.navigating.skip", $"nav={_navSeq} reason=cancelled");
            return;
        }

        // Shell's own unsolicited item fallback (#393) — chrome plumbing, not a destination change:
        // no handoff. See ShellNavigationService.IsExplicitNavigationInProgress.
        if (args.Source == ShellNavigationSource.ShellItemChanged
            && !(_navigationService?.IsExplicitNavigationInProgress ?? false))
        {
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "shell.navigating.skip", $"nav={_navSeq} reason=itemfallback");
            return;
        }

        var to = ParseDestination(args.Target?.Location?.OriginalString);
        if (to is null || to == _currentPrimaryDestination)
        {
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "shell.navigating.skip", $"nav={_navSeq} reason=noop");
            return;
        }

        if (!args.CanCancel)
        {
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "shell.navigating.skip", $"nav={_navSeq} reason=noncancelable");
            return;
        }

        var deferral = args.GetDeferral();
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "shell.deferral.taken", $"nav={_navSeq}");
        _handoffInProgress = true;

        RunNavigatingHandoff(to.Value, deferral);
    }

    /// <summary>Diagnostic cap only: the deferral no longer waits on the handoff, so this bound
    /// does not gate anything the user sees — it turns a wedged teardown into a log line instead
    /// of a silent leak.</summary>
    private static readonly TimeSpan HandoffDiagnosticTimeout = TimeSpan.FromSeconds(3);

    private void RunNavigatingHandoff(PrimaryNavDestination to, ShellNavigatingDeferral deferral)
    {
        var navSeq = _navSeq;
        var from = _currentPrimaryDestination;

        // The deferral exists to order the destination bookkeeping ahead of Shell's page swap, not
        // to hold the swap open while the NDI receiver tears down. Both fields are written here,
        // synchronously, so OnShellNavigated's fallback branch stays dead on this path exactly as
        // it is today.
        _currentPrimaryDestination = to;
        _handoffInProgress = false;
        // Traced BEFORE Complete(), not after: MAUI can resume the navigation inline from inside
        // deferral.Complete(), so OnShellNavigated may run re-entrantly and emit shell.navigated
        // first — which the #417 parser would read as a nested navigation. Functionally safe either
        // way (_currentPrimaryDestination is already `to`, so the fallback branch stays dead), but
        // the log has to stay parseable. ms= therefore measures navigating.begin -> deferral
        // released, which is the interval that matters.
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "shell.deferral.complete",
            $"nav={navSeq} ms={Environment.TickCount64 - _navStartedAtTicks}");
        deferral.Complete();

        RunHandoffDetached(navSeq, from, to);
    }

    /// <summary>
    /// Fires the handoff without gating anything on it. Deliberately no <c>Task.Run</c>: the
    /// handoff's synchronous part is now only the stop *request*, which must run in this UI-thread
    /// turn so it is ordered against whatever the incoming page does; the native teardown already
    /// runs on the bridge's own lifecycle worker.
    /// </summary>
    private void RunHandoffDetached(long navSeq, PrimaryNavDestination from, PrimaryNavDestination to)
    {
        _ = AwaitHandoffForDiagnosticsAsync(navSeq, from, to);
    }

    private async Task AwaitHandoffForDiagnosticsAsync(long navSeq, PrimaryNavDestination from, PrimaryNavDestination to)
    {
        var handoffStartedAt = Environment.TickCount64;
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "handoff.begin",
            $"nav={navSeq} from={from} to={to}");

        try
        {
            await _handoffService.HandlePrimaryDestinationChangeAsync(from, to)
                .WaitAsync(HandoffDiagnosticTimeout);
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "handoff.end",
                $"nav={navSeq} ms={Environment.TickCount64 - handoffStartedAt}");
        }
        catch (TimeoutException)
        {
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "handoff.timeout",
                $"nav={navSeq} ms={Environment.TickCount64 - handoffStartedAt}");
            System.Diagnostics.Debug.WriteLine(
                $"Navigation handoff still running after {HandoffDiagnosticTimeout.TotalSeconds}s: {from} -> {to}");
        }
        catch (Exception ex)
        {
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "handoff.end",
                $"nav={navSeq} ms={Environment.TickCount64 - handoffStartedAt}");
            System.Diagnostics.Debug.WriteLine($"Navigation handoff failed: {ex}");
        }
    }

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "shell.navigated",
            $"src={e.Source} loc={e.Current.Location.OriginalString}");

        // Shell's own unsolicited item fallback (#393): reconcile back to the destination that was
        // actually selected instead of adopting wherever Shell fell back to, and never run the
        // handoff or overwrite SelectedDestination for it. The rail keeps highlighting the real
        // selection, so the fallback is never visible as a selection change.
        // See ShellNavigationService.IsExplicitNavigationInProgress.
        if (e.Source == ShellNavigationSource.ShellItemChanged
            && !(_navigationService?.IsExplicitNavigationInProgress ?? false))
        {
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "shell.navigated.fallbackreconcile");
            UpdateRailHighlight(_stateViewModel.SelectedDestination);
            _appearanceService.ReapplyChrome();
            Dispatcher.Dispatch(async () => await EnsurePrimaryDestinationVisibleAsync());
            return;
        }

        var to = ParseDestination(e.Current.Location.OriginalString) ?? _currentPrimaryDestination;

        if (to != _currentPrimaryDestination)
        {
            var from = _currentPrimaryDestination;

            // Record the change before the teardown is even requested — this handler runs on the
            // UI thread, and the old await-with-no-cap here ran the pump joins in this turn on
            // every navigation that bailed out of OnNavigating (modal open, cancelled,
            // non-cancelable).
            _currentPrimaryDestination = to;
            RunHandoffDetached(_navSeq, from, to);
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

        // Posted rather than run inline: it must observe the next UI-thread turn, i.e. after
        // Shell's page swap and the layout pass it queues, so it under-reports by up to one frame
        // rather than marking a swap that has not actually happened yet.
        var diagnostics = _diagnostics;
        if (diagnostics?.IsDeveloperMode == true)
        {
            var navSeq = _navSeq;
            var navStartedAt = _navStartedAtTicks;
            Dispatcher.Dispatch(() => diagnostics.Trace(
                DiagnosticOverlayService.NavigationLogTag,
                "nav.firstframe",
                $"nav={navSeq} ms={Environment.TickCount64 - navStartedAt}"));
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
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "reconcile.begin");

        if (_handoffInProgress)
        {
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "reconcile.skip", "reason=handoff");
            return;
        }
        if (Navigation?.NavigationStack?.Count > 1)
        {
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "reconcile.skip", "reason=pushed");
            return;
        }
        if (Navigation?.ModalStack?.Count > 0)
        {
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "reconcile.skip", "reason=modal");
            return;
        }
        if (!TryGetRouteForCurrentPlacement(_stateViewModel.SelectedDestination, out var route))
        {
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "reconcile.skip", "reason=noroute");
            return;
        }

        var currentSegment = LastSegment(CurrentState?.Location?.OriginalString);
        if (string.Equals(currentSegment, route.Trim('/'), StringComparison.OrdinalIgnoreCase))
        {
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "reconcile.skip", "reason=sameroute");
            return;
        }

        _navigationService.BeginExplicitNavigation();
        var reconcileGotoStartedAt = Environment.TickCount64;
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "reconcile.goto.begin", $"route={route}");
        try { await GoToAsync(route); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Placement reconciliation failed: {ex}"); }
        finally
        {
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "reconcile.goto.end",
                $"ms={Environment.TickCount64 - reconcileGotoStartedAt}");
            _navigationService.EndExplicitNavigation();
        }
    }
}
