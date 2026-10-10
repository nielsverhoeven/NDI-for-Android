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
    /// Read only by <c>AppShell.ApplyPlacement</c> (through <see cref="NavigationChromePolicy"/>), and
    /// to hide the left rail (<c>FlyoutBehavior.Disabled</c>) and, through
    /// <see cref="NavigationChromePolicy.MirrorValue"/>, to write <c>Shell.TabBarIsVisible = false</c> on
    /// the section and <c>ShellContent</c>s hosting full screen — the effective bottom-bar scope on
    /// MAUI ≥ 10.0.70 (#449). It never changes the item-scope value, and the controller keeps page
    /// scope (<c>Shell.SetTabBarIsVisible(page, false)</c>); nothing here writes any Shell property.
    /// </summary>
    [ObservableProperty]
    private bool _isChromeSuppressed;

    [ObservableProperty]
    private PrimaryNavDestination _selectedDestination = PrimaryNavDestination.Home;

    public IReadOnlyList<PrimaryNavItem> PrimaryItems => PrimaryNavigationMetadata.Items;

    public IReadOnlyDictionary<PrimaryNavDestination, string> RouteByDestination { get; } =
        PrimaryNavigationMetadata.Items.ToDictionary(item => item.Destination, item => item.Route);

    /// <summary>
    /// Pure placement query: which chrome — bottom tab bar or left rail — the current window calls
    /// for. There is one route family (#395), so this never selects a route; it only drives the
    /// item-scope <c>Shell.TabBarIsVisible</c> on <c>PrimaryTabBar</c> and the rail's
    /// <c>FlyoutBehavior</c>. Deliberately does NOT fold in <see cref="IsChromeSuppressed"/>: the
    /// item-scope bar value must follow the placement only, because the full-screen controller hides
    /// the bar at page scope and restores it with <c>ClearValue</c>, which falls back to this value.
    /// Suppression is applied only to the rail, in <see cref="NavigationChromePolicy.Resolve"/>.
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
