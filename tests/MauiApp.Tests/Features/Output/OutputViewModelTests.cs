using Moq;
using NdiForAndroid.Features.Output.ViewModels;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;
using Xunit;

namespace MauiApp.Tests.Features.Output;

public class OutputViewModelTests
{
    private readonly Mock<INdiOutputBridge> _bridgeMock = new();
    private readonly Mock<IScreenSharePlatformService> _screenShareMock = new();

    private OutputViewModel CreateSut() => new(_bridgeMock.Object, _screenShareMock.Object);

    [Fact]
    public async Task StartOutputCommand_WithReachableSource_StartsOutputAndForegroundSession()
    {
        _bridgeMock.Setup(b => b.IsSourceReachableAsync("src-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _bridgeMock.Setup(b => b.StartOutputAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        sut.SourceId = "src-1";
        await sut.StartOutputCommand.ExecuteAsync(null);

        _screenShareMock.Verify(s => s.StartForegroundSessionAsync("NDI-Android", It.IsAny<CancellationToken>()), Times.Once);
        _bridgeMock.Verify(b => b.StartOutputAsync("src-1", "NDI-Android", It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(sut.IsOutputActive);
        Assert.Equal("Output active", sut.StatusMessage);
    }

    [Fact]
    public async Task StartOutputCommand_WhenSourceNotReachable_SetsStatusMessage()
    {
        _bridgeMock.Setup(b => b.IsSourceReachableAsync("src-1", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = CreateSut();
        sut.SourceId = "src-1";

        await sut.StartOutputCommand.ExecuteAsync(null);

        _screenShareMock.Verify(s => s.StartForegroundSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _bridgeMock.Verify(b => b.StartOutputAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(sut.IsOutputActive);
        Assert.Equal("Source is not reachable.", sut.StatusMessage);
    }

    [Fact]
    public async Task StopOutputCommand_WhenActive_StopsOutputAndForegroundSession()
    {
        _bridgeMock.Setup(b => b.IsSourceReachableAsync("src-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _bridgeMock.Setup(b => b.StartOutputAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _bridgeMock.Setup(b => b.StopOutputAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        sut.SourceId = "src-1";
        await sut.StartOutputCommand.ExecuteAsync(null);

        await sut.StopOutputCommand.ExecuteAsync(null);

        _bridgeMock.Verify(b => b.StopOutputAsync(It.IsAny<CancellationToken>()), Times.Once);
        _screenShareMock.Verify(s => s.StopForegroundSessionAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(sut.IsOutputActive);
        Assert.Null(sut.StatusMessage);
    }
}
