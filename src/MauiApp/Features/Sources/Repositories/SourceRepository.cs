using NdiForAndroid.Data;
using NdiForAndroid.Features.Settings.Services;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.NdiBridge;

namespace NdiForAndroid.Features.Sources.Repositories;

public sealed class SourceRepository : ISourceRepository
{
    private readonly INdiDiscoveryBridge _bridge;
    private readonly NdiDatabase _db;
    private readonly IDiscoverySettingsOrchestrator _orchestrator;

    public SourceRepository(INdiDiscoveryBridge bridge, NdiDatabase db, IDiscoverySettingsOrchestrator orchestrator)
    {
        _bridge = bridge;
        _db = db;
        _orchestrator = orchestrator;
    }

    public async Task<DiscoverySnapshot> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var snapshotId = Guid.NewGuid().ToString();
        try
        {
            var activeMode = _orchestrator.ActiveMode;
            var entries = await _bridge.DiscoverSourcesAsync(cancellationToken);
            var sources = entries.Select(e => new NdiSource(
                e.SourceId, e.DisplayName, e.EndpointAddress, e.IsAvailable,
                e.LastSeenAtEpochMillis, PreviouslyConnected: false,
                DiscoveryMode: e.DiscoveryMode)).ToList();

            foreach (var source in sources)
                await _db.UpsertSourceAsync(source);

            // After a Discovery Server poll, soft-delete sources no longer visible.
            if (activeMode == DiscoveryMode.DiscoveryServer)
            {
                var currentSourceIds = sources.Select(s => s.SourceId);
                await _db.MarkDiscoveryServerSourcesStaleAsync(currentSourceIds);
            }

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

    public Task<DiscoveryMode> GetActiveDiscoveryModeAsync() =>
        Task.FromResult(_orchestrator.ActiveMode);
}
