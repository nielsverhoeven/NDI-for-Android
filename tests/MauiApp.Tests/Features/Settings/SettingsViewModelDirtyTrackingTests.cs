using Moq;
using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Repositories;
using NdiForAndroid.Features.Settings.Services;
using NdiForAndroid.Features.Settings.ViewModels;
using NdiForAndroid.Features.Sources.Repositories;
using Xunit;

namespace NdiForAndroid.Tests.Features.Settings;

/// <summary>
/// Extended SettingsViewModel tests covering dirty-tracking, CanApply gate,
/// section navigation, and edit/remove/move flows not covered by the baseline suite.
/// </summary>
public sealed class SettingsViewModelDirtyTrackingTests
{
    private readonly Mock<ISettingsRepository> _repositoryMock = new();
    private readonly ISettingsValidationService _validationService = new SettingsValidationService();
    private readonly Mock<ISettingsPlatformService> _platformServiceMock = new();
    private readonly Mock<ISourceRepository> _sourceRepositoryMock = new();

    private SettingsViewModel CreateSut()
    {
        _platformServiceMock
            .Setup(s => s.GetAppInfo())
            .Returns(new SettingsAppInfo("NDI for Android", "2.0.0", "42"));

        _sourceRepositoryMock
            .Setup(r => r.GetCachedSourcesAsync())
            .ReturnsAsync(Array.Empty<NdiForAndroid.Features.Sources.Models.NdiSource>());

        return new SettingsViewModel(
            _repositoryMock.Object,
            _validationService,
            _platformServiceMock.Object,
            _sourceRepositoryMock.Object);
    }

    private void SetupDefaultRepository() =>
        _repositoryMock
            .Setup(r => r.GetSettingsAsync())
            .ReturnsAsync(NdiSettingsSnapshot.CreateDefault());

    // ─── Constructor — AppInfo wiring ──────────────────────────────────────────

    [Fact]
    public void Constructor_PopulatesAppNameFromPlatformService()
    {
        var sut = CreateSut();

        Assert.Equal("NDI for Android", sut.AppName);
    }

    [Fact]
    public void Constructor_PopulatesAppVersionBuildFromPlatformService()
    {
        var sut = CreateSut();

        Assert.Equal("2.0.0 (42)", sut.AppVersionBuild);
    }

    // ─── Dirty tracking — individual field triggers ────────────────────────────

    [Fact]
    public async Task DiscoveryHost_ChangedAfterLoad_SetsPendingChanges()
    {
        SetupDefaultRepository();
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.DiscoveryHost = "10.0.0.99";

        Assert.True(sut.HasPendingChanges);
    }

    [Fact]
    public async Task DiscoveryPort_ChangedAfterLoad_SetsPendingChanges()
    {
        SetupDefaultRepository();
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.DiscoveryPort = "5961";

        Assert.True(sut.HasPendingChanges);
    }

    [Fact]
    public async Task SelectedThemeOption_ChangedAfterLoad_SetsPendingChanges()
    {
        SetupDefaultRepository();
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.SelectedThemeOption = "Dark";

        Assert.True(sut.HasPendingChanges);
    }

    [Fact]
    public async Task SelectedAccentColor_ChangedAfterLoad_SetsPendingChanges()
    {
        SetupDefaultRepository();
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.SelectedAccentColor = "Orange";

        Assert.True(sut.HasPendingChanges);
    }

    [Fact]
    public async Task DeveloperModeEnabled_ChangedAfterLoad_SetsPendingChanges()
    {
        SetupDefaultRepository();
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.DeveloperModeEnabled = true;

        Assert.True(sut.HasPendingChanges);
    }

    // ─── Dirty tracking — revert clears pending ────────────────────────────────

    [Fact]
    public async Task DiscoveryHost_RevertedToBaseline_ClearsPendingChanges()
    {
        // Baseline: DiscoveryHost = null (default)
        SetupDefaultRepository();
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.DiscoveryHost = "10.0.0.1"; // dirty
        sut.DiscoveryHost = null;        // reverted

        Assert.False(sut.HasPendingChanges);
    }

    // ─── CanApply gate — invalid host ──────────────────────────────────────────

