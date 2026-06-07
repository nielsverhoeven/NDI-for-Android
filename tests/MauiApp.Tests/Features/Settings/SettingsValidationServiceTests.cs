using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Services;
using Xunit;

namespace MauiApp.Tests.Features.Settings;

public sealed class SettingsValidationServiceTests
{
    private readonly SettingsValidationService _sut = new();

    [Fact]
    public void Sanitize_InvalidPortAndNegativeTimestamp_AppliesDefaults()
    {
        var input = new NdiSettingsSnapshot(
            " 10.0.0.1 ",
            99999,
            true,
            -1,
            ThemeMode.System,
            AccentColorOption.Blue,
            Array.Empty<DiscoveryServerPreference>());

        var result = _sut.Sanitize(input);

        Assert.Equal("10.0.0.1", result.DiscoveryHost);
        Assert.Null(result.DiscoveryPort);
        Assert.Equal(0, result.UpdatedAtEpochMillis);
    }

    [Fact]
    public void TryValidateForSave_DuplicateDiscoveryServers_ReturnsFalse()
    {
        var input = new NdiSettingsSnapshot(
            null,
            null,
            false,
            10,
            ThemeMode.System,
            AccentColorOption.Blue,
            new[]
            {
                new DiscoveryServerPreference("host1", 5960, true, 0),
                new DiscoveryServerPreference("HOST1", 5960, true, 1),
            });

        var valid = _sut.TryValidateForSave(input, out var error);

        Assert.False(valid);
        Assert.Contains("duplicate", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidateForSave_ValidSnapshot_ReturnsTrue()
    {
        var input = new NdiSettingsSnapshot(
            "discovery.local",
            5960,
            true,
            10,
            ThemeMode.Dark,
            AccentColorOption.Orange,
            new[]
            {
                new DiscoveryServerPreference("server-a", 5960, true, 0),
                new DiscoveryServerPreference("10.10.10.10", 5961, false, 1),
            });

        var valid = _sut.TryValidateForSave(input, out var error);

        Assert.True(valid);
        Assert.Null(error);
    }
}