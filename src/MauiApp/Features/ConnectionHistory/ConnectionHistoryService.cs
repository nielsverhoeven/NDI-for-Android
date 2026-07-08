using NdiForAndroid.Features.ConnectionHistory.Services;
using NdiForAndroid.NdiBridge;
using SQLite;
using NdiForAndroid.Data;

namespace NdiForAndroid.Features.ConnectionHistory;

/// <summary>
/// Connection history service backed by NdiDatabase.
/// Records viewer connection/disconnection events and provides history + summary queries.
/// </summary>
public sealed class ConnectionHistoryService : IConnectionHistoryService
{
    private readonly SQLiteAsyncConnection _db;
    private readonly TimeProvider _time;

    /// <summary>
    /// Tracks the most recent active (not yet disconnected) connection for cleanup.
    /// </summary>
    private NdiDatabase.ConnectionHistoryEntity? _activeEntry;

    public ConnectionHistoryService(SQLiteAsyncConnection db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task RecordConnectedAsync(string sourceId, string displayName, QualityProfile qualityProfile)
    {
        var entry = new NdiDatabase.ConnectionHistoryEntity
        {
            SourceId = sourceId,
            DisplayName = displayName,
            ConnectedAtEpochMillis = _time.GetUtcNow().ToUnixTimeMilliseconds(),
            QualityProfile = qualityProfile,
            DurationSeconds = 0,
        };

        // Insert (not InsertOrReplace): each connect is a distinct history row.
        // AutoIncrement Id is 0 here; InsertAsync omits it so SQLite assigns a fresh
        // rowid and writes it back onto entry.Id for the later UpdateAsync in
        // RecordDisconnectedAsync. InsertOrReplace would instead pin every row to Id=0.
        await _db.InsertAsync(entry);
        _activeEntry = entry;
    }

    public async Task RecordDisconnectedAsync()
    {
        if (_activeEntry is null) return;

        var nowMillis = _time.GetUtcNow().ToUnixTimeMilliseconds();
        _activeEntry.DurationSeconds = (int)Math.Max(0, (nowMillis - _activeEntry.ConnectedAtEpochMillis) / 1000);

        await _db.UpdateAsync(_activeEntry);
        _activeEntry = null;
    }

    public async Task<IReadOnlyList<NdiConnectionRecord>> GetHistoryAsync(string? sourceId = null, int count = 50)
    {
        var entries = string.IsNullOrEmpty(sourceId)
            ? await _db.Table<NdiDatabase.ConnectionHistoryEntity>()
                .OrderByDescending(c => c.ConnectedAtEpochMillis)
                .Take(count)
                .ToListAsync()
            : await _db.Table<NdiDatabase.ConnectionHistoryEntity>()
                .Where(c => c.SourceId == sourceId)
                .OrderByDescending(c => c.ConnectedAtEpochMillis)
                .Take(count)
                .ToListAsync();

        return entries.Select(e => new NdiConnectionRecord(
            e.SourceId,
            e.DisplayName,
            e.ConnectedAtEpochMillis,
            e.QualityProfile,
            e.DurationSeconds)).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<ConnectionSourceSummary>> GetSummaryAsync(int daysBack = 7)
    {
        var cutoffMillis = DateTimeOffset.UtcNow.AddDays(-daysBack).ToUnixTimeMilliseconds();
        var entries = await _db.Table<NdiDatabase.ConnectionHistoryEntity>()
            .Where(c => c.ConnectedAtEpochMillis >= cutoffMillis)
            .ToListAsync();

        var summaryBySource = entries
            .GroupBy(e => e.SourceId)
            .Select(g => new ConnectionSourceSummary(
                g.Key,
                g.First().DisplayName,
                g.Count(),
                g.Max(e => e.ConnectedAtEpochMillis)))
            .OrderByDescending(s => s.LastConnectedAtEpochMillis)
            .ToList();

        return summaryBySource.AsReadOnly();
    }
}