    [Fact]
    public async Task CanApply_InvalidHost_ReturnsFalse()
    {
        SetupDefaultRepository();
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        // Set an invalid host — this marks pending but the build step will fail
        sut.DiscoveryHost = "invalid host with spaces!";

        // HasPendingChanges should be true (something changed)
        Assert.True(sut.HasPendingChanges);
        // But CanApply should be false because the host is invalid
        Assert.False(sut.ApplyCommand.CanExecute(null));
    }

    // ─── CanApply gate — valid changes re-enable Apply ─────────────────────────

    [Fact]
    public async Task CanApply_ValidHostChange_ReturnsTrue()
    {
        SetupDefaultRepository();
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.DiscoveryHost = "10.0.0.50";

        Assert.True(sut.ApplyCommand.CanExecute(null));
    }

    // ─── Apply — success resets dirty state ────────────────────────────────────

    [Fact]
    public async Task ApplyCommand_OnSuccess_ClearsHasPendingChanges()
    {
        SetupDefaultRepository();
        _repositoryMock.Setup(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.DiscoveryHost = "10.0.0.1";

        await sut.ApplyCommand.ExecuteAsync(null);

        Assert.False(sut.HasPendingChanges);
    }

    [Fact]
    public async Task ApplyCommand_OnSuccess_SetsIsAppliedTrue()
    {
        SetupDefaultRepository();
        _repositoryMock.Setup(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.DiscoveryHost = "10.0.0.1";

        await sut.ApplyCommand.ExecuteAsync(null);

        Assert.True(sut.IsApplied);
    }

    // ─── EditDiscoveryServer ────────────────────────────────────────────────────

    [Fact]
    public void EditDiscoveryServerCommand_PopulatesInputFields()
    {
        var sut = CreateSut();
        sut.DiscoveryServerEndpointInput = "10.0.0.5:5960";
        sut.AddOrUpdateDiscoveryServerCommand.Execute(null);

        var itemToEdit = sut.DiscoveryServers[0];
        sut.EditDiscoveryServerCommand.Execute(itemToEdit);

        Assert.Equal("10.0.0.5:5960", sut.DiscoveryServerEndpointInput);
    }

    [Fact]
    public void EditDiscoveryServerCommand_SetsActionTextToUpdateServer()
    {
        var sut = CreateSut();
        sut.DiscoveryServerEndpointInput = "10.0.0.5:5960";
        sut.AddOrUpdateDiscoveryServerCommand.Execute(null);

        sut.EditDiscoveryServerCommand.Execute(sut.DiscoveryServers[0]);

        Assert.Equal("Update Server", sut.DiscoveryServerActionText);
    }

    [Fact]
    public void EditDiscoveryServerCommand_Null_DoesNothing()
    {
        var sut = CreateSut();

        // Should not throw
        sut.EditDiscoveryServerCommand.Execute(null);

        Assert.Equal("Add Server", sut.DiscoveryServerActionText);
    }

    // ─── RemoveDiscoveryServer ─────────────────────────────────────────────────

    [Fact]
    public void RemoveDiscoveryServerCommand_RemovesItem()
    {
        var sut = CreateSut();
        sut.DiscoveryServerEndpointInput = "10.0.0.5:5960";
        sut.AddOrUpdateDiscoveryServerCommand.Execute(null);

        sut.RemoveDiscoveryServerCommand.Execute(sut.DiscoveryServers[0]);

        Assert.Empty(sut.DiscoveryServers);
    }

    [Fact]
    public async Task RemoveDiscoveryServerCommand_MarksPendingChanges()
    {
        SetupDefaultRepository();
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        // Add then apply to establish baseline with one server
        _repositoryMock.Setup(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()))
            .Returns(Task.CompletedTask);

        sut.DiscoveryServerEndpointInput = "10.0.0.5:5960";
        sut.AddOrUpdateDiscoveryServerCommand.Execute(null);
        await sut.ApplyCommand.ExecuteAsync(null);
        // Now baseline has one server, pending = false

        sut.RemoveDiscoveryServerCommand.Execute(sut.DiscoveryServers[0]);

        Assert.True(sut.HasPendingChanges);
    }

    [Fact]
    public void RemoveDiscoveryServerCommand_WhenEditingThatItem_ResetsEditState()
    {
        var sut = CreateSut();

        sut.DiscoveryServerEndpointInput = "10.0.0.5:5960";
        sut.AddOrUpdateDiscoveryServerCommand.Execute(null);

        var item = sut.DiscoveryServers[0];
        sut.EditDiscoveryServerCommand.Execute(item);

        // Now editing — remove the item being edited
        sut.RemoveDiscoveryServerCommand.Execute(item);

        Assert.Equal("Add Server", sut.DiscoveryServerActionText);
        Assert.Equal(string.Empty, sut.DiscoveryServerEndpointInput);
    }

    [Fact]
    public void RemoveDiscoveryServerCommand_Null_DoesNothing()
    {
        var sut = CreateSut();
        sut.DiscoveryServerEndpointInput = "10.0.0.5:5960";
        sut.AddOrUpdateDiscoveryServerCommand.Execute(null);

        sut.RemoveDiscoveryServerCommand.Execute(null);

        Assert.Single(sut.DiscoveryServers);
    }

    // ─── MoveDiscoveryServerUp ─────────────────────────────────────────────────

    [Fact]
    public void MoveDiscoveryServerUpCommand_ReordersListCorrectly()
    {
        var sut = CreateSut();

        sut.DiscoveryServerEndpointInput = "alpha:5960";
        sut.AddOrUpdateDiscoveryServerCommand.Execute(null);

        sut.DiscoveryServerEndpointInput = "beta:5961";
        sut.AddOrUpdateDiscoveryServerCommand.Execute(null);

        var second = sut.DiscoveryServers[1];
        sut.MoveDiscoveryServerUpCommand.Execute(second);

        Assert.Equal("beta", sut.DiscoveryServers[0].Host);
        Assert.Equal("alpha", sut.DiscoveryServers[1].Host);
    }

    [Fact]
    public void MoveDiscoveryServerUpCommand_FirstItem_DoesNothing()
    {
        var sut = CreateSut();

        sut.DiscoveryServerEndpointInput = "alpha:5960";
        sut.AddOrUpdateDiscoveryServerCommand.Execute(null);

        sut.DiscoveryServerEndpointInput = "beta:5961";
        sut.AddOrUpdateDiscoveryServerCommand.Execute(null);

        sut.MoveDiscoveryServerUpCommand.Execute(sut.DiscoveryServers[0]); // already first

        Assert.Equal("alpha", sut.DiscoveryServers[0].Host);
        Assert.Equal("beta", sut.DiscoveryServers[1].Host);
    }

    [Fact]
    public void MoveDiscoveryServerDownCommand_LastItem_DoesNothing()
    {
        var sut = CreateSut();

        sut.DiscoveryServerEndpointInput = "alpha:5960";
        sut.AddOrUpdateDiscoveryServerCommand.Execute(null);

        sut.DiscoveryServerEndpointInput = "beta:5961";
        sut.AddOrUpdateDiscoveryServerCommand.Execute(null);

        sut.MoveDiscoveryServerDownCommand.Execute(sut.DiscoveryServers[1]); // already last

        Assert.Equal("alpha", sut.DiscoveryServers[0].Host);
        Assert.Equal("beta", sut.DiscoveryServers[1].Host);
    }

    // ─── SelectSection — computed section flags ─────────────────────────────────

    [Fact]
    public void SelectSectionCommand_General_SetsIsGeneralSectionSelected()
    {
        var sut = CreateSut();
        sut.SelectSectionCommand.Execute(SettingsSection.Appearance); // move away first

        sut.SelectSectionCommand.Execute(SettingsSection.General);

        Assert.True(sut.IsGeneralSectionSelected);
        Assert.False(sut.IsAppearanceSectionSelected);
        Assert.False(sut.IsDiscoverySectionSelected);
        Assert.False(sut.IsDeveloperToolsSectionSelected);
        Assert.False(sut.IsAboutSectionSelected);
    }

    [Fact]
    public void SelectSectionCommand_Appearance_SetsIsAppearanceSectionSelected()
    {
        var sut = CreateSut();
        sut.SelectSectionCommand.Execute(SettingsSection.Appearance);

        Assert.False(sut.IsGeneralSectionSelected);
        Assert.True(sut.IsAppearanceSectionSelected);
    }

    [Fact]
    public void SelectSectionCommand_Discovery_SetsIsDiscoverySectionSelected()
    {
        var sut = CreateSut();
        sut.SelectSectionCommand.Execute(SettingsSection.Discovery);

        Assert.True(sut.IsDiscoverySectionSelected);
    }

    [Fact]
    public void SelectSectionCommand_DeveloperTools_SetsIsDeveloperToolsSectionSelected()
    {
        var sut = CreateSut();
        sut.SelectSectionCommand.Execute(SettingsSection.DeveloperTools);

        Assert.True(sut.IsDeveloperToolsSectionSelected);
    }

    [Fact]
    public void SelectSectionCommand_About_SetsIsAboutSectionSelected()
    {
        var sut = CreateSut();
        sut.SelectSectionCommand.Execute(SettingsSection.About);

        Assert.True(sut.IsAboutSectionSelected);
    }

    // ─── LoadCommand — CachedSourceRegistry populated ─────────────────────────

    [Fact]
    public async Task LoadCommand_WithCachedSources_PopulatesCachedSourceRegistry()
    {
        SetupDefaultRepository();
        var sut = CreateSut(); // sets up mock with empty array default

        // Override AFTER CreateSut() so this setup wins
        _sourceRepositoryMock
            .Setup(r => r.GetCachedSourcesAsync())
            .ReturnsAsync(new[]
            {
                new NdiForAndroid.Features.Sources.Models.NdiSource(
                    "id-1", "Camera A", "10.0.0.1", true, 1_700_000_000_000L),
            });

        await sut.LoadCommand.ExecuteAsync(null);

        Assert.Single(sut.CachedSourceRegistry);
        Assert.Equal("Camera A", sut.CachedSourceRegistry[0].SourceName);
        Assert.Equal("id-1", sut.CachedSourceRegistry[0].RegistryKey);
        Assert.Equal("Available", sut.CachedSourceRegistry[0].State);
    }

    [Fact]
    public async Task LoadCommand_SourceRepositoryThrows_CachedSourceRegistryIsEmpty()
    {
        SetupDefaultRepository();
        _sourceRepositoryMock.Reset();
        _sourceRepositoryMock
            .Setup(r => r.GetCachedSourcesAsync())
            .ThrowsAsync(new InvalidOperationException("NDI runtime unavailable"));

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null); // must not throw

        Assert.Empty(sut.CachedSourceRegistry);
    }

