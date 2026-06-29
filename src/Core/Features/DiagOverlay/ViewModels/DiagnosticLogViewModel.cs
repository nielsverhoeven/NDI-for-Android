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

    public record LogEntryViewModel(
        long TimestampEpochMillis, string Category, string Message, DiagnosticLogBuffer.LogLevel Level)
    {
        public string LevelColor => Level switch
        {
            DiagnosticLogBuffer.LogLevel.Error => "#FF4444",
            DiagnosticLogBuffer.LogLevel.Warning => "#FFA500",
            _ => "#888888",
        };

        public string TimestampRelative
        {
            get
            {
                var elapsed = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(TimestampEpochMillis);
                if (elapsed.TotalSeconds < 60) return $"{(int)elapsed.TotalSeconds}s ago";
                if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
                return $"{(int)elapsed.TotalHours}h ago";
            }
        }
    }
}
