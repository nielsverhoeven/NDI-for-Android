namespace NdiForAndroid.NdiBridge;

/// <summary>
/// Contract for NDI source discovery operations.
/// Implementations must not expose NDI SDK types outside this interface.
/// </summary>
public interface INdiDiscoveryBridge
{
    Task<IReadOnlyList<NdiSourceEntry>> DiscoverSourcesAsync(CancellationToken cancellationToken = default);

    Task<bool> IsDiscoveryServerReachableAsync(string host, int port, CancellationToken cancellationToken = default);

    Task<NdiDiscoveryCheckResult> PerformDiscoveryCheckAsync(
        string host, int port, string correlationId, CancellationToken cancellationToken = default);

    void SetDiscoveryEndpoint(string? host, int? port);
}

/// <summary>
/// Contract for NDI video receiver (viewer) operations.
/// NDI callbacks are marshaled to caller thread by the implementation.
/// </summary>
public interface INdiViewerBridge
{
    void StartReceiver(string sourceId);
    void StopReceiver();
    NdiVideoFrame? GetLatestFrame();
    float GetDroppedFramePercent();
    (int Width, int Height) GetActualResolution();
    float GetMeasuredFps();
}

/// <summary>
/// Contract for NDI output (sender) operations.
/// </summary>
public interface INdiOutputBridge
{
    Task<bool> IsSourceReachableAsync(string sourceId, CancellationToken cancellationToken = default);
    Task StartOutputAsync(string sourceId, string streamName, CancellationToken cancellationToken = default);
    Task StopOutputAsync(CancellationToken cancellationToken = default);
}
