namespace NdiForAndroid.Features.DiagOverlay.Services;

/// <summary>
/// Provides developer-mode diagnostics for the Viewer screen.
/// </summary>
public interface IDiagnosticOverlayService
{
    /// <summary>
    /// Gets the log buffer used by the diagnostic log page.
    /// </summary>
    DiagnosticLogBuffer LogBuffer { get; }

    /// <summary>
    /// Gets/sets whether developer mode is enabled (controls overlay visibility).
    /// </summary>
    bool IsDeveloperMode { get; set; }

    /// <summary>
    /// Updates viewer diagnostics (FPS, dropped frames, resolution, etc.).
    /// Called periodically by the viewer or from bridge state callbacks.
    /// </summary>
    void UpdateViewerDiagnostics(
        float fps,
        float dropPercent,
        int width,
        int height,
        string sourceEndpoint);

    /// <summary>
    /// Updates discovery diagnostics (last run status, duration, source count).
    /// Called after each discovery cycle completes.
    /// </summary>
    void UpdateDiscoveryDiagnostics(
        string lastStatus,
        int sourceCount,
        TimeSpan? duration = null);

    /// <summary>
    /// Emits one developer-mode trace line under <paramref name="tag"/> (logcat on Android,
    /// no-op elsewhere). No-op when developer mode is off. Never writes to <see cref="LogBuffer"/>.
    /// </summary>
    void Trace(string tag, string phase, string? detail = null);

    /// <summary>
    /// Retrieves the current viewer diagnostics snapshot.
    /// </summary>
    ViewerDiagnosticSnapshot GetCurrentViewerDiagnostics();

    /// <summary>
    /// Retrieves the current discovery diagnostics snapshot.
    /// </summary>
    DiscoveryDiagnosticSnapshot GetCurrentDiscoveryDiagnostics();
}

/// <summary>Current viewer diagnostic data.</summary>
public record ViewerDiagnosticSnapshot(
    float Fps,
    float DropPercent,
    int Width,
    int Height,
    string SourceEndpoint);

/// <summary>Current discovery diagnostic data.</summary>
public record DiscoveryDiagnosticSnapshot(
    string LastStatus,
    int SourceCount,
    TimeSpan? Duration);
