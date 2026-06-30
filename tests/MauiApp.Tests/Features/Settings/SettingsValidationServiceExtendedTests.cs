using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Services;
using Xunit;

namespace NdiForAndroid.Tests.Features.Settings;

/// <summary>
/// Extended coverage for SettingsValidationService —
/// IsValidHostOrEmpty paths, Sanitize edge-cases (invalid enums, server filtering,
/// ordering), and TryValidateForSave failure branches not covered by the baseline tests.
/// </summary>
public sealed class SettingsValidationServiceExtendedTests
{
    private readonly SettingsValidationService _sut = new();

    // ─── IsValidHostOrEmpty ────────────────────────────────────────────────────

    [Fact]
    public void IsValidHostOrEmpty_Null_ReturnsTrue()
    {
        Assert.True(_sut.IsValidHostOrEmpty(null));
    }

    [Fact]
    public void IsValidHostOrEmpty_EmptyString_ReturnsTrue()
    {
        Assert.True(_sut.IsValidHostOrEmpty(string.Empty));
    }

    [Fact]
    public void IsValidHostOrEmpty_WhitespaceOnly_ReturnsTrue()
    {
        Assert.True(_sut.IsValidHostOrEmpty("   "));
    }

    [Fact]
    public void IsValidHostOrEmpty_ValidIPv4_ReturnsTrue()
    {
        Assert.True(_sut.IsValidHostOrEmpty("192.168.1.100"));
    }

    [Fact]
    public void IsValidHostOrEmpty_ValidIPv6_ReturnsTrue()
    {
        Assert.True(_sut.IsValidHostOrEmpty("::1"));
    }

    [Fact]
    public void IsValidHostOrEmpty_ValidHostname_ReturnsTrue()
    {
        Assert.True(_sut.IsValidHostOrEmpty("discovery.local"));
    }

    [Fact]
    public void IsValidHostOrEmpty_ValidSimpleLabel_ReturnsTrue()
    {
        Assert.True(_sut.IsValidHostOrEmpty("ndi-server"));
    }

    [Theory]
    [InlineData("not a valid host!")]
    [InlineData("host name with spaces")]
    [InlineData("@badhost")]
    public void IsValidHostOrEmpty_InvalidHostname_ReturnsFalse(string badHost)
    {
        Assert.False(_sut.IsValidHostOrEmpty(badHost));
    }

    // ─── Sanitize – enum out-of-range defaults ─────────────────────────────────

    [Fact]
    public void Sanitize_OutOfRangeThemeMode_DefaultsToSystem()
    {
        var input = MakeSnapshot(themeMode: (ThemeMode)999);

        var result = _sut.Sanitize(input);

        Assert.Equal(ThemeMode.System, result.ThemeMode);
    }

    [Fact]
    public void Sanitize_OutOfRangeAccentColor_DefaultsToBlue()
    {
        var input = MakeSnapshot(accentColor: (AccentColorOption)999);

        var result = _sut.Sanitize(input);

        Assert.Equal(AccentColorOption.Blue, result.AccentColor);
    }

    // ─── Sanitize – valid enum values pass through ─────────────────────────────

    [Theory]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    [InlineData(ThemeMode.System)]
    public void Sanitize_ValidThemeMode_IsPreserved(ThemeMode mode)
    {
        var result = _sut.Sanitize(MakeSnapshot(themeMode: mode));

        Assert.Equal(mode, result.ThemeMode);
    }

    [Theory]
    [InlineData(AccentColorOption.Blue)]
    [InlineData(AccentColorOption.Teal)]
    [InlineData(AccentColorOption.Green)]
    [InlineData(AccentColorOption.Orange)]
    [InlineData(AccentColorOption.Red)]
    [InlineData(AccentColorOption.Pink)]
    public void Sanitize_ValidAccentColor_IsPreserved(AccentColorOption color)
    {
        var result = _sut.Sanitize(MakeSnapshot(accentColor: color));

        Assert.Equal(color, result.AccentColor);
    }

    // ─── Sanitize – server filtering ──────────────────────────────────────────

