namespace NdiForAndroid.Features.DiagOverlay.Services;

/// <summary>
/// Developer-mode diagnostics state: the developer-mode gate, the latest viewer and
/// discovery snapshots and the in-memory log buffer. MAUI-free and unit-tested (#333);
/// the platform mirror (logcat on Android) is injected as <see cref="IDiagnosticLogSink"/>.
/// </summary>
public sealed class DiagnosticOverlayService : IDiagnosticOverlayService
{
    /// <summary>logcat tag of the per-poll discovery line (<c>adb logcat -s NDI-Discovery</c>).</summary>
    public const string DiscoveryLogTag = "NDI-Discovery";

    private readonly IDiagnosticLogSink? _logSink;
    private ViewerDiagnosticSnapshot _viewerDiagnostics = new(0f, 0f, 0, 0, string.Empty);
    private DiscoveryDiagnosticSnapshot _discoveryDiagnostics = new("No discovery run yet", 0, null);

    // volatile: written on the UI thread (Settings toggle), read from NDI pump and discovery
    // poll threads as the gate for logcat diagnostics.
    private volatile bool _isDeveloperMode;

    public DiagnosticOverlayService(IDiagnosticLogSink? logSink = null, TimeProvider? timeProvider = null)
    {
        _logSink = logSink;
        LogBuffer = new DiagnosticLogBuffer(timeProvider);
    }

    public DiagnosticLogBuffer LogBuffer { get; }

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

    public void UpdateViewerDiagnostics(float fps, float dropPercent, int width, int height, string sourceEndpoint)
        => _viewerDiagnostics = new ViewerDiagnosticSnapshot(fps, dropPercent, width, height, sourceEndpoint);

    public void UpdateDiscoveryDiagnostics(string lastStatus, int sourceCount, TimeSpan? duration = null)
    {
        _discoveryDiagnostics = new DiscoveryDiagnosticSnapshot(lastStatus, sourceCount, duration);

        // Developer mode only: one line per discovery cycle (never per frame) so discovery
        // behaviour can be captured with `adb logcat -s NDI-Discovery`.
        if (!_isDeveloperMode || _logSink is null)
            return;

        try
        {
            _logSink.Debug(DiscoveryLogTag,
                $"status=\"{lastStatus}\" sources={sourceCount} durationMs={(duration?.TotalMilliseconds ?? -1):0}");
        }
        catch
        {
            // Logging is best-effort and runs on the poll thread - never fault the caller.
        }
    }
}
