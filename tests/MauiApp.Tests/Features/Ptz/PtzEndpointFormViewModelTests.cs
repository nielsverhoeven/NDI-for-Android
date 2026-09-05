using Moq;
using NdiForAndroid.Features.Ptz.Models;
using NdiForAndroid.Features.Ptz.Services;
using NdiForAndroid.Features.Ptz.ViewModels;
using Xunit;

namespace NdiForAndroid.Tests.Features.Ptz;

public class PtzEndpointFormViewModelTests
{
    private readonly Mock<IPtzControllerFactory> _controllerFactoryMock = new();

    private PtzEndpointFormViewModel CreateSut() => new(_controllerFactoryMock.Object);

    [Fact]
    public void Open_PopulatesFieldsAndOpensDialog()
    {
        var sut = CreateSut();

        sut.Open("192.168.1.50", 1234);

        Assert.Equal("192.168.1.50", sut.Host);
        Assert.Equal("1234", sut.PortText);
        Assert.True(sut.IsOpen);
    }

    [Fact]
    public void Open_WithNullHostAndPort_DefaultsToEmptyHostAndDefaultPort()
    {
        var sut = CreateSut();

        sut.Open(null, null);

        Assert.Equal(string.Empty, sut.Host);
        Assert.Equal(PtzEndpoint.DefaultPort.ToString(), sut.PortText);
    }

    [Fact]
    public void Save_WithValidInput_RaisesEndpointSavedAndClosesDialog()
    {
        var sut = CreateSut();
        sut.Open(null, null);
        sut.Host = "192.168.1.50";
        sut.PortText = "1234";
        PtzEndpoint? raised = null;
        var raisedCount = 0;
        sut.EndpointSaved += (_, e) => { raised = e; raisedCount++; };

        sut.SaveCommand.Execute(null);

        Assert.Equal(1, raisedCount);
        Assert.NotNull(raised);
        Assert.Equal("192.168.1.50", raised!.Host);
        Assert.Equal(1234, raised.Port);
        Assert.False(sut.IsOpen);
    }

    [Fact]
    public void Save_WithBlankHost_SetsValidationMessageAndDoesNotRaise()
    {
        var sut = CreateSut();
        sut.Open(null, null);
        sut.Host = "   ";
        var raised = false;
        sut.EndpointSaved += (_, _) => raised = true;

        sut.SaveCommand.Execute(null);

        Assert.False(raised);
        Assert.NotEqual(string.Empty, sut.ValidationMessage);
        Assert.True(sut.IsOpen);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("70000")]
    [InlineData("not-a-number")]
    public void Save_WithInvalidPort_SetsValidationMessageAndDoesNotRaise(string portText)
    {
        var sut = CreateSut();
        sut.Open(null, null);
        sut.Host = "192.168.1.50";
        sut.PortText = portText;
        var raised = false;
        sut.EndpointSaved += (_, _) => raised = true;

        sut.SaveCommand.Execute(null);

        Assert.False(raised);
        Assert.NotEqual(string.Empty, sut.ValidationMessage);
    }

    [Fact]
    public void Clear_RaisesEndpointSavedWithNullAndClosesDialog()
    {
        var sut = CreateSut();
        sut.Open("192.168.1.50", 1234);
        PtzEndpoint? raised = new("stale", 1);
        var raisedCount = 0;
        sut.EndpointSaved += (_, e) => { raised = e; raisedCount++; };

        sut.ClearCommand.Execute(null);

        Assert.Equal(1, raisedCount);
        Assert.Null(raised);
        Assert.False(sut.IsOpen);
        Assert.Equal(string.Empty, sut.Host);
    }

    [Fact]
    public void Cancel_ClosesDialogWithoutRaising()
    {
        var sut = CreateSut();
        sut.Open("192.168.1.50", 1234);
        var raised = false;
        sut.EndpointSaved += (_, _) => raised = true;

        sut.CancelCommand.Execute(null);

        Assert.False(raised);
        Assert.False(sut.IsOpen);
    }

    [Fact]
    public async Task Test_ControllerReportsConnected_SetsStatusTextConnected()
    {
        var controllerMock = new Mock<IPtzController>();
        controllerMock.Setup(c => c.LinkState).Returns(PtzLinkState.Connected);
        controllerMock.Setup(c => c.PanTiltAsync(0f, 0f, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _controllerFactoryMock.Setup(f => f.Create(It.IsAny<PtzEndpoint>())).Returns(controllerMock.Object);
        var sut = CreateSut();
        sut.Open(null, null);
        sut.Host = "192.168.1.50";

        await sut.TestCommand.ExecuteAsync(null);

        Assert.Equal(PtzLinkState.Connected, sut.Status);
        Assert.Equal("Connected.", sut.StatusText);
        controllerMock.Verify(c => c.ShutdownAsync(), Times.Once);
    }

    [Fact]
    public async Task Test_ControllerReportsError_SetsStatusTextFromLastError()
    {
        var controllerMock = new Mock<IPtzController>();
        controllerMock.Setup(c => c.LinkState).Returns(PtzLinkState.Error);
        controllerMock.Setup(c => c.LastError).Returns("boom");
        controllerMock.Setup(c => c.PanTiltAsync(0f, 0f, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _controllerFactoryMock.Setup(f => f.Create(It.IsAny<PtzEndpoint>())).Returns(controllerMock.Object);
        var sut = CreateSut();
        sut.Open(null, null);
        sut.Host = "192.168.1.50";

        await sut.TestCommand.ExecuteAsync(null);

        Assert.Equal(PtzLinkState.Error, sut.Status);
        Assert.Equal("boom", sut.StatusText);
    }

    [Fact]
    public async Task Test_WithBlankHost_SetsValidationMessageWithoutCreatingController()
    {
        var sut = CreateSut();
        sut.Open(null, null);

        await sut.TestCommand.ExecuteAsync(null);

        Assert.NotEqual(string.Empty, sut.ValidationMessage);
        _controllerFactoryMock.Verify(f => f.Create(It.IsAny<PtzEndpoint>()), Times.Never);
    }
}