    [Fact]
    public void Sanitize_ServerWithBlankHost_IsFilteredOut()
    {
        var input = MakeSnapshot(servers: new[]
        {
            new DiscoveryServerPreference("", 5960, true, 0),
            new DiscoveryServerPreference("good-host", 5960, true, 1),
        });

        var result = _sut.Sanitize(input);

        Assert.Single(result.DiscoveryServers);
        Assert.Equal("good-host", result.DiscoveryServers[0].Host);
    }

    [Fact]
    public void Sanitize_ServerWithWhitespaceOnlyHost_IsFilteredOut()
    {
        var input = MakeSnapshot(servers: new[]
        {
            new DiscoveryServerPreference("   ", 5960, true, 0),
        });

        var result = _sut.Sanitize(input);

        Assert.Empty(result.DiscoveryServers);
    }

    [Fact]
    public void Sanitize_ServerWithPortTooLarge_IsFilteredOut()
    {
        var input = MakeSnapshot(servers: new[]
        {
            new DiscoveryServerPreference("valid-host", 99999, true, 0),
            new DiscoveryServerPreference("other-host", 5960, true, 1),
        });

        var result = _sut.Sanitize(input);

        Assert.Single(result.DiscoveryServers);
        Assert.Equal("other-host", result.DiscoveryServers[0].Host);
    }

    [Fact]
    public void Sanitize_ServerWithPortZeroOrNegative_IsFilteredOut()
    {
        var input = MakeSnapshot(servers: new[]
        {
            new DiscoveryServerPreference("host", 0, true, 0),
            new DiscoveryServerPreference("host2", -1, true, 1),
        });

        var result = _sut.Sanitize(input);

        Assert.Empty(result.DiscoveryServers);
    }

    // ─── Sanitize – server order normalization ─────────────────────────────────

    [Fact]
    public void Sanitize_ServersAreRenumberedConsecutivelyFromZero()
    {
        var input = MakeSnapshot(servers: new[]
        {
            new DiscoveryServerPreference("z-host", 5960, true, 10),
            new DiscoveryServerPreference("a-host", 5961, true, 5),
        });

        var result = _sut.Sanitize(input);

        // After ordering by original Order the indices should be 0, 1
        Assert.Equal(2, result.DiscoveryServers.Count);
        Assert.Equal(0, result.DiscoveryServers[0].Order);
        Assert.Equal(1, result.DiscoveryServers[1].Order);
    }

    [Fact]
    public void Sanitize_HostWhitespaceTrimmedInServers()
    {
        var input = MakeSnapshot(servers: new[]
        {
            new DiscoveryServerPreference("  trimmed-host  ", 5960, true, 0),
        });

        var result = _sut.Sanitize(input);

        Assert.Single(result.DiscoveryServers);
        Assert.Equal("trimmed-host", result.DiscoveryServers[0].Host);
    }

    // ─── Sanitize – positive timestamp unchanged ────────────────────────────────

    [Fact]
    public void Sanitize_PositiveTimestamp_IsPreserved()
    {
        var input = MakeSnapshot(updatedAt: 999_000L);

        var result = _sut.Sanitize(input);

        Assert.Equal(999_000L, result.UpdatedAtEpochMillis);
    }

    // ─── TryValidateForSave – failure branches ─────────────────────────────────

