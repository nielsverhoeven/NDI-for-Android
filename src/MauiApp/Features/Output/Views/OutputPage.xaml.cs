using Microsoft.Maui.Controls;
using NdiForAndroid.Features.Output.ViewModels;

namespace NdiForAndroid.Features.Output.Views;

[QueryProperty(nameof(SourceId), "sourceId")]
public partial class OutputPage : ContentPage
{
    private readonly OutputViewModel _viewModel;

    public string? SourceId
    {
        set
        {
            if (_viewModel is not null)
                _viewModel.SourceId = value;
        }
    }

    public OutputPage(OutputViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }
}
