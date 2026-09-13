using Moq;
using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.Features.Navigation.Services;
using NdiForAndroid.NdiBridge;
using Xunit;

namespace NdiForAndroid.Tests.Features.Navigation;

public class NdiNavigationHandoffServiceTests
{
    private readonly Mock<INdiViewerBridge> _viewerBridgeMock = new();

    public NdiNavigationHandoffServiceTests()
    {
        _viewerBridgeMock
            .Setup(b => b.StopReceiverAsync())
            .Returns(Task.CompletedTask);
    }

    private NdiNavigationHandoffService CreateSut() => new(_viewerBridgeMock.Object);

    [Fact]
    public async Task HandlePrimaryDestinationChangeAsync_LeavingView_StopsReceiver()
    {
        var sut = CreateSut();

        await sut.HandlePrimaryDestinationChangeAsync(PrimaryNavDestination.View, PrimaryNavDestination.Home);

        _viewerBridgeMock.Verify(b => b.StopReceiverAsync(), Times.Once);
    }

    [Fact]
    public async Task HandlePrimaryDestinationChangeAsync_LeavingStream_DoesNotStopReceiverAsync()
    {
        var sut = CreateSut();

        await sut.HandlePrimaryDestinationChangeAsync(PrimaryNavDestination.Stream, PrimaryNavDestination.Home);

        _viewerBridgeMock.Verify(b => b.StopReceiverAsync(), Times.Never);
    }

    [Fact]
    public async Task HandlePrimaryDestinationChangeAsync_SameDestination_IsNoOp()
    {
        var sut = CreateSut();

        await sut.HandlePrimaryDestinationChangeAsync(PrimaryNavDestination.View, PrimaryNavDestination.View);

        _viewerBridgeMock.Verify(b => b.StopReceiverAsync(), Times.Never);
    }

    [Fact]
    public async Task HandlePrimaryDestinationChangeAsync_LeavingView_ReturnsTheBridgesStopTaskWithoutBlocking()
    {
        var neverCompletes = new TaskCompletionSource().Task;
        _viewerBridgeMock.Setup(b => b.StopReceiverAsync()).Returns(neverCompletes);
        var sut = CreateSut();

        var task = sut.HandlePrimaryDestinationChangeAsync(PrimaryNavDestination.View, PrimaryNavDestination.Home);

        Assert.False(task.IsCompleted);
        _viewerBridgeMock.Verify(b => b.StopReceiverAsync(), Times.Once);
    }
}
