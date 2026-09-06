namespace NdiForAndroid.Features.Settings.Models;

public enum ThemeMode
{
    System,
    Light,
    Dark,
}

public enum AccentColorOption
{
    Blue,
    Teal,
    Green,
    Orange,
    Red,
    Pink,
}

public sealed record DiscoveryServerPreference(
    string Host,
    int Port,
    bool Enabled,
    int Order,
    string? DisplayName = null);

public sealed record SettingsAppInfo(
    string AppName,
    string Version,
    string Build);

public sealed record CachedSourceRegistryEntry(
    string SourceName,
    string Endpoint,
    string State,
    string RegistryKey,
    string LastSeenDisplay);

public sealed record NdiSettingsSnapshot(
    bool DeveloperModeEnabled,
    long UpdatedAtEpochMillis,
    ThemeMode ThemeMode,
    AccentColorOption AccentColor,
    IReadOnlyList<DiscoveryServerPreference> DiscoveryServers)
{
    public static NdiSettingsSnapshot CreateDefault() =>
        new(false, 0, ThemeMode.System, AccentColorOption.Blue, Array.Empty<DiscoveryServerPreference>());
}

/// <summary>Live reachability state of a configured discovery server (TCP probe result).</summary>
public enum DiscoveryServerConnectionState
{
    Unknown,
    Disabled,
    Checking,
    Connected,
    Unreachable,
}
