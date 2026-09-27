using Microsoft.Extensions.Logging;
using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.Services;

namespace NdiForAndroid.Services;

/// <summary>
/// MAUI Shell implementation of <see cref="INavigationService"/>.
/// Registered in DI so ViewModels stay free of MAUI Shell references.
/// </summary>
/// <remarks>
/// There is one route family (#395): every primary destination has exactly one absolute route,
/// <see cref="PrimaryNavItem.Route"/>, whichever chrome — bottom tab bar or left rail — is showing.
/// The route no longer depends on the placement, so this service has no placement state and no
/// "explicit navigation in progress" marker: Shell never re-points <c>CurrentItem</c> on its own
/// any more, so there is no unsolicited navigation to tell apart from the app's own.
/// </remarks>
public sealed class ShellNavigationService : INavigationService
{
    private readonly ILogger<ShellNavigationService> _logger;

    public ShellNavigationService(ILogger<ShellNavigationService> logger)
    {
        _logger = logger;
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
        if (!TryGetPrimaryRoute(destination, out var route))
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

    /// <summary>The one absolute route for <paramref name="destination"/>, from
    /// <see cref="PrimaryNavigationMetadata.Items"/>.</summary>
    public static bool TryGetPrimaryRoute(PrimaryNavDestination destination, out string route)
    {
        foreach (var item in PrimaryNavigationMetadata.Items)
        {
            if (item.Destination == destination)
            {
                route = item.Route;
                return true;
            }
        }

        route = string.Empty;
        return false;
    }
}
