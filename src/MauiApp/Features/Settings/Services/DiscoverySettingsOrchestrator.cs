using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Services;
using NdiForAndroid.NdiBridge;

namespace NdiForAndroid.Features.Settings.Services;

public sealed class DiscoverySettingsOrchestrator : IDiscoverySettingsOrchestrator
{
    private readonly INdiDiscoveryBridge _bridge;

    public DiscoverySettingsOrchestrator(INdiDiscoveryBridge bridge)
    {
        _bridge = bridge;
    }

    public Task ApplyAsync(NdiSettingsSnapshot settings, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var endpoint = ResolveEndpoint(settings);
        _bridge.SetDiscoveryEndpoint(endpoint.Host, endpoint.Port);
        return Task.CompletedTask;
    }

    private static (string? Host, int? Port) ResolveEndpoint(NdiSettingsSnapshot settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.DiscoveryHost) && settings.DiscoveryPort.HasValue)
            return (settings.DiscoveryHost, settings.DiscoveryPort);

        var firstEnabledServer = settings.DiscoveryServers
            .Where(server => server.Enabled)
            .OrderBy(server => server.Order)
            .FirstOrDefault();

        if (firstEnabledServer is null)
            return (null, null);

        return (firstEnabledServer.Host, firstEnabledServer.Port);
    }
}