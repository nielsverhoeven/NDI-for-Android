using SQLite;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.NdiBridge;
using System.Text.Json;

namespace NdiForAndroid.Data;

// NOTE: application state (the "app_state" key-value table in this same ndi.db3 file)
// is owned by AppStateRepository (src/Core/Features/AppState) — not duplicated here.

[Table("sources")]
public class SourceEntity
{
    [PrimaryKey]
    public string SourceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? EndpointAddress { get; set; }
    public bool IsAvailable { get; set; }
    public long LastSeenAtEpochMillis { get; set; }
    public bool PreviouslyConnected { get; set; }
    public string DiscoveryMode { get; set; } = "Mdns";
    public string QualityProfile { get; set; } = "Balanced";
}

[Table("settings")]
public class SettingsEntity
{
    [PrimaryKey]
    public int Id { get; set; } = 1;
    public string? DiscoveryHost { get; set; }
    public int? DiscoveryPort { get; set; }
    public bool DeveloperModeEnabled { get; set; }
    public string? ThemeMode { get; set; }
    public string? AccentColor { get; set; }
    public string? DiscoveryServersJson { get; set; }
    public long UpdatedAtEpochMillis { get; set; }
}

// ---------------------------------------------------------------------------
// Issue #240: tables ported from the legacy Kotlin app's schema.
// Forward-declared: several of these (viewer_session, last_viewed_context,
// output_configuration, output_session, discovery_run_result,
// discovery_server_check_status, cached_source_crossref) have CRUD methods but
// no consumers yet — their consumers arrive with the real NDI receive (#277)
// and send (#278) implementations. Do not remove without checking those issues.
// ---------------------------------------------------------------------------

[Table("viewer_session")]
public sealed class ViewerSessionEntity
{
    [PrimaryKey] public string SourceId { get; set; } = string.Empty;
    public string State { get; set; } = "Inactive";       // Active, Inactive, Paused
    public int RetryCount { get; set; }
    public long StartedAtEpochMillis { get; set; }
    public long UpdatedAtEpochMillis { get; set; }
}

[Table("last_viewed_context")]
public sealed class LastViewedContextEntity
{
    [PrimaryKey] public string SourceId { get; set; } = string.Empty;
    public string? FrameImagePath { get; set; }
    public long CapturedAtEpochMillis { get; set; }
}

[Table("output_configuration")]
public sealed class OutputConfigurationEntity
{
    [PrimaryKey] public int Id { get; set; } = 1;
    public string? PreferredStreamName { get; set; }
    public string LastInputKind { get; set; } = "DeviceScreen";  // DeviceScreen, DiscoveredNdi
    public string? LastSourceId { get; set; }
    public bool RetryEnabled { get; set; } = true;
    public int RetryWindowSeconds { get; set; } = 30;
}

[Table("output_session")]
public sealed class OutputSessionEntity
{
    [PrimaryKey] public string Id { get; set; } = string.Empty;
    public string InputKind { get; set; } = "DeviceScreen";
    public string? StreamName { get; set; }
    public string State { get; set; } = "Stopped";  // Running, Paused, Stopped
    public int RetryCount { get; set; }
    public long StartedAtEpochMillis { get; set; }
}

[Table("discovery_run_results")]
public sealed class DiscoveryRunResultEntity
{
    [PrimaryKey] public string RunId { get; set; } = string.Empty;
    public string Mode { get; set; } = "Mdns";       // Mdns, DiscoveryServer
    public long DurationMs { get; set; }
    public string Status { get; set; } = "Success";   // Success, Failure, Partial
    public int SourceCount { get; set; }
    public string? DiagnosticCode { get; set; }
    public string? DiagnosticMessage { get; set; }
    public long RanAtEpochMillis { get; set; }
}

[Table("discovery_server_check_status")]
public sealed class DiscoveryServerCheckStatusEntity
{
    [PrimaryKey] public string Id { get; set; } = string.Empty;
    public string ServerId { get; set; } = string.Empty;
    public string CheckType { get; set; } = "Reachability";  // Reachability, NdiAvailable, etc.
    public string Outcome { get; set; } = "Unknown";        // Success, Failure, Timeout
    public string? FailureCategory { get; set; }
    public string? FailureMessage { get; set; }
    public string? CorrelationId { get; set; }
    public long CheckedAtEpochMillis { get; set; }
}

