using NdiForAndroid.Features.Ptz.Models;
using NdiForAndroid.NdiBridge;

namespace NdiForAndroid.Features.Ptz.Services;

/// <summary>Delegates PTZ control to the connected NDI source's own PTZ via <see cref="INdiViewerBridge"/>. No behavior beyond the bridge's own.</summary>
public sealed class NdiPtzController : IPtzController
{
    private readonly INdiViewerBridge _bridge;

    public NdiPtzController(INdiViewerBridge bridge)
    {
        ArgumentNullException.ThrowIfNull(bridge);
        _bridge = bridge;
    }

    public PtzLinkState LinkState => _bridge.IsPtzSupported ? PtzLinkState.Connected : PtzLinkState.Disconnected;

    public string? LastError => null;

    // NDI PTZ availability is polled via IsPtzSupported elsewhere; there is no push notification to relay.
    public event EventHandler<PtzLinkState>? LinkStateChanged { add { } remove { } }

    public Task<bool> PanTiltAsync(float panSpeed, float tiltSpeed, CancellationToken cancellationToken = default) =>
        Task.FromResult(_bridge.PtzPanTiltSpeed(panSpeed, tiltSpeed));

    public Task<bool> ZoomAsync(float zoomSpeed, CancellationToken cancellationToken = default) =>
        Task.FromResult(_bridge.PtzZoomSpeed(zoomSpeed));

    public Task<bool> AutoFocusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_bridge.PtzAutoFocus());

    public Task<bool> StorePresetAsync(int presetNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(_bridge.PtzStorePreset(presetNumber));

    public Task<bool> RecallPresetAsync(int presetNumber, float speed = 1f, CancellationToken cancellationToken = default) =>
        Task.FromResult(_bridge.PtzRecallPreset(presetNumber, speed));

    public Task ShutdownAsync() => Task.CompletedTask;
}
