using Moq;
using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
using NdiForAndroid.Features.Home.Models;
using NdiForAndroid.Features.Home.ViewModels;
using NdiForAndroid.Features.Navigation.Models;
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

    // ── Lifetime contract (#352/#359): one subscription per event for the app lifetime ─────────

    [Fact]
    public void Constructor_SubscribesToEachSingletonEventExactlyOnce()
    {
        _ = CreateSut();

        _discoveryServiceMock.VerifyAdd(d => d.SnapshotReady += It.IsAny<EventHandler<DiscoverySnapshot>>(), Times.Once);
        _outputBridgeMock.VerifyAdd(b => b.OutputStatusChanged += It.IsAny<EventHandler>(), Times.Once);
    }

    [Fact]
    public void RepeatedAppearances_OnTheSameInstance_DoNotAddSubscriptions()
    {
        var sut = CreateSut();

        // HomePage.OnAppearing re-runs RefreshCommand on every tab entry; under the singleton
        // lifetime that is the only per-visit work and it must leave the event wiring alone.
        sut.RefreshCommand.Execute(null);
        sut.RefreshCommand.Execute(null);
        sut.RefreshCommand.Execute(null);

        _discoveryServiceMock.VerifyAdd(d => d.SnapshotReady += It.IsAny<EventHandler<DiscoverySnapshot>>(), Times.Once);
        _outputBridgeMock.VerifyAdd(b => b.OutputStatusChanged += It.IsAny<EventHandler>(), Times.Once);
    }

    [Fact]
    public void Dispose_RemovesEverySubscriptionItAdded()
    {
        var sut = CreateSut();

        sut.Dispose();

        _discoveryServiceMock.VerifyRemove(d => d.SnapshotReady -= It.IsAny<EventHandler<DiscoverySnapshot>>(), Times.Once);
        _outputBridgeMock.VerifyRemove(b => b.OutputStatusChanged -= It.IsAny<EventHandler>(), Times.Once);

        // A snapshot after teardown must not reach the (disposed) instance.
        _discoveryServiceMock.Raise(d => d.SnapshotReady += null, sut, new DiscoverySnapshot(
            "snap-after-dispose", DiscoveryStatus.Empty, Array.Empty<NdiSource>(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        Assert.Equal("Waiting for discovery...", sut.DiscoveryStatus);
    }

    [Fact]
    public async Task StartViewingLastSourceCommand_WhenLastSourcePersisted_NavigatesToViewThenViewer()
    {
        _appStateRepoMock.Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot("src-1", null, false, null));

        var sut = CreateSut();

        await sut.StartViewingLastSourceCommand.ExecuteAsync(null);

        _navigationServiceMock.Verify(n => n.NavigateToPrimaryAsync(PrimaryNavDestination.View, null), Times.Once);
        _navigationServiceMock.Verify(n => n.NavigateToAsync("viewer?sourceId=src-1"), Times.Once);
    }

    [Fact]
    public void StartViewingLastSourceCommand_WhenNoLastSourcePersisted_IsDisabled()
    {
        var sut = CreateSut();

        Assert.False(sut.StartViewingLastSourceCommand.CanExecute(null));
    }

    [Fact]
    public async Task StartViewingLastSourceCommand_WhenForcedWithNoLastSource_MakesNoNavigationCalls()
    {
        var sut = CreateSut();

        await sut.StartViewingLastSourceCommand.ExecuteAsync(null);

        _navigationServiceMock.Verify(
            n => n.NavigateToPrimaryAsync(It.IsAny<PrimaryNavDestination>(), It.IsAny<string>()), Times.Never);
        _navigationServiceMock.Verify(n => n.NavigateToAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResumeOutputCommand_WhenStreamNamePersistedAndOutputNotActive_NavigatesToStreamWithResumeQuery()
    {
        _appStateRepoMock.Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot(null, "MyStream", false, null));

        var sut = CreateSut();

        await sut.ResumeOutputCommand.ExecuteAsync(null);

        _navigationServiceMock.Verify(
            n => n.NavigateToPrimaryAsync(PrimaryNavDestination.Stream, "resume=true"), Times.Once);
    }

    [Fact]
    public void RefreshCommand_WhenStreamNamePersistedAndOutputNotActive_EnablesCanResumeOutput()
    {
        _appStateRepoMock.Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot(null, "name", false, null));
        _outputBridgeMock.SetupGet(b => b.IsActive).Returns(false);

        var sut = CreateSut();

        Assert.True(sut.CanResumeOutput);
    }

    [Fact]
    public void ResumeOutputCommand_WhenNoPersistedStreamName_IsDisabled()
    {
        var sut = CreateSut();

        Assert.False(sut.ResumeOutputCommand.CanExecute(null));
    }

    [Fact]
    public void ResumeOutputCommand_WhenOutputAlreadyActive_IsDisabled()
    {
        _appStateRepoMock.Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot(null, "MyStream", true, null));
        _outputBridgeMock.SetupGet(b => b.IsActive).Returns(true);

        var sut = CreateSut();

        Assert.False(sut.ResumeOutputCommand.CanExecute(null));
    }

    [Fact]
    public async Task ResumeOutputCommand_WhenForcedWithNoPersistedStreamName_MakesNoNavigationCalls()
    {
        var sut = CreateSut();

        await sut.ResumeOutputCommand.ExecuteAsync(null);

        _navigationServiceMock.Verify(
            n => n.NavigateToPrimaryAsync(It.IsAny<PrimaryNavDestination>(), It.IsAny<string>()), Times.Never);
    }

    // ── Status-card colour by state (#370 home-nav-08) and friendly viewer name (home-nav-03) ───

    [Fact]
    public void RefreshCommand_WhenLastViewerSourceIsCached_ShowsItsDisplayName()
    {
        _appStateRepoMock.Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot("src-1", null, false, null));
        _sourceRepositoryMock.Setup(r => r.GetCachedSourcesAsync())
            .ReturnsAsync(new List<NdiSource> { new("src-1", "Camera 1", "192.168.1.10", true, 1000) });

        var sut = CreateSut();

        Assert.Equal("Last viewed: Camera 1", sut.ViewerStatus);
    }

    [Fact]
    public void RefreshCommand_WhenLastViewerSourceIsNotCached_FallsBackToTheRawId()
    {
        _appStateRepoMock.Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot("src-9", null, false, null));

        var sut = CreateSut();

        Assert.Equal("Last viewed: src-9", sut.ViewerStatus);
    }

    [Fact]
    public void Constructed_WithNoCachedSources_DiscoveryStatusKindIsIdle()
    {
        var sut = CreateSut();

        Assert.Equal(HomeStatusKind.Idle, sut.DiscoveryStatusKind);
    }

    [Fact]
    public void Constructed_WithCachedSources_DiscoveryStatusKindIsActive()
    {
        _sourceRepositoryMock.Setup(r => r.GetCachedSourcesAsync())
            .ReturnsAsync(new List<NdiSource> { new("src-1", "Camera 1", "192.168.1.10", true, 1000) });

        var sut = CreateSut();

        Assert.Equal(HomeStatusKind.Active, sut.DiscoveryStatusKind);
    }

    [Theory]
    [InlineData(DiscoveryStatus.Success, HomeStatusKind.Active)]
    [InlineData(DiscoveryStatus.Failure, HomeStatusKind.Failure)]
    [InlineData(DiscoveryStatus.Empty, HomeStatusKind.Idle)]
    public void DiscoverySnapshot_SetsDiscoveryStatusKindFromStatus(DiscoveryStatus status, HomeStatusKind expected)
    {
        var sut = CreateSut();

        _discoveryServiceMock.Raise(d => d.SnapshotReady += null, sut, new DiscoverySnapshot(
            "snap-1", status, Array.Empty<NdiSource>(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), "boom"));

        Assert.Equal(expected, sut.DiscoveryStatusKind);
    }

    [Fact]
    public void RefreshCommand_WhenOutputActive_SetsOutputStatusKindActive()
    {
        _appStateRepoMock.Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot(null, "X", true, null));
        _outputBridgeMock.SetupGet(b => b.IsActive).Returns(true);

        var sut = CreateSut();

        Assert.Equal(HomeStatusKind.Active, sut.OutputStatusKind);
    }

    [Fact]
    public void RefreshCommand_WhenBridgeInactive_OutputStatusKindIsIdle()
    {
        _appStateRepoMock.Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot(null, "X", true, null));
        _outputBridgeMock.SetupGet(b => b.IsActive).Returns(false);

        var sut = CreateSut();

        Assert.Equal(HomeStatusKind.Idle, sut.OutputStatusKind);
    }
}
