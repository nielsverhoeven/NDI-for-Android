using Microsoft.Maui.Controls;
using NdiForAndroid.Features.Output.ViewModels;

namespace NdiForAndroid.Features.Output.Views;

public partial class OutputPage : ContentPage
{
    private readonly OutputViewModel _viewModel;

    public OutputPage(OutputViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Lifecycle wiring only (no logic): load the persisted output configuration.
        _viewModel.LoadCommand.Execute(null);
    }
}
