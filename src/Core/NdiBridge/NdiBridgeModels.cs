namespace NdiForAndroid.NdiBridge;

/// <summary>NDI source entry returned by discovery.</summary>
public record NdiSourceEntry(
    string SourceId,
    string DisplayName,
    string? EndpointAddress,
    bool IsAvailable,
    long LastSeenAtEpochMillis);

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
