using NdiForAndroid.Features.DiagOverlay.Services;

namespace NdiForAndroid.Services;

/// <summary>
/// No-op implementation of <see cref="IDiagnosticLogSink"/> for non-Android build targets.
/// </summary>
public sealed class NoopDiagnosticLogSink : IDiagnosticLogSink
{
    public void Debug(string tag, string message) { }
}
