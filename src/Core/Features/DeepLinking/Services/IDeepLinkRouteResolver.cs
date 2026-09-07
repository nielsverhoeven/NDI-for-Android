namespace NdiForAndroid.Features.DeepLinking.Services;

/// <summary>
/// Parses an <c>ndi://</c> deep-link URI string into a normalized (destination, sourceId) route.
/// Pure/MAUI-free (#335): all URI parsing and validation live here so they are unit-testable from
/// <c>tests/MauiApp.Tests</c>; <see cref="DeepLinkType.View"/>/<see cref="DeepLinkType.Stream"/>
/// navigation and source-cache lookups stay in the MauiApp <c>DeepLinkService</c> adapter.
/// </summary>
public interface IDeepLinkRouteResolver
{
    /// <summary>
    /// Supported forms:
    /// <list type="bullet">
    /// <item><c>ndi://view?sourceId=&lt;id&gt;</c> / <c>ndi://stream?sourceId=&lt;id&gt;</c></item>
    /// <item><c>ndi://view/&lt;host:port&gt;</c> / <c>ndi://stream/&lt;host:port&gt;</c> (QR/NFC — the
    /// path segment is used directly as the source id).</item>
    /// </list>
    /// Returns the normalized route on success, or <c>null</c> with <paramref name="errorMessage"/>
    /// set on failure (empty/malformed URI, unsupported scheme, unknown action, or missing source id).
    /// </summary>
    NdiDeepLink? Resolve(string uriString, out string? errorMessage);
}
