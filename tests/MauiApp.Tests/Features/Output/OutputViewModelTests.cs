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
        _dispatcher);

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
}
