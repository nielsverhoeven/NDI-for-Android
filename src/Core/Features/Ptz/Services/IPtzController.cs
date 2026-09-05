using NdiForAndroid.Features.Ptz.Models;

namespace NdiForAndroid.Features.Ptz.Services;

/// <summary>
/// PTZ control seam. One instance targets either the connected NDI source's own PTZ or a
/// VISCA-over-TCP endpoint; the active backend is selected by <see cref="IPtzControllerFactory"/>.
/// </summary>
public interface IPtzController
{
    /// <summary>Current connection state of this controller's backend.</summary>
    PtzLinkState LinkState { get; }

    /// <summary>Message describing the most recent failure, or null when there is none.</summary>
    string? LastError { get; }

    /// <summary>Raised whenever <see cref="LinkState"/> changes.</summary>
    event EventHandler<PtzLinkState>? LinkStateChanged;

    /// <summary>Continuous pan/tilt speed, each -1..+1 (0 stops).</summary>
    Task<bool> PanTiltAsync(float panSpeed, float tiltSpeed, CancellationToken cancellationToken = default);

    /// <summary>Continuous zoom speed, -1..+1 (0 stops).</summary>
    Task<bool> ZoomAsync(float zoomSpeed, CancellationToken cancellationToken = default);

    /// <summary>Engages auto-focus.</summary>
    Task<bool> AutoFocusAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores the current position as the given preset.</summary>
    Task<bool> StorePresetAsync(int presetNumber, CancellationToken cancellationToken = default);

    /// <summary>Recalls the given preset at the given speed (0..1).</summary>
    Task<bool> RecallPresetAsync(int presetNumber, float speed = 1f, CancellationToken cancellationToken = default);

    /// <summary>Releases any backend connection. Never throws.</summary>
    Task ShutdownAsync();
}
