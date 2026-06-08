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
    public void Constructor_SetsInitialStatusMessage()
    {
        var sut = CreateSut();
        Assert.Equal("Tap Start to begin broadcasting from this device.", sut.StatusMessage);
    }

    [Fact]
    public void Constructor_SetsDefaultStreamName()
    {
        var sut = CreateSut();
        Assert.Equal("NDI-Android", sut.StreamName);
    }

    [Fact]
    public async Task StartOutputCommand_WithValidStreamName_StartsOutputAndForegroundSession()
    {
        _bridgeMock.Setup(b => b.StartOutputAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        sut.StreamName = "MyStream";
        await sut.StartOutputCommand.ExecuteAsync(null);

        _screenShareMock.Verify(s => s.StartForegroundSessionAsync("MyStream", It.IsAny<CancellationToken>()), Times.Once);
        _bridgeMock.Verify(b => b.StartOutputAsync("MyStream", It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(sut.IsOutputActive);
        Assert.Equal("Output active", sut.StatusMessage);
    }

    [Fact]
    public async Task StartOutputCommand_WithEmptyStreamName_SetsErrorMessageAndDoesNotStartOutput()
    {
        var sut = CreateSut();
        sut.StreamName = string.Empty;

        await sut.StartOutputCommand.ExecuteAsync(null);

        _screenShareMock.Verify(s => s.StartForegroundSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _bridgeMock.Verify(b => b.StartOutputAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(sut.IsOutputActive);
        Assert.NotNull(sut.StatusMessage);
    }

    [Fact]
    public async Task StopOutputCommand_WhenActive_StopsOutputAndForegroundSession()
    {
        _bridgeMock.Setup(b => b.StartOutputAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _bridgeMock.Setup(b => b.StopOutputAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        await sut.StartOutputCommand.ExecuteAsync(null);
        await sut.StopOutputCommand.ExecuteAsync(null);

        _bridgeMock.Verify(b => b.StopOutputAsync(It.IsAny<CancellationToken>()), Times.Once);
        _screenShareMock.Verify(s => s.StopForegroundSessionAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(sut.IsOutputActive);
        Assert.Null(sut.StatusMessage);
    }

    [Fact]
    public async Task StartOutputCommand_WhenBridgeThrows_SetsErrorStatus()
    {
        _bridgeMock.Setup(b => b.StartOutputAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("NDI send failed"));

        var sut = CreateSut();
        await sut.StartOutputCommand.ExecuteAsync(null);

        Assert.False(sut.IsOutputActive);
        Assert.Contains("Output failed", sut.StatusMessage);
    }

    [Fact]
    public void ViewModel_DoesNotHaveSourceIdProperty()
    {
        var sut = CreateSut();
        var type = sut.GetType();
        var prop = type.GetProperty("SourceId");
        Assert.Null(prop);
    }
}
