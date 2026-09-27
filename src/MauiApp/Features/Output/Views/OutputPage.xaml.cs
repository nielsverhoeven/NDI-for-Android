using Microsoft.Maui.Controls;
using NdiForAndroid.Features.DiagOverlay.Services;
using NdiForAndroid.Features.Output.ViewModels;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Output.Views;

/// <summary>Stream tab root. Singleton (matches <see cref="OutputViewModel"/>, #352/#359).</summary>
/// <remarks>
/// Hosted by the one <c>stream</c> <c>ShellContent</c> (#395); a placement change (rotation, size class)
/// only swaps the chrome around it, so it raises no Disappearing/Appearing and does not re-run
/// <c>LoadCommand</c> — the ViewModel's own <c>OutputStatusChanged</c> subscription keeps the status
/// live in between. Because the instance lives for the app lifetime, the three query
/// properties are one-shot: <see cref="ApplyEntryStateAsync"/> nulls them in <c>finally</c> so an old
/// <c>resume=true</c> / re-stream intent is never re-applied on a later plain tab entry. Never dispose the
/// ViewModel from page lifecycle.
/// </remarks>
[QueryProperty(nameof(ReStreamSourceId), "reStreamSourceId")]
[QueryProperty(nameof(IsReStreamMode), "isReStreamMode")]
[QueryProperty(nameof(ResumeRequested), "resume")]
public partial class OutputPage : ContentPage, IShellChromeHost
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

    /// <inheritdoc cref="IShellChromeHost.RefreshShellChrome"/>
    public void RefreshShellChrome() => OnPropertyChanged(Shell.TabBarIsVisibleProperty.PropertyName);
}
