using Moq;
using NdiForAndroid.Features.Settings.Services;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.Features.Sources.ViewModels;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;
using Xunit;

namespace MauiApp.Tests.Features.Sources;

public class SourceListViewModelTests
{
    private readonly Mock<ISourceRepository> _repositoryMock = new();
    private readonly Mock<INavigationService> _navigationMock = new();
    private readonly Mock<IDiscoverySettingsOrchestrator> _orchestratorMock = new();
    private readonly Mock<IDiscoveryRefreshService> _refreshServiceMock = new();

    private SourceListViewModel CreateSut(DiscoveryMode mode = DiscoveryMode.Mdns)
    {
        _orchestratorMock.Setup(o => o.ActiveMode).Returns(mode);
        return new(_repositoryMock.Object, _navigationMock.Object, _orchestratorMock.Object, _refreshServiceMock.Object);
    }

    private DiscoverySnapshot SuccessSnapshot(IReadOnlyList<NdiSource>? sources = null) => new(
        "snap-1", DiscoveryStatus.Success,
        sources ?? Array.Empty<NdiSource>(),
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    private DiscoverySnapshot FailureSnapshot(string error) => new(
        "snap-1", DiscoveryStatus.Failure,
        Array.Empty<NdiSource>(),
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        error);

    // ── SnapshotReady event integration ───────────────────────────────────────

    [Fact]
    public void SnapshotReady_Success_PopulatesSources()
    {
        var sources = new List<NdiSource>
        {
            new("src-1", "Camera 1", "192.168.1.10", true, 1000),
            new("src-2", "Camera 2", "192.168.1.11", true, 2000),
        };
        var sut = CreateSut();

        _refreshServiceMock.Raise(r => r.SnapshotReady += null, sut, SuccessSnapshot(sources));

        Assert.Equal(2, sut.Sources.Count);
        Assert.Equal("Camera 1", sut.Sources[0].DisplayName);
        Assert.Null(sut.ErrorMessage);
        Assert.False(sut.IsRefreshing);
    }

    [Fact]
    public void SnapshotReady_Failure_SetsErrorMessage()
    {
        var sut = CreateSut();

        _refreshServiceMock.Raise(r => r.SnapshotReady += null, sut, FailureSnapshot("Connection refused"));

        Assert.Empty(sut.Sources);
        Assert.Equal("Connection refused", sut.ErrorMessage);
        Assert.False(sut.IsRefreshing);
    }

    [Fact]
    public void SnapshotReady_AfterSuccess_ClearsErrorMessage()
    {
        var sut = CreateSut();
        _refreshServiceMock.Raise(r => r.SnapshotReady += null, sut, FailureSnapshot("oops"));

        _refreshServiceMock.Raise(r => r.SnapshotReady += null, sut, SuccessSnapshot());

        Assert.Null(sut.ErrorMessage);
    }

    // ── RefreshCommand ────────────────────────────────────────────────────────

    [Fact]
    public void RefreshCommand_SetsIsRefreshingAndDelegates()
    {
        var sut = CreateSut();

        sut.RefreshCommand.Execute(null);

        Assert.True(sut.IsRefreshing);
        _refreshServiceMock.Verify(r => r.RequestRefresh(), Times.Once);
    }

    [Fact]
    public void SnapshotReady_ClearsIsRefreshing()
    {
        var sut = CreateSut();
        sut.RefreshCommand.Execute(null);  // sets IsRefreshing = true

        _refreshServiceMock.Raise(r => r.SnapshotReady += null, sut, SuccessSnapshot());

        Assert.False(sut.IsRefreshing);
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task NavigateToViewerCommand_CallsNavigationService_WithCorrectRoute()
    {
        var source = new NdiSource("src-abc", "Camera 1", "192.168.1.10", true, 1000);
        var sut = CreateSut();

        await sut.NavigateToViewerCommand.ExecuteAsync(source);

        var expectedRoute = $"viewer?sourceId={Uri.EscapeDataString("src-abc")}";
        _navigationMock.Verify(
            n => n.NavigateToAsync(expectedRoute),
            Times.Once,
            $"Expected NavigateToAsync to be called with ''{expectedRoute}''");
    }

    [Fact]
    public async Task NavigateToViewerCommand_WithNullOrEmptySourceId_StillCallsNavigation()
    {
        var source = new NdiSource(string.Empty, "Unknown", "0.0.0.0", false, 0);
        var sut = CreateSut();

        await sut.NavigateToViewerCommand.ExecuteAsync(source);

        var expectedRoute = $"viewer?sourceId={Uri.EscapeDataString(string.Empty)}";
        _navigationMock.Verify(n => n.NavigateToAsync(expectedRoute), Times.Once);
    }

    // ── Discovery mode label ──────────────────────────────────────────────────

    [Fact]
    public void SnapshotReady_InMdnsMode_SetsLabelToMdns()
    {
        var sut = CreateSut(DiscoveryMode.Mdns);

        _refreshServiceMock.Raise(r => r.SnapshotReady += null, sut, SuccessSnapshot());

        Assert.Equal("mDNS", sut.ActiveDiscoveryModeLabel);
    }

    [Fact]
    public void SnapshotReady_InDiscoveryServerMode_SetsLabelContainingDiscoveryServer()
    {
        var sut = CreateSut(DiscoveryMode.DiscoveryServer);

        _refreshServiceMock.Raise(r => r.SnapshotReady += null, sut, SuccessSnapshot());

        Assert.Contains("Discovery Server", sut.ActiveDiscoveryModeLabel);
    }

    // ── Stop / misc ───────────────────────────────────────────────────────────

    [Fact]
    public void StopDiscoveryCommand_CallsServiceStop()
    {
        var sut = CreateSut();
        sut.StopDiscoveryCommand.Execute(null);
        _refreshServiceMock.Verify(r => r.Stop(), Times.Once);
    }

    [Fact]
    public void ViewModel_DoesNotHaveNavigateToOutputCommand()
    {
        var sut = CreateSut();
        var type = sut.GetType();
        var prop = type.GetProperty("NavigateToOutputCommand");
        Assert.Null(prop);
    }

    // ── AC-4: Hot-switch label reflects new mode without app restart ──────────

    [Fact]
    public void SnapshotReady_AfterHotSwitchToDiscoveryServer_ReflectsNewModeLabel()
    {
        _orchestratorMock.Setup(o => o.ActiveMode).Returns(DiscoveryMode.Mdns);
        var sut = CreateSut();

        _refreshServiceMock.Raise(r => r.SnapshotReady += null, sut, SuccessSnapshot());
        Assert.Equal("mDNS", sut.ActiveDiscoveryModeLabel);

        _orchestratorMock.Setup(o => o.ActiveMode).Returns(DiscoveryMode.DiscoveryServer);
        _refreshServiceMock.Raise(r => r.SnapshotReady += null, sut, SuccessSnapshot());

        Assert.Contains("Discovery Server", sut.ActiveDiscoveryModeLabel);
    }

    [Fact]
    public void SnapshotReady_AfterHotSwitchBackToMdns_ReflectsMdnsLabel()
    {
        var sut = CreateSut(DiscoveryMode.DiscoveryServer);

        _refreshServiceMock.Raise(r => r.SnapshotReady += null, sut, SuccessSnapshot());
        Assert.Contains("Discovery Server", sut.ActiveDiscoveryModeLabel);

        _orchestratorMock.Setup(o => o.ActiveMode).Returns(DiscoveryMode.Mdns);
        _refreshServiceMock.Raise(r => r.SnapshotReady += null, sut, SuccessSnapshot());

        Assert.Equal("mDNS", sut.ActiveDiscoveryModeLabel);
    }

    [Fact]
    public void SnapshotReady_HotSwitchDoesNotRequireRebuildingViewModel()
    {
        _orchestratorMock.Setup(o => o.ActiveMode).Returns(DiscoveryMode.Mdns);
        var sut = CreateSut();

        _refreshServiceMock.Raise(r => r.SnapshotReady += null, sut, SuccessSnapshot());
        Assert.Equal("mDNS", sut.ActiveDiscoveryModeLabel);

        _orchestratorMock.Setup(o => o.ActiveMode).Returns(DiscoveryMode.DiscoveryServer);
        _refreshServiceMock.Raise(r => r.SnapshotReady += null, sut, SuccessSnapshot());
        Assert.Contains("Discovery Server", sut.ActiveDiscoveryModeLabel);

        _orchestratorMock.Setup(o => o.ActiveMode).Returns(DiscoveryMode.Mdns);
        _refreshServiceMock.Raise(r => r.SnapshotReady += null, sut, SuccessSnapshot());
        Assert.Equal("mDNS", sut.ActiveDiscoveryModeLabel);
    }
}

