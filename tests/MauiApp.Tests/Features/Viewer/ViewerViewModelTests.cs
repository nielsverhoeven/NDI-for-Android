using Moq;
using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
using NdiForAndroid.Features.ConnectionHistory.Services;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.Features.Viewer.ViewModels;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;
using Xunit;

namespace NdiForAndroid.Tests.Features.Viewer;

public class ViewerViewModelTests
{
    private readonly Mock<INdiViewerBridge> _bridgeMock = new();
    private readonly FakeTimeProvider _timeProvider = new(initialSeconds: 0);
    private readonly FakeMainThreadDispatcher _dispatcher = new();
    private readonly Mock<IAppStateRepository> _appStateRepoMock = new();
    private readonly Mock<IAppLifecycleService> _lifecycleMock = new();
    private readonly Mock<ISourceRepository> _sourceRepoMock = new();
    private readonly Mock<IConnectionHistoryService> _connectionHistoryMock = new();

    public ViewerViewModelTests()
    {
        _appStateRepoMock
            .Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(AppStateSnapshot.Empty);
        _appStateRepoMock
            .Setup(r => r.SaveAsync(It.IsAny<AppStateSnapshot>()))
            .Returns(Task.CompletedTask);
        _sourceRepoMock
            .Setup(r => r.GetCachedSourcesAsync())
            .ReturnsAsync(new List<NdiForAndroid.Features.Sources.Models.NdiSource>());
    }

    private ViewerViewModel CreateSut() => new(_bridgeMock.Object, _timeProvider, _dispatcher, _appStateRepoMock.Object, _lifecycleMock.Object, _sourceRepoMock.Object, _connectionHistoryMock.Object);

    [Fact]
    public void StartCommand_WithSourceId_StartsReceiverAndSetsIsPlaying()
    {
        var sut = CreateSut();
        sut.SourceId = "src-1";
        sut.StartCommand.Execute(null);

        _bridgeMock.Verify(b => b.StartReceiver("src-1"), Times.AtLeastOnce);
        Assert.True(sut.IsPlaying);
        Assert.NotNull(sut.StatusMessage);
    }

    [Fact]
    public void StartCommand_WithNullSourceId_DoesNotStartReceiver()
    {
        var sut = CreateSut();

        sut.StartCommand.Execute(null);

        _bridgeMock.Verify(b => b.StartReceiver(It.IsAny<string>()), Times.Never);
        Assert.False(sut.IsPlaying);
    }

    [Fact]
    public void StopCommand_WhenPlaying_StopsReceiverAndClearsState()
    {
        var sut = CreateSut();
        sut.SourceId = "src-1";
        sut.StartCommand.Execute(null);

        sut.StopCommand.Execute(null);

        _bridgeMock.Verify(b => b.StopReceiver(), Times.Once);
        Assert.False(sut.IsPlaying);
        Assert.Null(sut.StatusMessage);
    }

    // --- Reconnection tests (FR1-FR8) ---

    [Fact]
    public void BeginReconnectWindow_SetsReconnectingStateAndStartsCountdown()
    {
        var sut = CreateSut();

        sut.IsPlaying = true;
        sut.BeginReconnectWindow();

        Assert.True(sut.IsReconnecting);
        Assert.Equal(15, sut.RetryRemainingSeconds);
        Assert.NotNull(sut.RetryStatusMessage);
    }

    [Fact]
    public void CheckForUnexpectedDrop_WhenDisconnectedAndNotUserStop_BeginsReconnectWindow()
    {
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Disconnected);

        var sut = CreateSut();
        sut.IsPlaying = true;

        sut.CheckForUnexpectedDrop();

