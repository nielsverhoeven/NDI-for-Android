using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Services;
using Xunit;

namespace NdiForAndroid.Tests.Features.Settings;

public sealed class SettingsValidationServiceTests
{
    private readonly SettingsValidationService _sut = new();

    [Fact]
    public void Sanitize_NegativeTimestamp_AppliesDefaults()
    {
        var input = new NdiSettingsSnapshot(
            true,
            -1,
            ThemeMode.System,
            AccentColorOption.Blue,
            Array.Empty<DiscoveryServerPreference>());

        var result = _sut.Sanitize(input);

        Assert.Equal(0, result.UpdatedAtEpochMillis);
    }

    [Fact]
    public void Sanitize_ServerDisplayName_IsTrimmedAndBlankBecomesNull()
    {
        var input = new NdiSettingsSnapshot(
            false,
            10,
            ThemeMode.System,
            AccentColorOption.Blue,
            new[]
            {
                new DiscoveryServerPreference("host1", 5960, true, 0, "  Studio rack  "),
                new DiscoveryServerPreference("host2", 5961, true, 1, "   "),
            });

        var result = _sut.Sanitize(input);

        Assert.Equal("Studio rack", result.DiscoveryServers[0].DisplayName);
        Assert.Null(result.DiscoveryServers[1].DisplayName);
    }

    [Fact]
    public void TryValidateForSave_DuplicateDiscoveryServers_ReturnsFalse()
    {
        var input = new NdiSettingsSnapshot(
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
            true,
            10,
            ThemeMode.Dark,
            AccentColorOption.Orange,
            new[]
            {
                new DiscoveryServerPreference("server-a", 5960, true, 0, "Server A"),
                new DiscoveryServerPreference("10.10.10.10", 5961, false, 1),
            });

        var valid = _sut.TryValidateForSave(input, out var error);

        Assert.True(valid);
        Assert.Null(error);
    }
}
