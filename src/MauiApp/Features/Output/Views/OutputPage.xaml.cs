using Microsoft.Maui.Controls;
using NdiForAndroid.Features.Output.ViewModels;

namespace NdiForAndroid.Features.Output.Views;

[QueryProperty(nameof(ReStreamSourceId), "reStreamSourceId")]
[QueryProperty(nameof(IsReStreamMode), "isReStreamMode")]
[QueryProperty(nameof(ResumeRequested), "resume")]
public partial class OutputPage : ContentPage
{
    private readonly OutputViewModel _viewModel;

    public string? ReStreamSourceId { get; set; }

    public string? IsReStreamMode { get; set; }

    public string? ResumeRequested { get; set; }

    public OutputPage(OutputViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = ApplyEntryStateAsync();
    }

    private async Task ApplyEntryStateAsync()
    {
        try
        {
            await _viewModel.LoadCommand.ExecuteAsync(null);

            if (!string.IsNullOrEmpty(ReStreamSourceId))
                _viewModel.ApplyReStreamRequest(ReStreamSourceId, bool.TryParse(IsReStreamMode, out var b) && b);
            else if (bool.TryParse(ResumeRequested, out var resume) && resume)
                await _viewModel.ApplyResumeRequestCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OutputPage entry state failed: {ex}");
        }
        finally
        {
            ReStreamSourceId = null;
            IsReStreamMode = null;
            ResumeRequested = null;
        }
    }
}
