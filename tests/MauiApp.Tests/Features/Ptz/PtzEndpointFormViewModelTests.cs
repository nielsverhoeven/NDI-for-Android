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

    // #374: a blank host is a deliberate "use the source's built-in NDI PTZ" state (the Host
    // field's own placeholder has always said "blank = use NDI PTZ"), so Save now commits it as
    // endpoint == null instead of refusing it — this is what makes an override removable again
    // now that Clear no longer commits on its own (see Clear_* tests below).
    [Fact]
    public void Save_WithBlankHost_RaisesEndpointSavedWithNullAndClosesDialog()
    {
        var sut = CreateSut();
        sut.Open("192.168.1.50", 1234);
        sut.Host = "   ";
        PtzEndpoint? raised = new("stale", 1);
        var raisedCount = 0;
        sut.EndpointSaved += (_, e) => { raised = e; raisedCount++; };

        sut.SaveCommand.Execute(null);

        Assert.Equal(1, raisedCount);
        Assert.Null(raised);
        Assert.False(sut.IsOpen);
        Assert.Equal(string.Empty, sut.ValidationMessage);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("70000")]
    [InlineData("not-a-number")]
    public void Save_WithInvalidPort_CannotExecute_AndDoesNotRaise(string portText)
    {
        var sut = CreateSut();
        sut.Open(null, null);
        sut.Host = "192.168.1.50";
        sut.PortText = portText;
        var raised = false;
        sut.EndpointSaved += (_, _) => raised = true;

        // Save is disabled (not merely click-time-validated) while the port is out of range
        // (#374 error prevention): assert both the gate and that a direct Execute still refuses.
        Assert.False(sut.SaveCommand.CanExecute(null));
        sut.SaveCommand.Execute(null);

        Assert.False(raised);
        Assert.True(sut.IsOpen);
        Assert.NotEqual(string.Empty, sut.PortValidationMessage);
    }

    [Theory]
    [InlineData("192.168.1.50", "1")]
    [InlineData("192.168.1.50", "65535")]
    [InlineData("192.168.1.50", "")]
    [InlineData("", "5678")]
    [InlineData("", "")]
    public void SaveCommand_CanExecute_WhenPortIsInRangeOrBlank_RegardlessOfHost(string host, string portText)
    {
        var sut = CreateSut();
        sut.Open(null, null);
        sut.Host = host;
        sut.PortText = portText;

        Assert.True(sut.SaveCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("not-a-number")]
    public void SaveCommand_CannotExecute_WhenPortIsOutOfRange(string portText)
    {
        var sut = CreateSut();
        sut.Open(null, null);
        sut.PortText = portText;

        Assert.False(sut.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void PortText_LiveUpdatesPortValidationMessage_AsTheUserTypes()
    {
        var sut = CreateSut();
        sut.Open(null, null);

        sut.PortText = "70000";
        Assert.Equal(PtzEndpointFormViewModel.PortRangeMessage, sut.PortValidationMessage);

        sut.PortText = "5678";
        Assert.Equal(string.Empty, sut.PortValidationMessage);
    }

    // #374: Clear only resets the two fields — it must not close the dialog or raise
    // EndpointSaved. Removing an existing override now goes through Clear then Save.
    [Fact]
    public void Clear_OnlyResetsFieldsWithoutClosingOrRaising()
    {
        var sut = CreateSut();
        sut.Open("192.168.1.50", 1234);
        var raised = false;
        sut.EndpointSaved += (_, _) => raised = true;

        sut.ClearCommand.Execute(null);

        Assert.False(raised);
        Assert.True(sut.IsOpen);
        Assert.Equal(string.Empty, sut.Host);
        Assert.Equal(PtzEndpoint.DefaultPort.ToString(), sut.PortText);
    }

    [Fact]
    public void Clear_ThenSave_CommitsTheRemovalAndCloses()
    {
        var sut = CreateSut();
        sut.Open("192.168.1.50", 1234);
        PtzEndpoint? raised = new("stale", 1);
        var raisedCount = 0;
        sut.EndpointSaved += (_, e) => { raised = e; raisedCount++; };

        sut.ClearCommand.Execute(null);
        Assert.Equal(0, raisedCount); // Clear itself commits nothing.

        sut.SaveCommand.Execute(null);

        Assert.Equal(1, raisedCount);
        Assert.Null(raised);
        Assert.False(sut.IsOpen);
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

    // Test's blank-host semantics are unchanged by #374 (unlike Save, a blank host is still not
    // testable — there is nothing to connect to) and use the #370 sources-viewer-007 wording.
    [Fact]
    public async Task Test_WithBlankHost_SetsValidationMessageWithoutCreatingController()
    {
        var sut = CreateSut();
        sut.Open(null, null);

        await sut.TestCommand.ExecuteAsync(null);

        Assert.Equal(PtzEndpointFormViewModel.HostRequiredMessage, sut.ValidationMessage);
        Assert.StartsWith("Enter a host to test a custom PTZ endpoint", sut.ValidationMessage);
        _controllerFactoryMock.Verify(f => f.Create(It.IsAny<PtzEndpoint>()), Times.Never);
    }

    [Fact]
    public async Task Test_WithInvalidPort_SetsValidationMessageWithoutCreatingController()
    {
        var sut = CreateSut();
        sut.Open(null, null);
        sut.Host = "192.168.1.50";
        sut.PortText = "70000";

        await sut.TestCommand.ExecuteAsync(null);

        Assert.Equal(PtzEndpointFormViewModel.PortRangeMessage, sut.ValidationMessage);
        _controllerFactoryMock.Verify(f => f.Create(It.IsAny<PtzEndpoint>()), Times.Never);
    }
}
