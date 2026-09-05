using Microsoft.Extensions.Logging;
using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.Features.Navigation.ViewModels;
using NdiForAndroid.Services;

namespace NdiForAndroid.Services;

/// <summary>
/// MAUI Shell implementation of <see cref="INavigationService"/>.
/// Registered in DI so ViewModels stay free of MAUI Shell references.
/// </summary>
public sealed class ShellNavigationService : INavigationService
{
    private readonly ILogger<ShellNavigationService> _logger;
    private readonly AdaptiveShellStateViewModel _stateViewModel;

    private readonly IReadOnlyDictionary<PrimaryNavDestination, string> _landscapeRoutes =
        new Dictionary<PrimaryNavDestination, string>
        {
            [PrimaryNavDestination.Home]     = "//home-rail",
            [PrimaryNavDestination.Stream]   = "//stream-rail",
            [PrimaryNavDestination.View]     = "//view-rail",
            [PrimaryNavDestination.Settings] = "//settings-rail",
        };

    private readonly IReadOnlyDictionary<PrimaryNavDestination, string> _portraitRoutes =
        new Dictionary<PrimaryNavDestination, string>
        {
            [PrimaryNavDestination.Home]     = "//home-tab",
            [PrimaryNavDestination.Stream]   = "//stream-tab",
            [PrimaryNavDestination.View]     = "//view-tab",
            [PrimaryNavDestination.Settings] = "//settings-tab",
        };

    public ShellNavigationService(ILogger<ShellNavigationService> logger, AdaptiveShellStateViewModel stateViewModel)
    {
        _logger = logger;
        _stateViewModel = stateViewModel;
    }

    public async Task NavigateToAsync(string route)
    {
        try
        {
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation failed for route '{Route}'", route);
            throw;
        }
    }

    public async Task NavigateToPrimaryAsync(PrimaryNavDestination destination, string? queryString = null)
    {
        if (!TryGetRouteForCurrentPlacement(destination, out var route))
            throw new ArgumentOutOfRangeException(nameof(destination), destination, "No route registered for this primary destination.");

        if (!string.IsNullOrEmpty(queryString))
            route = $"{route}?{queryString}";

        try
        {
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation failed for route '{Route}'", route);
            throw;
        }
    }

    public async Task GoBackAsync()
    {
        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GoBack navigation failed");
            throw;
        }
    }

    public bool TryGetRouteForCurrentPlacement(PrimaryNavDestination destination, out string route)
    {
        var routes = _stateViewModel.IsLeftRailNavigationVisible ? _landscapeRoutes : _portraitRoutes;
        return routes.TryGetValue(destination, out route!);
    }
}
