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

/// <summary>#416: coalesced draw-on-arrival and the 1 Hz NDI-Lat latency readout.</summary>
public class ViewerViewModelRenderingTests
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

    public ViewerViewModelRenderingTests()
    {
        _appStateRepoMock.Setup(r => r.RestoreStateAsync()).ReturnsAsync(AppStateSnapshot.Empty);
        _appStateRepoMock.Setup(r => r.SaveAsync(It.IsAny<AppStateSnapshot>())).Returns(Task.CompletedTask);
        _sourceRepoMock.Setup(r => r.GetCachedSourcesAsync()).ReturnsAsync(new List<NdiSource>());
        _ptzControllerFactoryMock.Setup(f => f.Create(It.IsAny<PtzEndpoint?>())).Returns(_ptzControllerMock.Object);
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Connected);
    }

    private ViewerViewModel CreateSut(IMainThreadDispatcher? dispatcher = null, IDiagnosticOverlayService? diagnostics = null) => new(
        _bridgeMock.Object, _timeProvider, dispatcher ?? _dispatcher, _appStateRepoMock.Object, _lifecycleMock.Object,
        _sourceRepoMock.Object, _connectionHistoryMock.Object,
        _ptzControllerFactoryMock.Object, new PtzEndpointFormViewModel(_ptzControllerFactoryMock.Object),
        _immersiveModeMock.Object, _announcerMock.Object, _orientationLockMock.Object,
        networkLink: null, diagnostics: diagnostics);

    private ViewerViewModel CreatePlayingSut(IMainThreadDispatcher? dispatcher = null, IDiagnosticOverlayService? diagnostics = null)
    {
        var sut = CreateSut(dispatcher, diagnostics);
        sut.SourceId = "src-1";
        Assert.True(sut.IsPlaying);
        return sut;
    }

    private void RaiseVideoFrameReady() => _bridgeMock.Raise(b => b.VideoFrameReady += null, EventArgs.Empty);

    [Fact]
    public void FrameReady_IsRaisedOncePerBridgeFrame_WhilePlaying()
    {
        var sut = CreatePlayingSut();
        var count = 0;
        sut.FrameReady += (_, _) => count++;

        RaiseVideoFrameReady();
        RaiseVideoFrameReady();
        RaiseVideoFrameReady();

        Assert.Equal(3, count);
    }

    [Fact]
    public void FrameReady_IsNotRaisedWhenNotPlaying()
    {
        var sut = CreateSut();
        Assert.False(sut.IsPlaying);
        var count = 0;
        sut.FrameReady += (_, _) => count++;

        for (var i = 0; i < 5; i++)
            RaiseVideoFrameReady();

        Assert.Equal(0, count);
    }

    [Fact]
    public void FrameReady_CoalescesWhenTheUiThreadIsBusy()
    {
        var queue = new List<Action>();
        var dispatcher = new QueueingDispatcher(queue);
        var sut = CreatePlayingSut(dispatcher);
        var count = 0;
        sut.FrameReady += (_, _) => count++;

        for (var i = 0; i < 50; i++)
            RaiseVideoFrameReady();

        Assert.Single(queue); // 50 pump frames, one queued invalidate

        queue[0]();
        queue.Clear();
        Assert.Equal(1, count);

        RaiseVideoFrameReady();

        Assert.Single(queue); // the flag was released, so the next frame posts again
    }

    [Fact]
    public void Dispose_UnsubscribesFromTheBridgeFrameEvent()
    {
        var sut = CreatePlayingSut();
        var count = 0;
        sut.FrameReady += (_, _) => count++;

        sut.Dispose();
        RaiseVideoFrameReady();

        Assert.Equal(0, count);
    }

    [Fact]
    public void ReportFrameDrawn_WhenDeveloperModeOff_TracesNothing()
    {
        var sinkMock = new Mock<IDiagnosticLogSink>();
        var diagnostics = new DiagnosticOverlayService(sinkMock.Object) { IsDeveloperMode = false };
        var sut = CreateSut(diagnostics: diagnostics);

        sut.ReportFrameDrawn(
            Environment.TickCount64, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), timestampIsSynthesized: false);

        sinkMock.Verify(s => s.Debug(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ReportFrameDrawn_ThrottlesToOneLinePerSecond()
    {
        var sinkMock = new Mock<IDiagnosticLogSink>();
        var diagnostics = new DiagnosticOverlayService(sinkMock.Object) { IsDeveloperMode = true };
        var sut = CreateSut(diagnostics: diagnostics);

        for (var i = 0; i < 100; i++)
        {
            sut.ReportFrameDrawn(
                Environment.TickCount64, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), timestampIsSynthesized: false);
        }

        sinkMock.Verify(s => s.Debug(DiagnosticOverlayService.LatencyLogTag, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void ReportFrameDrawn_SynthesizedTimestamp_ReportsSenderToDrawAsUnavailable()
    {
        var sinkMock = new Mock<IDiagnosticLogSink>();
        string? captured = null;
        sinkMock.Setup(s => s.Debug(DiagnosticOverlayService.LatencyLogTag, It.IsAny<string>()))
            .Callback<string, string>((_, message) => captured = message);
        var diagnostics = new DiagnosticOverlayService(sinkMock.Object) { IsDeveloperMode = true };
        var sut = CreateSut(diagnostics: diagnostics);

        sut.ReportFrameDrawn(
            Environment.TickCount64, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), timestampIsSynthesized: true);

        Assert.NotNull(captured);
        Assert.Contains("senderToDrawMs=-1", captured);
        Assert.Contains("synth=True", captured);
    }

    [Fact]
    public void ReportFrameDrawn_RealTimestamp_ReportsBothFigures()
    {
        var sinkMock = new Mock<IDiagnosticLogSink>();
        string? captured = null;
        sinkMock.Setup(s => s.Debug(DiagnosticOverlayService.LatencyLogTag, It.IsAny<string>()))
            .Callback<string, string>((_, message) => captured = message);
        var diagnostics = new DiagnosticOverlayService(sinkMock.Object) { IsDeveloperMode = true };
        var sut = CreateSut(diagnostics: diagnostics);

        sut.ReportFrameDrawn(
            Environment.TickCount64, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), timestampIsSynthesized: false);

        Assert.NotNull(captured);
        Assert.Contains("recvToDrawMs=", captured);
        Assert.DoesNotContain("senderToDrawMs=-1", captured);
    }

    /// <summary>Dispatcher test double that queues actions instead of invoking them, to simulate a
    /// UI thread busy painting the previous frame.</summary>
    private sealed class QueueingDispatcher : IMainThreadDispatcher
    {
        private readonly List<Action> _queue;

        public QueueingDispatcher(List<Action> queue) => _queue = queue;

        public void BeginInvokeOnMainThread(Action action) => _queue.Add(action);
    }
}
