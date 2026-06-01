using Microsoft.Maui.Controls;
using NdiForAndroid.Features.Viewer.ViewModels;

namespace NdiForAndroid.Features.Viewer.Views;

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
}
