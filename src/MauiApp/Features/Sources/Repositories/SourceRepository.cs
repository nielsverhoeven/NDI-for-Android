using NdiForAndroid.Data;
using NdiForAndroid.Features.Settings.Repositories;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.NdiBridge;

namespace NdiForAndroid.Features.Sources.Repositories;

public sealed class SourceRepository : ISourceRepository
{
    private readonly INdiDiscoveryBridge _bridge;
    private readonly NdiDatabase _db;
    private readonly ISettingsRepository _settingsRepository;

    public SourceRepository(INdiDiscoveryBridge bridge, NdiDatabase db, ISettingsRepository settingsRepository)
    {
        _bridge = bridge;
        _db = db;
        _settingsRepository = settingsRepository;
    }

    public async Task<DiscoverySnapshot> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var snapshotId = Guid.NewGuid().ToString();
        try
        {
            _ = await _settingsRepository.GetSettingsAsync();

            var entries = await _bridge.DiscoverSourcesAsync(cancellationToken);
            var sources = entries.Select(e => new NdiSource(
                e.SourceId, e.DisplayName, e.EndpointAddress, e.IsAvailable, e.LastSeenAtEpochMillis)).ToList();

            foreach (var source in sources)
                await _db.UpsertSourceAsync(source);

            return new DiscoverySnapshot(snapshotId, DiscoveryStatus.Success, sources,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }
        catch (OperationCanceledException)
        {
            return new DiscoverySnapshot(snapshotId, DiscoveryStatus.Failure, Array.Empty<NdiSource>(),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), "Discovery cancelled");
        }
        catch (Exception ex)
        {
            return new DiscoverySnapshot(snapshotId, DiscoveryStatus.Failure, Array.Empty<NdiSource>(),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), ex.Message);
        }
    }

    public Task<IReadOnlyList<NdiSource>> GetCachedSourcesAsync() => _db.GetSourcesAsync();

    public Task SaveSourceAsync(NdiSource source) => _db.UpsertSourceAsync(source);

    public Task RemoveSourceAsync(string sourceId) => _db.DeleteSourceAsync(sourceId);
}
