using Moq;
using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
using NdiForAndroid.Features.ConnectionHistory.Services;
using NdiForAndroid.Features.DiagOverlay.Services;
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

/// <summary>#331: the 1 s stats watchdog only feeds ConnectionHint — it never changes the profile.</summary>
public class ViewerViewModelConnectionHintTests
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
    private readonly Mock<INetworkLinkService> _networkLinkMock = new();
    private readonly DiagnosticOverlayService _diagnostics = new();

    public ViewerViewModelConnectionHintTests()
    {
        _appStateRepoMock.Setup(r => r.RestoreStateAsync()).ReturnsAsync(AppStateSnapshot.Empty);
        _appStateRepoMock.Setup(r => r.SaveAsync(It.IsAny<AppStateSnapshot>())).Returns(Task.CompletedTask);
        _sourceRepoMock.Setup(r => r.GetCachedSourcesAsync()).ReturnsAsync(new List<NdiSource>());
        _ptzControllerFactoryMock.Setup(f => f.Create(It.IsAny<PtzEndpoint?>())).Returns(_ptzControllerMock.Object);
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Connected);
    }

    private ViewerViewModel CreateSut() => new(
        _bridgeMock.Object, _timeProvider, _dispatcher, _appStateRepoMock.Object, _lifecycleMock.Object,
        _sourceRepoMock.Object, _connectionHistoryMock.Object,
        _ptzControllerFactoryMock.Object, new PtzEndpointFormViewModel(_ptzControllerFactoryMock.Object),
        _immersiveModeMock.Object, _announcerMock.Object, _orientationLockMock.Object);

    private ViewerViewModel CreatePlayingSut()
    {
        var sut = CreateSut();
        sut.SourceId = "src-1";
        Assert.True(sut.IsPlaying);
        return sut;
    }

    private ViewerViewModel CreateSutWithLink(NetworkLinkSnapshot link)
    {
        _networkLinkMock.Setup(s => s.GetSnapshot()).Returns(link);
        return new(
            _bridgeMock.Object, _timeProvider, _dispatcher, _appStateRepoMock.Object, _lifecycleMock.Object,
            _sourceRepoMock.Object, _connectionHistoryMock.Object,
            _ptzControllerFactoryMock.Object, new PtzEndpointFormViewModel(_ptzControllerFactoryMock.Object),
            _immersiveModeMock.Object, _announcerMock.Object, _orientationLockMock.Object,
            _networkLinkMock.Object, _diagnostics);
    }

    private ViewerViewModel CreatePlayingSutWithLink(NetworkLinkSnapshot link)
    {
        var sut = CreateSutWithLink(link);
        sut.SourceId = "src-1";
        Assert.True(sut.IsPlaying);
        return sut;
    }

    private void SetStats(float fps, float dropPercent)
    {
        _bridgeMock.Setup(b => b.GetMeasuredFps()).Returns(fps);
        _bridgeMock.Setup(b => b.GetDroppedFramePercent()).Returns(dropPercent);
    }

    [Fact]
    public void Watchdog_SamplesOncePerSecondWhilePlaying()
    {
        var sut = CreatePlayingSut();

        _timeProvider.Advance(TimeSpan.FromSeconds(3));

        _bridgeMock.Verify(b => b.GetMeasuredFps(), Times.Exactly(3));
    }

    [Fact]
    public void Hint_AppearsAfterFiveWeakSeconds()
    {
        SetStats(5f, 40f);
        var sut = CreatePlayingSut();

        _timeProvider.Advance(TimeSpan.FromSeconds(4));
        Assert.Null(sut.ConnectionHint);

        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("Connection weak — try Smooth", sut.ConnectionHint);
    }

    [Fact]
    public async Task Hint_OnSmooth_OmitsSuggestion()
    {
        _sourceRepoMock.Setup(r => r.GetCachedSourcesAsync())
            .ReturnsAsync(new List<NdiSource> { new("src-1", "Cam 1", "192.168.1.10", true, 0, QualityProfile: QualityProfile.Smooth) });
        var sut = CreatePlayingSut();
        Assert.Equal(QualityProfile.Smooth, sut.QualityProfile);
        SetStats(5f, 40f);

        _timeProvider.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal("Connection weak", sut.ConnectionHint);
        await Task.CompletedTask;
    }

    [Fact]
    public void Hint_ClearsAfterFiveGoodSeconds()
    {
        SetStats(5f, 40f);
        var sut = CreatePlayingSut();
        _timeProvider.Advance(TimeSpan.FromSeconds(5));
        Assert.NotNull(sut.ConnectionHint);

        SetStats(30f, 0f);
        _timeProvider.Advance(TimeSpan.FromSeconds(4));
        Assert.NotNull(sut.ConnectionHint);

        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(sut.ConnectionHint);
    }

    [Fact]
    public void Hint_NeverChangesProfile_NoAutoDegradation()
    {
        SetStats(5f, 40f);
        var sut = CreatePlayingSut();
        _bridgeMock.Invocations.Clear();

        _timeProvider.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(QualityProfile.Balanced, sut.QualityProfile);
        _bridgeMock.Verify(b => b.SetQualityProfile(It.IsAny<QualityProfile>()), Times.Never);
        _sourceRepoMock.Verify(r => r.SaveSourceAsync(It.IsAny<NdiSource>()), Times.Never);
    }

    [Fact]
    public void Hint_StaysHidden_WhenBridgeDisconnected()
    {
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Disconnected);
        SetStats(5f, 40f);
        var sut = CreatePlayingSut();

        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        Assert.Null(sut.ConnectionHint);
    }

    [Fact]
    public void Hint_StaysHidden_WhenBridgeConnecting()
    {
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Connecting);
        SetStats(5f, 40f);
        var sut = CreatePlayingSut();

        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        Assert.Null(sut.ConnectionHint);
    }

    [Fact]
    public void Hint_StaysHidden_WhileReconnecting()
    {
        var sut = CreatePlayingSut();
        sut.BeginReconnectWindow();
        SetStats(5f, 40f);

        _timeProvider.Advance(TimeSpan.FromSeconds(5));

        Assert.Null(sut.ConnectionHint);
    }

    [Fact]
    public void Hint_ClearsOnStop_AndWatchdogStops()
    {
        SetStats(5f, 40f);
        var sut = CreatePlayingSut();
        _timeProvider.Advance(TimeSpan.FromSeconds(5));
        Assert.NotNull(sut.ConnectionHint);

        sut.StopCommand.Execute(null);
        Assert.Null(sut.ConnectionHint);

        var callsBefore = _bridgeMock.Invocations.Count(i => i.Method.Name == nameof(INdiViewerBridge.GetMeasuredFps));
        _timeProvider.Advance(TimeSpan.FromSeconds(5));
        var callsAfter = _bridgeMock.Invocations.Count(i => i.Method.Name == nameof(INdiViewerBridge.GetMeasuredFps));
        Assert.Equal(callsBefore, callsAfter);
    }

    [Fact]
    public async Task Hint_ClearsWhenUserChangesProfile()
    {
        SetStats(5f, 40f);
        var sut = CreatePlayingSut();
        _timeProvider.Advance(TimeSpan.FromSeconds(5));
        Assert.NotNull(sut.ConnectionHint);

        await sut.ChangeQualityProfileCommand.ExecuteAsync(QualityProfile.Smooth);
        Assert.Null(sut.ConnectionHint);

        _timeProvider.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal("Connection weak", sut.ConnectionHint);
    }

    [Fact]
    public void ApplyConnectionSample_CanBeDrivenDirectly()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;

        for (var i = 0; i < 5; i++)
            sut.ApplyConnectionSample(true, 5f, 40f);

        Assert.NotNull(sut.ConnectionHint);
    }

    [Fact]
    public void Dispose_DisposesStatsTimer()
    {
        var sut = CreatePlayingSut();

        sut.Dispose();
        var callsBefore = _bridgeMock.Invocations.Count(i => i.Method.Name == nameof(INdiViewerBridge.GetMeasuredFps));
        _timeProvider.Advance(TimeSpan.FromSeconds(5));
        var callsAfter = _bridgeMock.Invocations.Count(i => i.Method.Name == nameof(INdiViewerBridge.GetMeasuredFps));

        Assert.Equal(callsBefore, callsAfter);
    }

    [Fact]
    public void Reconnect_Success_RestartsEvaluationCleanly()
    {
        var sut = CreatePlayingSut();
        sut.BeginReconnectWindow();
        _timeProvider.Advance(TimeSpan.FromSeconds(2)); // CompleteReconnect fires (GetConnectionState == Connected)
        Assert.False(sut.IsReconnecting);
        Assert.Null(sut.ConnectionHint);

        SetStats(5f, 40f);
        _timeProvider.Advance(TimeSpan.FromSeconds(5));

        Assert.NotNull(sut.ConnectionHint);
    }

    [Fact]
    public void Hint_OnAWeak24GhzLink_NamesTheBandAndTheSignal()
    {
        SetStats(5f, 40f);
        var link = new NetworkLinkSnapshot(true, WifiBand.TwoPointFourGhz, -78, 6, WifiStandard.Unknown);
        var sut = CreatePlayingSutWithLink(link);

        _timeProvider.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal("2.4 GHz / weak signal — switch to Smooth", sut.ConnectionHint);
    }

    [Fact]
    public void Hint_OnAStrong24GhzLink_NamesTheBandWithoutClaimingAWeakSignal()
    {
        SetStats(5f, 40f);
        var link = new NetworkLinkSnapshot(true, WifiBand.TwoPointFourGhz, -40, 200, WifiStandard.Unknown);
        var sut = CreatePlayingSutWithLink(link);

        _timeProvider.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal("Connection weak on 2.4 GHz — try Smooth", sut.ConnectionHint);
    }

    [Fact]
    public void Hint_OnAnIdleSource_NeverBlamesTheRadio()
    {
        // The receiver is up and the sender is sending nothing: weak by the fps arm, 0 % drops. Even
        // on the -78 dBm / 6 Mbit/s link that produced #415 the copy must not name the radio — this
        // sample is no evidence at all about the radio, and "switch to Smooth" cannot help a source
        // that is not sending.
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Stalled);
        SetStats(0f, 0f);
        var link = new NetworkLinkSnapshot(true, WifiBand.TwoPointFourGhz, -78, 6, WifiStandard.Unknown);
        var sut = CreatePlayingSutWithLink(link);

        _timeProvider.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal("Connection weak — try Smooth", sut.ConnectionHint);
    }

    [Fact]
    public void Hint_OnAStrong5GhzLink_KeepsTheGenericCopy()
    {
        SetStats(5f, 40f);
        var link = new NetworkLinkSnapshot(true, WifiBand.FiveGhz, -45, 400, WifiStandard.Unknown);
        var sut = CreatePlayingSutWithLink(link);

        _timeProvider.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal("Connection weak — try Smooth", sut.ConnectionHint);
    }

    [Fact]
    public void Hint_SurvivesAStalledSample()
    {
        SetStats(5f, 40f);
        var sut = CreatePlayingSut();
        _timeProvider.Advance(TimeSpan.FromSeconds(5));
        Assert.NotNull(sut.ConnectionHint);

        // A starved link demotes Connected -> Stalled every few seconds; before this change that
        // reset the run and wiped the hint, so it could never stay up on the link it exists for.
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Stalled);
        _timeProvider.Advance(TimeSpan.FromSeconds(3));

        Assert.NotNull(sut.ConnectionHint);
    }

    [Fact]
    public void Hint_AccumulatesAcrossStalledSamples()
    {
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Stalled);
        SetStats(0f, 0f);
        var sut = CreatePlayingSut();

        _timeProvider.Advance(TimeSpan.FromSeconds(5));

        Assert.NotNull(sut.ConnectionHint);
    }

    [Fact]
    public void Link_LogsOneDiagnosticEntryPerChange_NotPerSample()
    {
        SetStats(30f, 0f);
        var link24 = new NetworkLinkSnapshot(true, WifiBand.TwoPointFourGhz, -40, 200, WifiStandard.Unknown);
        CreatePlayingSutWithLink(link24);
        _timeProvider.Advance(TimeSpan.FromSeconds(5));

        var link5 = new NetworkLinkSnapshot(true, WifiBand.FiveGhz, -40, 400, WifiStandard.Unknown);
        _networkLinkMock.Setup(s => s.GetSnapshot()).Returns(link5);
        _timeProvider.Advance(TimeSpan.FromSeconds(5));

        var linkEntries = _diagnostics.LogBuffer.GetEntries().Where(e => e.Category == "Link").ToList();
        Assert.Equal(2, linkEntries.Count);
    }

    [Fact]
    public void Link_NeverChangesTheProfile()
    {
        SetStats(5f, 40f);
        var link = new NetworkLinkSnapshot(true, WifiBand.TwoPointFourGhz, -40, 200, WifiStandard.Unknown);
        var sut = CreatePlayingSutWithLink(link);
        _bridgeMock.Invocations.Clear();

        _timeProvider.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(QualityProfile.Balanced, sut.QualityProfile);
        _bridgeMock.Verify(b => b.SetQualityProfile(It.IsAny<QualityProfile>()), Times.Never);
    }

    [Fact]
    public void Hint_ShowsThenClears_OnASpikyRecoveringLink()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;

        foreach (var drop in new[] { 35f, 35f, 35f, 35f, 35f })
            sut.ApplyConnectionSample(true, 30f, drop);
        Assert.NotNull(sut.ConnectionHint);

        // ~7 % average with single seconds in the 10-30 % dead band: the hint must go away.
        foreach (var drop in new[] { 4f, 14f, 6f, 3f, 12f, 5f, 11f, 4f })
            sut.ApplyConnectionSample(true, 30f, drop);

        Assert.Null(sut.ConnectionHint);
    }

    [Fact]
    public void Hint_DoesNotFlap_OnAJitteryButFineLink()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;

        for (var i = 0; i < 40; i++)
            sut.ApplyConnectionSample(true, 30f, i % 2 == 0 ? 35f : 14f);

        Assert.Null(sut.ConnectionHint);
    }

    [Fact]
    public void Link_DiagnosticEntry_UsesTheRedactionSafeFormat()
    {
        SetStats(30f, 0f);
        var link = new NetworkLinkSnapshot(true, WifiBand.TwoPointFourGhz, -78, 6, WifiStandard.N);
        CreatePlayingSutWithLink(link);

        _timeProvider.Advance(TimeSpan.FromSeconds(1));

        var entry = Assert.Single(_diagnostics.LogBuffer.GetEntries(), e => e.Category == "Link");
        // No colon, and "2.4" is not four dot-separated groups: DiagnosticLogBuffer's IPv6 and IPv4
        // redaction patterns must leave this line intact. Asserting the count alone let a later edit
        // to FormatLink ship an entry the buffer mangles to [ipv6-redacted].
        Assert.Equal("Wi-Fi 2.4 GHz, -78 dBm, 6 Mbit/s", entry.Message);
    }

    [Fact]
    public async Task Link_TracesAgain_AfterAStopAndRestart()
    {
        SetStats(30f, 0f);
        var link = new NetworkLinkSnapshot(true, WifiBand.TwoPointFourGhz, -78, 6, WifiStandard.N);
        var sut = CreatePlayingSutWithLink(link);
        _timeProvider.Advance(TimeSpan.FromSeconds(2));
        Assert.Single(_diagnostics.LogBuffer.GetEntries(), e => e.Category == "Link");

        sut.StopCommand.Execute(null);
        await sut.StartCommand.ExecuteAsync(null);
        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        // Same band and same classification — but a new attempt, so the operator who restarted
        // playback to reproduce a problem gets a radio line for that attempt.
        Assert.Equal(2, _diagnostics.LogBuffer.GetEntries().Count(e => e.Category == "Link"));
    }
}
