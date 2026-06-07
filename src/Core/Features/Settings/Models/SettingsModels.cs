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
    int Order);

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
    string? DiscoveryHost,
    int? DiscoveryPort,
    bool DeveloperModeEnabled,
    long UpdatedAtEpochMillis,
    ThemeMode ThemeMode,
    AccentColorOption AccentColor,
    IReadOnlyList<DiscoveryServerPreference> DiscoveryServers)
{
    public static NdiSettingsSnapshot CreateDefault() =>
        new(null, null, false, 0, ThemeMode.System, AccentColorOption.Blue, Array.Empty<DiscoveryServerPreference>());
}
