using NdiForAndroid.Services;
using Moq;
using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Repositories;
using NdiForAndroid.Features.Settings.Services;
using NdiForAndroid.Features.Settings.ViewModels;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.NdiBridge;
using Xunit;

namespace NdiForAndroid.Tests.Features.Settings;

public class SettingsViewModelTests
{
    private readonly Mock<ISettingsRepository> _repositoryMock = new();
    private readonly ISettingsValidationService _validationService = new SettingsValidationService();
    private readonly Mock<ISettingsPlatformService> _platformServiceMock = new();
    private readonly Mock<ISourceRepository> _sourceRepositoryMock = new();
    private readonly Mock<INdiDiscoveryBridge> _discoveryBridgeMock = new();

    private SettingsViewModel CreateSut(Mock<INdiVersionInfo>? versionInfo = null)
    {
        _platformServiceMock.Setup(service => service.GetAppInfo())
            .Returns(new SettingsAppInfo("NDI for Android", "1.0.0", "100"));

        _sourceRepositoryMock.Setup(repository => repository.GetCachedSourcesAsync())
            .ReturnsAsync(Array.Empty<NdiForAndroid.Features.Sources.Models.NdiSource>());

        _repositoryMock.Setup(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()))
            .Returns(Task.CompletedTask);

        return new SettingsViewModel(
            _repositoryMock.Object,
            _validationService,
            _platformServiceMock.Object,
            _sourceRepositoryMock.Object,
            (versionInfo ?? new Mock<INdiVersionInfo>()).Object,
            _discoveryBridgeMock.Object,
            new FakeMainThreadDispatcher());
    }

    [Fact]
    public async Task LoadCommand_PopulatesFieldsFromRepository()
    {
        _repositoryMock.Setup(r => r.GetSettingsAsync())
            .ReturnsAsync(new NdiSettingsSnapshot(
                true,
                100,
                ThemeMode.Dark,
                AccentColorOption.Teal,
                new[]
                {
                    new DiscoveryServerPreference("192.168.1.10", 5961, true, 0, "Studio rack"),
                }));

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        Assert.True(sut.DeveloperModeEnabled);
        Assert.Equal("Dark", sut.SelectedThemeOption);
        Assert.Equal("Teal", sut.SelectedAccentColor);
        var server = Assert.Single(sut.DiscoveryServers);
        Assert.Equal("192.168.1.10", server.Host);
        Assert.Equal("Studio rack", server.DisplayName);
        Assert.Equal("Studio rack", server.NameDisplay);
    }

    [Fact]
    public async Task LoadCommand_DoesNotTriggerSave()
    {
        _repositoryMock.Setup(r => r.GetSettingsAsync())
            .ReturnsAsync(NdiSettingsSnapshot.CreateDefault());

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        _repositoryMock.Verify(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task AddDiscoveryServerCommand_ValidInput_AddsAndPersistsImmediately()
    {
        var sut = CreateSut();

        // Override AFTER CreateSut() so this setup wins over the default in CreateSut.
        NdiSettingsSnapshot? saved = null;
        _repositoryMock.Setup(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()))
            .Callback<NdiSettingsSnapshot>(s => saved = s)
            .Returns(Task.CompletedTask);

        sut.NewServerDisplayName = "Control room";
        sut.NewServerHost = "10.0.0.10";
        sut.NewServerPort = "5961";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);

        Assert.NotNull(saved);
        var server = Assert.Single(saved!.DiscoveryServers);
        Assert.Equal("10.0.0.10", server.Host);
        Assert.Equal(5961, server.Port);
        Assert.Equal("Control room", server.DisplayName);
        Assert.True(server.Enabled);

        // Input fields reset after a successful add.
        Assert.Equal(string.Empty, sut.NewServerDisplayName);
        Assert.Equal(string.Empty, sut.NewServerHost);
        Assert.Equal(string.Empty, sut.NewServerPort);
    }

    [Fact]
    public async Task AddDiscoveryServerCommand_EmptyPort_DefaultsTo5959()
    {
        var sut = CreateSut();
        sut.NewServerHost = "10.0.0.10";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);

        var item = Assert.Single(sut.DiscoveryServers);
        Assert.Equal("5959", item.Port);
    }

    [Fact]
    public async Task AddDiscoveryServerCommand_NoDisplayName_FallsBackToHostname()
    {
        var sut = CreateSut();
        sut.NewServerHost = "ndi-server.local";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);

        var item = Assert.Single(sut.DiscoveryServers);
        Assert.Null(item.DisplayName);
        Assert.Equal("ndi-server.local", item.NameDisplay);
    }

    [Fact]
    public async Task AddDiscoveryServerCommand_InvalidHost_SetsValidationErrorAndDoesNotSave()
    {
        var sut = CreateSut();
        sut.NewServerHost = "invalid host with spaces!";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);

        Assert.Empty(sut.DiscoveryServers);
        Assert.NotEqual(string.Empty, sut.DiscoveryServersValidationMessage);
        _repositoryMock.Verify(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task AddDiscoveryServerCommand_InvalidPort_SetsValidationErrorAndDoesNotSave()
    {
        var sut = CreateSut();
        sut.NewServerHost = "10.0.0.10";
        sut.NewServerPort = "99999";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);

        Assert.Empty(sut.DiscoveryServers);
        Assert.Contains("port", sut.DiscoveryServersValidationMessage, StringComparison.OrdinalIgnoreCase);
        _repositoryMock.Verify(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task AddDiscoveryServerCommand_DuplicateHostAndPort_SetsValidationError()
    {
        var sut = CreateSut();
        sut.NewServerHost = "10.1.0.7";
        sut.NewServerPort = "5960";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);

        sut.NewServerHost = "10.1.0.7";
        sut.NewServerPort = "5960";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);

        Assert.Single(sut.DiscoveryServers);
        Assert.Contains("duplicate", sut.DiscoveryServersValidationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MoveDiscoveryServerDownCommand_ReordersListAndPersists()
    {
        var sut = CreateSut();

        sut.NewServerHost = "10.1.0.10";
        sut.NewServerPort = "5960";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);

        sut.NewServerHost = "10.1.0.11";
        sut.NewServerPort = "5961";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);

        _repositoryMock.Invocations.Clear();

        var first = sut.DiscoveryServers[0];
        await sut.MoveDiscoveryServerDownCommand.ExecuteAsync(first);

        Assert.Equal("10.1.0.11", sut.DiscoveryServers[0].Host);
        Assert.Equal("10.1.0.10", sut.DiscoveryServers[1].Host);
        _repositoryMock.Verify(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()), Times.Once);
    }

    [Fact]
    public void NdiSdkVersion_RuntimeUnavailable_ShowsFallbackMessage()
    {
        var versionInfo = new Mock<INdiVersionInfo>();
        versionInfo.SetupGet(v => v.IsRuntimeAvailable).Returns(false);

        var sut = CreateSut(versionInfo);

        Assert.Equal("NDI runtime unavailable on this device", sut.NdiSdkVersion);
    }

    [Fact]
    public void NdiSdkVersion_RuntimeAvailable_ShowsNativeVersion()
    {
        var versionInfo = new Mock<INdiVersionInfo>();
        versionInfo.SetupGet(v => v.IsRuntimeAvailable).Returns(true);
        versionInfo.SetupGet(v => v.NativeVersion).Returns("NDI SDK ANDROID 6.3.1.0");

        var sut = CreateSut(versionInfo);

        Assert.Equal("NDI SDK ANDROID 6.3.1.0", sut.NdiSdkVersion);
    }
}