        Assert.True(sut.IsReconnecting);
    }

    [Fact]
    public void CheckForUnexpectedDrop_WhenUserStop_DoesNotBeginReconnectWindow()
    {
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Disconnected);

        var sut = CreateSut();
        sut.IsPlaying = true;
        sut.StopCommand.Execute(null);

        sut.CheckForUnexpectedDrop();

        Assert.False(sut.IsReconnecting);
    }

    [Fact]
    public void ReconnectCommand_WithLastSourceId_BeginsReconnectWindow()
    {
        var sut = CreateSut();

        sut.SourceId = "src-1";
        sut.StartCommand.Execute(null);
        sut.ReconnectCommand.Execute(null);

        Assert.True(sut.IsReconnecting);
        Assert.False(sut.CanReconnect);
    }

    [Fact]
    public void CancelRetry_ClearsReconnectingState()
    {
        var sut = CreateSut();

        sut.IsPlaying = true;
        sut.BeginReconnectWindow();
        sut.CancelRetryCommand.Execute(null);

        Assert.False(sut.IsReconnecting);
        Assert.Equal("Reconnection cancelled.", sut.RetryStatusMessage);
    }

    [Fact]
    public void Dispose_CleansUpResources()
    {
        var sut = CreateSut();

        // Verify dispose works even without any reconnect state.
        sut.Dispose();

        Assert.False(sut.IsReconnecting);
    }

    // ── Phase 2 (#277): tally, PTZ, audio wiring ────────────────────────────

    [Fact]
    public void TallyEcho_ProgramOn_SetsIsTallyProgram()
    {
        var sut = CreateSut();

        _bridgeMock.Raise(b => b.TallyEchoChanged += null, _bridgeMock.Object, new NdiTallyEcho(OnProgram: true, OnPreview: false));

        Assert.True(sut.IsTallyProgram);
    }

    [Fact]
    public void ConnectionStateChanged_Connected_RefreshesIsPtzSupported()
    {
        _bridgeMock.SetupGet(b => b.IsPtzSupported).Returns(true);
        var sut = CreateSut();

        _bridgeMock.Raise(b => b.ConnectionStateChanged += null, _bridgeMock.Object, ConnectionState.Connected);

        Assert.True(sut.IsPtzSupported);
    }

    [Fact]
    public void StartCommand_ReportsProgramTallyUpstream()
    {
        var sut = CreateSut();
        sut.SourceId = "192.168.1.10:5961";

        _bridgeMock.Verify(b => b.SetTally(true, false), Times.AtLeastOnce);
    }

    [Fact]
    public void StopCommand_ClearsTallyAndPtzState()
    {
        var sut = CreateSut();
        sut.SourceId = "192.168.1.10:5961";

        sut.StopCommand.Execute(null);

        _bridgeMock.Verify(b => b.SetTally(false, false), Times.AtLeastOnce);
        Assert.False(sut.IsTallyProgram);
        Assert.False(sut.IsPtzSupported);
    }

    [Fact]
    public void IsAudioEnabled_Set_ForwardsToBridge()
    {
        var sut = CreateSut();

        sut.IsAudioEnabled = true;

        _bridgeMock.VerifySet(b => b.IsAudioEnabled = true, Times.Once);
    }

    [Theory]
    [InlineData("left", -0.5f, 0f)]
    [InlineData("right", 0.5f, 0f)]
    [InlineData("up", 0f, 0.5f)]
    [InlineData("down", 0f, -0.5f)]
    public async Task PtzNudge_BurstsThenStops(string direction, float expectedPan, float expectedTilt)
    {
        var sut = CreateSut();

        await sut.PtzNudgeCommand.ExecuteAsync(direction);

        _bridgeMock.Verify(b => b.PtzPanTiltSpeed(expectedPan, expectedTilt), Times.Once);
        _bridgeMock.Verify(b => b.PtzPanTiltSpeed(0f, 0f), Times.Once);
    }

    [Fact]
    public async Task PtzZoomNudge_In_BurstsThenStops()
    {
        var sut = CreateSut();

        await sut.PtzZoomNudgeCommand.ExecuteAsync("in");

        _bridgeMock.Verify(b => b.PtzZoomSpeed(0.5f), Times.Once);
        _bridgeMock.Verify(b => b.PtzZoomSpeed(0f), Times.Once);
    }
}
