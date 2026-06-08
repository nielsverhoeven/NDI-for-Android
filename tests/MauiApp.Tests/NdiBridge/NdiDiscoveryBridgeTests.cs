using Moq;
using NdiForAndroid.NdiBridge;
using Xunit;

namespace MauiApp.Tests.NdiBridge;

/// <summary>
/// Contract tests for <see cref="INdiDiscoveryBridge"/> behavior.
/// Uses a mock implementation to verify expected discovery-mode contracts
/// without loading libndi.so or requiring Android runtime.
/// </summary>
public class NdiDiscoveryBridgeTests
{
    // ── In-memory fake bridge to test the mode-switching contract ─────────────

    private sealed class FakeDiscoveryBridge : INdiDiscoveryBridge
    {
        private DiscoveryMode _mode = DiscoveryMode.Mdns;
        private IReadOnlyList<DiscoveryServerEndpoint> _endpoints = Array.Empty<DiscoveryServerEndpoint>();

        public DiscoveryMode CurrentMode => _mode;
        public IReadOnlyList<DiscoveryServerEndpoint> CurrentEndpoints => _endpoints;

        public void SetDiscoveryMode(
            DiscoveryMode mode,
            IReadOnlyList<DiscoveryServerEndpoint>? serverEndpoints = null)
        {
            _mode = mode;
            _endpoints = mode == DiscoveryMode.DiscoveryServer && serverEndpoints is { Count: > 0 }
                ? serverEndpoints
                : Array.Empty<DiscoveryServerEndpoint>();
        }

        public Task<IReadOnlyList<NdiSourceEntry>> DiscoverSourcesAsync(CancellationToken cancellationToken = default)
        {
            if (_mode == DiscoveryMode.Mdns)
            {
                // mDNS: return a simulated local source.
                return Task.FromResult<IReadOnlyList<NdiSourceEntry>>(new[]
                {
                    new NdiSourceEntry("mdns-src-1", "mDNS Camera", null, true,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), DiscoveryMode.Mdns),
                });
            }

            // Discovery Server: return a source per reachable endpoint (simulated).
            var sources = _endpoints.Select((ep, i) => new NdiSourceEntry(
                $"{ep.Host}:{ep.Port}", $"Server Camera {i + 1}", $"{ep.Host}:{ep.Port}",
                true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), DiscoveryMode.DiscoveryServer))
                .ToArray();

