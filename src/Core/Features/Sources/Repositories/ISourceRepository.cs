using NdiForAndroid.Features.Sources.Models;

namespace NdiForAndroid.Features.Sources.Repositories;

public interface ISourceRepository
{
    Task<DiscoverySnapshot> DiscoverAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NdiSource>> GetCachedSourcesAsync();
    Task SaveSourceAsync(NdiSource source);
    Task RemoveSourceAsync(string sourceId);
}
