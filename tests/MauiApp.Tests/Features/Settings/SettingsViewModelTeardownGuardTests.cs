using Moq;
using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Repositories;
using NdiForAndroid.Features.Settings.Services;
using NdiForAndroid.Features.Settings.ViewModels;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.Services;
using Xunit;

namespace NdiForAndroid.Tests.Features.Settings;

/// <summary>
/// Regression tests for #300 — tearing the Settings page down must not be mistaken for the
/// user changing the theme.
/// </summary>
/// <remarks>
/// <c>RadioButtonGroup.SelectedValue</c> writes <c>null</c> back through its two-way binding
/// while the page's visual tree unloads. Left unguarded that null parses to the default
/// (<see cref="ThemeMode.System"/>) and is staged as a real edit, so any save that follows
/// persists the default theme over whatever the user actually picked.
/// </remarks>
public sealed class SettingsViewModelTeardownGuardTests
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
            _sourceRepositoryMock.Object,
            new Mock<INdiVersionInfo>().Object);
    }

    /// <summary>Loads a persisted snapshot whose theme is an explicit, non-default choice.</summary>
    private async Task<SettingsViewModel> CreateLoadedWithLightThemeAsync()
    {
        var saved = NdiSettingsSnapshot.CreateDefault() with
        {
            ThemeMode = ThemeMode.Light,
            AccentColor = AccentColorOption.Teal,
        };

        _repositoryMock.Setup(r => r.GetSettingsAsync()).ReturnsAsync(saved);

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        return sut;
    }

    // ─── Theme ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SelectedThemeOption_SetToNullByTeardown_KeepsUserSelection()
    {
        var sut = await CreateLoadedWithLightThemeAsync();

        sut.SelectedThemeOption = null;

        Assert.Equal("Light", sut.SelectedThemeOption);
    }

    [Fact]
    public async Task SelectedThemeOption_SetToNullByTeardown_DoesNotStagePendingChanges()
    {
        var sut = await CreateLoadedWithLightThemeAsync();

        sut.SelectedThemeOption = null;

        Assert.False(sut.HasPendingChanges);
    }

    [Fact]
    public async Task SelectedThemeOption_SetToNullByTeardown_LeavesApplyDisabled()
    {
        var sut = await CreateLoadedWithLightThemeAsync();

        sut.SelectedThemeOption = null;

        Assert.False(sut.ApplyCommand.CanExecute(null));
    }

    /// <summary>The defect's payload: a save after teardown must not write the default theme.</summary>
    [Fact]
    public async Task ApplyAfterTeardownNull_DoesNotPersistDefaultTheme()
    {
        var sut = await CreateLoadedWithLightThemeAsync();
        NdiSettingsSnapshot? persisted = null;
        _repositoryMock
            .Setup(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()))
            .Callback<NdiSettingsSnapshot>(snapshot => persisted = snapshot)
            .Returns(Task.CompletedTask);

        sut.SelectedThemeOption = null;
        // A genuine, unrelated edit so Apply is reachable.
        sut.DiscoveryHost = "10.0.0.99";
        await sut.ApplyCommand.ExecuteAsync(null);

        Assert.NotNull(persisted);
        Assert.Equal(ThemeMode.Light, persisted!.ThemeMode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Sepia")]
    public async Task SelectedThemeOption_SetToUnknownValue_KeepsUserSelection(string value)
    {
        var sut = await CreateLoadedWithLightThemeAsync();

        sut.SelectedThemeOption = value;

        Assert.Equal("Light", sut.SelectedThemeOption);
        Assert.False(sut.HasPendingChanges);
    }

    [Fact]
    public async Task SelectedThemeOption_SetToRealChoice_StillStagesPendingChanges()
    {
        var sut = await CreateLoadedWithLightThemeAsync();

        sut.SelectedThemeOption = "Dark";

        Assert.Equal("Dark", sut.SelectedThemeOption);
        Assert.True(sut.HasPendingChanges);
    }

    [Fact]
    public async Task SelectedThemeOption_RestoredAfterTeardown_StillAcceptsNextRealChange()
    {
        var sut = await CreateLoadedWithLightThemeAsync();

        sut.SelectedThemeOption = null;
        sut.SelectedThemeOption = "Dark";

        Assert.Equal("Dark", sut.SelectedThemeOption);
        Assert.True(sut.HasPendingChanges);
    }

    // ─── Accent color ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SelectedAccentColor_SetToNullByTeardown_KeepsUserSelection()
    {
        var sut = await CreateLoadedWithLightThemeAsync();

        sut.SelectedAccentColor = null;

        Assert.Equal("Teal", sut.SelectedAccentColor);
        Assert.False(sut.HasPendingChanges);
    }

    [Fact]
    public async Task SelectedAccentColor_SetToRealChoice_StillStagesPendingChanges()
    {
        var sut = await CreateLoadedWithLightThemeAsync();

        sut.SelectedAccentColor = "Orange";

        Assert.Equal("Orange", sut.SelectedAccentColor);
        Assert.True(sut.HasPendingChanges);
    }
}