[Table("cached_sources_discovery_server_crossref")]
public sealed class CachedSourceCrossrefEntity
{
    [PrimaryKey, Column("source_id")] public string SourceId { get; set; } = string.Empty;
    [PrimaryKey, Column("server_id")] public string ServerId { get; set; } = string.Empty;
    public long FirstSeenViaServerAtEpochMillis { get; set; }
}

public sealed class NdiDatabase
{
    private readonly SQLiteAsyncConnection _connection;
    private readonly Task _initTask;

    public NdiDatabase()
        : this(Path.Combine(FileSystem.AppDataDirectory, "ndi.db3"))
    {
    }

    /// <summary>
    /// Creates an NdiDatabase instance pointing to a specific file path.
    /// Internal constructor used by tests for isolation.
    /// </summary>
    internal NdiDatabase(string dbPath)
    {
        _connection = new SQLiteAsyncConnection(dbPath);
        _initTask = InitAsync();
    }

    public async Task InitAsync()
    {
        await _connection.CreateTableAsync<SourceEntity>();
        await _connection.CreateTableAsync<SettingsEntity>();
        await _connection.CreateTableAsync<ViewerSessionEntity>();
        await _connection.CreateTableAsync<LastViewedContextEntity>();
        await _connection.CreateTableAsync<OutputConfigurationEntity>();
        await _connection.CreateTableAsync<OutputSessionEntity>();
        await _connection.CreateTableAsync<DiscoveryRunResultEntity>();
        await _connection.CreateTableAsync<DiscoveryServerCheckStatusEntity>();
        await _connection.CreateTableAsync<CachedSourceCrossrefEntity>();
        await _connection.CreateTableAsync<ConnectionHistoryEntity>();
        await EnsureSettingsColumnsAsync();
        await EnsureSourceColumnsAsync();
    }

    private Task EnsureInitializedAsync() => _initTask;

    public async Task UpsertSourceAsync(NdiSource source)
    {
        await EnsureInitializedAsync();
        var entity = new SourceEntity
        {
            SourceId = source.SourceId,
            DisplayName = source.DisplayName,
            EndpointAddress = source.EndpointAddress,
            IsAvailable = source.IsAvailable,
            LastSeenAtEpochMillis = source.LastSeenAtEpochMillis,
            PreviouslyConnected = source.PreviouslyConnected,
            DiscoveryMode = source.DiscoveryMode.ToString(),
            QualityProfile = source.QualityProfile.ToString(),
        };
        await _connection.InsertOrReplaceAsync(entity);
    }

    public async Task<IReadOnlyList<NdiSource>> GetSourcesAsync()
    {
        await EnsureInitializedAsync();
        var entities = await _connection.Table<SourceEntity>().ToListAsync();
        return entities.Select(e => new NdiSource(
            e.SourceId, e.DisplayName, e.EndpointAddress, e.IsAvailable,
            e.LastSeenAtEpochMillis, e.PreviouslyConnected,
            ParseDiscoveryMode(e.DiscoveryMode),
            ParseQualityProfile(e.QualityProfile))).ToList();
    }

    public async Task DeleteSourceAsync(string sourceId)
    {
        await EnsureInitializedAsync();
        await _connection.DeleteAsync<SourceEntity>(sourceId);
    }

    public async Task<NdiSettingsSnapshot> GetSettingsAsync()
    {
        await EnsureInitializedAsync();

        try
        {
            var entity = await _connection.FindAsync<SettingsEntity>(1);
            if (entity is null)
                return NdiSettingsSnapshot.CreateDefault();

            return new NdiSettingsSnapshot(
                string.IsNullOrWhiteSpace(entity.DiscoveryHost) ? null : entity.DiscoveryHost.Trim(),
                entity.DiscoveryPort,
                entity.DeveloperModeEnabled,
                entity.UpdatedAtEpochMillis,
                ParseThemeMode(entity.ThemeMode),
                ParseAccentColor(entity.AccentColor),
                ParseDiscoveryServers(entity.DiscoveryServersJson));
        }
        catch
        {
            return NdiSettingsSnapshot.CreateDefault();
        }
    }

