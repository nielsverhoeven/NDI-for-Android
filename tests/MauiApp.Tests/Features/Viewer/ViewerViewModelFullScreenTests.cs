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
    private readonly Mock<IScreenReaderAnnouncer> _announcerMock = new();
    private readonly Mock<IPtzControllerFactory> _ptzControllerFactoryMock = new();
    private readonly Mock<IPtzController> _ptzControllerMock = new();
    private readonly Mock<IOrientationLockService> _orientationLockMock = new();

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
        _immersiveModeMock.Object, _announcerMock.Object, _orientationLockMock.Object);

    /// <summary>Configures the lifecycle mock as a compact (phone-class) device at the given
    /// orientation. Tests that never call this exercise the tablet/"unknown" path (SmallestWidthDp
    /// defaults to 0 via Moq), matching every pre-#384-slice-3 test unmodified.</summary>
    private void SetCompactDevice(bool isLandscape)
    {
        _lifecycleMock.Setup(l => l.SmallestWidthDp).Returns(360);
        _lifecycleMock.Setup(l => l.IsLandscape).Returns(isLandscape);
    }

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
    public void HideControlsOverlay_WhileFullScreen_HidesAfterTwoPointFiveSeconds()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;
        sut.ToggleFullScreenCommand.Execute(null);

        _timeProvider.Advance(TimeSpan.FromSeconds(2.5));

        Assert.False(sut.IsControlsOverlayVisible);
        Assert.False(sut.AreControlsVisible);
    }

    [Fact]
    public void HideControlsOverlay_WithPtzLayerOpen_HidesAfterFiveSeconds()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;
        sut.ToggleFullScreenCommand.Execute(null);
        sut.TogglePtzLayerCommand.Execute(null);

        _timeProvider.Advance(TimeSpan.FromSeconds(2.5));
        Assert.True(sut.IsControlsOverlayVisible, "2.5s must not hide the overlay while the PTZ layer is open");

        _timeProvider.Advance(TimeSpan.FromSeconds(2.5)); // total 5s
        Assert.False(sut.IsControlsOverlayVisible);
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
    public void ToggleControlsOverlayCommand_NotFullScreen_DoesNothing()
    {
        var sut = CreateSut();

        sut.ToggleControlsOverlayCommand.Execute(null);

        Assert.True(sut.IsControlsOverlayVisible);
    }

    [Fact]
    public void ToggleControlsOverlayCommand_WhileVisible_HidesImmediately()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;
        sut.ToggleFullScreenCommand.Execute(null);

        sut.ToggleControlsOverlayCommand.Execute(null);

        Assert.False(sut.IsControlsOverlayVisible);
    }

    [Fact]
    public void ToggleControlsOverlayCommand_WhileHidden_ShowsAndRearmsTimer()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;
        sut.ToggleFullScreenCommand.Execute(null);
        _timeProvider.Advance(TimeSpan.FromSeconds(3));
        Assert.False(sut.IsControlsOverlayVisible);

        sut.ToggleControlsOverlayCommand.Execute(null);

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
    public void Dispose_ReleasesOrientationLock()
    {
        var sut = CreateSut();

        sut.Dispose();

        _orientationLockMock.Verify(o => o.Release(), Times.AtLeastOnce);
    }

    [Fact]
    public void Dispose_UnsubscribesFromAppPausedAndOrientationChanged()
    {
        var sut = CreateSut();
        SetCompactDevice(isLandscape: false);
        sut.IsPlaying = true;
        sut.Dispose();

        // A disposed ViewModel must not act on the event. Raising it on a Moq mock never throws,
        // so the assertion has to be behavioural: if the handler were still attached it would set
        // IsFullScreen (compact + playing + landscape).
        _lifecycleMock.Setup(l => l.IsLandscape).Returns(true);
        _lifecycleMock.Raise(l => l.OrientationChanged += null, true);

        Assert.False(sut.IsFullScreen, "OrientationChanged still reached a disposed ViewerViewModel");
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

    // ----- Orientation-driven full screen (#383/#384 slice 3) --------------

    [Fact]
    public void OrientationChanged_ToLandscape_CompactAndPlaying_EntersFullScreen()
    {
        var sut = CreateSut();
        SetCompactDevice(isLandscape: false);
        sut.IsPlaying = true;

        _lifecycleMock.Raise(l => l.OrientationChanged += null, true);

        Assert.True(sut.IsFullScreen);
    }

    [Fact]
    public void OrientationChanged_ToLandscape_NotCompact_DoesNotEnterFullScreen()
    {
        var sut = CreateSut();
        _lifecycleMock.Setup(l => l.SmallestWidthDp).Returns(800); // tablet
        sut.IsPlaying = true;

        _lifecycleMock.Raise(l => l.OrientationChanged += null, true);

        Assert.False(sut.IsFullScreen);
    }

    [Fact]
    public void OrientationChanged_ToLandscape_CompactButNotPlaying_DoesNotEnterFullScreen()
    {
        var sut = CreateSut();
        SetCompactDevice(isLandscape: false);

        _lifecycleMock.Raise(l => l.OrientationChanged += null, true);

        Assert.False(sut.IsFullScreen);
    }

    [Fact]
    public void OrientationChanged_ToPortrait_CompactAndFullScreen_ExitsFullScreen()
    {
        var sut = CreateSut();
        SetCompactDevice(isLandscape: true);
        sut.IsPlaying = true;
        Assert.True(sut.IsFullScreen, "playback starting in landscape on a compact device auto-enters full screen");

        _lifecycleMock.Raise(l => l.OrientationChanged += null, false);

        Assert.False(sut.IsFullScreen);
    }

    [Fact]
    public void ToggleFullScreenCommand_CompactDeviceInPortrait_RequestsLandscapeWithoutSettingFullScreenSynchronously()
    {
        var sut = CreateSut();
        SetCompactDevice(isLandscape: false);
        sut.IsPlaying = true;

        sut.ToggleFullScreenCommand.Execute(null);

        Assert.False(sut.IsFullScreen);
        _orientationLockMock.Verify(o => o.RequestLandscape(), Times.Once);
    }

    [Fact]
    public void OrientationChanged_AfterButtonRequestedLandscape_EntersFullScreenAndKeepsTheLandscapeLock()
    {
        var sut = CreateSut();
        SetCompactDevice(isLandscape: false);
        sut.IsPlaying = true;
        sut.ToggleFullScreenCommand.Execute(null);

        _lifecycleMock.Setup(l => l.IsLandscape).Returns(true);
        _lifecycleMock.Raise(l => l.OrientationChanged += null, true);

        Assert.True(sut.IsFullScreen);
        _orientationLockMock.Verify(o => o.Release(), Times.Never);
    }

    [Fact]
    public void ExitingAfterAButtonEnteredFullScreen_ReleasesTheOrientationLock()
    {
        var sut = CreateSut();
        SetCompactDevice(isLandscape: false);
        sut.IsPlaying = true;
        sut.ToggleFullScreenCommand.Execute(null);          // requests landscape
        _lifecycleMock.Setup(l => l.IsLandscape).Returns(true);
        _lifecycleMock.Raise(l => l.OrientationChanged += null, true);   // enters, lock still held
        _orientationLockMock.Verify(o => o.Release(), Times.Never);

        sut.ToggleFullScreenCommand.Execute(null);          // requests portrait
        _lifecycleMock.Setup(l => l.IsLandscape).Returns(false);
        _lifecycleMock.Raise(l => l.OrientationChanged += null, false);  // exits

        Assert.False(sut.IsFullScreen);
        _orientationLockMock.Verify(o => o.Release(), Times.AtLeastOnce);
    }

    [Fact]
    public void PendingLandscapeRequest_TimesOutAfterThreeSeconds_EntersFullScreenAndReleasesLock()
    {
        var sut = CreateSut();
        SetCompactDevice(isLandscape: false);
        sut.IsPlaying = true;
        sut.ToggleFullScreenCommand.Execute(null);

        _timeProvider.Advance(TimeSpan.FromSeconds(3));

        Assert.True(sut.IsFullScreen);
        _orientationLockMock.Verify(o => o.Release(), Times.Once);
    }

    [Fact]
    public void ToggleFullScreenCommand_CompactDeviceInLandscape_RequestsPortraitAndStaysFullScreenUntilOrientationChanges()
    {
        var sut = CreateSut();
        SetCompactDevice(isLandscape: true);
        sut.IsPlaying = true;
        Assert.True(sut.IsFullScreen, "playback starting in landscape on a compact device auto-enters full screen");

        sut.ToggleFullScreenCommand.Execute(null); // requests exit

        Assert.True(sut.IsFullScreen);
        _orientationLockMock.Verify(o => o.RequestPortrait(), Times.Once);
    }

    [Fact]
    public void OrientationChanged_AfterButtonRequestedPortrait_ExitsFullScreenAndReleasesLock()
    {
        var sut = CreateSut();
        SetCompactDevice(isLandscape: true);
        sut.IsPlaying = true;
        Assert.True(sut.IsFullScreen, "playback starting in landscape on a compact device auto-enters full screen");
        sut.ToggleFullScreenCommand.Execute(null);

        _lifecycleMock.Setup(l => l.IsLandscape).Returns(false);
        _lifecycleMock.Raise(l => l.OrientationChanged += null, false);

        Assert.False(sut.IsFullScreen);
        _orientationLockMock.Verify(o => o.Release(), Times.Once);
    }

    [Fact]
    public void PendingPortraitRequest_TimesOutAfterThreeSeconds_ExitsFullScreenAndReleasesLock()
    {
        var sut = CreateSut();
        SetCompactDevice(isLandscape: true);
        sut.IsPlaying = true;
        Assert.True(sut.IsFullScreen, "playback starting in landscape on a compact device auto-enters full screen");
        sut.ToggleFullScreenCommand.Execute(null);

        _timeProvider.Advance(TimeSpan.FromSeconds(3));

        Assert.False(sut.IsFullScreen);
        _orientationLockMock.Verify(o => o.Release(), Times.Once);
    }

    [Fact]
    public void AppPaused_WhileFullScreen_ForcesExitAndReleasesOrientationLock()
    {
        var sut = CreateSut();
        SetCompactDevice(isLandscape: true);
        sut.IsPlaying = true;
        Assert.True(sut.IsFullScreen, "playback starting in landscape on a compact device auto-enters full screen");

        _lifecycleMock.Raise(l => l.AppPaused += null);

        Assert.False(sut.IsFullScreen);
        _orientationLockMock.Verify(o => o.Release(), Times.AtLeastOnce);
    }

    [Fact]
    public void Stop_WhilePendingLandscapeRequestNotYetRotated_CancelsPendingRequestAndReleasesLock()
    {
        var sut = CreateSut();
        sut.SourceId = "src-1";
        SetCompactDevice(isLandscape: false);
        sut.ToggleFullScreenCommand.Execute(null); // pending landscape, not yet full screen

        sut.StopCommand.Execute(null);

        Assert.False(sut.IsFullScreen);
        _orientationLockMock.Verify(o => o.Release(), Times.Once);

        // The rotation finally arrives after Stop() — must not resurrect full screen for a
        // stream that is no longer playing.
        _lifecycleMock.Setup(l => l.IsLandscape).Returns(true);
        _lifecycleMock.Raise(l => l.OrientationChanged += null, true);

        Assert.False(sut.IsFullScreen);
    }

    [Fact]
    public void Stop_WhileFullScreenCompactAndLandscape_StopsReceiverBeforeRequestingPortrait()
    {
        var sut = CreateSut();
        sut.SourceId = "src-1";
        SetCompactDevice(isLandscape: true);
        sut.ToggleFullScreenCommand.Execute(null);

        var callOrder = new List<string>();
        _bridgeMock.Setup(b => b.StopReceiver()).Callback(() => callOrder.Add("StopReceiver"));
        _orientationLockMock.Setup(o => o.RequestPortrait()).Callback(() => callOrder.Add("RequestPortrait"));

        sut.StopCommand.Execute(null);

        Assert.Equal(new[] { "StopReceiver", "RequestPortrait" }, callOrder);
        Assert.True(sut.IsFullScreen, "Still full screen — waiting for the portrait rotation");
    }

    [Fact]
    public void TogglePtzLayerCommand_TogglesIsPtzLayerVisible()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;
        sut.ToggleFullScreenCommand.Execute(null);

        sut.TogglePtzLayerCommand.Execute(null);
        Assert.True(sut.IsPtzLayerVisible);

        sut.TogglePtzLayerCommand.Execute(null);
        Assert.False(sut.IsPtzLayerVisible);
    }

    [Fact]
    public void IsFullScreenPtzVisible_RequiresBothPtzControlActiveAndLayerVisible()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;
        sut.ToggleFullScreenCommand.Execute(null);

        Assert.False(sut.IsFullScreenPtzVisible); // layer closed by default

        sut.TogglePtzLayerCommand.Execute(null);
        Assert.False(sut.IsFullScreenPtzVisible); // layer open, but no PTZ support yet

        sut.IsPtzSupported = true;
        Assert.True(sut.IsFullScreenPtzVisible);
    }

    [Fact]
    public void ExitingFullScreen_ResetsPtzLayerVisible()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;
        sut.ToggleFullScreenCommand.Execute(null);
        sut.TogglePtzLayerCommand.Execute(null);
        Assert.True(sut.IsPtzLayerVisible);

        sut.ToggleFullScreenCommand.Execute(null); // exits (tablet path, default mock)

        Assert.False(sut.IsPtzLayerVisible);
    }

    [Fact]
    public void HandleBackButtonPress_NotFullScreen_ReturnsFalse()
    {
        var sut = CreateSut();

        Assert.False(sut.HandleBackButtonPress());
    }

    [Fact]
    public void HandleBackButtonPress_WithPtzLayerOpen_ClosesLayerAndConsumes()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;
        sut.ToggleFullScreenCommand.Execute(null);
        sut.TogglePtzLayerCommand.Execute(null);

        var consumed = sut.HandleBackButtonPress();

        Assert.True(consumed);
        Assert.False(sut.IsPtzLayerVisible);
        Assert.True(sut.IsFullScreen, "The first Back closes the camera layer only, it must not also exit full screen");
    }

    [Fact]
    public void HandleBackButtonPress_FullScreen_ExitsAndConsumes()
    {
        var sut = CreateSut();
        sut.IsPlaying = true;
        sut.ToggleFullScreenCommand.Execute(null);

        var consumed = sut.HandleBackButtonPress();

        Assert.True(consumed);
        Assert.False(sut.IsFullScreen);
    }

    [Fact]
    public void HandleBackButtonPress_DuringPendingPortrait_SwallowsSecondPress()
    {
        var sut = CreateSut();
        SetCompactDevice(isLandscape: true);
        sut.IsPlaying = true;
        Assert.True(sut.IsFullScreen, "playback starting in landscape on a compact device auto-enters full screen");

        var firstPress = sut.HandleBackButtonPress(); // requests portrait, pending
        var secondPress = sut.HandleBackButtonPress(); // must not re-request

        Assert.True(firstPress);
        Assert.True(secondPress);
        Assert.True(sut.IsFullScreen, "Still waiting for the portrait rotation");
        _orientationLockMock.Verify(o => o.RequestPortrait(), Times.Once);
    }

    [Fact]
    public void OrientationDrivenFullScreenTransitions_NeverCallStopReceiver()
    {
        var sut = CreateSut();
        SetCompactDevice(isLandscape: false);
        sut.IsPlaying = true;

        sut.ToggleFullScreenCommand.Execute(null); // pending landscape
        _lifecycleMock.Setup(l => l.IsLandscape).Returns(true);
        _lifecycleMock.Raise(l => l.OrientationChanged += null, true); // enters
        sut.ToggleFullScreenCommand.Execute(null); // pending portrait
        _lifecycleMock.Setup(l => l.IsLandscape).Returns(false);
        _lifecycleMock.Raise(l => l.OrientationChanged += null, false); // exits

        _bridgeMock.Verify(b => b.StopReceiver(), Times.Never);
    }

    [Fact]
    public void PlaybackStartingWhileAlreadyLandscape_OnCompactDevice_EntersFullScreenWithoutRequestingRotation()
    {
        var sut = CreateSut();
        SetCompactDevice(isLandscape: true);

        sut.IsPlaying = true;

        Assert.True(sut.IsFullScreen);
        _orientationLockMock.Verify(o => o.RequestLandscape(), Times.Never);
    }

    [Fact]
    public void PlaybackStartingWhileAlreadyLandscape_OnTablet_StaysWindowed()
    {
        var sut = CreateSut();
        _lifecycleMock.Setup(l => l.SmallestWidthDp).Returns(800);
        _lifecycleMock.Setup(l => l.IsLandscape).Returns(true);

        sut.IsPlaying = true;

        Assert.False(sut.IsFullScreen);
    }

    [Fact]
    public void EveryFullScreenExitPath_NeverCallsStopReceiver()
    {
        var sut = CreateSut();
        SetCompactDevice(isLandscape: true);
        sut.IsPlaying = true;                                   // auto-enters (required change 2)

        sut.HandleBackButtonPress();                            // exit via Back -> pending portrait
        _lifecycleMock.Setup(l => l.IsLandscape).Returns(false);
        _lifecycleMock.Raise(l => l.OrientationChanged += null, false);

        _lifecycleMock.Setup(l => l.IsLandscape).Returns(true);
        _lifecycleMock.Raise(l => l.OrientationChanged += null, true);   // auto-enter again
        sut.ToggleFullScreenCommand.Execute(null);              // exit -> pending portrait
        _timeProvider.Advance(TimeSpan.FromSeconds(3));         // exit via the 3s fallback

        _lifecycleMock.Raise(l => l.AppPaused += null);         // force-exit path
        sut.ForceExitFullScreen();                              // Detach()'s path

        _bridgeMock.Verify(b => b.StopReceiver(), Times.Never);
    }

    [Fact]
    public void ToggleFullScreenCommand_OnATablet_NeverRequestsAnOrientation()
    {
        var sut = CreateSut();
        _lifecycleMock.Setup(l => l.SmallestWidthDp).Returns(800);
        sut.IsPlaying = true;

        sut.ToggleFullScreenCommand.Execute(null);
        sut.ToggleFullScreenCommand.Execute(null);

        Assert.False(sut.IsFullScreen);
        _orientationLockMock.Verify(o => o.RequestLandscape(), Times.Never);
        _orientationLockMock.Verify(o => o.RequestPortrait(), Times.Never);
    }
}
