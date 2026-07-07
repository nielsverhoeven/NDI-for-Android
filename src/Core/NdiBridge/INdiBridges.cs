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
    void StartReceiver(string sourceId, QualityProfile qualityProfile = QualityProfile.Balanced);
    void StopReceiver();
    void SetQualityProfile(QualityProfile profile);
    ConnectionState GetConnectionState();
    NdiVideoFrame? GetLatestFrame();
    float GetDroppedFramePercent();
    (int Width, int Height) GetActualResolution();
    float GetMeasuredFps();
    QualityProfile ActiveQualityProfile { get; }

    /// <summary>Raised (on the pump thread) when the receiver's connection state changes.</summary>
    event EventHandler<ConnectionState>? ConnectionStateChanged;

    /// <summary>Raised (on the pump thread) when the source echoes a tally state change.</summary>
    event EventHandler<NdiTallyEcho>? TallyEchoChanged;

    /// <summary>Reports this receiver's tally state upstream to the source (retained across reconnects).</summary>
    void SetTally(bool onProgram, bool onPreview);

    /// <summary>Enables/disables audio playback for the active connection. Default: enabled.</summary>
    bool IsAudioEnabled { get; set; }

    // ── PTZ (available only when the connected source supports it) ──────────

    /// <summary>True when the connected source exposes PTZ control. Only reliable after connection metadata has arrived.</summary>
    bool IsPtzSupported { get; }

    /// <summary>Continuous pan/tilt speed, each -1..+1 (0 stops).</summary>
    bool PtzPanTiltSpeed(float panSpeed, float tiltSpeed);

    /// <summary>Continuous zoom speed, -1..+1 (0 stops).</summary>
    bool PtzZoomSpeed(float zoomSpeed);

    /// <summary>Stores the current position as preset 0-99.</summary>
    bool PtzStorePreset(int presetNo);

    /// <summary>Recalls preset 0-99 at the given speed (0..1).</summary>
    bool PtzRecallPreset(int presetNo, float speed = 1f);

    /// <summary>Engages auto-focus.</summary>
    bool PtzAutoFocus();
}

/// <summary>
/// Contract for NDI output (sender) operations.
/// </summary>
public interface INdiOutputBridge
{
    /// <summary>
    /// Starts an NDI sender that advertises this device on the network under
    /// <paramref name="streamName"/>, fed by the selected capture input.
    /// </summary>
    /// <param name="streamName">Advertised NDI source name (device name is prepended by the SDK).</param>
    /// <param name="inputKind">Video input: device screen or front/rear camera.</param>
    /// <param name="captureMicrophone">Also capture and send device microphone audio.</param>
    Task StartOutputAsync(
        string streamName,
        Services.VideoInputKind inputKind = Services.VideoInputKind.Screen,
        bool captureMicrophone = false,
        CancellationToken cancellationToken = default);

    Task StopOutputAsync(CancellationToken cancellationToken = default);

    /// <summary>True while any connected receiver reports this sender on program tally.</summary>
    bool IsOnProgramTally { get; }

    /// <summary>Number of receivers currently connected to this sender (0 when idle).</summary>
    int ConnectionCount { get; }

    /// <summary>
    /// Raised (on a background thread) when <see cref="IsOnProgramTally"/> or
    /// <see cref="ConnectionCount"/> changed. Subscribers marshal to the UI thread.
    /// </summary>
    event EventHandler? OutputStatusChanged;

    /// <summary>
    /// Starts an NDI sender that re-streams frames captured from the specified
    /// remote NDI <paramref name="sourceId"/>. A new receiver is created
    /// specifically for the re-stream; it does NOT affect the viewer bridge's
    /// active connection.
    /// </summary>
    Task StartReStreamFromSourceAsync(
        string sourceId,
        QualityProfile qualityProfile,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops any active re-stream and releases the dedicated receiver resource.
    /// </summary>
    Task StopReStreamAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true while a re-stream session is active.
    /// </summary>
    bool IsReStreamActive { get; }
}
