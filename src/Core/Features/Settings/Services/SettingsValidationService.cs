using System.Net;
using NdiForAndroid.Features.Settings.Models;

namespace NdiForAndroid.Features.Settings.Services;

public interface ISettingsValidationService
{
    NdiSettingsSnapshot Sanitize(NdiSettingsSnapshot settings);
    bool TryValidateForSave(NdiSettingsSnapshot settings, out string? errorMessage);
    bool IsValidHostOrEmpty(string? host);
}

public sealed class SettingsValidationService : ISettingsValidationService
{
    public NdiSettingsSnapshot Sanitize(NdiSettingsSnapshot settings)
    {
        var sanitizedHost = NormalizeNullableHost(settings.DiscoveryHost);
        var sanitizedPort = NormalizeNullablePort(settings.DiscoveryPort);
        var sanitizedThemeMode = Enum.IsDefined(settings.ThemeMode) ? settings.ThemeMode : ThemeMode.System;
        var sanitizedAccentColor = Enum.IsDefined(settings.AccentColor) ? settings.AccentColor : AccentColorOption.Blue;
        var sanitizedServers = SanitizeServers(settings.DiscoveryServers);
        var updatedAt = settings.UpdatedAtEpochMillis < 0 ? 0 : settings.UpdatedAtEpochMillis;

        return new NdiSettingsSnapshot(
            sanitizedHost,
            sanitizedPort,
            settings.DeveloperModeEnabled,
            updatedAt,
            sanitizedThemeMode,
            sanitizedAccentColor,
            sanitizedServers);
    }

    public bool TryValidateForSave(NdiSettingsSnapshot settings, out string? errorMessage)
    {
        var sanitized = Sanitize(settings);

        if (!IsValidHostOrEmpty(sanitized.DiscoveryHost))
        {
            errorMessage = "Discovery host must be empty or a valid hostname or IP address.";
            return false;
        }

        if (sanitized.DiscoveryPort.HasValue && sanitized.DiscoveryPort.Value is < 1 or > 65535)
        {
            errorMessage = "Discovery port must be empty or a value between 1 and 65535.";
            return false;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var server in sanitized.DiscoveryServers)
        {
            if (!IsValidHostOrEmpty(server.Host) || string.IsNullOrWhiteSpace(server.Host))
            {
                errorMessage = "Each discovery server must provide a valid hostname or IP address.";
                return false;
            }

            if (server.Port is < 1 or > 65535)
            {
                errorMessage = "Each discovery server port must be between 1 and 65535.";
                return false;
            }

            var key = BuildServerDedupKey(server.Host, server.Port);
            if (!seen.Add(key))
            {
                errorMessage = "Discovery servers cannot contain duplicate host and port combinations.";
                return false;
            }
        }

        errorMessage = null;
        return true;
    }

    public bool IsValidHostOrEmpty(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return true;

        var trimmed = host.Trim();
        if (IPAddress.TryParse(trimmed.Trim('[', ']'), out _))
            return true;

        var hostNameType = Uri.CheckHostName(trimmed);
        return hostNameType is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6;
    }

    private static IReadOnlyList<DiscoveryServerPreference> SanitizeServers(IReadOnlyList<DiscoveryServerPreference>? servers)
    {
        if (servers is null || servers.Count == 0)
            return Array.Empty<DiscoveryServerPreference>();

        var normalized = new List<DiscoveryServerPreference>(servers.Count);

        foreach (var server in servers)
        {
            var host = NormalizeNullableHost(server.Host);
            if (string.IsNullOrWhiteSpace(host))
                continue;

            if (server.Port is < 1 or > 65535)
                continue;

            normalized.Add(new DiscoveryServerPreference(host, server.Port, server.Enabled, server.Order));
        }

        var ordered = normalized
            .OrderBy(server => server.Order)
            .ThenBy(server => server.Host, StringComparer.OrdinalIgnoreCase)
            .ThenBy(server => server.Port)
            .ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            var current = ordered[index];
            ordered[index] = current with { Order = index };
        }

        return ordered;
    }

    private static string? NormalizeNullableHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return null;

        return host.Trim();
    }

    private static int? NormalizeNullablePort(int? port)
    {
        if (!port.HasValue)
            return null;

        return port.Value is < 1 or > 65535 ? null : port.Value;
    }

    private static string BuildServerDedupKey(string host, int port)
        => $"{host.Trim().ToLowerInvariant()}:{port}";
}