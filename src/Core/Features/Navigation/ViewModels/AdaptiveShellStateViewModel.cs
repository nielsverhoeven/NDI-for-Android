using CommunityToolkit.Mvvm.ComponentModel;
using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.Features.Navigation.Services;

namespace NdiForAndroid.Features.Navigation.ViewModels;

public partial class AdaptiveShellStateViewModel : ObservableObject
{
    private readonly INavigationPolicyService _policyService;

    [ObservableProperty]
    private NavigationPlacementMode _placementMode;

    /// <summary>
    /// Set by <c>ViewerFullScreenChromeController</c> while a host page is showing full screen.
    /// Read only by <c>AppShell.ApplyPlacement</c>, and only to hide the left rail
    /// (<c>FlyoutBehavior.Disabled</c>). The bottom tab bar is hidden page-scoped via
    /// <c>Shell.SetTabBarIsVisible</c>, never from here.
    /// </summary>
    [ObservableProperty]
    private bool _isChromeSuppressed;

    [ObservableProperty]
    private PrimaryNavDestination _selectedDestination = PrimaryNavDestination.Home;

    public IReadOnlyList<PrimaryNavItem> PrimaryItems => PrimaryNavigationMetadata.Items;

    public IReadOnlyDictionary<PrimaryNavDestination, string> RouteByDestination { get; } =
        PrimaryNavigationMetadata.Items.ToDictionary(item => item.Destination, item => item.Route);

    /// <summary>
    /// Pure placement query: which navigation *route family* and chrome layout the current window
    /// calls for. Deliberately does NOT fold in <see cref="IsChromeSuppressed"/> —
    /// <c>ShellNavigationService.TryGetRouteForCurrentPlacement</c> selects the <c>-rail</c> vs
    /// <c>-tab</c> route table from these, and a route family must never change just because
    /// full-screen chrome happens to be suppressed right now. Suppression is applied only inside
    /// <c>AppShell.ApplyPlacement</c>'s rail branch (as <c>FlyoutBehavior</c>).
    /// </summary>
    public bool IsBottomNavigationVisible => PlacementMode == NavigationPlacementMode.Bottom;

    /// <inheritdoc cref="IsBottomNavigationVisible"/>
    public bool IsLeftRailNavigationVisible => PlacementMode == NavigationPlacementMode.LeftRail;

    /// <summary>Raised when a rail item is tapped. AppShell subscribes to drive navigation.</summary>
    public event EventHandler<PrimaryNavDestination>? RailItemSelected;

    public AdaptiveShellStateViewModel(INavigationPolicyService policyService)
    {
        _policyService = policyService;
        PlacementMode = _policyService.CurrentPlacement;
        _policyService.PlacementChanged += OnPlacementChanged;
    }

    public void SelectDestination(PrimaryNavDestination destination)
    {
        SelectedDestination = destination;
        RailItemSelected?.Invoke(this, destination);
    }

    private void OnPlacementChanged(object? sender, NavigationPlacementMode placement)
    {
        PlacementMode = placement;
    }

    partial void OnPlacementModeChanged(NavigationPlacementMode value)
    {
        OnPropertyChanged(nameof(IsBottomNavigationVisible));
        OnPropertyChanged(nameof(IsLeftRailNavigationVisible));
    }
}
