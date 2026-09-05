using Moq;
using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
using NdiForAndroid.Features.ConnectionHistory.Services;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.Features.Viewer.ViewModels;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Features.Ptz.Models;
using NdiForAndroid.Features.Ptz.Services;
using NdiForAndroid.Features.Ptz.ViewModels;
using NdiForAndroid.Services;
using Xunit;
using MsFakeTimeProvider = Microsoft.Extensions.Time.Testing.FakeTimeProvider;

namespace NdiForAndroid.Tests.Features.Viewer;

public class ViewerViewModelFullScreenTests
{
    private readonly Mock<INdiViewerBridge> _bridgeMock = new();
    private readonly MsFakeTimeProvider _timeProvider = new();
    private readonly FakeMainThreadDispatcher _dispatcher = new();
    private readonly Mock<IAppStateRepository> _appStateRepoMock = new();
    private readonly Mock<IAppLifecycleService> _lifecycleMock = new();
    private readonly Mock<ISourceRepository> _sourceRepoMock = new();
    private readonly Mock<IConnectionHistoryService> _connectionHistoryMock = new();
    private readonly Mock<IImmersiveModeService> _immersiveModeMock = new();
    private readonly Mock<IPtzControllerFactory> _ptzControllerFactoryMock = new();
    private readonly Mock<IPtzController> _ptzControllerMock = new();

    public ViewerViewModelFullScreenTests()
    {
        _appStateRepoMock
            .Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(AppStateSnapshot.Empty);
        _appStateRepoMock
            .Setup(r => r.SaveAsync(It.IsAny<AppStateSnapshot>()))
            .Returns(Task.CompletedTask);
        _sourceRepoMock
            .Setup(r => r.GetCachedSourcesAsync())
            .ReturnsAsync(new List<NdiForAndroid.Features.Sources.Models.NdiSource>());
        _ptzControllerFactoryMock
            .Setup(f => f.Create(It.IsAny<PtzEndpoint?>()))
            .Returns(_ptzControllerMock.Object);
    }

    private ViewerViewModel CreateSut() => new(
        _bridgeMock.Object, _timeProvider, _dispatcher, _appStateRepoMock.Object,
        _lifecycleMock.Object, _sourceRepoMock.Object, _connectionHistoryMock.Object,
        _ptzControllerFactoryMock.Object, new PtzEndpointFormViewModel(_ptzControllerFactoryMock.Object),
        _immersiveModeMock.Object);

    [Fact]
    public void ToggleFullScreenCommand_WhilePlaying_SetsIsFullScreenTrue()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;

        sut.ToggleFullScreenCommand.Execute(null);

        Assert.True(sut.IsFullScreen);
    }

    [Fact]
    public void ToggleFullScreenCommand_WhileNotPlaying_DoesNothing()
    {
        var sut = CreateSut();

        sut.ToggleFullScreenCommand.Execute(null);

        Assert.False(sut.IsFullScreen);
    }

    [Fact]
    public void ToggleFullScreenCommand_Twice_ReturnsToFalse()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;

        sut.ToggleFullScreenCommand.Execute(null);
        sut.ToggleFullScreenCommand.Execute(null);

        Assert.False(sut.IsFullScreen);
    }

    [Fact]
    public void EnteringFullScreen_SetsIsControlsOverlayVisibleTrue()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;

        sut.ToggleFullScreenCommand.Execute(null);

        Assert.True(sut.IsControlsOverlayVisible);
    }

    [Fact]
    public void HideControlsOverlay_WhileFullScreen_HidesOverlayAfterThreeSeconds()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;
        sut.ToggleFullScreenCommand.Execute(null);

        _timeProvider.Advance(TimeSpan.FromSeconds(3));

        Assert.False(sut.IsControlsOverlayVisible);
        Assert.False(sut.AreControlsVisible);
    }

    [Fact]
    public void HideControlsOverlay_AfterExitingFullScreen_IsGuardedNoOp()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;
        sut.ToggleFullScreenCommand.Execute(null);
        sut.ToggleFullScreenCommand.Execute(null); // exits full screen before the timer elapses

        _timeProvider.Advance(TimeSpan.FromSeconds(3));

        Assert.True(sut.IsControlsOverlayVisible);
    }

    [Fact]
    public void ShowControlsOverlayCommand_WhileFullScreen_RevealsControlsAndResetsTimer()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;
        sut.ToggleFullScreenCommand.Execute(null);
        _timeProvider.Advance(TimeSpan.FromSeconds(3));
        Assert.False(sut.IsControlsOverlayVisible);

        sut.ShowControlsOverlayCommand.Execute(null);

        Assert.True(sut.IsControlsOverlayVisible);

        _timeProvider.Advance(TimeSpan.FromSeconds(3));

        Assert.False(sut.IsControlsOverlayVisible);
    }

    [Fact]
    public void NotifyControlInteraction_ViaChangeQualityProfileCommand_RevealsControls()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;
        sut.ToggleFullScreenCommand.Execute(null);
        _timeProvider.Advance(TimeSpan.FromSeconds(3));
        Assert.False(sut.IsControlsOverlayVisible);

        sut.ChangeQualityProfileCommand.Execute("High");

        Assert.True(sut.IsControlsOverlayVisible);
    }

    [Fact]
    public void Stop_WhileFullScreen_ExitsFullScreen()
    {
        var sut = CreateSut();
        sut.SourceId = "src-1";
        sut.ToggleFullScreenCommand.Execute(null);

        sut.StopCommand.Execute(null);

        Assert.False(sut.IsFullScreen);
    }

    [Fact]
    public void Dispose_WhileFullScreenTimerPending_DoesNotThrow()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;
        sut.ToggleFullScreenCommand.Execute(null);

        var exception = Record.Exception(() => sut.Dispose());

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_ReleasesKeepScreenOn()
    {
        var sut = CreateSut();

        sut.Dispose();

        _immersiveModeMock.Verify(m => m.KeepScreenOn(false), Times.AtLeastOnce);
    }

    [Fact]
    public void OnIsPlayingChanged_True_CallsKeepScreenOnTrue()
    {
        var sut = CreateSut();

        sut.IsPlaying = true;

        _immersiveModeMock.Verify(m => m.KeepScreenOn(true), Times.AtLeastOnce);
    }

    [Fact]
    public void OnIsPlayingChanged_False_CallsKeepScreenOnFalse()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;

        sut.IsPlaying = false;

        _immersiveModeMock.Verify(m => m.KeepScreenOn(false), Times.AtLeastOnce);
    }

    [Fact]
    public void ToggleFullScreen_OnAndOff_NeverCallsStopReceiver()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;

        sut.ToggleFullScreenCommand.Execute(null);
        sut.ToggleFullScreenCommand.Execute(null);

        _bridgeMock.Verify(b => b.StopReceiver(), Times.Never);
    }
}
