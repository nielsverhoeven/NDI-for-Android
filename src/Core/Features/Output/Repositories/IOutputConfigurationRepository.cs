using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Output.Repositories;

/// <summary>Persisted NDI output preferences (stream name, video input, microphone flag).</summary>
public sealed record OutputConfiguration(
    string? PreferredStreamName,
    VideoInputKind InputKind,
    bool CaptureMicrophone);

/// <summary>
/// Persistence for the output configuration. Backed by the shared ndi.db3
/// database (output_configuration table) in the MAUI app implementation.
/// </summary>
public interface IOutputConfigurationRepository
{
    /// <summary>Returns the persisted configuration, or null when none was saved yet.</summary>
    Task<OutputConfiguration?> GetAsync();

    Task SaveAsync(OutputConfiguration config);
}

/// <summary>
/// Pure string ↔ enum mapping between the stored <c>LastInputKind</c> column and
/// <see cref="VideoInputKind"/>. Lives in Core so it is unit-testable without the
/// Android-targeted app assembly (which hosts the SQLite wrapper).
/// </summary>
public static class OutputConfigurationMapping
{
    /// <summary>
    /// Parses a stored input-kind value. Handles the legacy Kotlin-era value
    /// "DeviceScreen" (maps to <see cref="VideoInputKind.Screen"/>) as well as
    /// current enum names; anything unrecognized falls back to Screen.
    /// </summary>
    public static VideoInputKind ParseInputKind(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return VideoInputKind.Screen;

        // Legacy schema value from the ported Kotlin app ("DeviceScreen"/"DiscoveredNdi").
        if (stored.Equals("DeviceScreen", StringComparison.OrdinalIgnoreCase))
            return VideoInputKind.Screen;

        if (Enum.TryParse<VideoInputKind>(stored, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;

        return VideoInputKind.Screen;
    }

    /// <summary>Serializes an input kind to its storage string (current enum name).</summary>
    public static string ToStorageString(VideoInputKind kind) => kind.ToString();
}
