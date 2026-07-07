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
public record NdiVideoFrame(
    int Width,
    int Height,
    int[] ArgbPixels,
    long CapturedAtEpochMillis);

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
