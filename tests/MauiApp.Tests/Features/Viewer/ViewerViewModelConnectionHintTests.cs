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
    private readonly Mock<IAccessibilityAnnouncer> _announcerMock = new();

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
        _immersiveModeMock.Object, _announcerMock.Object);

    private ViewerViewModel CreatePlayingSut()
    {
        var sut = CreateSut();
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
    public void Hint_StaysHidden_WhenBridgeNotConnected()
    {
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Disconnected);
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
}
