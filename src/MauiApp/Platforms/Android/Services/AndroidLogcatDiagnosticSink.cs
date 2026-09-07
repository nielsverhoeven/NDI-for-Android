using NdiForAndroid.Features.DiagOverlay.Services;

namespace NdiForAndroid.Platforms.Android.Services;

/// <summary>
/// Mirrors developer-mode diagnostics to logcat so device soak tests can capture them with
/// <c>adb logcat -s NDI-Discovery</c>. Best-effort: never throws into the poll/pump thread.
/// </summary>
public sealed class AndroidLogcatDiagnosticSink : IDiagnosticLogSink
{
    public void Debug(string tag, string message)
    {
        try
        {
            global::Android.Util.Log.Debug(tag, message);
        }
        catch
        {
            // Logging is best-effort.
        }
    }
}
