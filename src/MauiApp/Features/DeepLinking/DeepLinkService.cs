using NdiForAndroid.Features.DeepLinking.Services;
using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.NdiBridge;
using Microsoft.Extensions.DependencyInjection;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.DeepLinking;

/// <summary>
/// Thin MauiApp adapter (#335): all URI parsing/validation is delegated to the Core, unit-tested
/// <see cref="IDeepLinkRouteResolver"/>. This class only does what needs a MAUI/DI context —
/// resolving the cached source list and driving <see cref="INavigationService"/>.
/// </summary>
public sealed class DeepLinkService : IDeepLinkService
{
    private readonly IDeepLinkRouteResolver _resolver;
    private readonly INavigationService _navigation;
    private readonly IServiceProvider _serviceProvider;
    private string? _lastErrorMessage;

    public string? LastErrorMessage => _lastErrorMessage;

    public DeepLinkService(IDeepLinkRouteResolver resolver, INavigationService navigation, IServiceProvider serviceProvider)
    {
        _resolver = resolver;
        _navigation = navigation;
        _serviceProvider = serviceProvider;
    }

    public async Task<bool> ProcessDeepLinkAsync(string uriString)
    {
        _lastErrorMessage = null;

        var route = _resolver.Resolve(uriString, out var parseError);
        if (route is null)
        {
            _lastErrorMessage = parseError;
            return false;
        }

        try
        {
            var sourceId = route.SourceId;

            // Check that the source exists in our cached discovery list. Unknown sources keep the
            // current behaviour (toast via LastErrorMessage) rather than failing navigation — the
            // sourceId is still forwarded as-is (#335 open question, resolved as "no change yet").
            var sourceRepo = _serviceProvider.GetService<NdiForAndroid.Features.Sources.Repositories.ISourceRepository>();
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

            switch (route.Type)
            {
                case DeepLinkType.View:
                    await NavigateToViewerAsync(sourceId);
                    return true;

                case DeepLinkType.Stream:
                    await NavigateToOutputForReStreamAsync(sourceId);
                    return true;

                default:
                    _lastErrorMessage = $"Unknown action '{route.Type}'. Use 'view' or 'stream'.";
                    return false;
            }
        }
        catch (Exception ex)
        {
            _lastErrorMessage = $"Navigation failed: {ex.Message}";
            return false;
        }
    }

    private async Task NavigateToViewerAsync(string sourceId)
    {
        // Navigate to the viewer page and set the selected source
        await _navigation.NavigateToAsync($"viewer?sourceId={Uri.EscapeDataString(sourceId)}");
    }

    private async Task NavigateToOutputForReStreamAsync(string sourceId)
    {
        await _navigation.NavigateToPrimaryAsync(
            PrimaryNavDestination.Stream,
            $"reStreamSourceId={Uri.EscapeDataString(sourceId)}&isReStreamMode=true");
    }
}
