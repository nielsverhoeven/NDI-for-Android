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

    /// <summary>
    /// Sets the active discovery mode. Calling this is idempotent and triggers
    /// an immediate internal switch (stopping the previous mode cleanly).
    /// </summary>
    /// <param name="mode">The discovery mode to activate.</param>
    /// <param name="serverEndpoints">
    /// Non-empty only when <paramref name="mode"/> is <see cref="DiscoveryMode.DiscoveryServer"/>.
    /// All endpoints are queried; results are merged with deduplication.
    /// </param>
    void SetDiscoveryMode(
        DiscoveryMode mode,
        IReadOnlyList<DiscoveryServerEndpoint>? serverEndpoints = null);
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

    /// <summary>
    /// Returns the current connection state of the receiver. Used by the
    /// ViewModel reconnection state machine to detect unexpected drops.
    /// </summary>
    ConnectionState GetConnectionState();
}

/// <summary>
/// Contract for NDI output (sender) operations.
/// </summary>
public interface INdiOutputBridge
{
    /// <summary>
    /// Starts an NDI sender that advertises this device on the network under
    /// <paramref name="streamName"/>. No remote sourceId is required.
    /// </summary>
    Task StartOutputAsync(string streamName, CancellationToken cancellationToken = default);
    Task StopOutputAsync(CancellationToken cancellationToken = default);
}
