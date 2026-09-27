using NdiForAndroid.Features.DiagOverlay.Services;
using NdiForAndroid.Features.Navigation.Services;
using NdiForAndroid.Features.Settings.ViewModels;

namespace NdiForAndroid.Features.Settings.Views;

public partial class SettingsPage : ContentPage
{
    private readonly IDiagnosticOverlayService? _diagnostics;

    public SettingsPage(SettingsViewModel viewModel, IDiagnosticOverlayService? diagnostics = null)
    {
        _diagnostics = diagnostics;
        var t0 = Environment.TickCount64;
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "settings.ctor.begin");
        InitializeComponent();
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "settings.ctor.end", $"ms={Environment.TickCount64 - t0}");
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var t0 = Environment.TickCount64;
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "page.appearing.begin", "name=Settings");
        if (BindingContext is not SettingsViewModel vm)
        {
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "page.appearing.end", $"name=Settings ms={Environment.TickCount64 - t0}");
            return;
        }

        var loadStartedAt = Environment.TickCount64;
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "settings.loadcommand.begin");
        if (vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "settings.loadcommand.end", $"ms={Environment.TickCount64 - loadStartedAt}");

        vm.StartConnectionMonitoring();
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "settings.monitor.start");

        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "page.appearing.end", $"name=Settings ms={Environment.TickCount64 - t0}");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        var t0 = Environment.TickCount64;
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "page.disappearing.begin", "name=Settings");
        if (BindingContext is SettingsViewModel vm)
            vm.StopConnectionMonitoring();
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "page.disappearing.end", $"name=Settings ms={Environment.TickCount64 - t0}");
    }

    /// <summary>
    /// Layout plumbing only: collapses the 220dp section rail into a wrapping selector above
    /// the panel at Compact width, where a fixed rail would leave too little room for the detail
    /// panel.
    /// </summary>
    /// <remarks>
    /// Reads the width MAUI hands this override rather than injecting
    /// <see cref="IWindowSizeClassService"/>: this page is transient (recreated on every Settings
    /// visit), so subscribing to that singleton's <c>Changed</c> event in the constructor would
    /// leak one subscription per visit.
    /// </remarks>
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        var t0 = Environment.TickCount64;

        if (width <= 0)
            return;

        var isCompact = WindowSizeClassService.Classify(width) == WindowSizeClass.Compact;
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "settings.sizeallocated.begin", $"w={width} compact={isCompact}");

        RailColumn.Width = isCompact ? new GridLength(0) : new GridLength(220);
        VerticalRail.IsVisible = !isCompact;
        CompactRail.IsVisible = isCompact;
        SectionsGrid.ColumnSpacing = isCompact ? 0 : 16;
        SectionsGrid.RowSpacing = isCompact ? 12 : 0;

        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "settings.sizeallocated.end", $"ms={Environment.TickCount64 - t0}");
    }
}
