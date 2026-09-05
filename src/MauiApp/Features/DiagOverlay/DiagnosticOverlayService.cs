using NdiForAndroid.Features.DiagOverlay.Services;

namespace NdiForAndroid.Features.DiagOverlay;

/// <summary>
/// Diagnostic overlay service: manages developer mode toggling, viewer diagnostics,
/// discovery diagnostics, and the in-memory log buffer.
/// </summary>
public sealed class DiagnosticOverlayService : IDiagnosticOverlayService
{
    private ViewerDiagnosticSnapshot _viewerDiagnostics = new(0f, 0f, 0, 0, string.Empty);
    private DiscoveryDiagnosticSnapshot _discoveryDiagnostics = new("No discovery run yet", 0, null);
    // volatile: written on the UI thread (Settings toggle), read from NDI pump threads
    // as the gate for logcat diagnostics.
    private volatile bool _isDeveloperMode;
    public DiagnosticLogBuffer LogBuffer { get; } = new();

    public bool IsDeveloperMode
    {
        get => _isDeveloperMode;
        set
        {
            if (_isDeveloperMode == value) return;
            _isDeveloperMode = value;
            LogBuffer.Add("DevOverlay", $"Developer mode {(value ? "enabled" : "disabled")}");
        }
    }

    public ViewerDiagnosticSnapshot GetCurrentViewerDiagnostics() => _viewerDiagnostics;

    public DiscoveryDiagnosticSnapshot GetCurrentDiscoveryDiagnostics() => _discoveryDiagnostics;

    public void UpdateViewerDiagnostics(
        float fps,
        float dropPercent,
        int width,
        int height,
        string sourceEndpoint)
    {
        _viewerDiagnostics = new ViewerDiagnosticSnapshot(fps, dropPercent, width, height, sourceEndpoint);
    }

    public void UpdateDiscoveryDiagnostics(
        string lastStatus,
        int sourceCount,
        TimeSpan? duration = null)
    {
        _discoveryDiagnostics = new DiscoveryDiagnosticSnapshot(lastStatus, sourceCount, duration);

        // Developer mode only: one logcat line per discovery cycle (never per frame) so
        // discovery behaviour can be captured with `adb logcat -s NDI-Discovery`.
        if (_isDeveloperMode)
        {
            try
            {
                Android.Util.Log.Debug("NDI-Discovery",
                    $"status=\"{lastStatus}\" sources={sourceCount} durationMs={(duration?.TotalMilliseconds ?? -1):0}");
            }
            catch
            {
                // Logging is best-effort.
            }
        }
    }

    /// <summary>
    /// Called from an NDI bridge layer to log a diagnostic event.
    /// </summary>
    public void LogBridgeEvent(string message, DiagnosticLogBuffer.LogLevel level = DiagnosticLogBuffer.LogLevel.Info)
    {
        LogBuffer.Add("NDI-Bridge", message, level);

        // Developer mode only: mirror to logcat so bridge events can be captured with
        // `adb logcat -s NDI-Bridge` during device soak tests.
        if (_isDeveloperMode)
            Android.Util.Log.Debug("NDI-Bridge", message);
    }
}
