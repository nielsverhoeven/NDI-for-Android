using Moq;
using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.Features.Navigation.Services;
using NdiForAndroid.NdiBridge;
using Xunit;

namespace NdiForAndroid.Tests.Features.Navigation;

public class NdiNavigationHandoffServiceTests
{
    private readonly Mock<INdiViewerBridge> _viewerBridgeMock = new();

    private NdiNavigationHandoffService CreateSut() => new(_viewerBridgeMock.Object);

    [Fact]
    public async Task HandlePrimaryDestinationChangeAsync_LeavingView_StopsReceiver()
    {
        var sut = CreateSut();

        await sut.HandlePrimaryDestinationChangeAsync(PrimaryNavDestination.View, PrimaryNavDestination.Home);

        _viewerBridgeMock.Verify(b => b.StopReceiver(), Times.Once);
    }

    [Fact]
    public async Task HandlePrimaryDestinationChangeAsync_LeavingStream_DoesNotStopReceiver()
    {
        var sut = CreateSut();

        await sut.HandlePrimaryDestinationChangeAsync(PrimaryNavDestination.Stream, PrimaryNavDestination.Home);

        _viewerBridgeMock.Verify(b => b.StopReceiver(), Times.Never);
    }

    [Fact]
    public async Task HandlePrimaryDestinationChangeAsync_SameDestination_IsNoOp()
    {
        var sut = CreateSut();

        await sut.HandlePrimaryDestinationChangeAsync(PrimaryNavDestination.View, PrimaryNavDestination.View);

        _viewerBridgeMock.Verify(b => b.StopReceiver(), Times.Never);
    }
}
