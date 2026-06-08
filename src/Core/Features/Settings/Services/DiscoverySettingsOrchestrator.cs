using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.NdiBridge;

namespace NdiForAndroid.Features.Settings.Services;

/// <summary>
/// Translates persisted <see cref="NdiSettingsSnapshot"/> into a concrete
/// <see cref="DiscoveryMode"/> call on <see cref="INdiDiscoveryBridge"/>.
/// When no servers are enabled, mDNS is activated.
/// When ≥1 server is enabled, Discovery Server mode is activated with all
/// enabled endpoints ordered by <see cref="DiscoveryServerPreference.Order"/>.
/// </summary>
public sealed class DiscoverySettingsOrchestrator : IDiscoverySettingsOrchestrator
{
    private readonly INdiDiscoveryBridge _bridge;
    private DiscoveryMode _activeMode = DiscoveryMode.Mdns;

    /// <inheritdoc />
    public DiscoveryMode ActiveMode => _activeMode;

    public DiscoverySettingsOrchestrator(INdiDiscoveryBridge bridge)
    {
        _bridge = bridge;
    }

    public Task ApplyAsync(NdiSettingsSnapshot settings, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var enabledServers = settings.DiscoveryServers
            .Where(server => server.Enabled)
            .OrderBy(server => server.Order)
            .ToList();

        if (enabledServers.Count == 0)
        {
            _activeMode = DiscoveryMode.Mdns;
            _bridge.SetDiscoveryMode(DiscoveryMode.Mdns);
        }
        else
        {
            var endpoints = enabledServers
                .Select(server => new DiscoveryServerEndpoint(server.Host, server.Port))
                .ToList();

            _activeMode = DiscoveryMode.DiscoveryServer;
            _bridge.SetDiscoveryMode(DiscoveryMode.DiscoveryServer, endpoints);
        }

        return Task.CompletedTask;
    }
}
