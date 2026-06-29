using NdiForAndroid.Features.DiagOverlay.Services;

namespace NdiForAndroid.Features.DiagOverlay.ViewModels;

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
