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

/// <summary>
/// #409: reconnect ordering on a queued UI thread. The timer ticks and bridge events below are
/// raised from the test thread while it is not draining — i.e. from "background" threads — so they
/// queue in the order they are raised, and <see cref="QueuedMainThreadDispatcher.Drain"/> then runs
/// them as the looper would. A user tap is a UI-thread message, so it is posted, not called.
/// </summary>
public class ViewerViewModelReconnectOrderingTests
{
    private readonly Mock<INdiViewerBridge> _bridgeMock = new();
    private readonly MsFakeTimeProvider _timeProvider = new();
    private readonly QueuedMainThreadDispatcher _dispatcher = new();
    private readonly Mock<IAppStateRepository> _appStateRepoMock = new();
    private readonly Mock<IAppLifecycleService> _lifecycleMock = new();
    private readonly Mock<ISourceRepository> _sourceRepoMock = new();
    private readonly Mock<IConnectionHistoryService> _connectionHistoryMock = new();
    private readonly Mock<IPtzControllerFactory> _ptzControllerFactoryMock = new();
    private readonly Mock<IPtzController> _ptzControllerMock = new();
    private readonly Mock<IImmersiveModeService> _immersiveModeMock = new();
    private readonly Mock<IScreenReaderAnnouncer> _announcerMock = new();
    private readonly Mock<IOrientationLockService> _orientationLockMock = new();

    public ViewerViewModelReconnectOrderingTests()
    {
        _appStateRepoMock.Setup(r => r.RestoreStateAsync()).ReturnsAsync(AppStateSnapshot.Empty);
        _appStateRepoMock.Setup(r => r.SaveAsync(It.IsAny<AppStateSnapshot>())).Returns(Task.CompletedTask);
        _sourceRepoMock.Setup(r => r.GetCachedSourcesAsync()).ReturnsAsync(new List<NdiSource>());
        _ptzControllerFactoryMock.Setup(f => f.Create(It.IsAny<PtzEndpoint?>())).Returns(_ptzControllerMock.Object);
        _bridgeMock.Setup(b => b.StopReceiverAsync()).Returns(Task.CompletedTask);
    }

    /// <summary>Started on "src-1" (every mocked await completes synchronously and Start posts
    /// nothing); nothing queued, bridge call log cleared.</summary>
    private ViewerViewModel CreatePlayingSut()
    {
        var sut = new ViewerViewModel(
            _bridgeMock.Object, _timeProvider, _dispatcher, _appStateRepoMock.Object, _lifecycleMock.Object,
            _sourceRepoMock.Object, _connectionHistoryMock.Object,
            _ptzControllerFactoryMock.Object, new PtzEndpointFormViewModel(_ptzControllerFactoryMock.Object),
            _immersiveModeMock.Object, _announcerMock.Object, _orientationLockMock.Object);
        sut.SourceId = "src-1";
        Assert.True(sut.IsPlaying);
        Assert.Equal(0, _dispatcher.PendingCount);
        _bridgeMock.Invocations.Clear();
        return sut;
    }

    /// <summary>Advances one second at a time, letting the looper catch up after each.</summary>
    private void AdvanceAndDrain(int seconds)
    {
        for (var i = 0; i < seconds; i++)
        {
            _timeProvider.Advance(TimeSpan.FromSeconds(1));
            _dispatcher.Drain();
        }
    }

    /// <summary>Opens a window (as a background drop callback does) and runs it to its last
    /// second: one countdown tick left, every attempt so far unsuccessful.</summary>
    private void RunAWindowToItsLastSecond(ViewerViewModel sut)
    {
        sut.BeginReconnectWindow();
        _dispatcher.Drain();
        Assert.True(sut.IsReconnecting);

        AdvanceAndDrain(14); // one second short of the 15 s window

        Assert.True(sut.IsReconnecting);
        Assert.Equal(1, sut.RetryRemainingSeconds);
    }

