using Moq;
using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Services;
using NdiForAndroid.NdiBridge;
using Xunit;

namespace NdiForAndroid.Tests.Features.Settings;

/// <summary>
/// Tests for <see cref="DiscoverySettingsOrchestrator"/> (concrete Core implementation).
/// </summary>
public class DiscoverySettingsOrchestratorTests
{
    private readonly Mock<INdiDiscoveryBridge> _bridgeMock = new();

    private DiscoverySettingsOrchestrator CreateSut() =>
        new(_bridgeMock.Object);

    private static NdiSettingsSnapshot SnapshotWithNoServers() =>
        new(null, null, false, 0, ThemeMode.System, AccentColorOption.Blue,
            Array.Empty<DiscoveryServerPreference>());

    private static NdiSettingsSnapshot SnapshotWithServers(params DiscoveryServerPreference[] servers) =>
        new(null, null, false, 0, ThemeMode.System, AccentColorOption.Blue, servers);

    [Fact]
    public async Task ApplyAsync_WithNoServers_CallsSetDiscoveryModeMdns()
    {
        var sut = CreateSut();
        await sut.ApplyAsync(SnapshotWithNoServers());

        _bridgeMock.Verify(b => b.SetDiscoveryMode(DiscoveryMode.Mdns, null), Times.Once);
        Assert.Equal(DiscoveryMode.Mdns, sut.ActiveMode);
    }

    [Fact]
    public async Task ApplyAsync_WithAllDisabledServers_CallsSetDiscoveryModeMdns()
    {
        var sut = CreateSut();
        var settings = SnapshotWithServers(
            new DiscoveryServerPreference("192.168.1.100", 5961, Enabled: false, Order: 0));

        await sut.ApplyAsync(settings);

        _bridgeMock.Verify(b => b.SetDiscoveryMode(DiscoveryMode.Mdns, null), Times.Once);
        Assert.Equal(DiscoveryMode.Mdns, sut.ActiveMode);
    }

    [Fact]
    public async Task ApplyAsync_WithOneEnabledServer_CallsSetDiscoveryModeDiscoveryServer()
    {
        var sut = CreateSut();
        var settings = SnapshotWithServers(
            new DiscoveryServerPreference("192.168.1.100", 5961, Enabled: true, Order: 0));

        await sut.ApplyAsync(settings);

        _bridgeMock.Verify(b => b.SetDiscoveryMode(
            DiscoveryMode.DiscoveryServer,
            It.Is<IReadOnlyList<DiscoveryServerEndpoint>>(eps =>
                eps.Count == 1 && eps[0].Host == "192.168.1.100" && eps[0].Port == 5961)),
            Times.Once);
        Assert.Equal(DiscoveryMode.DiscoveryServer, sut.ActiveMode);
    }

    [Fact]
    public async Task ApplyAsync_WithMultipleEnabledServers_PassesAllEndpointsOrderedByOrder()
    {
        var sut = CreateSut();
        var settings = SnapshotWithServers(
            new DiscoveryServerPreference("server-b.local", 5961, Enabled: true, Order: 1),
            new DiscoveryServerPreference("server-a.local", 5960, Enabled: true, Order: 0));

        await sut.ApplyAsync(settings);

        _bridgeMock.Verify(b => b.SetDiscoveryMode(
            DiscoveryMode.DiscoveryServer,
            It.Is<IReadOnlyList<DiscoveryServerEndpoint>>(eps =>
                eps.Count == 2 &&
                eps[0].Host == "server-a.local" && // order 0 first
                eps[1].Host == "server-b.local")), // order 1 second
            Times.Once);
    }

    [Fact]
    public async Task HotSwitch_FromMdnsToDiscoveryServer_UpdatesActiveModeImmediately()
    {
        var sut = CreateSut();

        // Start in mDNS mode
        await sut.ApplyAsync(SnapshotWithNoServers());
        Assert.Equal(DiscoveryMode.Mdns, sut.ActiveMode);

        // Hot-switch to Discovery Server
        var serverSettings = SnapshotWithServers(
            new DiscoveryServerPreference("192.168.1.200", 5961, Enabled: true, Order: 0));
        await sut.ApplyAsync(serverSettings);

        Assert.Equal(DiscoveryMode.DiscoveryServer, sut.ActiveMode);
        _bridgeMock.Verify(b => b.SetDiscoveryMode(DiscoveryMode.DiscoveryServer,
            It.IsAny<IReadOnlyList<DiscoveryServerEndpoint>>()), Times.Once);
    }

