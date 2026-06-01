namespace NdiForAndroid.Features.Settings.Models;

public record NdiSettingsSnapshot(
    string? DiscoveryHost,
    int? DiscoveryPort,
    bool DeveloperModeEnabled,
    long UpdatedAtEpochMillis);
