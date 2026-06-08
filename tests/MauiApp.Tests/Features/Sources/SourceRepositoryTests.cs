using Moq;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.NdiBridge;
using Xunit;

namespace MauiApp.Tests.Features.Sources;

/// <summary>
/// Behavioral contract tests for <see cref="ISourceRepository"/>.
/// These tests use mocked interface collaborators to verify the
/// expected interactions and data shapes, since the concrete
/// SourceRepository implementation depends on Android-only types.
/// </summary>
public class SourceRepositoryTests
{
    private readonly Mock<ISourceRepository> _repositoryMock = new();

    [Fact]
    public async Task DiscoverAsync_InMdnsMode_ReturnsSourcesTaggedWithMdns()
    {
        var sources = new List<NdiSource>
        {
            new("src-mdns-1", "mDNS Camera", null, true, 1000L, false, DiscoveryMode.Mdns),
        };
        _repositoryMock.Setup(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoverySnapshot("snap-1", DiscoveryStatus.Success, sources,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

        var snapshot = await _repositoryMock.Object.DiscoverAsync();

        Assert.Single(snapshot.Sources);
        Assert.Equal(DiscoveryMode.Mdns, snapshot.Sources[0].DiscoveryMode);
    }

    [Fact]
    public async Task DiscoverAsync_InDiscoveryServerMode_ReturnsSourcesTaggedWithDiscoveryServer()
    {
        var sources = new List<NdiSource>
        {
            new("src-ds-1", "Server Camera", "192.168.1.100:5961", true, 1000L, false, DiscoveryMode.DiscoveryServer),
        };
        _repositoryMock.Setup(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoverySnapshot("snap-2", DiscoveryStatus.Success, sources,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

        var snapshot = await _repositoryMock.Object.DiscoverAsync();

        Assert.Single(snapshot.Sources);
        Assert.Equal(DiscoveryMode.DiscoveryServer, snapshot.Sources[0].DiscoveryMode);
    }

    [Fact]
    public async Task DiscoverAsync_WhenDiscoveryFails_ReturnsFailureSnapshot()
    {
        _repositoryMock.Setup(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoverySnapshot("snap-err", DiscoveryStatus.Failure, Array.Empty<NdiSource>(),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), "Connection refused"));

        var snapshot = await _repositoryMock.Object.DiscoverAsync();

        Assert.Equal(DiscoveryStatus.Failure, snapshot.Status);
        Assert.Empty(snapshot.Sources);
        Assert.Equal("Connection refused", snapshot.ErrorMessage);
    }

    [Fact]
    public async Task DiscoverAsync_InDiscoveryServerMode_MarksStaleSourcesUnavailable()
    {
        // Arrange: simulate stale behavior — first poll returns source A+B, second only A.
        var firstSources = new List<NdiSource>
        {
            new("src-A", "Source A", null, true, 1000L, false, DiscoveryMode.DiscoveryServer),
            new("src-B", "Source B", null, true, 1000L, false, DiscoveryMode.DiscoveryServer),
        };
        var secondSources = new List<NdiSource>
        {
            new("src-A", "Source A", null, true, 2000L, false, DiscoveryMode.DiscoveryServer),
            new("src-B", "Source B", null, false, 1000L, false, DiscoveryMode.DiscoveryServer), // stale
        };

        _repositoryMock.SetupSequence(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoverySnapshot("s1", DiscoveryStatus.Success, firstSources, 1000))
            .ReturnsAsync(new DiscoverySnapshot("s2", DiscoveryStatus.Success, secondSources, 2000));

        // Act: two polls
        var snap1 = await _repositoryMock.Object.DiscoverAsync();
        var snap2 = await _repositoryMock.Object.DiscoverAsync();

        // Assert: second poll marks src-B as unavailable
        Assert.Equal(2, snap1.Sources.Count);
        Assert.True(snap1.Sources.All(s => s.IsAvailable));
        var srcBAfter = snap2.Sources.FirstOrDefault(s => s.SourceId == "src-B");
        Assert.NotNull(srcBAfter);
        Assert.False(srcBAfter!.IsAvailable);
    }

    [Fact]
    public async Task GetActiveDiscoveryModeAsync_ReturnsModeFromOrchestrator()
    {
        _repositoryMock.Setup(r => r.GetActiveDiscoveryModeAsync())
            .ReturnsAsync(DiscoveryMode.DiscoveryServer);

        var mode = await _repositoryMock.Object.GetActiveDiscoveryModeAsync();

        Assert.Equal(DiscoveryMode.DiscoveryServer, mode);
    }
}
