using Microsoft.Maui.Controls;
using NdiForAndroid.Features.DiagOverlay.Services;
using NdiForAndroid.Features.Output.ViewModels;

namespace NdiForAndroid.Features.Output.Views;

/// <summary>Stream tab root. Singleton (matches <see cref="OutputViewModel"/>, #352/#359).</summary>
/// <remarks>
/// Hosted by two <c>ShellContent</c>s (<c>stream-tab</c> and <c>stream-rail</c>); Shell re-parents this
/// single instance between them. Because the instance lives for the app lifetime, the three query
/// properties are one-shot: <see cref="ApplyEntryStateAsync"/> nulls them in <c>finally</c> so an old
/// <c>resume=true</c> / re-stream intent is never re-applied on a later plain tab entry. Never dispose the
/// ViewModel from page lifecycle.
/// </remarks>
[QueryProperty(nameof(ReStreamSourceId), "reStreamSourceId")]
[QueryProperty(nameof(IsReStreamMode), "isReStreamMode")]
[QueryProperty(nameof(ResumeRequested), "resume")]
public partial class OutputPage : ContentPage
{
    private readonly OutputViewModel _viewModel;
    private readonly IDiagnosticOverlayService? _diagnostics;

    public string? ReStreamSourceId { get; set; }

    public string? IsReStreamMode { get; set; }

    public string? ResumeRequested { get; set; }

    public OutputPage(OutputViewModel viewModel, IDiagnosticOverlayService? diagnostics = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _diagnostics = diagnostics;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var t0 = Environment.TickCount64;
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "page.appearing.begin", "name=Output");
        _ = ApplyEntryStateAsync();
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "page.appearing.end", $"name=Output ms={Environment.TickCount64 - t0}");
    }

    private async Task ApplyEntryStateAsync()
    {
        try
        {
            var loadStartedAt = Environment.TickCount64;
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "output.load.begin");
            await _viewModel.LoadCommand.ExecuteAsync(null);
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "output.load.end", $"ms={Environment.TickCount64 - loadStartedAt}");

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
