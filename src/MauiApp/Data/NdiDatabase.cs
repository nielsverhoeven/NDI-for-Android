using SQLite;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.NdiBridge;
using System.Text.Json;

namespace NdiForAndroid.Data;

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

public sealed class NdiDatabase
{
    private readonly SQLiteAsyncConnection _connection;
    private readonly Task _initTask;

    public NdiDatabase()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "ndi.db3");
        _connection = new SQLiteAsyncConnection(dbPath);
        _initTask = InitAsync();
    }

    public async Task InitAsync()
    {
        await _connection.CreateTableAsync<SourceEntity>();
        await _connection.CreateTableAsync<SettingsEntity>();
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
            ParseDiscoveryMode(e.DiscoveryMode))).ToList();
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
}
