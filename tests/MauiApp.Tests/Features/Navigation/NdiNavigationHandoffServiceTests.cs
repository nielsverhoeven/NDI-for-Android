using Moq;
using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.Features.Navigation.Services;
using NdiForAndroid.NdiBridge;
using Xunit;

namespace NdiForAndroid.Tests.Features.Navigation;

public class NdiNavigationHandoffServiceTests
{
    private readonly Mock<INdiViewerBridge> _viewerBridgeMock = new();
    private readonly Mock<INdiOutputBridge> _outputBridgeMock = new();
    private readonly Mock<IAppStateRepository> _appStateRepoMock = new();

    private NdiNavigationHandoffService CreateSut() => new(
        _viewerBridgeMock.Object,
        _outputBridgeMock.Object,
        _appStateRepoMock.Object);

    [Fact]
    public async Task HandlePrimaryDestinationChangeAsync_LeavingStream_ClearsIsOutputActiveInAppState()
    {
        _appStateRepoMock
            .Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot("v1", "X", true, "Y"));
        var sut = CreateSut();

        await sut.HandlePrimaryDestinationChangeAsync(PrimaryNavDestination.Stream, PrimaryNavDestination.Home);

        _outputBridgeMock.Verify(b => b.StopOutputAsync(It.IsAny<CancellationToken>()), Times.Once);
        _appStateRepoMock.Verify(r => r.SaveAsync(It.Is<AppStateSnapshot>(s =>
            s.LastViewerSourceId == "v1" &&
            s.StreamName == null &&
            s.IsOutputActive == false &&
            s.LastSelectedSourceId == "Y")), Times.Once);
    }

    [Fact]
    public async Task HandlePrimaryDestinationChangeAsync_LeavingView_DoesNotTouchAppState()
    {
        var sut = CreateSut();

        await sut.HandlePrimaryDestinationChangeAsync(PrimaryNavDestination.View, PrimaryNavDestination.Home);

        _appStateRepoMock.Verify(r => r.SaveAsync(It.IsAny<AppStateSnapshot>()), Times.Never);
    }
}
