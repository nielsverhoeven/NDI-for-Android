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
    /// <summary>
    /// Requests a receiver for <paramref name="sourceId"/>. Returns once the request has been
    /// recorded: <see cref="ReceiverGeneration"/> is bumped, the requested quality profile is
    /// published (so a <see cref="SetQualityProfile"/> that lands before the receiver exists is not
    /// lost), the previous receiver's frames stop being handed out by
    /// <see cref="GetLatestFrame"/>, and <see cref="GetConnectionState"/> is
    /// <see cref="ConnectionState.Disconnected"/> with
    /// <see cref="ReceiverStopReason.Intentional"/> — all before this returns. The native teardown
    /// of any previous receiver and the create/connect then run, in that order, on the bridge's
    /// single lifecycle worker.
    /// <para>
    /// The outcome arrives through <see cref="ConnectionStateChanged"/> and never by polling
    /// <see cref="GetConnectionState"/> straight after this call: at that point the bridge reports
    /// <see cref="ConnectionState.Disconnected"/>, not
    /// <see cref="ConnectionState.Connecting"/> — the receiver has not been created yet.
    /// </para>
    /// </summary>
    void StartReceiver(string sourceId, QualityProfile qualityProfile = QualityProfile.Balanced);

    /// <summary>
    /// Requests a stop. The synchronous part happens in the caller's turn, so it is ordered against
    /// whatever the caller does next, and it establishes four postconditions callers depend on:
    /// the pump threads are unwinding; <see cref="GetLatestFrame"/> returns <c>null</c> (so no
    /// render loop, frame-ready signal or second ViewModel can paint another frame of the stream
    /// just ended); <see cref="GetConnectionState"/> is
    /// <see cref="ConnectionState.Disconnected"/>; and every <c>Disconnected</c> raised from here
    /// until the teardown completes is tagged <see cref="ReceiverStopReason.Intentional"/>, so no
    /// reconnect window opens on a stop the app requested.
    /// <para>
    /// The native part — joining the pump threads, <c>recv_destroy</c>, releasing the runtime
    /// handle, stopping the audio sink — runs on the bridge's single lifecycle worker; the returned
    /// task completes when it has. Audio therefore fades up to a few hundred milliseconds after the
    /// video surface goes blank.
    /// </para>
    /// <para>
    /// Callers that merely want the receiver gone must NOT await this on the UI thread — use
    /// <c>StopReceiverAsync().FireAndForget()</c>. Never await or block on it from a bridge event
    /// handler: those can run on a pump thread and the worker joins that thread.
    /// </para>
    /// </summary>
    Task StopReceiverAsync();

    void SetQualityProfile(QualityProfile profile);
    ConnectionState GetConnectionState();

    /// <summary>Why the receiver is currently (or most recently was) <see cref="ConnectionState.Disconnected"/>.
    /// Read fresh alongside <see cref="GetConnectionState"/> — implementations must not require callers
    /// to cache it.</summary>
    ReceiverStopReason GetLastStopReason();

    /// <summary>Monotonic id of the receiver this bridge was last asked to run. Incremented by every
    /// <see cref="StartReceiver"/> call, successful or not. A caller that reads it straight after its
    /// own <see cref="StartReceiver"/> can tell, later, whether the receiver it asked for is still
    /// the one this (shared, single-receiver) bridge is driving — the bridge is a singleton and more
    /// than one ViewModel can be alive and subscribed at the same time. 0 before the first call.</summary>
    long ReceiverGeneration { get; }

    NdiVideoFrame? GetLatestFrame();
    float GetDroppedFramePercent();
    (int Width, int Height) GetActualResolution();
    float GetMeasuredFps();
    QualityProfile ActiveQualityProfile { get; }

    /// <summary>
    /// Raised when the receiver's connection state changes. <b>Any thread:</b> a pump thread, the
    /// implementation's lifecycle worker, or the caller's own thread during
    /// <see cref="StartReceiver"/> / <see cref="StopReceiverAsync"/> — those two apply their
    /// synchronous postconditions in the caller's turn and raise from there. Subscribers must
    /// marshal every observable mutation to the UI thread (<c>IMainThreadDispatcher</c> in Core),
    /// must not block, and must not assume they are off the UI thread.
    /// <para>
    /// A handler must also not synchronously read other members of this interface and expect it to
    /// be cheap: the implementation may raise this while holding the lock it holds across the
    /// native receiver create/connect, so <see cref="ActiveQualityProfile"/>,
    /// <see cref="SetTally"/> and the PTZ members can block for that duration. Post, do not read.
    /// </para>
    /// </summary>
    event EventHandler<ConnectionState>? ConnectionStateChanged;

    /// <summary>Raised (on a pump thread) when the source echoes a tally state change. Subscribers
    /// marshal to the UI thread.</summary>
    event EventHandler<NdiTallyEcho>? TallyEchoChanged;

    /// <summary>
    /// Raised on the video pump thread, after the native frame has been freed, once a newly
    /// captured frame is visible to <see cref="GetLatestFrame"/>. Exists so the viewer can draw on
    /// arrival instead of polling a 33 ms timer (#416).
    /// <para>
    /// Fires at the source frame rate — up to 60/s — on the thread a stop joins. A handler must
    /// therefore do nothing but post a single coalesced invalidate: no allocation, no lock, no UI
    /// work, no blocking call, and never a call back into this bridge. It carries no payload on
    /// purpose: the frame is re-read on the UI thread, so a subscriber that falls behind sees the
    /// newest frame rather than a queue of stale ones.
    /// </para>
    /// </summary>
    event EventHandler? VideoFrameReady;

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

    /// <summary>True while either a capture-output sender or a re-stream sender holds a live
    /// native handle — the authoritative "is output really active" signal, independent of any
    /// persisted app-state claim.</summary>
    bool IsActive { get; }

    /// <summary>True while any connected receiver reports this sender on program tally.</summary>
    bool IsOnProgramTally { get; }

    /// <summary>Number of receivers currently connected to this sender (0 when idle).</summary>
    int ConnectionCount { get; }

    /// <summary>
    /// Raised (on a background thread) when <see cref="IsActive"/>, <see cref="IsOnProgramTally"/>,
    /// or <see cref="ConnectionCount"/> changed. Subscribers marshal to the UI thread.
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
