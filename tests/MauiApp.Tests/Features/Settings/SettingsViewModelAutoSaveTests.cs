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

/// <summary>
/// Auto-save behavior tests (#292): every settings change persists immediately —
/// theme/accent/developer-mode toggles, discovery-server add/edit/remove/move/enable,
/// the edit-dialog flow, and the connection-status probe loop.
/// </summary>
public sealed class SettingsViewModelAutoSaveTests
{
    private readonly Mock<ISettingsRepository> _repositoryMock = new();
    private readonly ISettingsValidationService _validationService = new SettingsValidationService();
    private readonly Mock<ISettingsPlatformService> _platformServiceMock = new();
    private readonly Mock<ISourceRepository> _sourceRepositoryMock = new();
    private readonly Mock<INdiDiscoveryBridge> _discoveryBridgeMock = new();
    private readonly FakeUserPromptService _userPromptService = new();

    private SettingsViewModel CreateSut()
    {
        _platformServiceMock
            .Setup(s => s.GetAppInfo())
            .Returns(new SettingsAppInfo("NDI for Android", "2.0.0", "42"));

        _sourceRepositoryMock
            .Setup(r => r.GetCachedSourcesAsync())
            .ReturnsAsync(Array.Empty<NdiForAndroid.Features.Sources.Models.NdiSource>());

        _repositoryMock
            .Setup(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()))
            .Returns(Task.CompletedTask);

