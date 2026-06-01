namespace NdiForAndroid.Features.Sources.Models;

public enum DiscoveryStatus { InProgress, Success, Empty, Failure }

public record NdiSource(
    string SourceId,
    string DisplayName,
    string? EndpointAddress,
    bool IsAvailable,
    long LastSeenAtEpochMillis,
    bool PreviouslyConnected = false);

public record DiscoverySnapshot(
    string SnapshotId,
    DiscoveryStatus Status,
    IReadOnlyList<NdiSource> Sources,
    long CompletedAtEpochMillis,
    string? ErrorMessage = null);
