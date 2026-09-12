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

    private int _explicitNavigationDepth;

    /// <summary>
    /// True while this app is inside a <c>GoToAsync</c> call it issued itself — a rail tap, the
    /// placement reconciliation, or any <see cref="INavigationService"/> call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MAUI Shell re-points <c>CurrentItem</c> to the first visible <c>ShellItem</c> on its own when
    /// the current one is hidden — which is what <c>AppShell.ApplyPlacement</c> does to
    /// <c>PrimaryTabBar</c> on a rotation (#393) — and reports it exactly like a real cross-item move
    /// (<see cref="ShellNavigationSource.ShellItemChanged"/>). The source value alone cannot tell the
    /// two apart, because the app's own rail taps and reconciliations are also cross-item moves.
    /// </para>
    /// <para>
    /// A user has no other way to cause one: a bottom-tab tap is always
    /// <see cref="ShellNavigationSource.ShellSectionChanged"/>, and the four rail
    /// <c>FlyoutItem</c>s are <c>Shell.FlyoutItemIsVisible="False"</c> behind a custom
    /// <c>Shell.FlyoutContent</c>, so the rail can only navigate through
    /// <c>AppShell.OnRailItemSelected</c>. A <c>ShellItemChanged</c> that arrives while this is
    /// <c>false</c> is therefore, by construction, Shell's own unsolicited fallback.
    /// </para>
    /// <para>
    /// Counted rather than a flag: the reconciliation is dispatched, so it can run on a UI-thread
    /// turn taken while another navigation is suspended at its <c>await</c>, and a bool would let the
    /// inner call clear the outer call's marker. Written and read on the UI thread only — every
    /// mutation brackets a <c>GoToAsync</c>, which MAUI requires to be issued from the UI thread.
    /// </para>
    /// </remarks>
    public bool IsExplicitNavigationInProgress => _explicitNavigationDepth > 0;

    /// <summary>Marks the start of a navigation this app issued. Pair with
    /// <see cref="EndExplicitNavigation"/> in a <c>finally</c>.</summary>
    public void BeginExplicitNavigation() => _explicitNavigationDepth++;

    /// <summary>Marks the end of a navigation this app issued. Never drops below zero.</summary>
    public void EndExplicitNavigation()
    {
        if (_explicitNavigationDepth > 0)
            _explicitNavigationDepth--;
    }

    public ShellNavigationService(ILogger<ShellNavigationService> logger, AdaptiveShellStateViewModel stateViewModel)
    {
        _logger = logger;
        _stateViewModel = stateViewModel;
    }

    public async Task NavigateToAsync(string route)
    {
        BeginExplicitNavigation();
        try
        {
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation failed for route '{Route}'", route);
            throw;
        }
        finally
        {
            EndExplicitNavigation();
        }
    }

    public async Task NavigateToPrimaryAsync(PrimaryNavDestination destination, string? queryString = null)
    {
        if (!TryGetRouteForCurrentPlacement(destination, out var route))
            throw new ArgumentOutOfRangeException(nameof(destination), destination, "No route registered for this primary destination.");

        if (!string.IsNullOrEmpty(queryString))
            route = $"{route}?{queryString}";

        BeginExplicitNavigation();
        try
        {
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation failed for route '{Route}'", route);
            throw;
        }
        finally
        {
            EndExplicitNavigation();
        }
    }

    public async Task GoBackAsync()
    {
        BeginExplicitNavigation();
        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GoBack navigation failed");
            throw;
        }
        finally
        {
            EndExplicitNavigation();
        }
    }

    public bool TryGetRouteForCurrentPlacement(PrimaryNavDestination destination, out string route)
    {
        var routes = _stateViewModel.IsLeftRailNavigationVisible ? _landscapeRoutes : _portraitRoutes;
        return routes.TryGetValue(destination, out route!);
    }
}
