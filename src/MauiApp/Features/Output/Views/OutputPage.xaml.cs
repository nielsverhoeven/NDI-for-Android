using Microsoft.Maui.Controls;
using NdiForAndroid.Features.Output.ViewModels;

namespace NdiForAndroid.Features.Output.Views;

[QueryProperty(nameof(ReStreamSourceId), "reStreamSourceId")]
[QueryProperty(nameof(IsReStreamMode), "isReStreamMode")]
public partial class OutputPage : ContentPage
{
    private readonly OutputViewModel _viewModel;

    public string? ReStreamSourceId { get; set; }

    public string? IsReStreamMode { get; set; }

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

        if (!string.IsNullOrEmpty(ReStreamSourceId))
            _viewModel.ApplyReStreamRequest(ReStreamSourceId, bool.TryParse(IsReStreamMode, out var b) && b);
    }
}
