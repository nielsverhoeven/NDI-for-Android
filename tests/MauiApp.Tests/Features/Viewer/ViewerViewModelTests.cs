using Moq;
using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
using NdiForAndroid.Features.ConnectionHistory.Services;
using NdiForAndroid.Features.Ptz.Models;
using NdiForAndroid.Features.Ptz.Services;
using NdiForAndroid.Features.Ptz.ViewModels;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.Features.Viewer.ViewModels;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;
using Xunit;
using MsFakeTimeProvider = Microsoft.Extensions.Time.Testing.FakeTimeProvider;

namespace NdiForAndroid.Tests.Features.Viewer;

public class ViewerViewModelTests
{
    private readonly Mock<INdiViewerBridge> _bridgeMock = new();
    private readonly MsFakeTimeProvider _timeProvider = new();
    private readonly FakeMainThreadDispatcher _dispatcher = new();
    private readonly Mock<IAppStateRepository> _appStateRepoMock = new();
    private readonly Mock<IAppLifecycleService> _lifecycleMock = new();
    private readonly Mock<ISourceRepository> _sourceRepoMock = new();
    private readonly Mock<IConnectionHistoryService> _connectionHistoryMock = new();
    private readonly Mock<IPtzControllerFactory> _ptzControllerFactoryMock = new();
    private readonly Mock<IPtzController> _ptzControllerMock = new();
    private readonly Mock<IImmersiveModeService> _immersiveModeMock = new();

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
            .ReturnsAsync(new List<NdiSource>());
        _ptzControllerFactoryMock
            .Setup(f => f.Create(It.IsAny<PtzEndpoint?>()))
            .Returns(_ptzControllerMock.Object);
    }

    private ViewerViewModel CreateSut() => new(
        _bridgeMock.Object, _timeProvider, _dispatcher, _appStateRepoMock.Object, _lifecycleMock.Object,
        _sourceRepoMock.Object, _connectionHistoryMock.Object,
        _ptzControllerFactoryMock.Object, new PtzEndpointFormViewModel(_ptzControllerFactoryMock.Object),
        _immersiveModeMock.Object);

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
    public async Task StartCommand_PreservesExistingOutputAndSelectedSourceState()
    {
        _appStateRepoMock
            .Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot(null, "X", true, "Y"));
        AppStateSnapshot? savedSnapshot = null;
        _appStateRepoMock
            .Setup(r => r.SaveAsync(It.IsAny<AppStateSnapshot>()))
            .Callback<AppStateSnapshot>(s => savedSnapshot = s)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        sut.SourceId = "src-new";
        await sut.StartCommand.ExecuteAsync(null);

        Assert.NotNull(savedSnapshot);
        Assert.Equal("src-new", savedSnapshot!.LastViewerSourceId);
        Assert.Equal("X", savedSnapshot.StreamName);
        Assert.True(savedSnapshot.IsOutputActive);
        Assert.Equal("Y", savedSnapshot.LastSelectedSourceId);
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

        _ptzControllerMock.Verify(c => c.PanTiltAsync(expectedPan, expectedTilt, It.IsAny<CancellationToken>()), Times.Once);
        _ptzControllerMock.Verify(c => c.PanTiltAsync(0f, 0f, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PtzZoomNudge_In_BurstsThenStops()
    {
        var sut = CreateSut();

        await sut.PtzZoomNudgeCommand.ExecuteAsync("in");

        _ptzControllerMock.Verify(c => c.ZoomAsync(0.5f, It.IsAny<CancellationToken>()), Times.Once);
        _ptzControllerMock.Verify(c => c.ZoomAsync(0f, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Start_WithPtzOverrideConfigured_ResolvesViscaBackedController()
    {
        var sourceWithOverride = new NdiSource(
            "src-1", "Cam 1", "192.168.1.10", true, 0,
            PtzOverrideHost: "192.168.1.99", PtzOverridePort: 1234);
        _sourceRepoMock.Setup(r => r.GetCachedSourcesAsync())
            .ReturnsAsync(new List<NdiSource> { sourceWithOverride });
        var sut = CreateSut();

        sut.SourceId = "src-1";
        await sut.StartCommand.ExecuteAsync(null);

        _ptzControllerFactoryMock.Verify(
            f => f.Create(It.Is<PtzEndpoint>(e => e != null && e.Host == "192.168.1.99" && e.Port == 1234)),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Stop_DisposesActivePtzController()
    {
        var sut = CreateSut();
        sut.SourceId = "src-1";
        await sut.StartCommand.ExecuteAsync(null);

        sut.StopCommand.Execute(null);

        _ptzControllerMock.Verify(c => c.ShutdownAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task OpenPtzEndpointFormCommand_PopulatesFormFromActiveSource()
    {
        var sourceWithOverride = new NdiSource(
            "src-1", "Cam 1", "192.168.1.10", true, 0,
            PtzOverrideHost: "192.168.1.99", PtzOverridePort: 1234);
        _sourceRepoMock.Setup(r => r.GetCachedSourcesAsync())
            .ReturnsAsync(new List<NdiSource> { sourceWithOverride });
        var sut = CreateSut();
        sut.SourceId = "src-1";
        await sut.StartCommand.ExecuteAsync(null);

        sut.OpenPtzEndpointFormCommand.Execute(null);

        Assert.Equal("192.168.1.99", sut.PtzEndpointForm.Host);
        Assert.Equal("1234", sut.PtzEndpointForm.PortText);
        Assert.True(sut.PtzEndpointForm.IsOpen);
    }

    [Fact]
    public async Task PtzEndpointForm_SaveRequested_PersistsOverrideAndRebuildsController()
    {
        var sut = CreateSut();
        sut.SourceId = "src-1";
        await sut.StartCommand.ExecuteAsync(null);

        sut.PtzEndpointForm.Host = "10.0.0.5";
        sut.PtzEndpointForm.PortText = "5678";
        sut.PtzEndpointForm.SaveCommand.Execute(null);

        _sourceRepoMock.Verify(
            r => r.SavePtzOverrideAsync("src-1", It.Is<PtzEndpoint>(e => e != null && e.Host == "10.0.0.5" && e.Port == 5678)),
            Times.Once);
        _ptzControllerFactoryMock.Verify(f => f.Create(It.IsAny<PtzEndpoint?>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task PtzStorePresetCommand_StoresAndSetsConfirmationThenClearsAfterDelay()
    {
        var sut = CreateSut();

        await sut.PtzStorePresetCommand.ExecuteAsync(3);

        _ptzControllerMock.Verify(c => c.StorePresetAsync(3, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("Preset 3 stored", sut.PtzPresetStatusMessage);

        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        Assert.Null(sut.PtzPresetStatusMessage);
    }

    [Fact]
    public async Task PtzRecallPresetCommand_Recalls()
    {
        var sut = CreateSut();

        await sut.PtzRecallPresetCommand.ExecuteAsync(5);

        _ptzControllerMock.Verify(c => c.RecallPresetAsync(5, It.IsAny<float>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void IsPtzControlActive_TrueWhenPtzSupported()
    {
        var sut = CreateSut();

        _bridgeMock.SetupGet(b => b.IsPtzSupported).Returns(true);
        _bridgeMock.Raise(b => b.ConnectionStateChanged += null, _bridgeMock.Object, ConnectionState.Connected);

        Assert.True(sut.IsPtzControlActive);
    }

    [Fact]
    public void IsPtzControlActive_FalseWhenNoPtzSupportAndNoOverride()
    {
        var sut = CreateSut();

        Assert.False(sut.IsPtzControlActive);
    }

    [Fact]
    public void PresetNumbers_IsOneToEight()
    {
        Assert.Equal(Enumerable.Range(1, 8), ViewerViewModel.PresetNumbers);
    }
}
