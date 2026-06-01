using SQLite;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Settings.Models;

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
}

[Table("settings")]
public class SettingsEntity
{
    [PrimaryKey]
    public int Id { get; set; } = 1;
    public string? DiscoveryHost { get; set; }
    public int? DiscoveryPort { get; set; }
    public bool DeveloperModeEnabled { get; set; }
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
        };
        await _connection.InsertOrReplaceAsync(entity);
    }

    public async Task<IReadOnlyList<NdiSource>> GetSourcesAsync()
    {
        await EnsureInitializedAsync();
        var entities = await _connection.Table<SourceEntity>().ToListAsync();
        return entities.Select(e => new NdiSource(
            e.SourceId, e.DisplayName, e.EndpointAddress, e.IsAvailable,
            e.LastSeenAtEpochMillis, e.PreviouslyConnected)).ToList();
    }

    public async Task DeleteSourceAsync(string sourceId)
    {
        await EnsureInitializedAsync();
        await _connection.DeleteAsync<SourceEntity>(sourceId);
    }

    public async Task<NdiSettingsSnapshot> GetSettingsAsync()
    {
        await EnsureInitializedAsync();
        var entity = await _connection.FindAsync<SettingsEntity>(1);
        if (entity is null)
            return new NdiSettingsSnapshot(null, null, false, 0);
        return new NdiSettingsSnapshot(
            entity.DiscoveryHost, entity.DiscoveryPort, entity.DeveloperModeEnabled, entity.UpdatedAtEpochMillis);
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
            UpdatedAtEpochMillis = settings.UpdatedAtEpochMillis,
        };
        await _connection.InsertOrReplaceAsync(entity);
    }
}