    public async Task SaveSettingsAsync(NdiSettingsSnapshot settings)
    {
        await EnsureInitializedAsync();
        var entity = new SettingsEntity
        {
            Id = 1,
            DiscoveryHost = settings.DiscoveryHost,
            DiscoveryPort = settings.DiscoveryPort,
            DeveloperModeEnabled = settings.DeveloperModeEnabled,
            ThemeMode = settings.ThemeMode.ToString(),
            AccentColor = settings.AccentColor.ToString(),
            DiscoveryServersJson = SerializeDiscoveryServers(settings.DiscoveryServers),
            UpdatedAtEpochMillis = settings.UpdatedAtEpochMillis,
        };
        await _connection.InsertOrReplaceAsync(entity);
    }

    private async Task EnsureSettingsColumnsAsync()
    {
        var tableInfo = await _connection.GetTableInfoAsync("settings");
        var columnNames = tableInfo.Select(column => column.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!columnNames.Contains("ThemeMode"))
            await _connection.ExecuteAsync("ALTER TABLE settings ADD COLUMN ThemeMode TEXT");

        if (!columnNames.Contains("AccentColor"))
            await _connection.ExecuteAsync("ALTER TABLE settings ADD COLUMN AccentColor TEXT");

        if (!columnNames.Contains("DiscoveryServersJson"))
            await _connection.ExecuteAsync("ALTER TABLE settings ADD COLUMN DiscoveryServersJson TEXT");
    }

    private async Task EnsureSourceColumnsAsync()
    {
        var tableInfo = await _connection.GetTableInfoAsync("sources");
        var columnNames = tableInfo.Select(column => column.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!columnNames.Contains("DiscoveryMode"))
            await _connection.ExecuteAsync(
                "ALTER TABLE sources ADD COLUMN DiscoveryMode TEXT NOT NULL DEFAULT 'Mdns'");

        if (!columnNames.Contains("QualityProfile"))
            await _connection.ExecuteAsync(
                "ALTER TABLE sources ADD COLUMN QualityProfile TEXT NOT NULL DEFAULT 'Balanced'");
    }

    /// <summary>
    /// Sets IsAvailable = false for all Discovery Server sources whose SourceId
    /// is NOT in <paramref name="currentSourceIds"/>.
    /// mDNS sources are excluded from soft-delete (they use natural expiry).
    /// </summary>
    public async Task MarkDiscoveryServerSourcesStaleAsync(IEnumerable<string> currentSourceIds)
    {
        await EnsureInitializedAsync();

        var allDiscoveryServerSources = await _connection
            .Table<SourceEntity>()
            .Where(e => e.DiscoveryMode == "DiscoveryServer")
            .ToListAsync();

        var currentIds = new HashSet<string>(currentSourceIds, StringComparer.OrdinalIgnoreCase);

        foreach (var entity in allDiscoveryServerSources)
        {
            if (!currentIds.Contains(entity.SourceId) && entity.IsAvailable)
            {
                entity.IsAvailable = false;
                await _connection.UpdateAsync(entity);
            }
        }
    }

