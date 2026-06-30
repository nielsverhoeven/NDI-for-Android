using Moq;
using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Repositories;
using NdiForAndroid.Features.Settings.Services;
using NdiForAndroid.Features.Settings.ViewModels;
using NdiForAndroid.Features.Sources.Repositories;
using Xunit;

namespace NdiForAndroid.Tests.Features.Settings;

public class SettingsViewModelTests
{
    private readonly Mock<ISettingsRepository> _repositoryMock = new();
    private readonly ISettingsValidationService _validationService = new SettingsValidationService();
    private readonly Mock<ISettingsPlatformService> _platformServiceMock = new();
    private readonly Mock<ISourceRepository> _sourceRepositoryMock = new();

    private SettingsViewModel CreateSut()
    {
        _platformServiceMock.Setup(service => service.GetAppInfo())
            .Returns(new SettingsAppInfo("NDI for Android", "1.0.0", "100"));

        _sourceRepositoryMock.Setup(repository => repository.GetCachedSourcesAsync())
            .ReturnsAsync(Array.Empty<NdiForAndroid.Features.Sources.Models.NdiSource>());

        return new SettingsViewModel(_repositoryMock.Object, _validationService, _platformServiceMock.Object, _sourceRepositoryMock.Object);
    }

    [Fact]
    public async Task LoadCommand_PopulatesFieldsFromRepository()
    {
        _repositoryMock.Setup(r => r.GetSettingsAsync())
            .ReturnsAsync(new NdiSettingsSnapshot(
                "192.168.1.5",
                5960,
                true,
                100,
                ThemeMode.Dark,
                AccentColorOption.Teal,
                new[]
                {
                    new DiscoveryServerPreference("192.168.1.10", 5961, true, 0),
                }));

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        Assert.Equal("192.168.1.5", sut.DiscoveryHost);
        Assert.Equal("5960", sut.DiscoveryPort);
        Assert.True(sut.DeveloperModeEnabled);
        Assert.Equal("Dark", sut.SelectedThemeOption);
        Assert.Equal("Teal", sut.SelectedAccentColor);
        Assert.Single(sut.DiscoveryServers);
        Assert.Equal("192.168.1.10", sut.DiscoveryServers[0].Host);
    }

    [Fact]
    public async Task ApplyCommand_ValidInput_CallsRepository()
    {
        NdiSettingsSnapshot? saved = null;
        _repositoryMock.Setup(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()))
            .Callback<NdiSettingsSnapshot>(s => saved = s)
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        sut.DiscoveryHost = "10.0.0.1";
        sut.DiscoveryPort = "5960";
        sut.SelectedThemeOption = "Light";
        sut.SelectedAccentColor = "Red";
        sut.DiscoveryServerEndpointInput = "10.0.0.10:5961";
        sut.AddOrUpdateDiscoveryServerCommand.Execute(null);
        await sut.ApplyCommand.ExecuteAsync(null);

        Assert.Null(sut.ValidationError);
        Assert.True(sut.IsApplied);
        Assert.NotNull(saved);
        Assert.Equal("10.0.0.1", saved!.DiscoveryHost);
        Assert.Equal(5960, saved.DiscoveryPort);
        Assert.Equal(ThemeMode.Light, saved.ThemeMode);
        Assert.Equal(AccentColorOption.Red, saved.AccentColor);
        Assert.Single(saved.DiscoveryServers);
    }

    [Fact]
    public async Task ApplyCommand_InvalidPort_SetsValidationError()
    {
        var sut = CreateSut();
        sut.DiscoveryPort = "not-a-number";
        await sut.ApplyCommand.ExecuteAsync(null);

        Assert.NotNull(sut.ValidationError);
        Assert.False(sut.IsApplied);
        _repositoryMock.Verify(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task ApplyCommand_PortOutOfRange_SetsValidationError()
    {
        var sut = CreateSut();
        sut.DiscoveryPort = "99999";
        await sut.ApplyCommand.ExecuteAsync(null);

        Assert.NotNull(sut.ValidationError);
        Assert.False(sut.IsApplied);
    }

    [Fact]
    public void AddOrUpdateDiscoveryServerCommand_DuplicateHostAndPort_SetsValidationError()
    {
        var sut = CreateSut();
        sut.DiscoveryServerEndpointInput = "10.1.0.7:5960";
        sut.AddOrUpdateDiscoveryServerCommand.Execute(null);

        sut.DiscoveryServerEndpointInput = "10.1.0.7:5960";
        sut.AddOrUpdateDiscoveryServerCommand.Execute(null);

        Assert.Single(sut.DiscoveryServers);
        Assert.NotNull(sut.ValidationError);
    }

    [Fact]
    public void MoveDiscoveryServerDownCommand_ReordersList()
    {
        var sut = CreateSut();

        sut.DiscoveryServerEndpointInput = "10.1.0.10:5960";
        sut.AddOrUpdateDiscoveryServerCommand.Execute(null);

        sut.DiscoveryServerEndpointInput = "10.1.0.11:5961";
        sut.AddOrUpdateDiscoveryServerCommand.Execute(null);

        var first = sut.DiscoveryServers[0];
        sut.MoveDiscoveryServerDownCommand.Execute(first);

        Assert.Equal("10.1.0.11", sut.DiscoveryServers[0].Host);
        Assert.Equal("10.1.0.10", sut.DiscoveryServers[1].Host);
    }

    [Fact]
    public async Task ApplyCommand_LoadThenNoChanges_RemainsDisabled()
    {
        _repositoryMock.Setup(r => r.GetSettingsAsync())
            .ReturnsAsync(NdiSettingsSnapshot.CreateDefault());

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        Assert.False(sut.HasPendingChanges);
        Assert.False(sut.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public async Task ApplyCommand_ChangeAfterLoad_EnablesApply()
    {
        _repositoryMock.Setup(r => r.GetSettingsAsync())
            .ReturnsAsync(NdiSettingsSnapshot.CreateDefault());

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.DeveloperModeEnabled = true;

        Assert.True(sut.HasPendingChanges);
        Assert.True(sut.ApplyCommand.CanExecute(null));
    }
}
