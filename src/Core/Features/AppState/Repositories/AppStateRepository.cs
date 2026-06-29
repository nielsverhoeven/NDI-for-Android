using SQLite;
using NdiForAndroid.Features.AppState.Models;

namespace NdiForAndroid.Features.AppState.Repositories;

/// <summary>
/// Key-value persistence for app state that survives backgrounding and process death.
/// Accepts a constructor parameter for the database file path so it doesn't depend on any MAUI-specific APIs.
/// </summary>
public sealed class AppStateRepository : IDisposable, IAppStateRepository
{
    private const string TableName = "app_state";

    private readonly SQLiteAsyncConnection _connection;
    private readonly Task _initTask;

    [Table(TableName)]
    private sealed class KeyValueEntity
    {
        [PrimaryKey] public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public AppStateRepository(string databasePath)
    {
        _connection = new SQLiteAsyncConnection(databasePath);
        _initTask = _connection.CreateTableAsync<KeyValueEntity>();
    }

    private Task EnsureInitialized() => _initTask;

    public void Dispose()
    {
        try { _connection.CloseAsync().GetAwaiter().GetResult(); } catch { }
    }

    public async Task SaveAsync(AppStateSnapshot snapshot)
    {
        await EnsureInitialized();
        await SetKey("lastViewerSourceId", snapshot.LastViewerSourceId ?? string.Empty);
        await SetKey("streamName", snapshot.StreamName ?? string.Empty);
        await SetKey("isOutputActive", snapshot.IsOutputActive ? "true" : "false");
        await SetKey("lastSelectedSourceId", snapshot.LastSelectedSourceId ?? string.Empty);
    }

    public async Task<AppStateSnapshot> RestoreStateAsync()
    {
        await EnsureInitialized();
        var lastViewer     = await GetKey("lastViewerSourceId");
        var streamName     = await GetKey("streamName");
        var isOutputActive = await GetKey("isOutputActive");
        var lastSelected   = await GetKey("lastSelectedSourceId");

        return new AppStateSnapshot(
            string.IsNullOrEmpty(lastViewer) ? null : lastViewer,
            string.IsNullOrEmpty(streamName) ? null : streamName,
            isOutputActive == "true",
            string.IsNullOrEmpty(lastSelected) ? null : lastSelected);
    }

    private async Task SetKey(string key, string value)
    {
        await _connection.InsertOrReplaceAsync(new KeyValueEntity { Key = key, Value = value });
    }

    private async Task<string?> GetKey(string key)
    {
        try
        {
            var entity = await _connection.FindAsync<KeyValueEntity>(key);
            return entity?.Value;
        }
        catch
        {
            return null;
        }
    }
}
