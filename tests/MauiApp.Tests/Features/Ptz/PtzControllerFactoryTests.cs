using Moq;
using NdiForAndroid.Features.Ptz.Models;
using NdiForAndroid.Features.Ptz.Services;
using NdiForAndroid.NdiBridge;
using Xunit;

namespace NdiForAndroid.Tests.Features.Ptz;

public class PtzControllerFactoryTests
{
    private readonly Mock<INdiViewerBridge> _bridgeMock = new();
    private readonly Mock<IViscaTransportFactory> _transportFactoryMock = new();

    private PtzControllerFactory CreateSut() => new(_bridgeMock.Object, _transportFactoryMock.Object);

    [Fact]
    public void Create_WithEndpoint_ReturnsViscaPtzController()
    {
        _transportFactoryMock.Setup(f => f.Create()).Returns(Mock.Of<IViscaTransport>());
        var sut = CreateSut();

        var controller = sut.Create(new PtzEndpoint("192.168.1.50", 5678));

        Assert.IsType<ViscaPtzController>(controller);
    }

    [Fact]
    public void Create_WithNullEndpoint_ReturnsNdiPtzController()
    {
        var sut = CreateSut();

        var controller = sut.Create(null);

        Assert.IsType<NdiPtzController>(controller);
        _transportFactoryMock.Verify(f => f.Create(), Times.Never);
    }
}