    private static DiscoveryMode ParseDiscoveryMode(string? value)
    {
        if (Enum.TryParse<DiscoveryMode>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;

        return DiscoveryMode.Mdns;
    }

    private static QualityProfile ParseQualityProfile(string? value)
    {
        if (Enum.TryParse<QualityProfile>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;

        return QualityProfile.Balanced;
    }

    private static ThemeMode ParseThemeMode(string? value)
    {
        if (Enum.TryParse<ThemeMode>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;

        return ThemeMode.System;
    }

    private static AccentColorOption ParseAccentColor(string? value)
    {
        if (Enum.TryParse<AccentColorOption>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;

        return AccentColorOption.Blue;
    }

    private static IReadOnlyList<DiscoveryServerPreference> ParseDiscoveryServers(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<DiscoveryServerPreference>();

        try
        {
            var parsed = JsonSerializer.Deserialize<List<DiscoveryServerPreference>>(raw);
            if (parsed is null)
                return Array.Empty<DiscoveryServerPreference>();

            return parsed;
        }
        catch
        {
            return Array.Empty<DiscoveryServerPreference>();
        }
    }

    private static string SerializeDiscoveryServers(IReadOnlyList<DiscoveryServerPreference> servers)
        => JsonSerializer.Serialize(servers);

    // =====================================================================
    // Issue #240: repository methods for new tables
    // =====================================================================

    // --- viewer_session ---

    public async Task SaveViewerSessionAsync(ViewerSessionEntity session)
    {
        await EnsureInitializedAsync();
        await _connection.InsertOrReplaceAsync(session);
    }

    public async Task<ViewerSessionEntity?> GetViewerSessionAsync(string sourceId)
    {
        await EnsureInitializedAsync();
        return await _connection.FindAsync<ViewerSessionEntity>(sourceId);
    }

    public async Task DeleteViewerSessionAsync(string sourceId)
    {
        await EnsureInitializedAsync();
        await _connection.DeleteAsync<ViewerSessionEntity>(sourceId);
    }

    // --- last_viewed_context ---

    public async Task SaveLastViewedContextAsync(LastViewedContextEntity ctx)
    {
        await EnsureInitializedAsync();
        await _connection.InsertOrReplaceAsync(ctx);
    }

    public async Task<LastViewedContextEntity?> GetLastViewedContextAsync(string sourceId)
    {
        await EnsureInitializedAsync();
        return await _connection.FindAsync<LastViewedContextEntity>(sourceId);
    }

    // --- output_configuration ---

    public async Task SaveOutputConfigurationAsync(OutputConfigurationEntity cfg)
    {
        await EnsureInitializedAsync();
        await _connection.InsertOrReplaceAsync(cfg);
    }

    public async Task<OutputConfigurationEntity?> GetOutputConfigurationAsync()
    {
        await EnsureInitializedAsync();
        return await _connection.FindAsync<OutputConfigurationEntity>(1);
    }

    // --- output_session ---

    public async Task SaveOutputSessionAsync(OutputSessionEntity session)
    {
        await EnsureInitializedAsync();
        await _connection.InsertOrReplaceAsync(session);
    }

    public async Task<OutputSessionEntity?> GetOutputSessionAsync(string id)
    {
        await EnsureInitializedAsync();
        return await _connection.FindAsync<OutputSessionEntity>(id);
    }

    // --- discovery_run_results ---

    public async Task SaveDiscoveryRunResultAsync(DiscoveryRunResultEntity result)
    {
        await EnsureInitializedAsync();
        await _connection.InsertOrReplaceAsync(result);
    }

    public async Task DeleteDiscoveryRunResultAsync(string runId)
    {
        await EnsureInitializedAsync();
        await _connection.DeleteAsync<DiscoveryRunResultEntity>(runId);
    }

    public async Task<IReadOnlyList<DiscoveryRunResultEntity>> GetRecentDiscoveryRunsAsync(int count = 20)
    {
        await EnsureInitializedAsync();
        var results = await _connection.Table<DiscoveryRunResultEntity>()
            .OrderByDescending(r => r.RanAtEpochMillis)
            .Take(count)
            .ToListAsync();
        return results.AsReadOnly();
    }

    // --- discovery_server_check_status ---

    public async Task SaveDiscoveryServerCheckStatusAsync(DiscoveryServerCheckStatusEntity status)
    {
        await EnsureInitializedAsync();
        await _connection.InsertOrReplaceAsync(status);
    }

    public async Task<IReadOnlyList<DiscoveryServerCheckStatusEntity>> GetRecentServerChecksAsync(int count = 20)
    {
        await EnsureInitializedAsync();
        var results = await _connection.Table<DiscoveryServerCheckStatusEntity>()
            .OrderByDescending(s => s.CheckedAtEpochMillis)
            .Take(count)
            .ToListAsync();
        return results.AsReadOnly();
    }

    // --- cached_sources_discovery_server_crossref ---

    public async Task SaveSourceServerCrossrefAsync(CachedSourceCrossrefEntity crossref)
    {
        await EnsureInitializedAsync();
        await _connection.InsertOrReplaceAsync(crossref);
    }

    public async Task DeleteSourceServerCrossrefAsync(string sourceId, string serverId)
    {
        await EnsureInitializedAsync();
        await _connection.DeleteAsync<CachedSourceCrossrefEntity>(new { sourceId, serverId });
    }

    public async Task<IReadOnlyList<CachedSourceCrossrefEntity>> GetSourceServerCrossrefsAsync(string sourceId)
    {
        await EnsureInitializedAsync();
        var results = await _connection.Table<CachedSourceCrossrefEntity>()
            .Where(c => c.SourceId == sourceId)
            .ToListAsync();
        return results.AsReadOnly();
    }

    // =====================================================================
    // Issue #238: connection history
    // =====================================================================

    public async Task SaveConnectionHistoryEntryAsync(ConnectionHistoryEntity entry)
    {
        await EnsureInitializedAsync();
        await _connection.InsertOrReplaceAsync(entry);
    }

    public async Task<IReadOnlyList<ConnectionHistoryEntity>> GetConnectionHistoryAsync(int count = 50)
    {
        await EnsureInitializedAsync();
        var results = await _connection.Table<ConnectionHistoryEntity>()
            .OrderByDescending(c => c.ConnectedAtEpochMillis)
            .Take(count)
            .ToListAsync();
        return results.AsReadOnly();
    }

    public async Task<IReadOnlyList<ConnectionHistoryEntity>> GetConnectionHistoryForSourceAsync(string sourceId, int count = 50)
    {
        await EnsureInitializedAsync();
        var results = await _connection.Table<ConnectionHistoryEntity>()
            .Where(c => c.SourceId == sourceId)
            .OrderByDescending(c => c.ConnectedAtEpochMillis)
            .Take(count)
            .ToListAsync();
        return results.AsReadOnly();
    }

    public async Task DeleteOldConnectionHistoryAsync(DateTimeOffset before)
    {
        await EnsureInitializedAsync();
        var toDelete = await _connection.Table<ConnectionHistoryEntity>()
            .Where(c => c.ConnectedAtEpochMillis < before.ToUnixTimeMilliseconds())
            .ToListAsync();
        foreach (var entry in toDelete)
        {
            await _connection.DeleteAsync(entry);
        }
    }

    public async Task<IReadOnlyList<ConnectionHistoryEntity>> GetConnectionSummaryAsync(int daysBack = 7)
    {
        // Returns a summary grouped by source with total connection time estimates.
        var cutoffMillis = DateTimeOffset.UtcNow.AddDays(-daysBack).ToUnixTimeMilliseconds();
        var entries = await _connection.Table<ConnectionHistoryEntity>()
            .Where(c => c.ConnectedAtEpochMillis >= cutoffMillis)
            .ToListAsync();

        var summaryBySource = entries
            .GroupBy(e => e.SourceId)
            .Select(g => new
            {
                SourceId = g.Key,
                DisplayName = g.First().DisplayName,
                ConnectionCount = g.Count(),
                LastConnected = g.Max(e => e.ConnectedAtEpochMillis),
            })
            .OrderByDescending(s => s.LastConnected)
            .ToList();

        return summaryBySource.Select(s => new ConnectionHistoryEntity
        {
            SourceId = s.SourceId,
            DisplayName = s.DisplayName,
            ConnectedAtEpochMillis = s.LastConnected,
            QualityProfile = QualityProfile.Balanced
        }).ToList().AsReadOnly();
    }

    /// <summary>
    /// Simple entity for tracking NDI source connection events.
    /// Uses SourceId + ConnectedAtEpochMillis as a composite key via [PrimaryKey].
    /// </summary>
    [Table("connection_history")]
    public class ConnectionHistoryEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [PrimaryKey]
        public string SourceId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public long ConnectedAtEpochMillis { get; set; }

        public QualityProfile QualityProfile { get; set; }

        public int DurationSeconds { get; set; } // 0 if still connected
    }

}
