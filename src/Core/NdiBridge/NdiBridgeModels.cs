namespace NdiForAndroid.NdiBridge;

/// <summary>Connection state of an NDI receiver.</summary>
public enum ConnectionState
{
    Connecting,
    Connected,
    Disconnected,
}

/// <summary>Discovery mode used to find NDI sources.</summary>
public enum DiscoveryMode
{
    /// <summary>Use mDNS (zero-config multicast) to discover local NDI sources.</summary>
    Mdns,

    /// <summary>Use one or more NDI Discovery Servers (unicast TCP) as the source of truth.</summary>
    DiscoveryServer,
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
