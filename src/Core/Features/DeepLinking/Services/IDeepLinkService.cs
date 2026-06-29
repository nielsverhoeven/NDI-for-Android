using NdiForAndroid.NdiBridge;

namespace NdiForAndroid.Features.DeepLinking.Services;

/// <summary>
/// Parses ndi:// deep links and routes them to the appropriate app feature.
/// </summary>
public interface IDeepLinkService
{
    /// <summary>
    /// Parses and applies an ndi:// URI deep link.
    /// Routes naviagtion based on scheme + path:
    /// - ndi://view?sourceId=xxx → navigate to Viewer page for that source
    /// - ndi://stream?sourceId=xxx → navigate to Output page in re-stream mode for that source
    /// </summary>
    Task<bool> ProcessDeepLinkAsync(string uriString);

    /// <summary>
    /// Returns an error message if the URI could not be processed, or null on success.
    /// </summary>
    string? LastErrorMessage { get; }
}

/// <summary>Represents a parsed ndi:// deep link.</summary>
public record NdiDeepLink(
    DeepLinkType Type,
    string SourceId,
    QualityProfile QualityProfile = QualityProfile.Balanced);

/// <summary>Type of ndi:// deep link action.</summary>
public enum DeepLinkType
{
    View,    // Open viewer for a source
    Stream,  // Start re-streaming a source
}
