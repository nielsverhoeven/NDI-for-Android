using Moq;
using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Repositories;
using NdiForAndroid.Features.Settings.ViewModels;
using Xunit;

namespace MauiApp.Tests.Features.Settings;

public class SettingsViewModelTests
{
    private readonly Mock<ISettingsRepository> _repositoryMock = new();
    private SettingsViewModel CreateSut() => new(_repositoryMock.Object);

    [Fact]
    public async Task LoadCommand_PopulatesFieldsFromRepository()
    {
        _repositoryMock.Setup(r => r.GetSettingsAsync())
            .ReturnsAsync(new NdiSettingsSnapshot("192.168.1.5", 5960, true, 100));

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        Assert.Equal("192.168.1.5", sut.DiscoveryHost);
        Assert.Equal("5960", sut.DiscoveryPort);
        Assert.True(sut.DeveloperModeEnabled);
    }

    [Fact]
    public async Task SaveCommand_ValidInput_CallsRepository()
    {
        NdiSettingsSnapshot? saved = null;
        _repositoryMock.Setup(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()))
            .Callback<NdiSettingsSnapshot>(s => saved = s)
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        sut.DiscoveryHost = "10.0.0.1";
        sut.DiscoveryPort = "5960";
        await sut.SaveCommand.ExecuteAsync(null);

        Assert.Null(sut.ValidationError);
        Assert.True(sut.IsSaved);
        Assert.NotNull(saved);
        Assert.Equal("10.0.0.1", saved!.DiscoveryHost);
        Assert.Equal(5960, saved.DiscoveryPort);
    }

    [Fact]
    public async Task SaveCommand_InvalidPort_SetsValidationError()
    {
        var sut = CreateSut();
        sut.DiscoveryPort = "not-a-number";
        await sut.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(sut.ValidationError);
        Assert.False(sut.IsSaved);
        _repositoryMock.Verify(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task SaveCommand_PortOutOfRange_SetsValidationError()
    {
        var sut = CreateSut();
        sut.DiscoveryPort = "99999";
        await sut.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(sut.ValidationError);
        Assert.False(sut.IsSaved);
    }
}
