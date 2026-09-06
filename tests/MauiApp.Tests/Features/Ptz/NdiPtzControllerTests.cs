using Moq;
using NdiForAndroid.Features.Ptz.Models;
using NdiForAndroid.Features.Ptz.Services;
using NdiForAndroid.NdiBridge;
using Xunit;

namespace NdiForAndroid.Tests.Features.Ptz;

public class NdiPtzControllerTests
{
    private readonly Mock<INdiViewerBridge> _bridgeMock = new();

    private NdiPtzController CreateSut() => new(_bridgeMock.Object);

    [Fact]
    public async Task PanTiltAsync_DelegatesToBridgeAndReturnsResult()
    {
        _bridgeMock.Setup(b => b.PtzPanTiltSpeed(0.5f, -0.5f)).Returns(true);
        var sut = CreateSut();

        var result = await sut.PanTiltAsync(0.5f, -0.5f);

        Assert.True(result);
        _bridgeMock.Verify(b => b.PtzPanTiltSpeed(0.5f, -0.5f), Times.Once);
    }

    [Fact]
    public async Task ZoomAsync_DelegatesToBridgeAndReturnsResult()
    {
        _bridgeMock.Setup(b => b.PtzZoomSpeed(1f)).Returns(false);
        var sut = CreateSut();

        var result = await sut.ZoomAsync(1f);

        Assert.False(result);
        _bridgeMock.Verify(b => b.PtzZoomSpeed(1f), Times.Once);
    }

    [Fact]
    public async Task AutoFocusAsync_DelegatesToBridge()
    {
        _bridgeMock.Setup(b => b.PtzAutoFocus()).Returns(true);
        var sut = CreateSut();

        var result = await sut.AutoFocusAsync();

        Assert.True(result);
        _bridgeMock.Verify(b => b.PtzAutoFocus(), Times.Once);
    }

    [Fact]
    public async Task StorePresetAsync_DelegatesToBridge()
    {
        _bridgeMock.Setup(b => b.PtzStorePreset(5)).Returns(true);
        var sut = CreateSut();

        var result = await sut.StorePresetAsync(5);

        Assert.True(result);
        _bridgeMock.Verify(b => b.PtzStorePreset(5), Times.Once);
    }

    [Fact]
    public async Task RecallPresetAsync_DelegatesToBridgeWithSpeed()
    {
        _bridgeMock.Setup(b => b.PtzRecallPreset(5, 0.75f)).Returns(true);
        var sut = CreateSut();

        var result = await sut.RecallPresetAsync(5, 0.75f);

        Assert.True(result);
        _bridgeMock.Verify(b => b.PtzRecallPreset(5, 0.75f), Times.Once);
    }

    [Theory]
    [InlineData(true, PtzLinkState.Connected)]
    [InlineData(false, PtzLinkState.Disconnected)]
    public void LinkState_ReflectsBridgeIsPtzSupported(bool isPtzSupported, PtzLinkState expected)
    {
        _bridgeMock.Setup(b => b.IsPtzSupported).Returns(isPtzSupported);
        var sut = CreateSut();

        Assert.Equal(expected, sut.LinkState);
    }

    [Fact]
    public async Task ShutdownAsync_CompletesWithoutTouchingBridge()
    {
        var sut = CreateSut();

        await sut.ShutdownAsync();

        _bridgeMock.VerifyNoOtherCalls();
    }
}
