using Microsoft.Extensions.Logging;
using NdiForAndroid.Services;

namespace NdiForAndroid.Services;

/// <summary>
/// MAUI Shell implementation of <see cref="INavigationService"/>.
/// Registered in DI so ViewModels stay free of MAUI Shell references.
/// </summary>
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
}
