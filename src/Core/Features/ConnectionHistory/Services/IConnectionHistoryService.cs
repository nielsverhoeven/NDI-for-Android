using NdiForAndroid.NdiBridge;

namespace NdiForAndroid.Features.ConnectionHistory.Services;

/// <summary>
/// Records and retrieves NDI source connection events for history tracking.
/// </summary>
public interface IConnectionHistoryService
{
    /// <summary>
    /// Records a connection event for an NDI source.
    /// </summary>
    Task RecordConnectedAsync(string sourceId, string displayName, QualityProfile qualityProfile);

    /// <summary>
    /// Records that a viewer session ended (disconnect/disconnect time).
    /// Updates the duration on the most recent unrecorded entry.
    /// </summary>
    Task RecordDisconnectedAsync();

    /// <summary>
    /// Retrieves the connection history, optionally filtered by source ID.
    /// </summary>
    Task<IReadOnlyList<NdiConnectionRecord>> GetHistoryAsync(string? sourceId = null, int count = 50);

    /// <summary>
    /// Returns a summary of connections grouped by source over the last N days.
    /// </summary>
    Task<IReadOnlyList<ConnectionSourceSummary>> GetSummaryAsync(int daysBack = 7);
}

/// <summary>Recorded connection event returned to callers.</summary>
public record NdiConnectionRecord(
    string SourceId,
    string DisplayName,
    long ConnectedAtEpochMillis,
    QualityProfile QualityProfile,
    int DurationSeconds);

/// <summary>Summary of connections for a source over a time window.</summary>
public record ConnectionSourceSummary(
    string SourceId,
    string DisplayName,
    int TotalConnections,
    long LastConnectedAtEpochMillis);
