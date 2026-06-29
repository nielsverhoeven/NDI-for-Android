using Moq;
using NdiForAndroid.Features.Viewer.ViewModels;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;
using Xunit;

namespace MauiApp.Tests.Features.Viewer;

public class ViewerViewModelTests
{
    private readonly Mock<INdiViewerBridge> _bridgeMock = new();
    private readonly FakeTimeProvider _timeProvider = new(initialSeconds: 0);
    private readonly FakeMainThreadDispatcher _dispatcher = new();

    private ViewerViewModel CreateSut() => new(_bridgeMock.Object, _timeProvider, _dispatcher);

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
}
