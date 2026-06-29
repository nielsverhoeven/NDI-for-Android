using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.DiagOverlay.Services;

namespace NdiForAndroid.Features.DiagOverlay.ViewModels;

public partial class DiagnosticLogViewModel : ObservableObject
{
    private readonly IDiagnosticOverlayService _overlayService;
    private Timer? _refreshTimer;

    [ObservableProperty]
    private IReadOnlyList<LogEntryViewModel> _logEntries = Array.Empty<LogEntryViewModel>();

    public DiagnosticLogViewModel(IDiagnosticOverlayService overlayService)
    {
        _overlayService = overlayService;
        RefreshLog();
        _refreshTimer = new Timer(_ =>
        {
            Application.Current!.MainDispatcher.Dispatch(RefreshLog);
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
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
                if (elapsed.TotalSeconds < 60) return $\"{(int)elapsed.TotalSeconds}s ago\";
                if (elapsed.TotalMinutes < 60) return $\"{(int)elapsed.TotalMinutes}m ago\";
                return $\"{(int)elapsed.TotalHours}h ago\";
            }
        }
    }
}