    // ─── AddOrUpdateDiscoveryServer — update existing item ─────────────────────

    [Fact]
    public void AddOrUpdateDiscoveryServerCommand_UpdateExistingItem_ModifiesInPlace()
    {
        var sut = CreateSut();

        // Add original
        sut.DiscoveryServerEndpointInput = "original-host:5960";
        sut.AddOrUpdateDiscoveryServerCommand.Execute(null);

        // Edit it
        var item = sut.DiscoveryServers[0];
        sut.EditDiscoveryServerCommand.Execute(item);

        sut.DiscoveryServerEndpointInput = "updated-host:7000";
        sut.AddOrUpdateDiscoveryServerCommand.Execute(null);

        Assert.Single(sut.DiscoveryServers);
        Assert.Equal("updated-host", sut.DiscoveryServers[0].Host);
        Assert.Equal("7000", sut.DiscoveryServers[0].Port);
        Assert.Equal("Add Server", sut.DiscoveryServerActionText);
    }

    // ─── ValidationError cleared after successful apply ────────────────────────

    [Fact]
    public async Task ApplyCommand_AfterPreviousValidationError_ClearsError()
    {
        SetupDefaultRepository();
        _repositoryMock.Setup(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        // Trigger validation error with invalid port
        sut.DiscoveryPort = "not-a-port";
        await sut.ApplyCommand.ExecuteAsync(null);
        Assert.NotNull(sut.ValidationError);

        // Now fix and apply successfully
        sut.DiscoveryPort = null;
        sut.DiscoveryHost = "10.0.0.1"; // introduce a real change
        await sut.ApplyCommand.ExecuteAsync(null);

        Assert.Null(sut.ValidationError);
        Assert.True(sut.IsApplied);
    }
}
