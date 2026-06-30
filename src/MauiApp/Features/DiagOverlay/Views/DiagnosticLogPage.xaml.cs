using NdiForAndroid.Features.DiagOverlay.ViewModels;

namespace NdiForAndroid.Features.DiagOverlay.Views;

public partial class DiagnosticLogPage : ContentPage
{
    public DiagnosticLogPage(DiagnosticLogViewModel viewModel)
    {
        BindingContext = viewModel;
        InitializeComponent();
    }
}