    [Fact]
    public async Task HotSwitch_FromDiscoveryServerToMdns_UpdatesActiveModeImmediately()
    {
        var sut = CreateSut();

        // Start in Discovery Server mode
        var serverSettings = SnapshotWithServers(
            new DiscoveryServerPreference("192.168.1.200", 5961, Enabled: true, Order: 0));
        await sut.ApplyAsync(serverSettings);
        Assert.Equal(DiscoveryMode.DiscoveryServer, sut.ActiveMode);

        // Hot-switch back to mDNS
        await sut.ApplyAsync(SnapshotWithNoServers());

        Assert.Equal(DiscoveryMode.Mdns, sut.ActiveMode);
    }

    // ── AC-2: Additional coverage ─────────────────────────────────────────────

    [Fact]
    public async Task ApplyAsync_WithMixedEnabledDisabledServers_OnlyPassesEnabledEndpointsToBridge()
    {
        // AC-2: only enabled servers should become endpoints; disabled ones must be filtered out.
        var sut = CreateSut();
        var settings = SnapshotWithServers(
            new DiscoveryServerPreference("active.local",   5961, Enabled: true,  Order: 0),
            new DiscoveryServerPreference("inactive.local", 5961, Enabled: false, Order: 1),
            new DiscoveryServerPreference("also-active.local", 5960, Enabled: true, Order: 2));

        await sut.ApplyAsync(settings);

        _bridgeMock.Verify(b => b.SetDiscoveryMode(
            DiscoveryMode.DiscoveryServer,
            It.Is<IReadOnlyList<DiscoveryServerEndpoint>>(eps =>
                eps.Count == 2 &&
                eps.Any(e => e.Host == "active.local") &&
                eps.Any(e => e.Host == "also-active.local") &&
                eps.All(e => e.Host != "inactive.local"))),
            Times.Once);
        Assert.Equal(DiscoveryMode.DiscoveryServer, sut.ActiveMode);
    }

    [Fact]
    public async Task ApplyAsync_EachCall_MakesExactlyOneBridgeCall()
    {
        // AC-2: Each ApplyAsync triggers exactly one atomic SetDiscoveryMode call —
        // no double-stop, no partial-start race condition.
        var sut = CreateSut();

        await sut.ApplyAsync(SnapshotWithNoServers()); // mDNS call
        var serverSettings = SnapshotWithServers(
            new DiscoveryServerPreference("srv.local", 5961, Enabled: true, Order: 0));
        await sut.ApplyAsync(serverSettings);          // DS call

        // Exactly 2 total calls (one per ApplyAsync).
        _bridgeMock.Verify(
            b => b.SetDiscoveryMode(It.IsAny<DiscoveryMode>(), It.IsAny<IReadOnlyList<DiscoveryServerEndpoint>>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ApplyAsync_MultipleHotSwitches_ActiveModeTracksEachTransition()
    {
        // AC-4 (orchestrator layer): ActiveMode property must update with every ApplyAsync.
        var sut = CreateSut();
        var dsSettings = SnapshotWithServers(
            new DiscoveryServerPreference("192.168.1.50", 5961, Enabled: true, Order: 0));

        await sut.ApplyAsync(SnapshotWithNoServers());
        Assert.Equal(DiscoveryMode.Mdns, sut.ActiveMode);

        await sut.ApplyAsync(dsSettings);
        Assert.Equal(DiscoveryMode.DiscoveryServer, sut.ActiveMode);

        await sut.ApplyAsync(SnapshotWithNoServers());
        Assert.Equal(DiscoveryMode.Mdns, sut.ActiveMode);

        await sut.ApplyAsync(dsSettings);
        Assert.Equal(DiscoveryMode.DiscoveryServer, sut.ActiveMode);
    }
}
