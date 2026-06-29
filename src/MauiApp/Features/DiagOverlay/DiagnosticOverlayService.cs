using System.Reactive.Linq;

namespace NdiForAndroid.Features.DiagOverlay;

/// <summary>
/// Diagnostic overlay service: manages developer mode toggling, viewer diagnostics,
/// discovery diagnostics, and the in-memory log buffer.
/// </summary>
public sealed class DiagnosticOverlayService : IDiagnosticOverlayService
{
    private ViewerDiagnosticSnapshot _viewerDiagnostics;
    private DiscoveryDiagnosticSnapshot _discoveryDiagnostics;
    private bool _isDeveloperMode;
    public readonly DiagnosticLogBuffer LogBuffer = new();

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
    }

    /// <summary>
    /// Called from an NDI bridge layer to log a diagnostic event.
    /// </summary>
    public void LogBridgeEvent(string message, DiagnosticLogBuffer.LogLevel level = DiagnosticLogBuffer.LogLevel.Info)
        => LogBuffer.Add("NDI-Bridge", message, level);
}
