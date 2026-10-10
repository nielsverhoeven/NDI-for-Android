using NdiForAndroid.Features.DiagOverlay.Services;
using NdiForAndroid.Features.Viewer.Services;
using NdiForAndroid.Features.Viewer.ViewModels;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Viewer.Views;

/// <summary>
/// Thin host page for the reusable <see cref="ViewerView"/>: sets the
/// <see cref="ViewerViewModel"/> BindingContext, forwards the sourceId query
/// parameter, drives the embedded view's render loop from page lifecycle, and
/// attaches/detaches full-screen chrome ownership via <see cref="ViewerFullScreenChromeController"/>.
/// </summary>
[QueryProperty(nameof(SourceId), "sourceId")]
public partial class ViewerPage : ContentPage, IShellChromeHost
{
    private readonly ViewerViewModel _viewModel;
    private readonly ViewerFullScreenChromeController _fullScreenChromeController;
    private readonly IDiagnosticOverlayService? _diagnostics;
    private bool _tornDown;

    public string? SourceId
    {
        set
        {
            if (_viewModel is not null)
                _viewModel.SourceId = value;
        }
    }

    public ViewerPage(
        ViewerViewModel viewModel,
        ViewerFullScreenChromeController fullScreenChromeController,
        IDiagnosticOverlayService? diagnostics = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _fullScreenChromeController = fullScreenChromeController;
        _diagnostics = diagnostics;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var t0 = Environment.TickCount64;
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "page.appearing.begin", "name=Viewer");
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "viewer.startrendering");
        Viewer.StartRendering();
        _fullScreenChromeController.Attach(this, _viewModel);
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "page.appearing.end", $"name=Viewer ms={Environment.TickCount64 - t0}");
    }

    protected override void OnDisappearing()
    {
        var t0 = Environment.TickCount64;
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "page.disappearing.begin", "name=Viewer");
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "viewer.stoprendering");
        Viewer.StopRendering();
        _fullScreenChromeController.Detach();

        // OnDisappearing also fires when the app is merely backgrounded, so the render surface
        // and the ViewModel must survive it. The pop path is OnNavigatedFrom below: in a Shell app
        // Disappearing is raised *before* the page leaves the nav stack, so on a pop this stack
        // check is still true and the branch never runs (#450 — 13/13 Back cycles on device never
        // reached viewer.teardown from here). It is kept as defence only, in case Shell's ordering
        // ever changes and a page does reach Disappearing already off the stack. The ModalStack
        // check stays for the same reason; nothing in the app pushes a modal over this page.
        if (Shell.Current?.Navigation?.NavigationStack?.Contains(this) != true
            && Shell.Current?.Navigation?.ModalStack?.Count is not > 0)
        {
            TeardownOnce("disappearing-off-stack");
        }

        base.OnDisappearing();
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "page.disappearing.end", $"name=Viewer ms={Environment.TickCount64 - t0}");
    }

    /// <summary>
    /// The pop path (#450). Shell raises NavigatedFrom after the navigation has completed, so a
    /// popped page is no longer in <see cref="INavigation.NavigationStack"/> here — that fact,
    /// not the enum, is the gate. Backgrounding and rotation never reach this override: an
    /// activity pause raises Disappearing only, and a placement change swaps chrome without
    /// navigating (#395), so the ViewModel survives both as before. A tab switch with this page
    /// pushed (View → Home via the rail) does reach here but leaves the page on the View stack:
    /// not disposed, correct — the #410 handoff stops the receiver and the page is still there
    /// on return.
    /// </summary>
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);

        var offStack = Navigation?.NavigationStack?.Contains(this) != true;
        var popped = args.NavigationType is NavigationType.Pop or NavigationType.PopToRoot or NavigationType.Remove;
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "viewer.navigatedfrom",
            $"type={args.NavigationType} offStack={offStack} dest={args.DestinationPage?.GetType().Name}");

        if (offStack)
        {
            TeardownOnce("navigatedfrom");
        }
        else if (popped)
        {
            // Cross-check only: a pop that leaves this page on the stack is the one finding that
            // would change the gate above (to offStack || popped). Traced, never acted on.
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "viewer.navigatedfrom.mismatch",
                $"type={args.NavigationType}");
        }
    }

    /// <summary>
    /// Releases the render surface and disposes the Transient ViewModel exactly once, whichever
    /// of the two lifecycle paths gets there first. <see cref="ViewerView.Teardown"/> is itself
    /// idempotent and <see cref="ViewerViewModel.Dispose"/> carries its own guard, but the traces
    /// must be emitted once too — the e2e counts them.
    /// </summary>
    private void TeardownOnce(string reason)
    {
        if (_tornDown)
            return;
        _tornDown = true;

        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "viewer.teardown", $"reason={reason}");
        Viewer.Teardown();

        var disposeStartedAt = Environment.TickCount64;
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "viewer.vm.dispose.begin");
        _viewModel.Dispose();
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "viewer.vm.dispose.end", $"ms={Environment.TickCount64 - disposeStartedAt}");
    }

    protected override bool OnBackButtonPressed()
        => _fullScreenChromeController.HandleBackButton() || base.OnBackButtonPressed();

    /// <inheritdoc cref="IShellChromeHost.RefreshShellChrome"/>
    public void RefreshShellChrome() => OnPropertyChanged(Shell.TabBarIsVisibleProperty.PropertyName);
}
