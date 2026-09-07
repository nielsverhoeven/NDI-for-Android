namespace NdiForAndroid.Features.DiagOverlay.Services;

/// <summary>
/// Platform mirror for developer-mode diagnostics (logcat on Android, no-op elsewhere).
/// Called from discovery poll / NDI pump threads: implementations must be thread-safe
/// and must never throw.
/// </summary>
public interface IDiagnosticLogSink
{
    /// <summary>Writes one debug line under <paramref name="tag"/> (e.g. "NDI-Discovery").</summary>
    void Debug(string tag, string message);
}