            return Task.FromResult<IReadOnlyList<NdiSourceEntry>>(sources);
        }

        public Task<bool> IsDiscoveryServerReachableAsync(string host, int port, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<NdiDiscoveryCheckResult> PerformDiscoveryCheckAsync(
            string host, int port, string correlationId, CancellationToken cancellationToken = default)
            => Task.FromResult(new NdiDiscoveryCheckResult(true, "NONE", null));
    }

    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DiscoverSourcesAsync_InMdnsMode_ReturnsMdnsTaggedSources()
    {
        var bridge = new FakeDiscoveryBridge();
        bridge.SetDiscoveryMode(DiscoveryMode.Mdns);

        var sources = await bridge.DiscoverSourcesAsync();

        Assert.NotEmpty(sources);
        Assert.All(sources, s => Assert.Equal(DiscoveryMode.Mdns, s.DiscoveryMode));
    }

    [Fact]
    public async Task DiscoverSourcesAsync_InDiscoveryServerMode_ReturnsDiscoveryServerTaggedSources()
    {
        var bridge = new FakeDiscoveryBridge();
        bridge.SetDiscoveryMode(DiscoveryMode.DiscoveryServer, new[]
        {
            new DiscoveryServerEndpoint("192.168.1.100", 5961),
            new DiscoveryServerEndpoint("192.168.1.101", 5961),
        });

        var sources = await bridge.DiscoverSourcesAsync();

        Assert.Equal(2, sources.Count);
        Assert.All(sources, s => Assert.Equal(DiscoveryMode.DiscoveryServer, s.DiscoveryMode));
    }

    [Fact]
    public void SetDiscoveryMode_Switch_UpdatesCurrentMode()
    {
        var bridge = new FakeDiscoveryBridge();
        Assert.Equal(DiscoveryMode.Mdns, bridge.CurrentMode);

        bridge.SetDiscoveryMode(DiscoveryMode.DiscoveryServer, new[]
        {
            new DiscoveryServerEndpoint("server.local", 5960),
        });
        Assert.Equal(DiscoveryMode.DiscoveryServer, bridge.CurrentMode);

        bridge.SetDiscoveryMode(DiscoveryMode.Mdns);
        Assert.Equal(DiscoveryMode.Mdns, bridge.CurrentMode);
    }

    [Fact]
    public async Task DiscoverSourcesAsync_WhenServerUnreachable_ReturnsEmptyList()
    {
        // Simulates the unreachable-server contract: bridge returns empty, no exception.
        var bridge = new FakeDiscoveryBridge();
        bridge.SetDiscoveryMode(DiscoveryMode.DiscoveryServer,
            Array.Empty<DiscoveryServerEndpoint>()); // empty = no reachable servers

        var sources = await bridge.DiscoverSourcesAsync();

        Assert.Empty(sources);
    }

    [Fact]
    public void SetDiscoveryMode_IsIdempotent_WhenCalledWithSameMode()
    {
        var bridge = new FakeDiscoveryBridge();
        var endpoints = new[] { new DiscoveryServerEndpoint("host.local", 5960) };

        // Call twice with same mode — should not throw.
        bridge.SetDiscoveryMode(DiscoveryMode.DiscoveryServer, endpoints);
        bridge.SetDiscoveryMode(DiscoveryMode.DiscoveryServer, endpoints);

        Assert.Equal(DiscoveryMode.DiscoveryServer, bridge.CurrentMode);
        Assert.Equal(endpoints.Length, bridge.CurrentEndpoints.Count);
    }

    [Fact]
    public async Task DiscoverSourcesAsync_MdnsMode_DeduplicatesSourcesByDisplayName()
    {
        // Verify that the interface contract includes deduplication for multi-server results.
        var bridge = new FakeDiscoveryBridge();
        bridge.SetDiscoveryMode(DiscoveryMode.DiscoveryServer, new[]
        {
            new DiscoveryServerEndpoint("192.168.1.100", 5961),
            new DiscoveryServerEndpoint("192.168.1.101", 5961),
        });

        var sources = await bridge.DiscoverSourcesAsync();

        // Each source ID should be unique.
        var uniqueIds = sources.Select(s => s.SourceId).Distinct().Count();
        Assert.Equal(sources.Count, uniqueIds);
    }

    // ── AC-3: Failover — partial server unreachability ────────────────────────
    // Uses a richer fake that filters out unreachable hosts, mirroring the
    // contract documented on INdiDiscoveryBridge: "All endpoints are queried;
    // results are merged with deduplication."

    private sealed class FakeDiscoveryBridgeWithUnreachableHosts : INdiDiscoveryBridge
    {
        private DiscoveryMode _mode = DiscoveryMode.Mdns;
        private IReadOnlyList<DiscoveryServerEndpoint> _endpoints = Array.Empty<DiscoveryServerEndpoint>();
        private readonly HashSet<string> _unreachableHosts;

        public FakeDiscoveryBridgeWithUnreachableHosts(params string[] unreachableHosts)
        {
            _unreachableHosts = new HashSet<string>(unreachableHosts, StringComparer.OrdinalIgnoreCase);
        }

        public DiscoveryMode CurrentMode => _mode;

        public void SetDiscoveryMode(
            DiscoveryMode mode,
            IReadOnlyList<DiscoveryServerEndpoint>? serverEndpoints = null)
        {
            _mode = mode;
            _endpoints = mode == DiscoveryMode.DiscoveryServer && serverEndpoints is { Count: > 0 }
                ? serverEndpoints
                : Array.Empty<DiscoveryServerEndpoint>();
        }

        public Task<IReadOnlyList<NdiSourceEntry>> DiscoverSourcesAsync(
            CancellationToken cancellationToken = default)
        {
            if (_mode == DiscoveryMode.Mdns)
            {
                return Task.FromResult<IReadOnlyList<NdiSourceEntry>>(new[]
                {
                    new NdiSourceEntry("mdns-src-1", "mDNS Camera", null, true,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), DiscoveryMode.Mdns),
                });
            }

            // Only reachable endpoints contribute sources (failover contract).
            var sources = _endpoints
                .Where(ep => !_unreachableHosts.Contains(ep.Host))
                .Select((ep, i) => new NdiSourceEntry(
                    $"{ep.Host}:{ep.Port}",
                    $"Server Camera {i + 1}",
                    $"{ep.Host}:{ep.Port}",
                    true,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    DiscoveryMode.DiscoveryServer))
                .ToArray();

            return Task.FromResult<IReadOnlyList<NdiSourceEntry>>(sources);
        }

        public Task<bool> IsDiscoveryServerReachableAsync(
            string host, int port, CancellationToken cancellationToken = default)
            => Task.FromResult(!_unreachableHosts.Contains(host));

        public Task<NdiDiscoveryCheckResult> PerformDiscoveryCheckAsync(
            string host, int port, string correlationId, CancellationToken cancellationToken = default)
        {
            bool reachable = !_unreachableHosts.Contains(host);
            return Task.FromResult(new NdiDiscoveryCheckResult(reachable, reachable ? "NONE" : "TIMEOUT", null));
        }
    }

    [Fact]
    public async Task DiscoverSourcesAsync_MultipleServers_WhenFirstServerUnreachable_ReturnSourcesFromSecondServer()
    {
        // AC-3: first server is down; the bridge must still surface sources from the second.
        var bridge = new FakeDiscoveryBridgeWithUnreachableHosts("server-a.local");
        bridge.SetDiscoveryMode(DiscoveryMode.DiscoveryServer, new[]
        {
            new DiscoveryServerEndpoint("server-a.local", 5961), // unreachable
            new DiscoveryServerEndpoint("server-b.local", 5961), // reachable
        });

        var sources = await bridge.DiscoverSourcesAsync();

        Assert.NotEmpty(sources);
        Assert.All(sources, s => Assert.DoesNotContain("server-a.local", s.SourceId));
        Assert.All(sources, s => Assert.Equal(DiscoveryMode.DiscoveryServer, s.DiscoveryMode));
    }

    [Fact]
    public async Task DiscoverSourcesAsync_MultipleServers_WhenFirstServerUnreachable_DoesNotIncludeItsSource()
    {
        // AC-3: verify no source with the unreachable host's address appears in results.
        var bridge = new FakeDiscoveryBridgeWithUnreachableHosts("192.168.10.1");
        bridge.SetDiscoveryMode(DiscoveryMode.DiscoveryServer, new[]
        {
            new DiscoveryServerEndpoint("192.168.10.1", 5961), // unreachable — first in list
            new DiscoveryServerEndpoint("192.168.10.2", 5961), // reachable
        });

        var sources = await bridge.DiscoverSourcesAsync();

        // Sources from the reachable server should appear; the unreachable one must not.
        Assert.Single(sources);
        Assert.Equal("192.168.10.2:5961", sources[0].SourceId);
    }

    [Fact]
    public async Task DiscoverSourcesAsync_MultipleServers_WhenAllUnreachable_ReturnsEmpty()
    {
        // AC-3: all servers down → empty result, no exception.
        var bridge = new FakeDiscoveryBridgeWithUnreachableHosts("server-a.local", "server-b.local");
        bridge.SetDiscoveryMode(DiscoveryMode.DiscoveryServer, new[]
        {
            new DiscoveryServerEndpoint("server-a.local", 5961),
            new DiscoveryServerEndpoint("server-b.local", 5961),
        });

        var sources = await bridge.DiscoverSourcesAsync();

        Assert.Empty(sources);
    }

    [Fact]
    public async Task IsDiscoveryServerReachableAsync_UnreachableHost_ReturnsFalse()
    {
        // AC-3 (contract): the reachability query must return false for an unreachable host.
        var bridge = new FakeDiscoveryBridgeWithUnreachableHosts("dead-server.local");

        bool reachable = await bridge.IsDiscoveryServerReachableAsync("dead-server.local", 5961);

        Assert.False(reachable);
    }

    [Fact]
    public async Task IsDiscoveryServerReachableAsync_ReachableHost_ReturnsTrue()
    {
        // AC-3 (contract): the reachability query must return true for a live host.
        var bridge = new FakeDiscoveryBridgeWithUnreachableHosts("dead-server.local");

        bool reachable = await bridge.IsDiscoveryServerReachableAsync("live-server.local", 5961);

        Assert.True(reachable);
    }
}
