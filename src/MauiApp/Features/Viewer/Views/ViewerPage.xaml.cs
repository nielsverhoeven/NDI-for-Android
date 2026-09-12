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

    public string? SourceId
    {
        set
        {
            if (_viewModel is not null)
                _viewModel.SourceId = value;
        }
    }

    public ViewerPage(ViewerViewModel viewModel, ViewerFullScreenChromeController fullScreenChromeController)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _fullScreenChromeController = fullScreenChromeController;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Viewer.StartRendering();
        _fullScreenChromeController.Attach(this, _viewModel);
    }

    protected override void OnDisappearing()
    {
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
            Viewer.Teardown();
            _viewModel.Dispose();
        }

        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
        => _fullScreenChromeController.HandleBackButton() || base.OnBackButtonPressed();
}
