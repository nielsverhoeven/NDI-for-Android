namespace NdiForAndroid.NdiBridge;

/// <summary>Discovery mode used to find NDI sources.</summary>
public enum DiscoveryMode
{
    /// <summary>Use mDNS (zero-config multicast) to discover local NDI sources.</summary>
    Mdns,

    /// <summary>Use one or more NDI Discovery Servers (unicast TCP) as the source of truth.</summary>
    DiscoveryServer,
}

/// <summary>
/// Connection state of an NDI receiver, surfaced for reconnection logic.
/// Plain C# enum — no NDI SDK types cross the bridge boundary.
/// </summary>
public enum ConnectionState
{
    /// <summary>The receiver is attempting to establish a connection.</summary>
    Connecting,

    /// <summary>The receiver has an active connection and is receiving frames.</summary>
    Connected,

    /// <summary>The receiver has no active connection.</summary>
    Disconnected,

    /// <summary>The transport connection is up but no video frame has arrived for longer than the
    /// bridge's stall threshold (<c>NdiViewerBridge.StalledAfterMs</c>) — a sender on standby, one
    /// mid-reconfiguration, an audio-only stretch, or a stream starved by the network. Distinct
    /// from <see cref="Connecting"/> on purpose: a stall is not a failed connection attempt, and
    /// recreating the receiver cannot make a sender send video, so the viewer's reconnect logic
    /// must be able to tell the two apart. Declared last so <see cref="Connecting"/> keeps enum
    /// value 0 (mocks and defaults depend on it).</summary>
    Stalled,
}

/// <summary>
/// Why the receiver most recently transitioned to <see cref="ConnectionState.Disconnected"/>.
/// Meaningless while <see cref="INdiViewerBridge.GetConnectionState"/> reports anything else.
/// <see cref="Intentional"/> is deliberately the default value: an implementation that has not
/// classified the stop must never be able to trigger an automatic reconnect window.
/// </summary>
public enum ReceiverStopReason
{
    /// <summary>An explicit, intentional stop requested by application code — the user's Stop
    /// button, an internal restart (source switch, quality-profile bandwidth change, a reconnect
    /// attempt), or a navigation handoff. Never an unexpected drop.</summary>
    Intentional,

    /// <summary>The pump thread detected the live connection was lost on its own
    /// (<c>recv_get_no_connections</c> reached 0 after having been connected, or the pump thread
    /// faulted) — a genuine unexpected-drop candidate.</summary>
    ConnectionLost,
}

/// <summary>A single NDI Discovery Server endpoint (host + port).</summary>
public record DiscoveryServerEndpoint(string Host, int Port);

/// <summary>NDI source entry returned by discovery.</summary>
public record NdiSourceEntry(
    string SourceId,
    string DisplayName,
    string? EndpointAddress,
    bool IsAvailable,
    long LastSeenAtEpochMillis,
    DiscoveryMode DiscoveryMode = DiscoveryMode.Mdns);

/// <summary>A single decoded video frame from an NDI receiver.</summary>
/// <param name="CapturedAtEpochMillis">The sender's timestamp (NDI 100 ns units ÷ 10 000) when the
/// sender supplied one; otherwise the receiver's own wall clock at copy time — see
/// <paramref name="TimestampIsSynthesized"/>. Also the render loop's dedupe key.</param>
/// <param name="ReceivedAtTickMillis">Monotonic device time (<see cref="Environment.TickCount64"/>)
/// at the moment the pump finished copying this frame. Single-clock, so draw time minus this is a
/// latency the app can state with no assumption about the sender's clock (#416). 0 means "not
/// stamped" — only possible for a frame constructed outside the bridge.</param>
/// <param name="TimestampIsSynthesized">True when the sender supplied no timestamp and
/// <paramref name="CapturedAtEpochMillis"/> is therefore the receiver's own clock. A sender→draw
/// figure computed from a synthesized timestamp measures nothing and would read as a suspiciously
/// *good* number, so it must be reported as unavailable. Defaults to true: the unsafe answer is
/// never the default (same rule as ReceiverStopReason.Intentional = 0).</param>
public record NdiVideoFrame(
    int Width,
    int Height,
    int[] ArgbPixels,
    long CapturedAtEpochMillis,
    long ReceivedAtTickMillis = 0,
    bool TimestampIsSynthesized = true);

/// <summary>Result of a discovery server reachability check.</summary>
public record NdiDiscoveryCheckResult(
    bool Success,
    string FailureCategory,
    string? FailureMessage);

/// <summary>
/// Tally state echoed by the connected source (<c>ndi_tally_echo</c> metadata):
/// whether ANY receiver anywhere has the source on program/preview.
/// </summary>
public record NdiTallyEcho(bool OnProgram, bool OnPreview);

/// <summary>Live receiver statistics sampled by the frame pump.</summary>
public record NdiReceiverStats(
    float MeasuredFps,
    float DroppedFramePercent,
    int Width,
    int Height);
