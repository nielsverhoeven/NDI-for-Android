using NdiForAndroid.Features.DeepLinking.Services;
using NdiForAndroid.NdiBridge;
using Microsoft.Extensions.DependencyInjection;

namespace NdiForAndroid.Features.DeepLinking;

/// <summary>
/// Deep link handler that parses ndi:// URIs and routes to the appropriate feature.
/// </summary>
public sealed class DeepLinkService : IDeepLinkService
{
    private readonly IShellNavigationService _navigation;
    private readonly IServiceProvider _serviceProvider;
    private string? _lastErrorMessage;

    public string? LastErrorMessage => _lastErrorMessage;

    public DeepLinkService(IShellNavigationService navigation, IServiceProvider serviceProvider)
    {
        _navigation = navigation;
        _serviceProvider = serviceProvider;
    }

    public async Task<bool> ProcessDeepLinkAsync(string uriString)
    {
        _lastErrorMessage = null;

        if (string.IsNullOrWhiteSpace(uriString))
        {
            _lastErrorMessage = "Invalid deep link: empty URI.";
            return false;
        }

        try
        {
            var uri = new Uri(uriString);

            if (!string.Equals(uri.Scheme, "ndi", StringComparison.OrdinalIgnoreCase))
            {
                _lastErrorMessage = $"Unsupported scheme '{uri.Scheme}'. Expected 'ndi://'.";
                return false;
            }

            var path = uri.AbsolutePath.TrimStart('/');
            var query = uri.Query.TrimStart('?');
            var sourceId = ParseQueryString(query, "sourceId");

            if (string.IsNullOrWhiteSpace(sourceId))
            {
                _lastErrorMessage = "Invalid deep link: missing 'sourceId' parameter.";
                return false;
            }

            // Check that the source exists in our cached discovery list
            var sourceRepo = _serviceProvider.GetService<Core.Features.Sources.Repositories.ISourceRepository>();
            if (sourceRepo != null)
            {
                var cachedSources = await sourceRepo.GetCachedSourcesAsync();
                var matched = cachedSources.Any(s => s.SourceId.Equals(sourceId, StringComparison.OrdinalIgnoreCase));

                // Also match by display name for convenience
                if (!matched)
                {
                    var foundByDisplayName = cachedSources.FirstOrDefault(s =>
                        s.DisplayName?.IndexOf(sourceId, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (foundByDisplayName != null)
                        sourceId = foundByDisplayName.SourceId;
                }
            }

            // Route based on path segment
            switch (path.ToLowerInvariant())
            {
                case "view":
                    await NavigateToViewerAsync(sourceId);
                    return true;

                case "stream":
                    await NavigateToOutputForReStreamAsync(sourceId);
                    return true;

                default:
                    _lastErrorMessage = $"Unknown action '{path}'. Use 'view' or 'stream'.";
                    return false;
            }
        }
        catch (UriFormatException)
        {
            _lastErrorMessage = "Invalid deep link: malformed URI.";
            return false;
        }
    }

    private async Task NavigateToViewerAsync(string sourceId)
    {
        // Navigate to the viewer page and set the selected source
        await _navigation.NavigateAsync($"//viewer?sourceId={Uri.EscapeDataString(sourceId)}");
    }

    private async Task NavigateToOutputForReStreamAsync(string sourceId)
    {
        // Navigate to the output page in re-stream mode with the source ID pre-set
        await _navigation.NavigateAsync($"//output?reStreamSourceId={Uri.EscapeDataString(sourceId)}&isReStreamMode=true");
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
