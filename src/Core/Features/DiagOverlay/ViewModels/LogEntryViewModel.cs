using NdiForAndroid.Features.DiagOverlay.Services;

namespace NdiForAndroid.Features.DiagOverlay.ViewModels;

// Level's display colour comes from the app's DynamicResource theme tokens, resolved by
// NdiForAndroid.Converters.LogLevelColorConverter (MauiApp) — not hardcoded here (#329). Core
// stays MAUI-free, so it exposes only the enum value and lets the view layer own colour.
public record LogEntryViewModel(
    long TimestampEpochMillis, string Category, string Message, DiagnosticLogBuffer.LogLevel Level)
{
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
