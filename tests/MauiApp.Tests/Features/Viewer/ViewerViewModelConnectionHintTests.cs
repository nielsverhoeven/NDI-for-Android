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
    private readonly Mock<IDiagnosticLogSink> _sinkMock = new();
    private readonly DiagnosticOverlayService _diagnostics;

    /// <summary>The link #415 was reported on: 2.4 GHz, -78 dBm, 6 Mbit/s — weak by both arms.</summary>
    private static readonly NetworkLinkSnapshot WeakLink =
        new(true, WifiBand.TwoPointFourGhz, -78, 6, WifiStandard.N);

    public ViewerViewModelConnectionHintTests()
    {
        // Developer mode is off by default. A test that turns it on also gets a "DevOverlay" entry,
        // which is why the assertions below filter the buffer on the "Link" category.
        _diagnostics = new DiagnosticOverlayService(_sinkMock.Object);
        _appStateRepoMock.Setup(r => r.RestoreStateAsync()).ReturnsAsync(AppStateSnapshot.Empty);
        _appStateRepoMock.Setup(r => r.SaveAsync(It.IsAny<AppStateSnapshot>())).Returns(Task.CompletedTask);
        _sourceRepoMock.Setup(r => r.GetCachedSourcesAsync()).ReturnsAsync(new List<NdiSource>());
        _ptzControllerFactoryMock.Setup(f => f.Create(It.IsAny<PtzEndpoint?>())).Returns(_ptzControllerMock.Object);
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Connected);
        _bridgeMock.Setup(b => b.StopReceiverAsync()).Returns(Task.CompletedTask);
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

    /// <summary>What the bridge reports from now on, and the event it raises to say so.</summary>
    private void RaiseBridgeState(ConnectionState state)
    {
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(state);
        _bridgeMock.Raise(b => b.ConnectionStateChanged += null, _bridgeMock.Object, state);
    }

    /// <summary>The in-app Diagnostic Log's "Link" entries, oldest first.</summary>
    private List<string> LinkEntries() => _diagnostics.LogBuffer.GetEntries()
        .Where(e => e.Category == "Link")
        .Select(e => e.Message)
        .ToList();

    /// <summary>The developer-mode logcat lines written under the NDI-Link tag, oldest first.</summary>
    private List<string> LinkLines() => _sinkMock.Invocations
        .Where(i => i.Method.Name == nameof(IDiagnosticLogSink.Debug)
                    && Equals(i.Arguments[0], DiagnosticOverlayService.LinkLogTag))
        .Select(i => i.Arguments[1])
        .OfType<string>()
        .ToList();

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
        Assert.Equal("Wi-Fi 2.4 GHz, -78 dBm, 6 Mbit/s, connected", entry.Message);
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

    // ----- #427: the link is traced while an attempt this ViewModel owns is in progress -----

    [Fact]
    public void Link_WhileConnecting_WritesOneConnectingEntry_AndNoHint()
    {
        // A source that never connects is when the operator needs the radio line most, and the hint
        // guard used to turn every one of those samples away before the link was traced.
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Connecting);
        SetStats(5f, 40f); // weak numbers: the hint must stay off anyway, it is connected-only
        var sut = CreatePlayingSutWithLink(WeakLink);

        _timeProvider.Advance(TimeSpan.FromSeconds(10)); // inside the 15 s initial-connect budget (#413)

        Assert.True(sut.IsPlaying);
        Assert.Equal(new[] { "Wi-Fi 2.4 GHz, -78 dBm, 6 Mbit/s, connecting" }, LinkEntries());
        Assert.Null(sut.ConnectionHint);
    }

    [Fact]
    public void Link_WhenConnectingBecomesConnected_WritesASecondEntry()
    {
        // The log carries no connection events, so without the connected bit in the change key a
        // "connecting" entry would stand across the connect that followed it.
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Connecting);
        SetStats(30f, 0f);
        CreatePlayingSutWithLink(WeakLink);
        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        RaiseBridgeState(ConnectionState.Connected);
        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(
            new[] { "Wi-Fi 2.4 GHz, -78 dBm, 6 Mbit/s, connecting", "Wi-Fi 2.4 GHz, -78 dBm, 6 Mbit/s, connected" },
            LinkEntries());
    }

    [Fact]
    public void Link_InsideAReconnectWindow_TracesWhileDisconnected()
    {
        // Between attempts a window holds the bridge Disconnected. That is still an attempt this
        // ViewModel owns, and a drop is exactly when the radio line matters.
        SetStats(30f, 0f);
        _diagnostics.IsDeveloperMode = true;
        var sut = CreatePlayingSutWithLink(WeakLink);
        RaiseBridgeState(ConnectionState.Connected);
        _timeProvider.Advance(TimeSpan.FromSeconds(1));

        _bridgeMock.Setup(b => b.GetLastStopReason()).Returns(ReceiverStopReason.ConnectionLost);
        RaiseBridgeState(ConnectionState.Disconnected);
        Assert.True(sut.IsReconnecting);
        _timeProvider.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(
            new[] { "Wi-Fi 2.4 GHz, -78 dBm, 6 Mbit/s, connected", "Wi-Fi 2.4 GHz, -78 dBm, 6 Mbit/s, connecting" },
            LinkEntries());
        Assert.EndsWith("state=Disconnected", LinkLines().Last());
    }

    [Fact]
    public void Link_AfterTheReconnectWindowFails_TracesNothingFurther()
    {
        SetStats(30f, 0f);
        _diagnostics.IsDeveloperMode = true;
        var sut = CreatePlayingSutWithLink(WeakLink);
        RaiseBridgeState(ConnectionState.Connected);
        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        _bridgeMock.Setup(b => b.GetLastStopReason()).Returns(ReceiverStopReason.ConnectionLost);
        RaiseBridgeState(ConnectionState.Disconnected);
        // Each attempt leaves a receiver that never connects, so the window sees Disconnected, then
        // Connecting.
        _bridgeMock.Setup(b => b.StartReceiver(It.IsAny<string>(), It.IsAny<QualityProfile>()))
                   .Callback(() => _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Connecting));

        _timeProvider.Advance(TimeSpan.FromSeconds(15)); // the window runs out: nothing ever reconnects

        Assert.Equal("Connection lost. Reconnection failed.", sut.StatusMessage);
        // The window itself was traced — that is the record of the failed attempt: one entry for the
        // whole window, however the bridge churned between attempts.
        Assert.Contains(LinkLines(), line => line.EndsWith("state=Disconnected"));
        Assert.Contains(LinkLines(), line => line.EndsWith("state=Connecting"));
        Assert.Equal(
            new[] { "Wi-Fi 2.4 GHz, -78 dBm, 6 Mbit/s, connected", "Wi-Fi 2.4 GHz, -78 dBm, 6 Mbit/s, connecting" },
            LinkEntries());
        var entries = LinkEntries().Count;
        var lines = LinkLines().Count;

        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(entries, LinkEntries().Count);
        Assert.Equal(lines, LinkLines().Count);
    }

    [Fact]
    public void Link_WhileDisconnectedOutsideAReconnectWindow_TracesNothing()
    {
        // Disconnected with no window open is no attempt at all — a handoff stop, the x86
        // soft-disable, a failed receiver create — and the latter two hold for as long as it plays.
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Disconnected);
        _bridgeMock.Setup(b => b.GetLastStopReason()).Returns(ReceiverStopReason.Intentional);
        SetStats(0f, 0f);
        _diagnostics.IsDeveloperMode = true;
        var sut = CreatePlayingSutWithLink(WeakLink);

        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        // The watchdog ran and read the link every second; it just had no attempt to trace.
        Assert.True(sut.IsPlaying);
        _networkLinkMock.Verify(s => s.GetSnapshot(), Times.Exactly(10));
        Assert.Empty(LinkEntries());
        Assert.Empty(LinkLines());
    }

    [Fact]
    public void Link_OnAViewModelTheBridgeWasTakenFrom_TracesNothing()
    {
        var generation = 0L;
        _bridgeMock.Setup(b => b.ReceiverGeneration).Returns(() => generation);
        _bridgeMock.Setup(b => b.StartReceiver(It.IsAny<string>(), It.IsAny<QualityProfile>()))
                   .Callback(() => generation++);
        SetStats(30f, 0f);
        _diagnostics.IsDeveloperMode = true;
        var pane = CreatePlayingSutWithLink(WeakLink);   // the Expanded PaneViewer: owns generation 1
        var pushed = CreatePlayingSutWithLink(WeakLink); // a pushed ViewerPage takes the bridge: generation 2

        _timeProvider.Advance(TimeSpan.FromSeconds(3)); // both watchdogs sample

        // One ViewModel traces, the owner; the pane retired on its first sample without a line.
        Assert.False(pane.IsPlaying);
        Assert.True(pushed.IsPlaying);
        Assert.Single(LinkEntries());
        Assert.Equal(3, LinkLines().Count);
    }

    [Fact]
    public void Link_OnAViewModelThatNeverClaimedTheReceiver_TracesNothing()
    {
        // ReleaseIfDisowned only retires a ViewModel that once owned the receiver, so this is a
        // non-owner it leaves playing. The trace checks ownership itself instead of relying on
        // that retire having run first.
        SetStats(30f, 0f);
        _diagnostics.IsDeveloperMode = true;
        var sut = CreateSutWithLink(WeakLink);
        sut.IsPlaying = true; // no Start(): the bridge's receiver was never this ViewModel's

        _timeProvider.Advance(TimeSpan.FromSeconds(5));

        Assert.True(sut.IsPlaying);
        Assert.Empty(LinkEntries());
        Assert.Empty(LinkLines());
    }

    [Fact]
    public void Link_AfterTheInitialConnectTimesOut_TracesNothingFurther()
    {
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Connecting);
        SetStats(0f, 0f);
        _diagnostics.IsDeveloperMode = true;
        var sut = CreatePlayingSutWithLink(WeakLink);
        _timeProvider.Advance(TimeSpan.FromSeconds(14));
        Assert.NotEmpty(LinkLines()); // the attempt itself was traced
        var entries = LinkEntries().Count;
        var lines = LinkLines().Count;

        // The 15th sample ends the connect (#413); it is not traced as part of the attempt it ended,
        // and nothing is traced after it.
        _timeProvider.Advance(TimeSpan.FromSeconds(11));

        Assert.Equal("Could not connect to the source.", sut.StatusMessage);
        Assert.Equal(entries, LinkEntries().Count);
        Assert.Equal(lines, LinkLines().Count);
    }

    [Theory]
    [InlineData(ConnectionState.Connecting)]
    [InlineData(ConnectionState.Connected)]
    [InlineData(ConnectionState.Stalled)]
    public void Link_LogcatLine_EndsWithTheBridgeState(ConnectionState state)
    {
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(state);
        SetStats(30f, 0f);
        _diagnostics.IsDeveloperMode = true;
        CreatePlayingSutWithLink(WeakLink);

        _timeProvider.Advance(TimeSpan.FromSeconds(1));

        // Appended last, so the prefix an operator already filters on is unchanged.
        var line = Assert.Single(LinkLines());
        Assert.EndsWith(
            $"viewer.link band=TwoPointFourGhz rssi=-78 speedMbps=6 std=N weak=True state={state}", line);
    }
}
