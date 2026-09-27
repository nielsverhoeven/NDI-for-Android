using NdiForAndroid.Features.DiagOverlay.ViewModels;

namespace NdiForAndroid.Features.DiagOverlay.Views;

public partial class DiagnosticLogPage : ContentPage
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
}
