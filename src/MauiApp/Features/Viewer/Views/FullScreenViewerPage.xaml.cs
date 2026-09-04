using System.ComponentModel;
using NdiForAndroid.Features.Viewer.ViewModels;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Viewer.Views;

/// <summary>
/// Chromeless modal page hosting a second <see cref="ViewerView"/> bound to the donor's live
/// <see cref="ViewerViewModel"/>. Pops itself when <see cref="ViewerViewModel.IsFullScreen"/>
/// turns false again (toggle button, back button, or app pause).
/// </summary>
public partial class FullScreenViewerPage : ContentPage
{
    private readonly IImmersiveModeService _immersiveMode;
    private readonly IAppLifecycleService _lifecycle;
    private ViewerViewModel? _viewModel;
    private Action? _onClosed;
    private bool _closing;
    private bool _closeAnimated = true;

    public FullScreenViewerPage(IImmersiveModeService immersiveMode, IAppLifecycleService lifecycle)
    {
        InitializeComponent();
        _immersiveMode = immersiveMode;
        _lifecycle = lifecycle;
    }

    public void Initialize(ViewerViewModel viewModel, Action onClosed)
    {
        _viewModel = viewModel;
        _onClosed = onClosed;
        BindingContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _lifecycle.AppPaused += OnAppPaused;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        FullScreenViewer.StartRendering();
        _immersiveMode.EnterImmersive();
    }

    protected override void OnDisappearing()
    {
        FullScreenViewer.StopRendering();
        _immersiveMode.ExitImmersive();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel is not null)
            _viewModel.IsFullScreen = false;
        return true; // swallow — never reaches Shell / the donor page's own back handling
    }

    private void OnAppPaused()
    {
        _closeAnimated = false;
        if (_viewModel is not null)
            _viewModel.IsFullScreen = false; // never restored on resume
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewerViewModel.IsFullScreen) && _viewModel?.IsFullScreen == false)
            _ = CloseAsync();
    }

    private async Task CloseAsync()
    {
        if (_closing) return;
        _closing = true;

        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _lifecycle.AppPaused -= OnAppPaused;

        if (Shell.Current?.Navigation.ModalStack.Contains(this) == true)
            await Shell.Current.Navigation.PopModalAsync(_closeAnimated);

        FullScreenViewer.Teardown();
        _onClosed?.Invoke();
    }
}
