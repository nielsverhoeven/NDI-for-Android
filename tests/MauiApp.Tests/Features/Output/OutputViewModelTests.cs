using Moq;
using NdiForAndroid.Features.Output.ViewModels;
using NdiForAndroid.NdiBridge;
using Xunit;

namespace MauiApp.Tests.Features.Output;

public class OutputViewModelTests
{
    private readonly Mock<INdiOutputBridge> _bridgeMock = new();
    private OutputViewModel CreateSut() => new(_bridgeMock.Object);

    [Fact]
    public async Task StartOutputCommand_WithSourceId_StartsOutputAndSetsIsActive()
    {
        _bridgeMock.Setup(b => b.StartOutputAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        sut.SourceId = "src-1";
        await sut.StartOutputCommand.ExecuteAsync(null);

        _bridgeMock.Verify(b => b.StartOutputAsync("src-1", "NDI-Android", It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(sut.IsOutputActive);
        Assert.NotNull(sut.StatusMessage);
    }

    [Fact]
    public async Task StartOutputCommand_WithNullSourceId_DoesNotStartOutput()
    {
        var sut = CreateSut();

        await sut.StartOutputCommand.ExecuteAsync(null);

        _bridgeMock.Verify(b => b.StartOutputAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(sut.IsOutputActive);
    }

    [Fact]
    public async Task StopOutputCommand_WhenActive_StopsOutputAndClearsState()
    {
        _bridgeMock.Setup(b => b.StartOutputAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _bridgeMock.Setup(b => b.StopOutputAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        sut.SourceId = "src-1";
        await sut.StartOutputCommand.ExecuteAsync(null);

        await sut.StopOutputCommand.ExecuteAsync(null);

        _bridgeMock.Verify(b => b.StopOutputAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(sut.IsOutputActive);
        Assert.Null(sut.StatusMessage);
    }
}
