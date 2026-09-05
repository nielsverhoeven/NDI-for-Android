using NdiForAndroid.Features.Viewer.ViewModels;

namespace NdiForAndroid.Features.Viewer.Views;

/// <summary>
/// Thin host page for the reusable <see cref="ViewerView"/>: sets the
/// <see cref="ViewerViewModel"/> BindingContext, forwards the sourceId query
/// parameter, and drives the embedded view's render loop from page lifecycle.
/// </summary>
[QueryProperty(nameof(SourceId), "sourceId")]
public partial class ViewerPage : ContentPage
{
    private readonly ViewerViewModel _viewModel;

    public string? SourceId
    {
        set
        {
            if (_viewModel is not null)
                _viewModel.SourceId = value;
        }
    }

    public ViewerPage(ViewerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Viewer.StartRendering();
    }

    protected override void OnDisappearing()
    {
        Viewer.StopRendering();

        // OnDisappearing also fires when the app is merely backgrounded, or when a modal (the
        // full-screen viewer) is pushed over this page — only dispose once it has actually left
        // the nav stack, so the ViewModel survives backgrounding and is still alive for OnAppResumed.
        if (Shell.Current?.Navigation?.NavigationStack?.Contains(this) != true
            && Shell.Current?.Navigation?.ModalStack?.Count is not > 0)
            _viewModel.Dispose();

        base.OnDisappearing();
    }
}
