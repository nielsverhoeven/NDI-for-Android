using Moq;
using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
using NdiForAndroid.Features.Home.ViewModels;
using NdiForAndroid.Features.Navigation.Services;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;
using Xunit;

namespace NdiForAndroid.Tests.Features.Home;

public class HomeViewModelTests
{
    private readonly Mock<IDiscoveryRefreshService> _discoveryServiceMock = new();
    private readonly Mock<ISourceRepository> _sourceRepositoryMock = new();
    private readonly Mock<IAppStateRepository> _appStateRepoMock = new();
    private readonly Mock<INavigationService> _navigationServiceMock = new();
    private readonly Mock<INdiOutputBridge> _outputBridgeMock = new();
    private readonly FakeMainThreadDispatcher _dispatcher = new();

    public HomeViewModelTests()
    {
        _appStateRepoMock
            .Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(AppStateSnapshot.Empty);
        _sourceRepositoryMock
            .Setup(r => r.GetCachedSourcesAsync())
            .ReturnsAsync(new List<NdiSource>());
    }

    private HomeViewModel CreateSut() => new(
        _discoveryServiceMock.Object,
        _sourceRepositoryMock.Object,
        _appStateRepoMock.Object,
        _navigationServiceMock.Object,
        _outputBridgeMock.Object,
        _dispatcher);

    [Fact]
    public void Constructed_WithCachedSources_ShowsCurrentCountImmediately()
    {
        _sourceRepositoryMock
            .Setup(r => r.GetCachedSourcesAsync())
            .ReturnsAsync(new List<NdiSource>
            {
                new("src-1", "Camera 1", "192.168.1.10", true, 1000),
                new("src-2", "Camera 2", "192.168.1.11", true, 2000),
                new("src-3", "Camera 3", "192.168.1.12", true, 3000),
            });

        var sut = CreateSut();

        Assert.Equal(3, sut.SourceCount);
        Assert.Equal("Connected to NDI network", sut.DiscoveryStatus);
    }

    [Fact]
    public void Constructed_WithNoCachedSources_ShowsWaitingDefault()
    {
        var sut = CreateSut();

        Assert.Equal("Waiting for discovery...", sut.DiscoveryStatus);
        Assert.Equal(0, sut.SourceCount);
    }

    [Fact]
    public void RefreshCommand_ReRunsAfterEarlierUpdate_StillLive()
    {
        var sut = CreateSut();

        _discoveryServiceMock.Raise(d => d.SnapshotReady += null, sut, new DiscoverySnapshot(
            "snap-1", DiscoveryStatus.Success, Array.Empty<NdiSource>(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

        sut.RefreshCommand.Execute(null);

        _discoveryServiceMock.Raise(d => d.SnapshotReady += null, sut, new DiscoverySnapshot(
            "snap-2", DiscoveryStatus.Success,
            new List<NdiSource> { new("src-1", "Camera 1", "192.168.1.10", true, 1000) },
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

        Assert.Equal(1, sut.SourceCount);
    }

    [Fact]
    public void RefreshCommand_WhenAppStateActiveButBridgeNotActive_ShowsIdle()
    {
        _appStateRepoMock.Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot(null, "X", true, null));
        _outputBridgeMock.SetupGet(b => b.IsActive).Returns(false);

        var sut = CreateSut();

        Assert.Equal("Idle (no active output)", sut.OutputStatus);
    }

    [Fact]
    public void RefreshCommand_WhenAppStateActiveAndBridgeActive_ShowsActiveOutput()
    {
        _appStateRepoMock.Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot(null, "X", true, null));
        _outputBridgeMock.SetupGet(b => b.IsActive).Returns(true);

        var sut = CreateSut();

        Assert.Equal("Active output to \"X\"", sut.OutputStatus);
    }

    [Fact]
    public void OutputStatusChanged_FromBridge_RefreshesOutputStatus()
    {
        var sut = CreateSut();
        Assert.Equal("Idle (no active output)", sut.OutputStatus);

        _appStateRepoMock.Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot(null, "X", true, null));
        _outputBridgeMock.SetupGet(b => b.IsActive).Returns(true);

        _outputBridgeMock.Raise(b => b.OutputStatusChanged += null, EventArgs.Empty);

        Assert.Equal("Active output to \"X\"", sut.OutputStatus);
    }

    [Fact]
    public void Dispose_UnsubscribesFromOutputStatusChanged()
    {
        var sut = CreateSut();
        sut.Dispose();

        _appStateRepoMock.Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot(null, "X", true, null));
        _outputBridgeMock.SetupGet(b => b.IsActive).Returns(true);

        _outputBridgeMock.Raise(b => b.OutputStatusChanged += null, EventArgs.Empty);

        Assert.Equal("Idle (no active output)", sut.OutputStatus);
    }
}
