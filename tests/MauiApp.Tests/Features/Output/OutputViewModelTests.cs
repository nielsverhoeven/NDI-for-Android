using Moq;
using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
using NdiForAndroid.Features.Output.Repositories;
using NdiForAndroid.Features.Output.ViewModels;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;
using Xunit;

namespace NdiForAndroid.Tests.Features.Output;

public class OutputViewModelTests
{
    private readonly Mock<INdiOutputBridge> _bridgeMock = new();
    private readonly Mock<IAppStateRepository> _appStateRepoMock = new();
    private readonly Mock<IAppLifecycleService> _lifecycleMock = new();
    private readonly Mock<IOutputConfigurationRepository> _configRepoMock = new();
    private readonly FakeMainThreadDispatcher _dispatcher = new();
    private readonly FakeScreenReaderAnnouncer _announcer = new();

    public OutputViewModelTests()
    {
        _bridgeMock.Setup(b => b.StartOutputAsync(
                It.IsAny<string>(), It.IsAny<VideoInputKind>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _bridgeMock.Setup(b => b.StopOutputAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _appStateRepoMock
            .Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(AppStateSnapshot.Empty);
        _configRepoMock
            .Setup(r => r.GetAsync())
            .ReturnsAsync((OutputConfiguration?)null);
    }

    private OutputViewModel CreateSut() => new(
        _bridgeMock.Object,
        _appStateRepoMock.Object,
        _lifecycleMock.Object,
        _configRepoMock.Object,
        _dispatcher,
        _announcer);

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
    public void Constructor_DefaultsToScreenInputWithoutMicrophone()
    {
        var sut = CreateSut();
        Assert.Equal(VideoInputKind.Screen, sut.SelectedInputKind);
        Assert.False(sut.CaptureMicrophone);
        Assert.Equal(
            new[] { VideoInputKind.Screen, VideoInputKind.CameraFront, VideoInputKind.CameraRear },
            sut.AvailableInputKinds);
    }

    [Fact]
    public async Task StartOutputCommand_PassesInputKindAndMicrophoneToBridge()
    {
        var sut = CreateSut();
        sut.StreamName = "MyStream";
        sut.SelectedInputKind = VideoInputKind.CameraRear;
        sut.CaptureMicrophone = true;

        await sut.StartOutputCommand.ExecuteAsync(null);

        _bridgeMock.Verify(b => b.StartOutputAsync(
            "MyStream", VideoInputKind.CameraRear, true, It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(sut.IsOutputActive);
        Assert.Equal("Output active", sut.StatusMessage);
    }

    [Fact]
    public async Task StartOutputCommand_WithEmptyStreamName_SetsErrorMessageAndDoesNotStartOutput()
    {
        var sut = CreateSut();
        sut.StreamName = string.Empty;

        await sut.StartOutputCommand.ExecuteAsync(null);

        _bridgeMock.Verify(b => b.StartOutputAsync(
            It.IsAny<string>(), It.IsAny<VideoInputKind>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.False(sut.IsOutputActive);
        Assert.NotNull(sut.StatusMessage);
    }

    [Fact]
    public async Task StartOutputCommand_WhenPermissionDeclined_SetsStatusAndDoesNotMarkActive()
    {
        _bridgeMock.Setup(b => b.StartOutputAsync(
                It.IsAny<string>(), It.IsAny<VideoInputKind>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var sut = CreateSut();
        await sut.StartOutputCommand.ExecuteAsync(null);

        Assert.False(sut.IsOutputActive);
        Assert.Equal("Permission declined — output not started.", sut.StatusMessage);
        _configRepoMock.Verify(r => r.SaveAsync(It.IsAny<OutputConfiguration>()), Times.Never);
    }

    [Fact]
    public async Task StartOutputCommand_PersistsConfigurationOnSuccessfulStart()
    {
        var sut = CreateSut();
        sut.StreamName = "MyStream";
        sut.SelectedInputKind = VideoInputKind.CameraFront;
        sut.CaptureMicrophone = true;

        await sut.StartOutputCommand.ExecuteAsync(null);

        _configRepoMock.Verify(r => r.SaveAsync(
            new OutputConfiguration("MyStream", VideoInputKind.CameraFront, true)), Times.Once);
    }

    [Fact]
    public async Task StartOutputCommand_WhenBridgeThrows_SetsErrorStatusAndDoesNotPersistConfig()
    {
        _bridgeMock.Setup(b => b.StartOutputAsync(
                It.IsAny<string>(), It.IsAny<VideoInputKind>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("NDI send failed"));

        var sut = CreateSut();
        await sut.StartOutputCommand.ExecuteAsync(null);

        Assert.False(sut.IsOutputActive);
        Assert.Contains("Output failed", sut.StatusMessage);
        _configRepoMock.Verify(r => r.SaveAsync(It.IsAny<OutputConfiguration>()), Times.Never);
    }

    [Fact]
    public async Task StopOutputCommand_WhenActive_StopsOutputAndResetsStatus()
    {
        var sut = CreateSut();
        await sut.StartOutputCommand.ExecuteAsync(null);
        await sut.StopOutputCommand.ExecuteAsync(null);

        _bridgeMock.Verify(b => b.StopOutputAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(sut.IsOutputActive);
        Assert.Null(sut.StatusMessage);
        Assert.False(sut.IsOnProgramTally);
        Assert.Equal(0, sut.ConnectionCount);
    }

    [Fact]
    public void OutputStatusChanged_UpdatesTallyAndConnectionCount()
    {
        _bridgeMock.SetupGet(b => b.IsOnProgramTally).Returns(true);
        _bridgeMock.SetupGet(b => b.ConnectionCount).Returns(3);

        var sut = CreateSut();
        _bridgeMock.Raise(b => b.OutputStatusChanged += null, EventArgs.Empty);

        Assert.True(sut.IsOnProgramTally);
        Assert.Equal(3, sut.ConnectionCount);
    }

    [Fact]
    public void Dispose_UnsubscribesFromOutputStatusChanged()
    {
        _bridgeMock.SetupGet(b => b.IsOnProgramTally).Returns(true);
        _bridgeMock.SetupGet(b => b.ConnectionCount).Returns(3);

        var sut = CreateSut();
        sut.Dispose();
        _bridgeMock.Raise(b => b.OutputStatusChanged += null, EventArgs.Empty);

        Assert.False(sut.IsOnProgramTally);
        Assert.Equal(0, sut.ConnectionCount);
    }

    // ── Lifetime contract (#352/#359): one subscription per event for the app lifetime ─────────

    [Fact]
    public void Constructor_SubscribesToEachSingletonEventExactlyOnce()
    {
        _ = CreateSut();

        _bridgeMock.VerifyAdd(b => b.OutputStatusChanged += It.IsAny<EventHandler>(), Times.Once);
        _lifecycleMock.VerifyAdd(l => l.AppResumed += It.IsAny<Action>(), Times.Once);
    }

    [Fact]
    public async Task LoadCommand_RepeatedAppearances_DoNotAddSubscriptions()
    {
        var sut = CreateSut();

        // OutputPage.OnAppearing awaits LoadCommand on every tab entry.
        await sut.LoadCommand.ExecuteAsync(null);
        await sut.LoadCommand.ExecuteAsync(null);
        await sut.LoadCommand.ExecuteAsync(null);

        _bridgeMock.VerifyAdd(b => b.OutputStatusChanged += It.IsAny<EventHandler>(), Times.Once);
        _lifecycleMock.VerifyAdd(l => l.AppResumed += It.IsAny<Action>(), Times.Once);
    }

    [Fact]
    public void Dispose_RemovesEverySubscriptionItAdded()
    {
        var sut = CreateSut();

        sut.Dispose();
        _appStateRepoMock.Invocations.Clear();

        _bridgeMock.VerifyRemove(b => b.OutputStatusChanged -= It.IsAny<EventHandler>(), Times.Once);
        _lifecycleMock.VerifyRemove(l => l.AppResumed -= It.IsAny<Action>(), Times.Once);

        // A resume after teardown must not run the corroboration (and its SaveAsync) any more.
        _lifecycleMock.Raise(l => l.AppResumed += null);
        _appStateRepoMock.Verify(r => r.RestoreStateAsync(), Times.Never);
    }

    [Fact]
    public async Task LoadCommand_WhenNothingPersisted_KeepsTheStreamNameTypedBeforeTheTabSwitch()
    {
        // Unit twin of the e2e Stream_TypedStreamName_SurvivesATabSwitch: with no persisted
        // configuration and no persisted session, re-entering the tab (LoadCommand) must not
        // reset what the user typed on the singleton instance.
        var sut = CreateSut();
        sut.StreamName = "Typed-Before-Leaving";

        await sut.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Typed-Before-Leaving", sut.StreamName);
    }

    [Fact]
    public async Task LoadCommand_AppliesPersistedConfiguration()
    {
        _configRepoMock.Setup(r => r.GetAsync())
            .ReturnsAsync(new OutputConfiguration("PersistedName", VideoInputKind.CameraFront, true));

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        Assert.Equal("PersistedName", sut.StreamName);
        Assert.Equal(VideoInputKind.CameraFront, sut.SelectedInputKind);
        Assert.True(sut.CaptureMicrophone);
    }

    [Fact]
    public async Task LoadCommand_WhenNoPersistedConfiguration_KeepsDefaults()
    {
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        Assert.Equal("NDI-Android", sut.StreamName);
        Assert.Equal(VideoInputKind.Screen, sut.SelectedInputKind);
        Assert.False(sut.CaptureMicrophone);
    }

    [Fact]
    public async Task LoadCommand_WhenBridgeActive_ReflectsRunningOutput()
    {
        _appStateRepoMock.Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot(null, "X", true, null));
        _bridgeMock.SetupGet(b => b.IsActive).Returns(true);
        _bridgeMock.SetupGet(b => b.ConnectionCount).Returns(2);

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        Assert.True(sut.IsOutputActive);
        Assert.Equal("X", sut.StreamName);
        Assert.Equal("Output active", sut.StatusMessage);
        Assert.Equal(2, sut.ConnectionCount);
    }

    [Fact]
    public async Task LoadCommand_WhenBridgeActiveWithReStream_RestoresReStreamMode()
    {
        _appStateRepoMock.Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot(null, "X", true, null));
        _bridgeMock.SetupGet(b => b.IsActive).Returns(true);
        _bridgeMock.SetupGet(b => b.IsReStreamActive).Returns(true);

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        Assert.True(sut.IsOutputActive);
        Assert.True(sut.IsReStreamMode);
    }

    [Fact]
    public async Task LoadCommand_WhenPersistedActiveButBridgeInactive_ShowsResumeHint()
    {
        _appStateRepoMock.Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot(null, "X", true, null));
        _bridgeMock.SetupGet(b => b.IsActive).Returns(false);

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        Assert.False(sut.IsOutputActive);
        Assert.Equal("Tap Start to resume output", sut.StatusMessage);
        _appStateRepoMock.Verify(r => r.SaveAsync(It.Is<AppStateSnapshot>(
            s => s.IsOutputActive == false && s.StreamName == "X")), Times.Once);
    }

    [Fact]
    public async Task LoadCommand_WhenStreamNamePersistedAndFlagAlreadyFalse_ShowsResumeHintWithoutWriting()
    {
        _appStateRepoMock.Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot(null, "X", false, null));
        _bridgeMock.SetupGet(b => b.IsActive).Returns(false);

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        Assert.False(sut.IsOutputActive);
        Assert.Equal("Tap Start to resume output", sut.StatusMessage);
        _appStateRepoMock.Verify(r => r.SaveAsync(It.IsAny<AppStateSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task LoadCommand_WhenNothingPersisted_KeepsDefault()
    {
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Tap Start to begin broadcasting from this device.", sut.StatusMessage);
        Assert.False(sut.IsOutputActive);
    }

    [Fact]
    public async Task StartOutputCommand_InReStreamMode_StartsReStreamFromSource()
    {
        _bridgeMock.Setup(b => b.StartReStreamFromSourceAsync(
                It.IsAny<string>(), It.IsAny<QualityProfile>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        sut.ReStreamSourceId = "SOURCE-1";
        await sut.ToggleReStreamModeCommand.ExecuteAsync(null);
        await sut.StartOutputCommand.ExecuteAsync(null);

        _bridgeMock.Verify(b => b.StartReStreamFromSourceAsync(
            "SOURCE-1", QualityProfile.Balanced, It.IsAny<CancellationToken>()), Times.Once);
        _bridgeMock.Verify(b => b.StartOutputAsync(
            It.IsAny<string>(), It.IsAny<VideoInputKind>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.True(sut.IsOutputActive);
        Assert.Equal("Re-stream active", sut.StatusMessage);
    }

    [Fact]
    public async Task StopOutputCommand_InReStreamMode_StopsReStream()
    {
        _bridgeMock.Setup(b => b.StartReStreamFromSourceAsync(
                It.IsAny<string>(), It.IsAny<QualityProfile>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _bridgeMock.Setup(b => b.StopReStreamAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        sut.ReStreamSourceId = "SOURCE-1";
        await sut.ToggleReStreamModeCommand.ExecuteAsync(null);
        await sut.StartOutputCommand.ExecuteAsync(null);
        await sut.StopOutputCommand.ExecuteAsync(null);

        _bridgeMock.Verify(b => b.StopReStreamAsync(It.IsAny<CancellationToken>()), Times.Once);
        _bridgeMock.Verify(b => b.StopOutputAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(sut.IsOutputActive);
    }

    [Fact]
    public void ViewModel_DoesNotHaveSourceIdProperty()
    {
        var sut = CreateSut();
        var type = sut.GetType();
        var prop = type.GetProperty("SourceId");
        Assert.Null(prop);
    }

    [Fact]
    public void ApplyReStreamRequest_SetsReStreamSourceIdAndModeAndDefaultStreamName()
    {
        var sut = CreateSut();

        sut.ApplyReStreamRequest("abc123", true);

        Assert.Equal("abc123", sut.ReStreamSourceId);
        Assert.True(sut.IsReStreamMode);
        Assert.StartsWith("NDI-", sut.StreamName);
    }

    [Fact]
    public void OnAppResumed_WhenBridgeCorroboratesActive_ShowsRestoredMessage()
    {
        _appStateRepoMock.Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot(null, "X", true, null));
        _bridgeMock.SetupGet(b => b.IsActive).Returns(true);
        var sut = CreateSut();

        _lifecycleMock.Raise(l => l.AppResumed += null);

        Assert.True(sut.IsOutputActive);
        Assert.Equal("Output session restored.", sut.StatusMessage);
        Assert.Equal("X", sut.StreamName);
    }

    [Fact]
    public void OnAppResumed_WhenBridgeDoesNotCorroborate_ShowsTapStartAndClearsPersistedFlag()
    {
        _appStateRepoMock.Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot(null, "X", true, null));
        _bridgeMock.SetupGet(b => b.IsActive).Returns(false);
        var sut = CreateSut();

        _lifecycleMock.Raise(l => l.AppResumed += null);

        Assert.False(sut.IsOutputActive);
        Assert.Equal("Tap Start to resume output", sut.StatusMessage);
        _appStateRepoMock.Verify(r => r.SaveAsync(It.Is<AppStateSnapshot>(
            s => s.IsOutputActive == false && s.StreamName == "X")), Times.Once);
    }

    [Fact]
    public async Task OutputStatusChanged_WhenBridgeReportsInactive_CorrectsIsOutputActiveAndStatusMessage()
    {
        var sut = CreateSut();
        await sut.StartOutputCommand.ExecuteAsync(null);
        _bridgeMock.SetupGet(b => b.IsActive).Returns(false);

        _bridgeMock.Raise(b => b.OutputStatusChanged += null, EventArgs.Empty);

        Assert.False(sut.IsOutputActive);
        Assert.Equal("Output stopped", sut.StatusMessage);
    }

    [Fact]
    public async Task OutputStatusChanged_WhenBridgeStillActive_DoesNotChangeIsOutputActiveOrStatusMessage()
    {
        var sut = CreateSut();
        await sut.StartOutputCommand.ExecuteAsync(null);
        _bridgeMock.SetupGet(b => b.IsActive).Returns(true);

        _bridgeMock.Raise(b => b.OutputStatusChanged += null, EventArgs.Empty);

        Assert.True(sut.IsOutputActive);
        Assert.Equal("Output active", sut.StatusMessage);
    }

    [Fact]
    public async Task ApplyResumeRequestCommand_WhenStreamNamePersisted_PrePopulatesWithoutStartingOutput()
    {
        _appStateRepoMock.Setup(r => r.RestoreStateAsync())
            .ReturnsAsync(new AppStateSnapshot(null, "MyStream", true, null));

        var sut = CreateSut();

        await sut.ApplyResumeRequestCommand.ExecuteAsync(null);

        Assert.Equal("MyStream", sut.StreamName);
        Assert.Equal("Tap Start to resume output", sut.StatusMessage);
        Assert.False(sut.IsOutputActive);
        _bridgeMock.Verify(b => b.StartOutputAsync(
            It.IsAny<string>(), It.IsAny<VideoInputKind>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ApplyResumeRequestCommand_WhenNoPersistedStreamName_LeavesStreamNameAndStatusUnchanged()
    {
        var sut = CreateSut();
        var originalStreamName = sut.StreamName;
        var originalStatusMessage = sut.StatusMessage;

        await sut.ApplyResumeRequestCommand.ExecuteAsync(null);

        Assert.Equal(originalStreamName, sut.StreamName);
        Assert.Equal(originalStatusMessage, sut.StatusMessage);
        Assert.False(sut.IsOutputActive);
    }

    // ── #349: IsStatusError + recovery-hint wording ──────────────────────────────────────────

    [Fact]
    public void Constructor_InitialStatusIsInformationalAndNotAnnounced()
    {
        var sut = CreateSut();

        Assert.False(sut.IsStatusError);
        Assert.Empty(_announcer.Announcements);
    }

    [Fact]
    public async Task StartOutputCommand_WithEmptyStreamName_FlagsStatusAsErrorAndAnnounces()
    {
        var sut = CreateSut();
        sut.StreamName = string.Empty;

        await sut.StartOutputCommand.ExecuteAsync(null);

        Assert.True(sut.IsStatusError);
        Assert.Equal(
            new[] { "Please enter a stream name before starting output." },
            _announcer.Announcements);
    }

    [Fact]
    public async Task StartOutputCommand_WhenPermissionDeclined_FlagsStatusAsErrorAndAnnounces()
    {
        _bridgeMock.Setup(b => b.StartOutputAsync(
                It.IsAny<string>(), It.IsAny<VideoInputKind>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var sut = CreateSut();
        await sut.StartOutputCommand.ExecuteAsync(null);

        Assert.True(sut.IsStatusError);
        Assert.Equal("Permission declined — output not started.", sut.StatusMessage);
        Assert.Contains("Permission declined — output not started.", _announcer.Announcements);
    }

    [Fact]
    public async Task StartOutputCommand_WhenBridgeThrows_FlagsStatusAsErrorWithRecoveryHint()
    {
        _bridgeMock.Setup(b => b.StartOutputAsync(
                It.IsAny<string>(), It.IsAny<VideoInputKind>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("NDI send failed"));

        var sut = CreateSut();
        await sut.StartOutputCommand.ExecuteAsync(null);

        Assert.True(sut.IsStatusError);
        Assert.Equal(
            "Output failed to start: NDI send failed. Tap Start to try again.",
            sut.StatusMessage);
        Assert.Contains(
            "Output failed to start: NDI send failed. Tap Start to try again.",
            _announcer.Announcements);
    }

    [Fact]
    public async Task StartOutputCommand_WhenBridgeThrowsWithTrailingPeriod_DoesNotDoubleThePeriod()
    {
        _bridgeMock.Setup(b => b.StartOutputAsync(
                It.IsAny<string>(), It.IsAny<VideoInputKind>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Runtime failed."));

        var sut = CreateSut();
        await sut.StartOutputCommand.ExecuteAsync(null);

        Assert.Equal(
            "Output failed to start: Runtime failed. Tap Start to try again.",
            sut.StatusMessage);
    }

    [Fact]
    public async Task StartOutputCommand_OnSuccess_KeepsStatusInformationalAndAnnouncesIt()
    {
        var sut = CreateSut();
        await sut.StartOutputCommand.ExecuteAsync(null);

        Assert.Equal("Output active", sut.StatusMessage);
        Assert.False(sut.IsStatusError);
        Assert.Contains("Output active", _announcer.Announcements);
    }

    [Fact]
    public async Task StartOutputCommand_AfterFailure_SuccessfulRetryClearsErrorFlag()
    {
        _bridgeMock.Setup(b => b.StartOutputAsync(
                It.IsAny<string>(), It.IsAny<VideoInputKind>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var sut = CreateSut();
        await sut.StartOutputCommand.ExecuteAsync(null);
        Assert.True(sut.IsStatusError);

        _bridgeMock.Setup(b => b.StartOutputAsync(
                It.IsAny<string>(), It.IsAny<VideoInputKind>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        await sut.StartOutputCommand.ExecuteAsync(null);

        Assert.False(sut.IsStatusError);
        Assert.Equal("Output active", sut.StatusMessage);
        Assert.True(sut.IsOutputActive);
    }

    [Fact]
    public async Task StopOutputCommand_ClearsStatusAndErrorFlag()
    {
        _bridgeMock.Setup(b => b.StartOutputAsync(
                It.IsAny<string>(), It.IsAny<VideoInputKind>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var sut = CreateSut();
        await sut.StartOutputCommand.ExecuteAsync(null);
        await sut.StopOutputCommand.ExecuteAsync(null);

        Assert.Null(sut.StatusMessage);
        Assert.False(sut.IsStatusError);
    }

    [Fact]
    public void ApplyReStreamRequest_AfterFailure_ResetsErrorFlag()
    {
        var sut = CreateSut();
        sut.StreamName = string.Empty;
        sut.StartOutputCommand.Execute(null);

        sut.ApplyReStreamRequest("abc123", true);

        Assert.False(sut.IsStatusError);
    }

    [Fact]
    public void OutputStatusChanged_WhenBridgeStopsItself_IsInformational()
    {
        var sut = CreateSut();
        sut.StartOutputCommand.Execute(null);
        _bridgeMock.SetupGet(b => b.IsActive).Returns(false);

        _bridgeMock.Raise(b => b.OutputStatusChanged += null, EventArgs.Empty);

        Assert.Equal("Output stopped", sut.StatusMessage);
        Assert.False(sut.IsStatusError);
    }

    [Fact]
    public void IsStatusError_RaisesPropertyChanged()
    {
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        sut.StreamName = string.Empty;
        sut.StartOutputCommand.Execute(null);

        Assert.Contains(nameof(OutputViewModel.IsStatusError), changed);
    }

    // ── #345 OUT-03: every status transition is announced exactly once ──────────────────────

    [Fact]
    public async Task SameStatusTwice_IsAnnouncedOnce()
    {
        var sut = CreateSut();
        sut.StreamName = string.Empty;

        await sut.StartOutputCommand.ExecuteAsync(null);
        await sut.StartOutputCommand.ExecuteAsync(null);

        Assert.Single(_announcer.Announcements);
    }

    // ── #346 OUT-07: row-tap commands mirror the Switch's own plain flip ────────────────────

    [Fact]
    public void ToggleOutputModeCommand_FlipsIsReStreamMode_WithoutRenamingOrPersisting()
    {
        var sut = CreateSut();
        var originalStreamName = sut.StreamName;

        sut.ToggleOutputModeCommand.Execute(null);
        Assert.True(sut.IsReStreamMode);

        sut.ToggleOutputModeCommand.Execute(null);
        Assert.False(sut.IsReStreamMode);

        Assert.Equal(originalStreamName, sut.StreamName);
        _appStateRepoMock.Verify(r => r.SaveAsync(It.IsAny<AppStateSnapshot>()), Times.Never);
    }

    [Fact]
    public void ToggleMicrophoneCommand_FlipsCaptureMicrophone()
    {
        var sut = CreateSut();

        sut.ToggleMicrophoneCommand.Execute(null);
        Assert.True(sut.CaptureMicrophone);

        sut.ToggleMicrophoneCommand.Execute(null);
        Assert.False(sut.CaptureMicrophone);
    }

    [Fact]
    public async Task ToggleMicrophoneCommand_CannotExecuteWhileOutputIsActive()
    {
        var sut = CreateSut();
        await sut.StartOutputCommand.ExecuteAsync(null);

        // Note: RelayCommand.Execute() does not itself gate on CanExecute — only a binding
        // (e.g. MAUI's TapGestureRecognizer) checks it before invoking. CanExecute is the
        // contract under test here.
        Assert.False(sut.ToggleMicrophoneCommand.CanExecute(null));

        await sut.StopOutputCommand.ExecuteAsync(null);
        Assert.True(sut.ToggleMicrophoneCommand.CanExecute(null));
    }

    [Fact]
    public void IsOutputActive_Change_RaisesCanExecuteChangedForToggleMicrophone()
    {
        var sut = CreateSut();
        var raised = 0;
        sut.ToggleMicrophoneCommand.CanExecuteChanged += (_, _) => raised++;

        sut.IsOutputActive = true;

        Assert.Equal(1, raised);
    }
}
