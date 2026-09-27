using NdiForAndroid.Features.DiagOverlay.ViewModels;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.DiagOverlay.Views;

public partial class DiagnosticLogPage : ContentPage, IShellChromeHost
{
    private readonly DiagnosticLogViewModel _viewModel;

    public DiagnosticLogPage(DiagnosticLogViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();
    }

    // Lifecycle plumbing only: the 2 s list refresh runs while the page is on screen (#437).
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Activate();
    }

    protected override void OnDisappearing()
    {
        _viewModel.Deactivate();
        base.OnDisappearing();
    }

    /// <inheritdoc cref="IShellChromeHost.RefreshShellChrome"/>
    public void RefreshShellChrome() => OnPropertyChanged(Shell.TabBarIsVisibleProperty.PropertyName);
}