    [Fact]
    public void TryValidateForSave_InvalidNonEmptyDiscoveryHost_ReturnsFalse()
    {
        var input = MakeSnapshot(host: "invalid host!!");

        var valid = _sut.TryValidateForSave(input, out var error);

        Assert.False(valid);
        Assert.NotNull(error);
        Assert.Contains("host", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidateForSave_DiscoveryServerWithInvalidNonEmptyHost_ReturnsFalse()
    {
        // An invalid hostname that is NOT blank/whitespace survives Sanitize's server filter
        // (Sanitize only strips null/whitespace hosts, not syntactically invalid ones).
        // TryValidateForSave then rejects it via IsValidHostOrEmpty.
        var input = new NdiSettingsSnapshot(
            null, null, false, 10,
            ThemeMode.System, AccentColorOption.Blue,
            new[] { new DiscoveryServerPreference("bad host with spaces!", 5960, true, 0) });

        var valid = _sut.TryValidateForSave(input, out var error);

        Assert.False(valid);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidateForSave_DiscoveryServerWithOutOfRangePort_SanitizeFiltersItAndReturnsTrue()
    {
        // TryValidateForSave calls Sanitize() internally, which FILTERS servers with
        // out-of-range ports before the validation loop runs. The filtered list is empty
        // and therefore valid. This test documents that Sanitize+Validate silently drops
        // bad-port servers rather than returning a hard error — consistent with the
        // SettingsRepository GetSettingsAsync fallback behaviour.
        var input = new NdiSettingsSnapshot(
            null, null, false, 10,
            ThemeMode.System, AccentColorOption.Blue,
            new[] { new DiscoveryServerPreference("good-host", 99999, true, 0) });

        var valid = _sut.TryValidateForSave(input, out var error);

        Assert.True(valid, "Sanitize filters the server with bad port; the remaining empty list is valid");
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateForSave_EmptyServerList_ReturnsTrue()
    {
        var input = MakeSnapshot();

        var valid = _sut.TryValidateForSave(input, out var error);

        Assert.True(valid);
        Assert.Null(error);
    }

    // ─── Sanitize full-malformed-data round-trip (repository fallback pipeline) ──

    [Fact]
    public void SanitizeThenValidate_FullyMalformedSnapshot_ProducesValidResult()
    {
        // Simulate what SettingsRepository does when it receives corrupt DB data:
        // Sanitize produces a clean snapshot; TryValidateForSave confirms it is safe to use.
        var malformed = new NdiSettingsSnapshot(
            "   ",           // DiscoveryHost: whitespace-only → null after sanitize
            99999,           // DiscoveryPort: out of range → null
            false,           // DeveloperModeEnabled
            -999L,           // UpdatedAtEpochMillis: negative → 0
            (ThemeMode)(-1),         // ThemeMode: invalid enum → System
            (AccentColorOption)42,   // AccentColor: invalid enum → Blue
            new[]
            {
                new DiscoveryServerPreference("", 0, true, 5),       // blank host, bad port → filtered
                new DiscoveryServerPreference("  ", 99999, true, 6), // whitespace, bad port → filtered
            });

        var sanitized = _sut.Sanitize(malformed);
        var valid = _sut.TryValidateForSave(sanitized, out var error);

        Assert.True(valid, $"Expected sanitized malformed snapshot to be valid but got: {error}");
        Assert.Null(sanitized.DiscoveryPort);
        Assert.Equal(ThemeMode.System, sanitized.ThemeMode);
        Assert.Equal(AccentColorOption.Blue, sanitized.AccentColor);
        Assert.Equal(0L, sanitized.UpdatedAtEpochMillis);
        Assert.Empty(sanitized.DiscoveryServers);
    }

    [Fact]
    public void SanitizeThenValidate_CreateDefault_AlwaysProducesValidSnapshot()
    {
        // NdiSettingsSnapshot.CreateDefault() is the value returned by SettingsRepository
        // on exception (the fallback). It must always pass TryValidateForSave.
        var defaultSnapshot = NdiSettingsSnapshot.CreateDefault();

        var sanitized = _sut.Sanitize(defaultSnapshot);
        var valid = _sut.TryValidateForSave(sanitized, out var error);

        Assert.True(valid, $"CreateDefault() fallback should always be valid but got: {error}");
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private static NdiSettingsSnapshot MakeSnapshot(
        string? host = null,
        int? port = null,
        bool developerMode = false,
        long updatedAt = 0L,
        ThemeMode themeMode = ThemeMode.System,
        AccentColorOption accentColor = AccentColorOption.Blue,
        IReadOnlyList<DiscoveryServerPreference>? servers = null)
        => new(host, port, developerMode, updatedAt, themeMode, accentColor,
               servers ?? Array.Empty<DiscoveryServerPreference>());
}
