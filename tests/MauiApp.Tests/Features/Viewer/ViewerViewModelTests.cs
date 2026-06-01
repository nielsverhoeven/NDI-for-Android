using Moq;
using NdiForAndroid.Features.Viewer.ViewModels;
using NdiForAndroid.NdiBridge;
using Xunit;

namespace MauiApp.Tests.Features.Viewer;

public class ViewerViewModelTests
{
    private readonly Mock<INdiViewerBridge> _bridgeMock = new();
    private ViewerViewModel CreateSut() => new(_bridgeMock.Object);

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
}