        return new SettingsViewModel(
            _repositoryMock.Object,
            _validationService,
            _platformServiceMock.Object,
            _sourceRepositoryMock.Object,
            new Mock<INdiVersionInfo>().Object,
            _discoveryBridgeMock.Object,
            new FakeMainThreadDispatcher(),
            _userPromptService);
    }

    private void SetupDefaultRepository() =>
        _repositoryMock
            .Setup(r => r.GetSettingsAsync())
            .ReturnsAsync(NdiSettingsSnapshot.CreateDefault());

    private async Task<SettingsViewModel> CreateLoadedSutAsync()
    {
        SetupDefaultRepository();
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        _repositoryMock.Invocations.Clear();
        return sut;
    }

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

    // ─── Auto-save — simple field changes persist immediately ──────────────────

    [Fact]
    public async Task SelectedThemeOption_Changed_PersistsImmediately()
    {
        var sut = await CreateLoadedSutAsync();

        sut.SelectedThemeOption = "Dark";

        _repositoryMock.Verify(r => r.SaveSettingsAsync(
            It.Is<NdiSettingsSnapshot>(s => s.ThemeMode == ThemeMode.Dark)), Times.Once);
    }

    [Fact]
    public async Task SelectedAccentColor_Changed_PersistsImmediately()
    {
        var sut = await CreateLoadedSutAsync();

        sut.SelectedAccentColor = "Orange";

        _repositoryMock.Verify(r => r.SaveSettingsAsync(
            It.Is<NdiSettingsSnapshot>(s => s.AccentColor == AccentColorOption.Orange)), Times.Once);
    }

    [Fact]
    public async Task SelectedThemeOption_ResetToNullOnTeardown_DoesNotPersist()
    {
        var sut = await CreateLoadedSutAsync();
        sut.SelectedThemeOption = "Dark";
        _repositoryMock.Invocations.Clear();

        // MAUI radio groups push null/empty through the binding when the page unloads.
        sut.SelectedThemeOption = null!;

        _repositoryMock.Verify(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task PersistAfterThemeReset_KeepsLastCommittedTheme()
    {
        var sut = await CreateLoadedSutAsync();
        sut.SelectedThemeOption = "Dark";
        sut.SelectedThemeOption = string.Empty; // teardown reset
        _repositoryMock.Invocations.Clear();

        // Any later save (e.g. a server toggle) must still carry the committed theme.
        sut.NewServerHost = "10.0.0.9";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);

        _repositoryMock.Verify(r => r.SaveSettingsAsync(
            It.Is<NdiSettingsSnapshot>(s => s.ThemeMode == ThemeMode.Dark)), Times.Once);
    }

    [Fact]
    public async Task SelectedAccentColor_ResetToEmptyOnTeardown_DoesNotPersist()
    {
        var sut = await CreateLoadedSutAsync();
        sut.SelectedAccentColor = "Red";
        _repositoryMock.Invocations.Clear();

        sut.SelectedAccentColor = string.Empty;

        _repositoryMock.Verify(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task DeveloperModeEnabled_Changed_PersistsImmediately()
    {
        var sut = await CreateLoadedSutAsync();

        sut.DeveloperModeEnabled = true;

        _repositoryMock.Verify(r => r.SaveSettingsAsync(
            It.Is<NdiSettingsSnapshot>(s => s.DeveloperModeEnabled)), Times.Once);
    }

    [Fact]
    public async Task ServerEnabledToggle_PersistsImmediately()
    {
        var sut = await CreateLoadedSutAsync();
        sut.NewServerHost = "10.0.0.5";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);
        _repositoryMock.Invocations.Clear();

        sut.DiscoveryServers[0].Enabled = false;

        _repositoryMock.Verify(r => r.SaveSettingsAsync(
            It.Is<NdiSettingsSnapshot>(s => !s.DiscoveryServers[0].Enabled)), Times.Once);
    }

    // ─── Edit dialog flow ──────────────────────────────────────────────────────

    [Fact]
    public async Task EditDiscoveryServerCommand_OpensDialogWithItemValues()
    {
        var sut = await CreateLoadedSutAsync();
        sut.NewServerDisplayName = "Rack A";
        sut.NewServerHost = "10.0.0.5";
        sut.NewServerPort = "5960";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);

        sut.EditDiscoveryServerCommand.Execute(sut.DiscoveryServers[0]);

        Assert.True(sut.IsEditServerDialogOpen);
        Assert.Equal("Rack A", sut.EditServerDisplayName);
        Assert.Equal("10.0.0.5", sut.EditServerHost);
        Assert.Equal("5960", sut.EditServerPort);
    }

    [Fact]
    public void EditDiscoveryServerCommand_Null_DoesNotOpenDialog()
    {
        var sut = CreateSut();

        sut.EditDiscoveryServerCommand.Execute(null);

        Assert.False(sut.IsEditServerDialogOpen);
    }

    [Fact]
    public async Task SaveEditedDiscoveryServerCommand_UpdatesItemClosesDialogAndPersists()
    {
        var sut = await CreateLoadedSutAsync();
        sut.NewServerHost = "original-host";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);
        sut.EditDiscoveryServerCommand.Execute(sut.DiscoveryServers[0]);
        _repositoryMock.Invocations.Clear();

        sut.EditServerDisplayName = "Renamed";
        sut.EditServerHost = "updated-host";
        sut.EditServerPort = "7000";
        await sut.SaveEditedDiscoveryServerCommand.ExecuteAsync(null);

        Assert.False(sut.IsEditServerDialogOpen);
        var item = Assert.Single(sut.DiscoveryServers);
        Assert.Equal("Renamed", item.DisplayName);
        Assert.Equal("updated-host", item.Host);
        Assert.Equal("7000", item.Port);
        _repositoryMock.Verify(r => r.SaveSettingsAsync(
            It.Is<NdiSettingsSnapshot>(s => s.DiscoveryServers[0].Host == "updated-host"
                                         && s.DiscoveryServers[0].DisplayName == "Renamed")), Times.Once);
    }

    [Fact]
    public async Task SaveEditedDiscoveryServerCommand_InvalidHost_KeepsDialogOpenAndDoesNotSave()
    {
        var sut = await CreateLoadedSutAsync();
        sut.NewServerHost = "original-host";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);
        sut.EditDiscoveryServerCommand.Execute(sut.DiscoveryServers[0]);
        _repositoryMock.Invocations.Clear();

        sut.EditServerHost = "bad host!";
        await sut.SaveEditedDiscoveryServerCommand.ExecuteAsync(null);

        Assert.True(sut.IsEditServerDialogOpen);
        Assert.NotEqual(string.Empty, sut.EditServerValidationMessage);
        Assert.Equal("original-host", sut.DiscoveryServers[0].Host);
        _repositoryMock.Verify(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task SaveEditedDiscoveryServerCommand_SameEndpointAsSelf_IsNotADuplicate()
    {
        var sut = await CreateLoadedSutAsync();
        sut.NewServerHost = "10.0.0.5";
        sut.NewServerPort = "5960";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);
        sut.EditDiscoveryServerCommand.Execute(sut.DiscoveryServers[0]);

        // Change only the display name — host:port stays identical to the row being edited.
        sut.EditServerDisplayName = "Just a name";
        await sut.SaveEditedDiscoveryServerCommand.ExecuteAsync(null);

        Assert.False(sut.IsEditServerDialogOpen);
        Assert.Equal("Just a name", sut.DiscoveryServers[0].DisplayName);
    }

    [Fact]
    public async Task SaveEditedDiscoveryServerCommand_DuplicateOfOtherRow_KeepsDialogOpen()
    {
        var sut = await CreateLoadedSutAsync();
        sut.NewServerHost = "10.0.0.5";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);
        sut.NewServerHost = "10.0.0.6";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);

        sut.EditDiscoveryServerCommand.Execute(sut.DiscoveryServers[1]);
        sut.EditServerHost = "10.0.0.5"; // collides with the first row (same default port)
        await sut.SaveEditedDiscoveryServerCommand.ExecuteAsync(null);

        Assert.True(sut.IsEditServerDialogOpen);
        Assert.Contains("duplicate", sut.EditServerValidationMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("10.0.0.6", sut.DiscoveryServers[1].Host);
    }

    [Fact]
    public async Task CancelEditDiscoveryServerCommand_ClosesDialogWithoutChanges()
    {
        var sut = await CreateLoadedSutAsync();
        sut.NewServerHost = "10.0.0.5";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);
        sut.EditDiscoveryServerCommand.Execute(sut.DiscoveryServers[0]);
        _repositoryMock.Invocations.Clear();

        sut.EditServerHost = "something-else";
        sut.CancelEditDiscoveryServerCommand.Execute(null);

        Assert.False(sut.IsEditServerDialogOpen);
        Assert.Equal("10.0.0.5", sut.DiscoveryServers[0].Host);
        _repositoryMock.Verify(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()), Times.Never);
    }

    // ─── RemoveDiscoveryServer ─────────────────────────────────────────────────

    [Fact]
    public async Task RemoveDiscoveryServerCommand_RemovesItemAndPersists()
    {
        var sut = await CreateLoadedSutAsync();
        sut.NewServerHost = "10.0.0.5";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);
        _repositoryMock.Invocations.Clear();

        await sut.RemoveDiscoveryServerCommand.ExecuteAsync(sut.DiscoveryServers[0]);

        Assert.Empty(sut.DiscoveryServers);
        _repositoryMock.Verify(r => r.SaveSettingsAsync(
            It.Is<NdiSettingsSnapshot>(s => s.DiscoveryServers.Count == 0)), Times.Once);
    }

    [Fact]
    public async Task RemoveDiscoveryServerCommand_WhileEditingThatItem_ClosesDialog()
    {
        var sut = await CreateLoadedSutAsync();
        sut.NewServerHost = "10.0.0.5";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);

        var item = sut.DiscoveryServers[0];
        sut.EditDiscoveryServerCommand.Execute(item);
        await sut.RemoveDiscoveryServerCommand.ExecuteAsync(item);

        Assert.False(sut.IsEditServerDialogOpen);
        Assert.Empty(sut.DiscoveryServers);
    }

    // ─── RemoveDiscoveryServer confirmation (#347 SET-2) ────────────────────────

    [Fact]
    public async Task RemoveDiscoveryServerCommand_WhenUserConfirms_RemovesAndPersists()
    {
        var sut = await CreateLoadedSutAsync();
        sut.NewServerHost = "10.0.0.5";
        sut.NewServerDisplayName = "Studio server";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);
        _repositoryMock.Invocations.Clear();
        _userPromptService.NextResult = true;

        await sut.RemoveDiscoveryServerCommand.ExecuteAsync(sut.DiscoveryServers[0]);

        Assert.Empty(sut.DiscoveryServers);
        Assert.Single(_userPromptService.Requests);
        Assert.Contains("Studio server", _userPromptService.Requests[0].Message);
        _repositoryMock.Verify(r => r.SaveSettingsAsync(
            It.Is<NdiSettingsSnapshot>(s => s.DiscoveryServers.Count == 0)), Times.Once);
    }

    [Fact]
    public async Task RemoveDiscoveryServerCommand_WhenUserDeclines_LeavesTheServerListUnchanged()
    {
        var sut = await CreateLoadedSutAsync();
        sut.NewServerHost = "10.0.0.5";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);
        _repositoryMock.Invocations.Clear();
        _userPromptService.NextResult = false;

        await sut.RemoveDiscoveryServerCommand.ExecuteAsync(sut.DiscoveryServers[0]);

        Assert.Single(sut.DiscoveryServers);
        _repositoryMock.Verify(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()), Times.Never);
    }

    // ─── Discovery empty state (#370) ────────────────────────────────────────────

    [Fact]
    public async Task HasNoDiscoveryServers_IsTrueWhenEmpty_AndFlipsWhenAServerIsAddedOrRemoved()
    {
        var sut = CreateSut();
        Assert.True(sut.HasNoDiscoveryServers);

        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.NewServerHost = "10.0.0.5";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);

        Assert.False(sut.HasNoDiscoveryServers);
        Assert.Contains(nameof(SettingsViewModel.HasNoDiscoveryServers), raised);

        _userPromptService.NextResult = true;
        await sut.RemoveDiscoveryServerCommand.ExecuteAsync(sut.DiscoveryServers[0]);

        Assert.True(sut.HasNoDiscoveryServers);
    }

    [Fact]
    public async Task LoadCommand_WithPersistedServers_ClearsEmptyState()
    {
        _repositoryMock.Setup(r => r.GetSettingsAsync())
            .ReturnsAsync(NdiSettingsSnapshot.CreateDefault() with
            {
                DiscoveryServers = [new DiscoveryServerPreference("10.0.0.5", 5959, true, 0, null)],
            });

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        Assert.False(sut.HasNoDiscoveryServers);
    }

    // ─── Developer Mode row tap (#343 SET-8) ─────────────────────────────────────

    [Fact]
    public async Task ToggleDeveloperModeCommand_FlipsAndPersists()
    {
        var sut = await CreateLoadedSutAsync();

        sut.ToggleDeveloperModeCommand.Execute(null);
        Assert.True(sut.DeveloperModeEnabled);
        _repositoryMock.Verify(r => r.SaveSettingsAsync(
            It.Is<NdiSettingsSnapshot>(s => s.DeveloperModeEnabled)), Times.Once);

        sut.ToggleDeveloperModeCommand.Execute(null);
        Assert.False(sut.DeveloperModeEnabled);
        _repositoryMock.Verify(r => r.SaveSettingsAsync(
            It.Is<NdiSettingsSnapshot>(s => !s.DeveloperModeEnabled)), Times.Once);
    }

    [Fact]
    public void ToggleDeveloperModeCommand_WhenNotLoaded_StillGoesThroughAutoSave()
    {
        // Not loaded — auto-save is suppressed only inside LoadAsync itself, so this pins that
        // the command goes through the same OnDeveloperModeEnabledChanged path as the Switch
        // rather than bypassing the guard.
        var sut = CreateSut();

        sut.ToggleDeveloperModeCommand.Execute(null);

        _repositoryMock.Verify(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()), Times.Once);
    }

    // ─── Section-agnostic save-failure banner (#355 item 4) ─────────────────────

    [Fact]
    public async Task PersistAsync_WhenSaveThrows_SetsSectionAgnosticStatus()
    {
        var sut = await CreateLoadedSutAsync();
        _repositoryMock.Setup(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()))
            .ThrowsAsync(new InvalidOperationException("disk full"));

        sut.ToggleDeveloperModeCommand.Execute(null);

        Assert.True(sut.HasSettingsSaveError);
        Assert.Contains("disk full", sut.SettingsSaveErrorMessage);
    }

    [Fact]
    public async Task PersistAsync_WhenSaveSucceedsAfterAFailure_ClearsTheSectionAgnosticStatus()
    {
        var sut = await CreateLoadedSutAsync();
        _repositoryMock.Setup(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()))
            .ThrowsAsync(new InvalidOperationException("disk full"));
        sut.ToggleDeveloperModeCommand.Execute(null);
        Assert.True(sut.HasSettingsSaveError);

        _repositoryMock.Setup(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()))
            .Returns(Task.CompletedTask);
        sut.ToggleDeveloperModeCommand.Execute(null);

        Assert.False(sut.HasSettingsSaveError);
        Assert.Equal(string.Empty, sut.SettingsSaveErrorMessage);
    }

    [Fact]
    public async Task RemoveDiscoveryServerCommand_Null_DoesNothing()
    {
        var sut = await CreateLoadedSutAsync();
        sut.NewServerHost = "10.0.0.5";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);

        await sut.RemoveDiscoveryServerCommand.ExecuteAsync(null);

        Assert.Single(sut.DiscoveryServers);
    }

    // ─── Move up/down ──────────────────────────────────────────────────────────

    [Fact]
    public async Task MoveDiscoveryServerUpCommand_ReordersListCorrectly()
    {
        var sut = await CreateLoadedSutAsync();
        sut.NewServerHost = "alpha";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);
        sut.NewServerHost = "beta";
        sut.NewServerPort = "5961";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);

        await sut.MoveDiscoveryServerUpCommand.ExecuteAsync(sut.DiscoveryServers[1]);

        Assert.Equal("beta", sut.DiscoveryServers[0].Host);
        Assert.Equal("alpha", sut.DiscoveryServers[1].Host);
    }

    [Fact]
    public async Task MoveDiscoveryServerUpCommand_FirstItem_DoesNothing()
    {
        var sut = await CreateLoadedSutAsync();
        sut.NewServerHost = "alpha";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);
        sut.NewServerHost = "beta";
        sut.NewServerPort = "5961";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);
        _repositoryMock.Invocations.Clear();

        await sut.MoveDiscoveryServerUpCommand.ExecuteAsync(sut.DiscoveryServers[0]);

        Assert.Equal("alpha", sut.DiscoveryServers[0].Host);
        _repositoryMock.Verify(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task MoveDiscoveryServerDownCommand_LastItem_DoesNothing()
    {
        var sut = await CreateLoadedSutAsync();
        sut.NewServerHost = "alpha";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);
        sut.NewServerHost = "beta";
        sut.NewServerPort = "5961";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);
        _repositoryMock.Invocations.Clear();

        await sut.MoveDiscoveryServerDownCommand.ExecuteAsync(sut.DiscoveryServers[1]);

        Assert.Equal("beta", sut.DiscoveryServers[1].Host);
        _repositoryMock.Verify(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()), Times.Never);
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
    public void SelectSectionCommand_Discovery_SetsIsDiscoverySectionSelected()
    {
        var sut = CreateSut();
        sut.SelectSectionCommand.Execute(SettingsSection.Discovery);

        Assert.True(sut.IsDiscoverySectionSelected);
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

    // ─── Connection status probing ─────────────────────────────────────────────

    [Fact]
    public async Task RefreshServerStatuses_ReachableServer_ShowsConnected()
    {
        _discoveryBridgeMock
            .Setup(b => b.IsDiscoveryServerReachableAsync("10.0.0.5", 5959, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = await CreateLoadedSutAsync();
        sut.NewServerHost = "10.0.0.5";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);

        await sut.RefreshServerStatusesAsync();

        Assert.Equal(DiscoveryServerConnectionState.Connected, sut.DiscoveryServers[0].ConnectionState);
        Assert.Equal("Connected", sut.DiscoveryServers[0].ConnectionStateDisplay);
    }

    [Fact]
    public async Task RefreshServerStatuses_UnreachableServer_ShowsUnreachable()
    {
        _discoveryBridgeMock
            .Setup(b => b.IsDiscoveryServerReachableAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = await CreateLoadedSutAsync();
        sut.NewServerHost = "10.0.0.5";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);

        await sut.RefreshServerStatusesAsync();

        Assert.Equal(DiscoveryServerConnectionState.Unreachable, sut.DiscoveryServers[0].ConnectionState);
    }

    [Fact]
    public async Task RefreshServerStatuses_DisabledServer_ShowsDisabledAndIsNotProbed()
    {
        var sut = await CreateLoadedSutAsync();
        sut.NewServerHost = "10.0.0.5";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);
        sut.DiscoveryServers[0].Enabled = false;

        await sut.RefreshServerStatusesAsync();

        Assert.Equal(DiscoveryServerConnectionState.Disabled, sut.DiscoveryServers[0].ConnectionState);
        _discoveryBridgeMock.Verify(
            b => b.IsDiscoveryServerReachableAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RefreshServerStatuses_BridgeThrows_ShowsUnreachable()
    {
        _discoveryBridgeMock
            .Setup(b => b.IsDiscoveryServerReachableAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("socket error"));

        var sut = await CreateLoadedSutAsync();
        sut.NewServerHost = "10.0.0.5";
        await sut.AddDiscoveryServerCommand.ExecuteAsync(null);

        await sut.RefreshServerStatusesAsync();

        Assert.Equal(DiscoveryServerConnectionState.Unreachable, sut.DiscoveryServers[0].ConnectionState);
    }
}
