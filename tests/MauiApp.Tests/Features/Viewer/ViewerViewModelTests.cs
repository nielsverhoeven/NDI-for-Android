using Microsoft.Extensions.Time.Testing;
using Moq;
using NdiForAndroid.Features.Viewer.ViewModels;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;
using Xunit;

namespace MauiApp.Tests.Features.Viewer;

public class ViewerViewModelTests
{
    private readonly Mock<INdiViewerBridge> _bridgeMock = new();
    private readonly FakeTimeProvider _time = new();
    private ConnectionState _connectionState = ConnectionState.Connecting;

    public ViewerViewModelTests()
    {
        _bridgeMock.Setup(b => b.GetConnectionState()).Returns(() => _connectionState);
    }

    private ViewerViewModel CreateSut() =>
        new(_bridgeMock.Object, _time, new ImmediateMainThreadDispatcher());

    /// <summary>Synchronous fake that runs marshaled work inline (no MAUI dependency).</summary>
    private sealed class ImmediateMainThreadDispatcher : IMainThreadDispatcher
    {
        public void Invoke(Action action) => action();
        public Task InvokeAsync(Func<Task> action) => action();
    }

    private ViewerViewModel StartPlaying(string sourceId = "src-1")
    {
        var sut = CreateSut();
        sut.SourceId = sourceId;
        return sut;
    }

    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    // ----- Existing regression tests (new ctor) ---------------------------

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

    // ----- Reconnection behaviour -----------------------------------------

    [Fact] // AC1
    public void Drop_WhilePlaying_EntersReconnecting()
    {
        _connectionState = ConnectionState.Disconnected;
        var sut = StartPlaying();

        _time.Advance(OneSecond); // one monitor poll

        Assert.True(sut.IsReconnecting);
        Assert.Equal("Reconnecting... 15s remaining", sut.RetryStatusMessage);
    }

    [Fact] // AC1 / FR2
    public void RetryWindow_RunsAttemptsEvery2s()
    {
        _connectionState = ConnectionState.Disconnected;
        var sut = StartPlaying();
        _time.Advance(OneSecond); // enter window

        _bridgeMock.Invocations.Clear();
        _time.Advance(TimeSpan.FromSeconds(6)); // attempts at +2, +4, +6

        _bridgeMock.Verify(b => b.StopReceiver(), Times.Exactly(3));
        _bridgeMock.Verify(b => b.StartReceiver("src-1"), Times.Exactly(3));
        Assert.True(sut.IsReconnecting);
    }

    [Fact] // AC2
    public void Countdown_DecrementsEachSecond()
    {
        _connectionState = ConnectionState.Disconnected;
        var sut = StartPlaying();
        _time.Advance(OneSecond); // enter window, message shows 15s

        _time.Advance(OneSecond);
        Assert.Equal("Reconnecting... 14s remaining", sut.RetryStatusMessage);

        _time.Advance(OneSecond);
        Assert.Equal("Reconnecting... 13s remaining", sut.RetryStatusMessage);

        _time.Advance(OneSecond);
        Assert.Equal("Reconnecting... 12s remaining", sut.RetryStatusMessage);
    }

    [Fact] // FR3
    public void Reconnect_Succeeds_ResumesPlayback()
    {
        _connectionState = ConnectionState.Disconnected;
        var sut = StartPlaying();
        _time.Advance(OneSecond);                 // enter window
        _time.Advance(TimeSpan.FromSeconds(2));   // attempt 1 -> still disconnected

        _connectionState = ConnectionState.Connected;
        _time.Advance(TimeSpan.FromSeconds(2));   // attempt 2 -> connected

        Assert.False(sut.IsReconnecting);
        Assert.True(sut.IsPlaying);

        _bridgeMock.Invocations.Clear();
        _time.Advance(TimeSpan.FromSeconds(5));   // no further attempts
        _bridgeMock.Verify(b => b.StartReceiver(It.IsAny<string>()), Times.Never);
    }

    [Fact] // AC3
    public void Expiry_NoReconnect_SetsErrorState()
    {
        _connectionState = ConnectionState.Disconnected;
        var sut = StartPlaying();
        _time.Advance(OneSecond);                  // enter window

        _time.Advance(TimeSpan.FromSeconds(15));   // window elapses

        Assert.False(sut.IsReconnecting);
        Assert.False(sut.IsPlaying);
        Assert.Equal("Connection lost. Reconnection failed.", sut.StatusMessage);
        Assert.True(sut.CanReconnect);
    }

    [Fact] // AC4
    public void CancelRetry_DuringWindow_StopsAndClears()
    {
        _connectionState = ConnectionState.Disconnected;
        var sut = StartPlaying();
        _time.Advance(OneSecond); // enter window

        sut.CancelRetryCommand.Execute(null);

        Assert.False(sut.IsReconnecting);
        Assert.False(sut.IsPlaying);
        _bridgeMock.Verify(b => b.StopReceiver(), Times.AtLeastOnce);

        _bridgeMock.Invocations.Clear();
        _time.Advance(TimeSpan.FromSeconds(10));
        _bridgeMock.Verify(b => b.StartReceiver(It.IsAny<string>()), Times.Never);
    }

    [Fact] // AC5
    public void Stop_ByUser_DoesNotTriggerRetry()
    {
        _connectionState = ConnectionState.Disconnected;
        var sut = StartPlaying();

        sut.StopCommand.Execute(null);

        _bridgeMock.Invocations.Clear();
        _time.Advance(TimeSpan.FromSeconds(10));

        Assert.False(sut.IsReconnecting);
        _bridgeMock.Verify(b => b.StartReceiver(It.IsAny<string>()), Times.Never);
    }

    [Fact] // FR7
    public void Reconnect_FromErrorState_RestartsFlow()
    {
        _connectionState = ConnectionState.Disconnected;
        var sut = StartPlaying();
        _time.Advance(OneSecond);
        _time.Advance(TimeSpan.FromSeconds(15)); // drive to Failed
        Assert.True(sut.CanReconnect);

        _bridgeMock.Invocations.Clear();
        sut.ReconnectCommand.Execute(null);

        _bridgeMock.Verify(b => b.StartReceiver("src-1"), Times.Once);
        Assert.False(sut.CanReconnect);
        Assert.True(sut.IsPlaying);
    }

    [Fact] // Edge case §8
    public void Drop_DuringInitialConnect_EntersReconnecting()
    {
        _connectionState = ConnectionState.Disconnected; // never reaches Connected
        var sut = StartPlaying();

        _time.Advance(OneSecond);

        Assert.True(sut.IsReconnecting);
    }
}
