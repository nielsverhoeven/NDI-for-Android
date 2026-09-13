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
    private readonly Mock<IScreenReaderAnnouncer> _announcerMock = new();
    private readonly Mock<IOrientationLockService> _orientationLockMock = new();

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
        _bridgeMock
            .Setup(b => b.StopReceiverAsync())
            .Returns(Task.CompletedTask);
    }

    private ViewerViewModel CreateSut() => new(
        _bridgeMock.Object, _timeProvider, _dispatcher, _appStateRepoMock.Object, _lifecycleMock.Object,
        _sourceRepoMock.Object, _connectionHistoryMock.Object,
        _ptzControllerFactoryMock.Object, new PtzEndpointFormViewModel(_ptzControllerFactoryMock.Object),
        _immersiveModeMock.Object, _announcerMock.Object, _orientationLockMock.Object);

    /// <summary>Started on "src-1" (all mocked awaits complete synchronously); bridge call log
    /// cleared so tests count only the traffic they generate from here.</summary>
    private ViewerViewModel CreatePlayingSut()
    {
        var sut = CreateSut();
        sut.SourceId = "src-1";
        Assert.True(sut.IsPlaying);
        _bridgeMock.Invocations.Clear();
        return sut;
    }

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

        _bridgeMock.Verify(b => b.StopReceiverAsync(), Times.Once);
        Assert.False(sut.IsPlaying);
        Assert.Equal("Stopped.", sut.StatusMessage);
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
        _bridgeMock.Setup(b => b.GetLastStopReason()).Returns(ReceiverStopReason.ConnectionLost);

        var sut = CreatePlayingSut();

        sut.CheckForUnexpectedDrop();

        Assert.True(sut.IsReconnecting);
    }

    [Fact]
    public void CheckForUnexpectedDrop_WhenUserStop_DoesNotBeginReconnectWindow()
    {
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Disconnected);
        _bridgeMock.Setup(b => b.GetLastStopReason()).Returns(ReceiverStopReason.ConnectionLost);

        var sut = CreatePlayingSut();
        sut.StopCommand.Execute(null);
        sut.IsPlaying = true; // re-arm every other term, so _userInitiatedStop is the only one that can reject

        sut.CheckForUnexpectedDrop();

        Assert.False(sut.IsReconnecting);
    }

    [Fact]
    public void OnBridgeConnectionStateChanged_Disconnected_TriggersCheckForUnexpectedDrop()
    {
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Disconnected);
        _bridgeMock.Setup(b => b.GetLastStopReason()).Returns(ReceiverStopReason.ConnectionLost);
        var sut = CreatePlayingSut();

        _bridgeMock.Raise(b => b.ConnectionStateChanged += null, _bridgeMock.Object, ConnectionState.Disconnected);

        Assert.True(sut.IsReconnecting);
    }

    [Fact]
    public void OnBridgeConnectionStateChanged_DisconnectedWithIntentionalReason_DoesNotBeginReconnectWindow()
    {
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Disconnected);
        _bridgeMock.Setup(b => b.GetLastStopReason()).Returns(ReceiverStopReason.Intentional);
        var sut = CreatePlayingSut();

        _bridgeMock.Raise(b => b.ConnectionStateChanged += null, _bridgeMock.Object, ConnectionState.Disconnected);

        Assert.False(sut.IsReconnecting);
    }

    [Fact]
    public void CheckForUnexpectedDrop_WhenLastStopReasonIsIntentional_DoesNotBeginReconnectWindow()
    {
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Disconnected);
        _bridgeMock.Setup(b => b.GetLastStopReason()).Returns(ReceiverStopReason.Intentional);
        var sut = CreatePlayingSut();

        sut.CheckForUnexpectedDrop();

        Assert.False(sut.IsReconnecting);
    }

    [Fact]
    public void OnBridgeConnectionStateChanged_ConnectedDuringReconnectWindow_CompletesTheWindow()
    {
        var sut = CreatePlayingSut(); // GetConnectionState left un-setup -> the attempt loop can never self-complete
        sut.BeginReconnectWindow();
        Assert.True(sut.IsReconnecting);

        // The receiver really has connected by the time the event is delivered; CompleteReconnect
        // re-reads the bridge and refuses to declare success against a Connecting one.
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Connected);

        _bridgeMock.Raise(b => b.ConnectionStateChanged += null, _bridgeMock.Object, ConnectionState.Connected);

        Assert.False(sut.IsReconnecting);
        Assert.True(sut.IsPlaying);
        Assert.Equal("Connected.", sut.StatusMessage);
        Assert.Null(sut.RetryStatusMessage);

        _timeProvider.Advance(TimeSpan.FromSeconds(30));
        _bridgeMock.Verify(b => b.StartReceiver(It.IsAny<string>(), It.IsAny<QualityProfile>()), Times.Never);
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
        Assert.Equal("Reconnection cancelled.", sut.StatusMessage);
        Assert.Null(sut.RetryStatusMessage);
        Assert.True(sut.CanReconnect);
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

    // ── #350: TalkBack announcement redundant with the "ON PROGRAM" badge ──────

    [Fact]
    public void TallyEcho_ProgramOn_WhilePlaying_AnnouncesOnProgram()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;

        _bridgeMock.Raise(b => b.TallyEchoChanged += null, _bridgeMock.Object, new NdiTallyEcho(OnProgram: true, OnPreview: false));

        _announcerMock.Verify(a => a.Announce(ViewerViewModel.TallyOnProgramAnnouncement), Times.Once);
        Assert.True(sut.IsTallyProgram);
    }

    [Fact]
    public void TallyEcho_ProgramOff_WhilePlaying_AnnouncesOffProgram()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;

        _bridgeMock.Raise(b => b.TallyEchoChanged += null, _bridgeMock.Object, new NdiTallyEcho(OnProgram: true, OnPreview: false));
        _bridgeMock.Raise(b => b.TallyEchoChanged += null, _bridgeMock.Object, new NdiTallyEcho(OnProgram: false, OnPreview: false));

        _announcerMock.Verify(a => a.Announce(ViewerViewModel.TallyOffProgramAnnouncement), Times.Once);
        Assert.False(sut.IsTallyProgram);
    }

    [Fact]
    public void TallyEcho_RepeatedSameState_AnnouncesOnce()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;

        _bridgeMock.Raise(b => b.TallyEchoChanged += null, _bridgeMock.Object, new NdiTallyEcho(OnProgram: true, OnPreview: false));
        _bridgeMock.Raise(b => b.TallyEchoChanged += null, _bridgeMock.Object, new NdiTallyEcho(OnProgram: true, OnPreview: false));

        _announcerMock.Verify(a => a.Announce(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void TallyEcho_WhenNotPlaying_DoesNotAnnounce()
    {
        var sut = CreateSut();

        _bridgeMock.Raise(b => b.TallyEchoChanged += null, _bridgeMock.Object, new NdiTallyEcho(OnProgram: true, OnPreview: false));

        Assert.True(sut.IsTallyProgram);
        _announcerMock.Verify(a => a.Announce(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void StopCommand_DoesNotAnnounceOffProgram()
    {
        var sut = CreateSut();
        sut.SourceId = "192.168.1.10:5961";
        _bridgeMock.Raise(b => b.TallyEchoChanged += null, _bridgeMock.Object, new NdiTallyEcho(OnProgram: true, OnPreview: false));
        _announcerMock.Invocations.Clear();

        sut.StopCommand.Execute(null);

        Assert.False(sut.IsTallyProgram);
        _announcerMock.Verify(a => a.Announce(It.IsAny<string>()), Times.Never);
    }

    // ── #348: explicit stopped state (Nielsen #1 — visibility of system status) ──

    [Fact]
    public void StopCommand_ShowsStoppedStatusAndClearsPlaying()
    {
        var sut = CreateSut();
        sut.SourceId = "src-1";

        sut.StopCommand.Execute(null);

        Assert.False(sut.IsPlaying);
        Assert.Equal("Stopped.", sut.StatusMessage);
        Assert.True(sut.IsStopped);
    }

    [Fact]
    public void StopCommand_LeavesAVisibleActionableControl()
    {
        var sut = CreateSut();
        sut.SourceId = "src-1";

        sut.StopCommand.Execute(null);

        // Reused Reconnect affordance (#348): the screen is never left with neither status text
        // nor a control.
        Assert.True(sut.CanReconnect);
    }

    [Fact]
    public void StartCommand_ClearsStoppedStateAndCanReconnect()
    {
        var sut = CreateSut();
        sut.SourceId = "src-1";
        sut.StopCommand.Execute(null);
        Assert.True(sut.IsStopped);
        Assert.True(sut.CanReconnect);

        sut.SourceId = null;
        sut.SourceId = "src-1"; // re-runs Start

        Assert.False(sut.IsStopped);
        Assert.False(sut.CanReconnect);
        Assert.True(sut.IsPlaying);
    }

    [Fact]
    public void ReconnectCommand_AfterStop_ClearsStoppedStateOnceConnected()
    {
        var sut = CreatePlayingSut();
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Connected);
        sut.StopCommand.Execute(null);
        Assert.True(sut.IsStopped);

        sut.ReconnectCommand.Execute(null);
        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        Assert.False(sut.IsStopped);
        Assert.True(sut.IsPlaying);
    }

    [Fact]
    public void IsStopped_IsFalseWhileNeverStarted()
    {
        var sut = CreateSut();

        Assert.False(sut.IsStopped);
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
    public async Task PtzNudge_BurstsThenStopsAfter250Ms(string direction, float expectedPan, float expectedTilt)
    {
        var sut = CreateSut();

        var nudge = sut.PtzNudgeCommand.ExecuteAsync(direction);

        _ptzControllerMock.Verify(c => c.PanTiltAsync(expectedPan, expectedTilt, It.IsAny<CancellationToken>()), Times.Once);
        _ptzControllerMock.Verify(c => c.PanTiltAsync(0f, 0f, It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(nudge.IsCompleted); // parked on Task.Delay(..., _timeProvider)

        _timeProvider.Advance(TimeSpan.FromMilliseconds(250));
        await nudge;

        _ptzControllerMock.Verify(c => c.PanTiltAsync(0f, 0f, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PtzZoomNudge_In_BurstsThenStopsAfter250Ms()
    {
        var sut = CreateSut();

        var nudge = sut.PtzZoomNudgeCommand.ExecuteAsync("in");

        _ptzControllerMock.Verify(c => c.ZoomAsync(0.5f, It.IsAny<CancellationToken>()), Times.Once);
        _ptzControllerMock.Verify(c => c.ZoomAsync(0f, It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(nudge.IsCompleted);

        _timeProvider.Advance(TimeSpan.FromMilliseconds(250));
        await nudge;

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

    // --- Quality profile selection (#330/#331): data-driven strip, no auto-degradation ---

    [Fact]
    public void AvailableProfiles_ContainsEveryEnumValueInOrder()
    {
        var sut = CreateSut();

        Assert.Equal(Enum.GetValues<QualityProfile>(), sut.AvailableProfiles.Select(o => o.Profile));
    }

    [Fact]
    public void AvailableProfiles_DefaultSelection_IsBalanced()
    {
        var sut = CreateSut();

        var selected = sut.AvailableProfiles.Where(o => o.IsSelected).ToList();
        Assert.Single(selected);
        Assert.Equal(QualityProfile.Balanced, selected[0].Profile);
    }

    [Fact]
    public async Task ChangeQualityProfileCommand_WithEnumParameter_SelectsAndForwardsToBridge()
    {
        var sut = CreateSut();

        await sut.ChangeQualityProfileCommand.ExecuteAsync(QualityProfile.High);

        Assert.Equal(QualityProfile.High, sut.QualityProfile);
        _bridgeMock.Verify(b => b.SetQualityProfile(QualityProfile.High), Times.Once);
        Assert.True(sut.AvailableProfiles.Single(o => o.Profile == QualityProfile.High).IsSelected);
        Assert.All(sut.AvailableProfiles.Where(o => o.Profile != QualityProfile.High), o => Assert.False(o.IsSelected));
    }

    [Fact]
    public async Task ChangeQualityProfileCommand_WithOptionParameter_Selects()
    {
        var sut = CreateSut();

        await sut.ChangeQualityProfileCommand.ExecuteAsync(sut.AvailableProfiles[0]);

        Assert.Equal(QualityProfile.Smooth, sut.QualityProfile);
    }

    [Fact]
    public async Task ChangeQualityProfileCommand_WithStringParameter_StillWorks()
    {
        var sut = CreateSut();

        await sut.ChangeQualityProfileCommand.ExecuteAsync("smooth");

        Assert.Equal(QualityProfile.Smooth, sut.QualityProfile);
    }

    [Fact]
    public async Task ChangeQualityProfileCommand_WithUnknownParameter_IsIgnored()
    {
        var sut = CreateSut();

        await sut.ChangeQualityProfileCommand.ExecuteAsync(42);

        Assert.Equal(QualityProfile.Balanced, sut.QualityProfile);
        _bridgeMock.Verify(b => b.SetQualityProfile(It.IsAny<QualityProfile>()), Times.Never);
    }

    [Fact]
    public async Task ChangeQualityProfileCommand_PersistsProfileToCachedSource()
    {
        _sourceRepoMock.Setup(r => r.GetCachedSourcesAsync())
            .ReturnsAsync(new List<NdiSource> { new("src-1", "Cam 1", "192.168.1.10", true, 0) });
        var sut = CreateSut();
        sut.SourceId = "src-1";

        await sut.ChangeQualityProfileCommand.ExecuteAsync(QualityProfile.High);

        _sourceRepoMock.Verify(r => r.SaveSourceAsync(It.Is<NdiSource>(s => s.QualityProfile == QualityProfile.High)), Times.Once);
    }

    [Fact]
    public async Task QualityProfileLabel_IsNullWhileIdle_AndNamesProfileWhilePlaying()
    {
        var sut = CreateSut();
        Assert.Null(sut.QualityProfileLabel);

        sut.SourceId = "src-1";
        Assert.Equal("Quality: Balanced", sut.QualityProfileLabel);

        await sut.ChangeQualityProfileCommand.ExecuteAsync(QualityProfile.Smooth);
        Assert.Equal("Quality: Smooth", sut.QualityProfileLabel);
    }

    [Fact]
    public async Task QualityProfileLabel_RaisesPropertyChanged_WhenProfileOrPlayingChanges()
    {
        var sut = CreateSut();
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.IsPlaying = true;
        await sut.ChangeQualityProfileCommand.ExecuteAsync(QualityProfile.High);

        Assert.Contains(nameof(ViewerViewModel.QualityProfileLabel), raised);
    }

    [Fact]
    public void NextQualityProfile_WrapsAround()
    {
        var sut = CreateSut();

        Assert.Equal(QualityProfile.High, sut.NextQualityProfile);

        sut.QualityProfile = QualityProfile.High;
        Assert.Equal(QualityProfile.Smooth, sut.NextQualityProfile);
    }

    [Fact]
    public void CycleQualityProfileCommand_AdvancesAndForwardsToBridge()
    {
        var sut = CreateSut();

        sut.CycleQualityProfileCommand.Execute(null);

        Assert.Equal(QualityProfile.High, sut.QualityProfile);
        _bridgeMock.Verify(b => b.SetQualityProfile(QualityProfile.High), Times.Once);
        Assert.Equal("H", sut.QualityProfileShortLabel);
        Assert.Equal("Quality High. Activate for Smooth.", sut.QualityProfileCycleDescription);
    }

    [Fact]
    public void StatusMessages_NoLongerEmbedProfile()
    {
        var sut = CreateSut();

        sut.SourceId = "src-1";

        Assert.Equal("Connecting...", sut.StatusMessage);
    }

    // --- Reconnect timing (#332): every timer runs on the injected TimeProvider ---

    [Fact]
    public async Task ReconnectAttempt_UsesTheCurrentlySelectedQualityProfile()
    {
        var sut = CreatePlayingSut();
        await sut.ChangeQualityProfileCommand.ExecuteAsync("High");
        _bridgeMock.Invocations.Clear();

        sut.BeginReconnectWindow();
        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        _bridgeMock.Verify(b => b.StopReceiverAsync(), Times.Once);
        _bridgeMock.Verify(b => b.StartReceiver("src-1", QualityProfile.High), Times.Once);
        _bridgeMock.Verify(b => b.StartReceiver("src-1", QualityProfile.Balanced), Times.Never);
    }

    [Fact]
    public void ReconnectWindow_CountsDownOneSecondPerTick()
    {
        var sut = CreatePlayingSut();
        sut.BeginReconnectWindow();

        _timeProvider.Advance(TimeSpan.FromMilliseconds(999));
        Assert.Equal(15, sut.RetryRemainingSeconds);
        Assert.Equal("Reconnecting... 15s remaining", sut.RetryStatusMessage);

        _timeProvider.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(14, sut.RetryRemainingSeconds);
        Assert.Equal("Reconnecting... 14s remaining", sut.RetryStatusMessage);

        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(13, sut.RetryRemainingSeconds);
        Assert.True(sut.IsReconnecting);
    }

    [Fact]
    public void ReconnectWindow_WhenAttemptConnects_CompletesAndStopsAllTimers()
    {
        var sut = CreatePlayingSut();
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Connected);

        sut.BeginReconnectWindow();
        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        Assert.False(sut.IsReconnecting);
        Assert.True(sut.IsPlaying);
        Assert.Equal(0, sut.RetryRemainingSeconds);
        Assert.Null(sut.RetryStatusMessage);

        _timeProvider.Advance(TimeSpan.FromSeconds(30));

        // The bridge was already Connected when the tick fired, so the attempt completes the window
        // instead of destroying a healthy receiver.
        _bridgeMock.Verify(b => b.StartReceiver(It.IsAny<string>(), It.IsAny<QualityProfile>()), Times.Never);
        _bridgeMock.Verify(b => b.StopReceiverAsync(), Times.Never);
        Assert.Equal(0, sut.RetryRemainingSeconds);
    }

    [Fact]
    public void ReconnectWindow_AfterFifteenSeconds_FailsAndStopsAllTimers()
    {
        var sut = CreatePlayingSut(); // GetConnectionState left un-setup -> default Connecting, attempts never succeed

        sut.BeginReconnectWindow();
        _timeProvider.Advance(TimeSpan.FromSeconds(15));

        Assert.False(sut.IsReconnecting);
        Assert.False(sut.IsPlaying);
        Assert.True(sut.CanReconnect);
        Assert.Equal(0, sut.RetryRemainingSeconds);
        Assert.Null(sut.RetryStatusMessage);
        Assert.Equal("Connection lost. Reconnection failed.", sut.StatusMessage);
        Assert.True(sut.IsStopped);
        _bridgeMock.Verify(b => b.StartReceiver("src-1", QualityProfile.Balanced), Times.Exactly(7));
        _bridgeMock.Verify(b => b.StopReceiverAsync(), Times.Exactly(8)); // 7 attempts + the terminal stop

        _timeProvider.Advance(TimeSpan.FromSeconds(30));
        _bridgeMock.Verify(b => b.StartReceiver("src-1", QualityProfile.Balanced), Times.Exactly(7));
    }

    [Fact]
    public void RunAttempt_DoesNotPollConnectionStateAfterStartingTheReceiver()
    {
        var sut = CreatePlayingSut();
        sut.BeginReconnectWindow();
        _bridgeMock.Invocations.Clear();

        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        _bridgeMock.Verify(b => b.StopReceiverAsync(), Times.Once);
        _bridgeMock.Verify(b => b.StartReceiver("src-1", QualityProfile.Balanced), Times.Once);

        // The post-start poll was deleted: StartReceiver's next invocation on the mock must be the
        // ReceiverGeneration read RunAttempt does immediately after it, never GetConnectionState —
        // nothing can interleave between two adjacent statements of the same synchronous method.
        // (The connection-hint stats watchdog also polls GetConnectionState once a second, so a
        // raw call count over this 2s window is not a useful assertion on its own.)
        var invocations = _bridgeMock.Invocations.ToList();
        var startIndex = invocations.FindIndex(i => i.Method.Name == nameof(INdiViewerBridge.StartReceiver));
        Assert.True(startIndex >= 0, "StartReceiver was not invoked.");
        Assert.True(startIndex + 1 < invocations.Count, "No invocation followed StartReceiver.");
        Assert.Equal("get_ReceiverGeneration", invocations[startIndex + 1].Method.Name);
    }

    [Fact]
    public void RunAttempt_RequestsTheStopBeforeTheStart()
    {
        var sut = CreatePlayingSut();
        var order = new List<string>();
        _bridgeMock.Setup(b => b.StopReceiverAsync()).Callback(() => order.Add("StopReceiverAsync")).Returns(Task.CompletedTask);
        _bridgeMock.Setup(b => b.StartReceiver(It.IsAny<string>(), It.IsAny<QualityProfile>())).Callback(() => order.Add("StartReceiver"));

        sut.BeginReconnectWindow();
        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(new[] { "StopReceiverAsync", "StartReceiver" }, order);
    }

    [Fact]
    public void StopCommand_DoesNotWaitForTheBridgeTeardown()
    {
        // A teardown that never finishes: if any call site awaits it, this test hangs or the
        // assertions below fail.
        var neverCompletes = new TaskCompletionSource().Task;
        _bridgeMock.Setup(b => b.StopReceiverAsync()).Returns(neverCompletes);
        var sut = CreatePlayingSut();

        sut.StopCommand.Execute(null);

        Assert.False(sut.IsPlaying);
        Assert.True(sut.IsStopped);
        Assert.Equal("Stopped.", sut.StatusMessage);
    }

    [Fact]
    public void CancelRetryCommand_DoesNotWaitForTheBridgeTeardown()
    {
        var neverCompletes = new TaskCompletionSource().Task;
        _bridgeMock.Setup(b => b.StopReceiverAsync()).Returns(neverCompletes);
        var sut = CreatePlayingSut();
        sut.BeginReconnectWindow();

        sut.CancelRetryCommand.Execute(null);

        Assert.Equal("Reconnection cancelled.", sut.StatusMessage);
        Assert.True(sut.IsStopped);
    }

    [Fact]
    public void FailReconnect_DoesNotWaitForTheBridgeTeardown()
    {
        var neverCompletes = new TaskCompletionSource().Task;
        _bridgeMock.Setup(b => b.StopReceiverAsync()).Returns(neverCompletes);
        var sut = CreatePlayingSut();

        sut.BeginReconnectWindow();
        _timeProvider.Advance(TimeSpan.FromSeconds(15));

        Assert.Equal("Connection lost. Reconnection failed.", sut.StatusMessage);
        Assert.True(sut.CanReconnect);
    }

    [Fact]
    public void Dispose_DoesNotWaitForTheBridgeTeardown()
    {
        var neverCompletes = new TaskCompletionSource().Task;
        _bridgeMock.Setup(b => b.StopReceiverAsync()).Returns(neverCompletes);
        var sut = CreatePlayingSut();

        Assert.Null(Record.Exception(() => sut.Dispose()));
    }

    [Fact]
    public void CancelRetry_StopsTimers()
    {
        var sut = CreatePlayingSut();
        sut.BeginReconnectWindow();
        _timeProvider.Advance(TimeSpan.FromSeconds(1));

        sut.CancelRetryCommand.Execute(null);
        _timeProvider.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(15, sut.RetryRemainingSeconds);
        Assert.Equal("Reconnection cancelled.", sut.StatusMessage);
        Assert.False(sut.IsReconnecting);
        _bridgeMock.Verify(b => b.StartReceiver(It.IsAny<string>(), It.IsAny<QualityProfile>()), Times.Never);
    }

    [Fact]
    public void Stop_DuringReconnectWindow_StopsTimers()
    {
        var sut = CreatePlayingSut();
        sut.BeginReconnectWindow();
        _timeProvider.Advance(TimeSpan.FromSeconds(3)); // one attempt has run
        _bridgeMock.Invocations.Clear();

        sut.StopCommand.Execute(null);
        _timeProvider.Advance(TimeSpan.FromSeconds(30));

        Assert.False(sut.IsReconnecting);
        Assert.Equal(15, sut.RetryRemainingSeconds);
        _bridgeMock.Verify(b => b.StartReceiver(It.IsAny<string>(), It.IsAny<QualityProfile>()), Times.Never);
    }

    [Fact]
    public void Dispose_DuringReconnectWindow_StopsTimersWithoutThrowing()
    {
        var sut = CreatePlayingSut();
        sut.BeginReconnectWindow();

        Assert.Null(Record.Exception(() => sut.Dispose()));
        _timeProvider.Advance(TimeSpan.FromSeconds(30));

        _bridgeMock.Verify(b => b.StartReceiver(It.IsAny<string>(), It.IsAny<QualityProfile>()), Times.Never);
        Assert.Equal(15, sut.RetryRemainingSeconds);
    }

    [Fact]
    public void ReconnectCommand_AfterWindowExpired_OpensNewWindow()
    {
        var sut = CreatePlayingSut();
        sut.BeginReconnectWindow();
        _timeProvider.Advance(TimeSpan.FromSeconds(15));
        Assert.True(sut.CanReconnect);
        _bridgeMock.Invocations.Clear();

        sut.ReconnectCommand.Execute(null);

        Assert.True(sut.IsReconnecting);
        Assert.False(sut.CanReconnect);
        Assert.Equal(15, sut.RetryRemainingSeconds);

        _timeProvider.Advance(TimeSpan.FromSeconds(2));
        _bridgeMock.Verify(b => b.StartReceiver("src-1", QualityProfile.Balanced), Times.Once);
    }

    [Fact]
    public void CheckForUnexpectedDrop_AfterSuccessfulReconnect_OpensNewWindow()
    {
        var sut = CreatePlayingSut();
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Connected);
        sut.BeginReconnectWindow();
        _timeProvider.Advance(TimeSpan.FromSeconds(2));
        Assert.False(sut.IsReconnecting);

        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Disconnected);
        _bridgeMock.Setup(b => b.GetLastStopReason()).Returns(ReceiverStopReason.ConnectionLost);
        sut.CheckForUnexpectedDrop();

        Assert.True(sut.IsReconnecting);
        Assert.Equal(15, sut.RetryRemainingSeconds);
    }

    [Fact]
    public void StopThenRestart_AfterFailedWindow_AllowsDropDetectionAgain()
    {
        var sut = CreatePlayingSut();
        sut.BeginReconnectWindow();
        _timeProvider.Advance(TimeSpan.FromSeconds(15));

        sut.StopCommand.Execute(null);
        sut.SourceId = null;
        sut.SourceId = "src-1"; // re-runs Start

        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Disconnected);
        _bridgeMock.Setup(b => b.GetLastStopReason()).Returns(ReceiverStopReason.ConnectionLost);
        sut.CheckForUnexpectedDrop();

        Assert.True(sut.IsReconnecting);
    }

    [Fact]
    public void CheckForUnexpectedDrop_WhenAnotherViewerHasTakenOverTheBridge_DoesNotBeginReconnectWindow()
    {
        // Models the real bridge: every StartReceiver bumps the ownership token.
        var generation = 0L;
        _bridgeMock.Setup(b => b.ReceiverGeneration).Returns(() => generation);
        _bridgeMock.Setup(b => b.StartReceiver(It.IsAny<string>(), It.IsAny<QualityProfile>()))
                   .Callback(() => generation++);
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Disconnected);
        _bridgeMock.Setup(b => b.GetLastStopReason()).Returns(ReceiverStopReason.ConnectionLost);

        var stale = CreatePlayingSut();   // the Expanded PaneViewer: still subscribed, still IsPlaying
        var current = CreatePlayingSut(); // a pushed ViewerPage takes the bridge over

        _bridgeMock.Raise(b => b.ConnectionStateChanged += null, _bridgeMock.Object, ConnectionState.Disconnected);

        Assert.False(stale.IsReconnecting);
        Assert.True(current.IsReconnecting);
    }

    [Fact]
    public void CancelRetry_StopsTheReceiverAndIsNotUndoneByTheNextDrop()
    {
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Disconnected);
        _bridgeMock.Setup(b => b.GetLastStopReason()).Returns(ReceiverStopReason.ConnectionLost);
        var sut = CreatePlayingSut();
        sut.BeginReconnectWindow();

        sut.CancelRetryCommand.Execute(null);

        Assert.False(sut.IsReconnecting);
        Assert.True(sut.IsStopped);
        Assert.True(sut.CanReconnect);
        _bridgeMock.Verify(b => b.StopReceiverAsync(), Times.AtLeastOnce);

        sut.IsPlaying = true; // the only guard term the sticky user-stop flag has to beat
        _bridgeMock.Raise(b => b.ConnectionStateChanged += null, _bridgeMock.Object, ConnectionState.Disconnected);

        Assert.False(sut.IsReconnecting);
    }

    [Fact]
    public void OnBridgeConnectionStateChanged_StaleConnectedAgainstAConnectingBridge_DoesNotCompleteTheWindow()
    {
        var sut = CreatePlayingSut(); // GetConnectionState left un-setup -> Moq default Connecting
        sut.BeginReconnectWindow();

        _bridgeMock.Raise(b => b.ConnectionStateChanged += null, _bridgeMock.Object, ConnectionState.Connected);

        Assert.True(sut.IsReconnecting);
    }

    [Fact]
    public void ReconnectCommand_AfterAStop_ClearsTheStoppedSurface()
    {
        var sut = CreatePlayingSut();
        sut.StopCommand.Execute(null);
        Assert.True(sut.IsStopped);

        sut.ReconnectCommand.Execute(null);

        Assert.False(sut.IsStopped);
        Assert.True(sut.IsReconnecting);
    }

    [Fact]
    public void SustainedConnecting_AfterHavingBeenConnected_OpensAReconnectWindow()
    {
        var sut = CreatePlayingSut();
        _bridgeMock.Raise(b => b.ConnectionStateChanged += null, _bridgeMock.Object, ConnectionState.Connected);
        // A restart superseded the drop: the bridge is stuck in Connecting and will never report it again.
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Connecting);

        _timeProvider.Advance(TimeSpan.FromSeconds(4));
        Assert.False(sut.IsReconnecting);

        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        Assert.True(sut.IsReconnecting);
    }

    [Fact]
    public void SustainedConnecting_OnAnInitialConnectThatNeverSucceeded_DoesNotOpenAReconnectWindow()
    {
        var sut = CreatePlayingSut();
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Connecting);

        _timeProvider.Advance(TimeSpan.FromSeconds(30));

        Assert.False(sut.IsReconnecting);
        Assert.Equal("Connecting...", sut.StatusMessage);
    }

    [Fact]
    public void SustainedDisconnected_AfterAnIntentionalStop_DoesNotOpenAReconnectWindow()
    {
        var sut = CreatePlayingSut();
        _bridgeMock.Raise(b => b.ConnectionStateChanged += null, _bridgeMock.Object, ConnectionState.Connected);
        // The navigation handoff stopped the bridge behind this ViewModel's back (D6).
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Disconnected);
        _bridgeMock.Setup(b => b.GetLastStopReason()).Returns(ReceiverStopReason.Intentional);

        _timeProvider.Advance(TimeSpan.FromSeconds(30));

        Assert.False(sut.IsReconnecting);
    }

    [Fact]
    public void StatsSample_OnAViewModelTheBridgeWasTakenFrom_StopsClaimingToPlayWithoutTouchingTheReceiver()
    {
        var generation = 0L;
        _bridgeMock.Setup(b => b.ReceiverGeneration).Returns(() => generation);
        _bridgeMock.Setup(b => b.StartReceiver(It.IsAny<string>(), It.IsAny<QualityProfile>()))
                   .Callback(() => generation++);

        var pane = CreatePlayingSut();   // the Expanded PaneViewer: owns generation 1
        var pushed = CreatePlayingSut(); // a pushed ViewerPage takes the bridge: generation 2
        _bridgeMock.Invocations.Clear();

        pane.ApplyConnectionSample(connected: true, fps: 30f, dropPercent: 0f);

        Assert.False(pane.IsPlaying);
        Assert.True(pane.IsStopped);
        Assert.True(pane.CanReconnect);
        Assert.Equal("Stopped.", pane.StatusMessage);
        // The receiver belongs to the other ViewModel now — demoting must never stop it.
        _bridgeMock.Verify(b => b.StopReceiverAsync(), Times.Never);
        Assert.True(pushed.IsPlaying);
    }

    [Fact]
    public void Dispose_WhenThisViewModelOwnsTheReceiver_HandsItBack()
    {
        var sut = CreatePlayingSut();

        sut.Dispose();

        _bridgeMock.Verify(b => b.StopReceiverAsync(), Times.Once);
    }

    [Fact]
    public void Dispose_WhenAnotherViewerOwnsTheReceiver_LeavesItRunning()
    {
        var generation = 0L;
        _bridgeMock.Setup(b => b.ReceiverGeneration).Returns(() => generation);
        _bridgeMock.Setup(b => b.StartReceiver(It.IsAny<string>(), It.IsAny<QualityProfile>()))
                   .Callback(() => generation++);
        var stale = CreatePlayingSut();
        var current = CreatePlayingSut();
        _bridgeMock.Invocations.Clear();

        stale.Dispose();

        _bridgeMock.Verify(b => b.StopReceiverAsync(), Times.Never);
        Assert.True(current.IsPlaying);
    }

    [Fact]
    public async Task ChangeQualityProfile_OnAViewModelThatDoesNotOwnTheReceiver_DoesNotClaimOwnership()
    {
        var generation = 0L;
        _bridgeMock.Setup(b => b.ReceiverGeneration).Returns(() => generation);
        _bridgeMock.Setup(b => b.StartReceiver(It.IsAny<string>(), It.IsAny<QualityProfile>()))
                   .Callback(() => generation++);
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Disconnected);
        _bridgeMock.Setup(b => b.GetLastStopReason()).Returns(ReceiverStopReason.ConnectionLost);
        var stale = CreatePlayingSut();
        var current = CreatePlayingSut();

        // Balanced -> High maps to the same bandwidth tier, so the bridge neither restarts nor bumps
        // the token: an unconditional re-record would hand ownership back to the wrong ViewModel.
        await stale.ChangeQualityProfileCommand.ExecuteAsync(QualityProfile.High);
        stale.CheckForUnexpectedDrop();

        Assert.False(stale.IsReconnecting);
    }

    [Fact]
    public void OnBridgeConnectionStateChanged_ConnectedOnAViewModelTheBridgeWasTakenFrom_DoesNotOverwriteItsStatus()
    {
        var generation = 0L;
        _bridgeMock.Setup(b => b.ReceiverGeneration).Returns(() => generation);
        _bridgeMock.Setup(b => b.StartReceiver(It.IsAny<string>(), It.IsAny<QualityProfile>()))
                   .Callback(() => generation++);
        var stale = CreatePlayingSut();
        var current = CreatePlayingSut();
        Assert.Equal("Connecting...", stale.StatusMessage);

        _bridgeMock.Raise(b => b.ConnectionStateChanged += null, _bridgeMock.Object, ConnectionState.Connected);

        Assert.Equal("Connecting...", stale.StatusMessage); // the other ViewModel's connection
        Assert.Equal("Connected.", current.StatusMessage);
    }

    [Fact]
    public void SustainedStalled_OnAConnectedSourceThatStoppedSendingVideo_DoesNotOpenAReconnectWindow()
    {
        var sut = CreatePlayingSut();
        _bridgeMock.Raise(b => b.ConnectionStateChanged += null, _bridgeMock.Object, ConnectionState.Connected);
        // The sender is still connected but has sent no video for >3 s, so the bridge demotes to
        // Stalled, not Connecting. Recreating the receiver cannot make a sender send video.
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Stalled);

        _timeProvider.Advance(TimeSpan.FromSeconds(30));

        Assert.False(sut.IsReconnecting);
        Assert.True(sut.IsPlaying);
        _bridgeMock.Verify(b => b.StopReceiverAsync(), Times.Never);
    }

    [Fact]
    public void SustainedConnecting_CounterDoesNotSurviveAStop()
    {
        var sut = CreatePlayingSut();
        _bridgeMock.Raise(b => b.ConnectionStateChanged += null, _bridgeMock.Object, ConnectionState.Connected);
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Connecting);
        _timeProvider.Advance(TimeSpan.FromSeconds(4)); // counter reaches 4, one short of the threshold
        Assert.False(sut.IsReconnecting);

        sut.StopCommand.Execute(null);
        sut.SourceId = null;
        sut.SourceId = "src-1"; // re-runs Start, re-arming the watchdog
        _bridgeMock.Raise(b => b.ConnectionStateChanged += null, _bridgeMock.Object, ConnectionState.Connected);

        _timeProvider.Advance(TimeSpan.FromSeconds(4)); // four *fresh* samples must not be enough
        Assert.False(sut.IsReconnecting);

        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        Assert.True(sut.IsReconnecting);
    }
}