    [Fact]
    public void FinalCountdownTick_QueuedBehindTheBridgesConnectedEvent_EndsPlaying()
    {
        var sut = CreatePlayingSut();
        RunAWindowToItsLastSecond(sut);

        // The source comes back in the window's last second and the pump's post wins the race.
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Connected);
        _bridgeMock.Raise(b => b.ConnectionStateChanged += null, _bridgeMock.Object, ConnectionState.Connected);
        _timeProvider.Advance(TimeSpan.FromSeconds(1)); // the final tick queues behind it
        _bridgeMock.Invocations.Clear();

        _dispatcher.Drain();

        Assert.True(sut.IsPlaying);
        Assert.False(sut.IsReconnecting);
        Assert.False(sut.IsStopped);
        Assert.Equal("Connected.", sut.StatusMessage);
        _bridgeMock.Verify(b => b.StopReceiverAsync(), Times.Never);
    }

    [Fact]
    public void FinalCountdownTick_QueuedAheadOfTheBridgesConnectedEvent_StillEndsPlaying()
    {
        var sut = CreatePlayingSut();
        RunAWindowToItsLastSecond(sut);

        // Same instant, the other order: the timer's post wins. The pump has already published
        // Connected when the tick runs; only the event announcing it is still queued.
        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(ConnectionState.Connected);
        _bridgeMock.Raise(b => b.ConnectionStateChanged += null, _bridgeMock.Object, ConnectionState.Connected);
        _bridgeMock.Invocations.Clear();

        _dispatcher.Drain();

        // Not "Connection lost. Reconnection failed." over live video, and the receiver that just
        // connected is not stopped.
        Assert.True(sut.IsPlaying);
        Assert.False(sut.IsReconnecting);
        Assert.False(sut.IsStopped);
        Assert.Equal("Connected.", sut.StatusMessage);
        _bridgeMock.Verify(b => b.StopReceiverAsync(), Times.Never);
    }

    [Fact]
    public void ReconnectWindow_PostedBehindAUserStop_DoesNotOpenAfterIt()
    {
        var sut = CreatePlayingSut();

        // The user's Stop tap is already queued when a background drop callback posts a window.
        _dispatcher.BeginInvokeOnMainThread(() => sut.StopCommand.Execute(null));
        sut.BeginReconnectWindow();
        _dispatcher.Drain();

        Assert.False(sut.IsReconnecting);
        Assert.False(sut.IsPlaying);
        Assert.True(sut.IsStopped);
        Assert.Equal("Stopped.", sut.StatusMessage);

        _bridgeMock.Invocations.Clear();
        AdvanceAndDrain(30);
        _bridgeMock.Verify(b => b.StartReceiver(It.IsAny<string>(), It.IsAny<QualityProfile>()), Times.Never);
    }

    [Fact]
    public void ReconnectWindow_PostedBehindDispose_DoesNotOpenOnTheDisposedViewModel()
    {
        var sut = CreatePlayingSut();

        _dispatcher.BeginInvokeOnMainThread(sut.Dispose); // ViewerPage disposes on the UI thread
        sut.BeginReconnectWindow();
        _dispatcher.Drain();

        Assert.False(sut.IsReconnecting);
        _bridgeMock.Invocations.Clear();
        AdvanceAndDrain(30);
        _bridgeMock.Verify(b => b.StartReceiver(It.IsAny<string>(), It.IsAny<QualityProfile>()), Times.Never);
    }

    [Fact]
    public void Reconnect_AfterAStop_StillOpensAWindowThatAttempts()
    {
        var sut = CreatePlayingSut();
        _dispatcher.BeginInvokeOnMainThread(() => sut.StopCommand.Execute(null));
        _dispatcher.Drain();
        Assert.False(sut.IsPlaying);

        // The one legitimate way to open a window while not playing.
        _dispatcher.BeginInvokeOnMainThread(() => sut.ReconnectCommand.Execute(null));
        _dispatcher.Drain();

        Assert.True(sut.IsReconnecting);
        Assert.False(sut.IsStopped);

        _bridgeMock.Invocations.Clear();
        AdvanceAndDrain(2);
        _bridgeMock.Verify(b => b.StartReceiver("src-1", QualityProfile.Balanced), Times.Once);
    }
}
