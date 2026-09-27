using NdiForAndroid.Features.DiagOverlay.Services;
using NdiForAndroid.Features.Viewer.Services;
using NdiForAndroid.Features.Viewer.ViewModels;

namespace NdiForAndroid.Features.Viewer.Views;

/// <summary>
/// Thin host page for the reusable <see cref="ViewerView"/>: sets the
/// <see cref="ViewerViewModel"/> BindingContext, forwards the sourceId query
/// parameter, drives the embedded view's render loop from page lifecycle, and
/// attaches/detaches full-screen chrome ownership via <see cref="ViewerFullScreenChromeController"/>.
/// </summary>
[QueryProperty(nameof(SourceId), "sourceId")]
public partial class ViewerPage : ContentPage
{
    private readonly ViewerViewModel _viewModel;
    private readonly ViewerFullScreenChromeController _fullScreenChromeController;
    private readonly IDiagnosticOverlayService? _diagnostics;

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

        // OnDisappearing also fires when the app is merely backgrounded — only tear down the
        // render surface and dispose the ViewModel once this page has actually left the nav
        // stack, so both survive backgrounding and remain alive for OnAppResumed. The ModalStack
        // check is kept as defence in case Shell's own navigation-handoff ordering ever changes;
        // nothing in the app pushes a modal over this page any more.
        if (Shell.Current?.Navigation?.NavigationStack?.Contains(this) != true
            && Shell.Current?.Navigation?.ModalStack?.Count is not > 0)
        {
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "viewer.teardown");
            Viewer.Teardown();

            var disposeStartedAt = Environment.TickCount64;
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "viewer.vm.dispose.begin");
            _viewModel.Dispose();
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "viewer.vm.dispose.end", $"ms={Environment.TickCount64 - disposeStartedAt}");
        }

        base.OnDisappearing();
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "page.disappearing.end", $"name=Viewer ms={Environment.TickCount64 - t0}");
    }

    protected override bool OnBackButtonPressed()
        => _fullScreenChromeController.HandleBackButton() || base.OnBackButtonPressed();
}
