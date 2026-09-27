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
    public void HandlePrimaryDestinationChangeAsync_LeavingView_ReturnsTheBridgesStopTaskWithoutBlocking()
    {
        var neverCompletes = new TaskCompletionSource().Task;
        _viewerBridgeMock.Setup(b => b.StopReceiverAsync()).Returns(neverCompletes);
        var sut = CreateSut();

        var task = sut.HandlePrimaryDestinationChangeAsync(PrimaryNavDestination.View, PrimaryNavDestination.Home);

        // It must be the *bridge's* task, not a swallowed one — that is what makes AppShell's
        // diagnostic WaitAsync(3s) mean anything.
        Assert.False(task.IsCompleted);
        _viewerBridgeMock.Verify(b => b.StopReceiverAsync(), Times.Once);
    }

    // --- #410: the ViewModel that owned the receiver is told its session ended ---

    [Fact]
    public async Task HandlePrimaryDestinationChangeAsync_LeavingView_RaisesViewerReceiverStoppedAfterTheStop()
    {
        var order = new List<string>();
        _viewerBridgeMock.Setup(b => b.StopReceiverAsync())
                         .Callback(() => order.Add("StopReceiverAsync"))
                         .Returns(Task.CompletedTask);
        var sut = CreateSut();
        sut.ViewerReceiverStopped += (_, _) => order.Add("ViewerReceiverStopped");

        await sut.HandlePrimaryDestinationChangeAsync(PrimaryNavDestination.View, PrimaryNavDestination.Home);

        Assert.Equal(new[] { "StopReceiverAsync", "ViewerReceiverStopped" }, order);
    }

    [Theory]
    [InlineData(PrimaryNavDestination.Stream, PrimaryNavDestination.Home)]
    [InlineData(PrimaryNavDestination.View, PrimaryNavDestination.View)]
    public async Task HandlePrimaryDestinationChangeAsync_WhenNothingIsStopped_DoesNotRaiseViewerReceiverStopped(
        PrimaryNavDestination from, PrimaryNavDestination to)
    {
        var sut = CreateSut();
        var raised = false;
        sut.ViewerReceiverStopped += (_, _) => raised = true;

        await sut.HandlePrimaryDestinationChangeAsync(from, to);

        Assert.False(raised);
    }

    [Fact]
    public void HandlePrimaryDestinationChangeAsync_ASubscriberThatThrows_DoesNotFailTheHandoff()
    {
        var neverCompletes = new TaskCompletionSource().Task;
        _viewerBridgeMock.Setup(b => b.StopReceiverAsync()).Returns(neverCompletes);
        var sut = CreateSut();
        var secondSubscriberRan = false;
        sut.ViewerReceiverStopped += (_, _) => throw new InvalidOperationException("subscriber fault");
        sut.ViewerReceiverStopped += (_, _) => secondSubscriberRan = true;

        var task = sut.HandlePrimaryDestinationChangeAsync(PrimaryNavDestination.View, PrimaryNavDestination.Home);

        Assert.False(task.IsCompleted); // still the bridge's own stop task
        Assert.True(secondSubscriberRan); // one faulting ViewModel does not keep the others in "Connected."
    }
}
