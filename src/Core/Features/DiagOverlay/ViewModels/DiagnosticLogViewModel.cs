using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.DiagOverlay.Services;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.DiagOverlay.ViewModels;

public partial class DiagnosticLogViewModel : ObservableObject
{
    private readonly IDiagnosticOverlayService _overlayService;
    private readonly IMainThreadDispatcher _dispatcher;
    private System.Timers.Timer? _refreshTimer;

    [ObservableProperty]
    private IReadOnlyList<LogEntryViewModel> _logEntries = Array.Empty<LogEntryViewModel>();

    public DiagnosticLogViewModel(IDiagnosticOverlayService overlayService, IMainThreadDispatcher dispatcher)
    {
        _overlayService = overlayService;
        _dispatcher = dispatcher;
        RefreshLog();
        _refreshTimer = new System.Timers.Timer(2000) { AutoReset = true };
        _refreshTimer.Elapsed += (_, _) => _dispatcher.BeginInvokeOnMainThread(RefreshLog);
        _refreshTimer.Start();
    }

    [RelayCommand]
    private void Clear()
    {
        _overlayService.LogBuffer.Clear();
        RefreshLog();
    }

    private void RefreshLog()
    {
        var entries = _overlayService.LogBuffer.GetEntries();
        LogEntries = entries.Select(e => new LogEntryViewModel(
            e.TimestampEpochMillis, e.Category, e.Message, e.Level)).ToList().AsReadOnly();
    }
}
