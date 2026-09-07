namespace NdiForAndroid.Features.DeepLinking.Services;

/// <summary>
/// Pure parser for <c>ndi://</c> deep links. See <see cref="IDeepLinkRouteResolver"/> for the
/// supported forms.
/// </summary>
public sealed class DeepLinkRouteResolver : IDeepLinkRouteResolver
{
    public NdiDeepLink? Resolve(string uriString, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(uriString))
        {
            errorMessage = "Invalid deep link: empty URI.";
            return null;
        }

        Uri uri;
        try
        {
            uri = new Uri(uriString);
        }
        catch (UriFormatException)
        {
            errorMessage = "Invalid deep link: malformed URI.";
            return null;
        }

        if (!string.Equals(uri.Scheme, "ndi", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = $"Unsupported scheme '{uri.Scheme}'. Expected 'ndi://'.";
            return null;
        }

        // The action ("view"/"stream") is the URI's host (ndi://view/... or ndi://view?...);
        // fall back to the path only for a hostless form (e.g. "ndi:view?...").
        var action = string.IsNullOrEmpty(uri.Host) ? uri.AbsolutePath.Trim('/') : uri.Host;

        DeepLinkType type;
        switch (action.ToLowerInvariant())
        {
            case "view":
                type = DeepLinkType.View;
                break;
            case "stream":
                type = DeepLinkType.Stream;
                break;
            default:
                errorMessage = $"Unknown action '{action}'. Use 'view' or 'stream'.";
                return null;
        }

        var query = uri.Query.TrimStart('?');
        var sourceId = ParseQueryString(query, "sourceId");

        // Path form (QR/NFC): ndi://view/<host:port> or ndi://stream/<host:port> — the path
        // segment (everything after the host) is the source id directly.
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            var pathSourceId = uri.AbsolutePath.Trim('/');
            if (!string.IsNullOrEmpty(uri.Host) && !string.IsNullOrWhiteSpace(pathSourceId))
                sourceId = Uri.UnescapeDataString(pathSourceId);
        }

        if (string.IsNullOrWhiteSpace(sourceId))
        {
            errorMessage = "Invalid deep link: missing 'sourceId' parameter.";
            return null;
        }

        return new NdiDeepLink(type, sourceId);
    }

    private static string? ParseQueryString(string query, string key)
    {
        if (string.IsNullOrEmpty(query)) return null;

        foreach (var pair in query.Split('&'))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0] == key)
                return Uri.UnescapeDataString(parts[1]);
        }
        return null;
    }
}
