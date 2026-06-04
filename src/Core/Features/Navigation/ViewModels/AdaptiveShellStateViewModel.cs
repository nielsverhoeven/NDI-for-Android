using CommunityToolkit.Mvvm.ComponentModel;
using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.Features.Navigation.Services;

namespace NdiForAndroid.Features.Navigation.ViewModels;

public partial class AdaptiveShellStateViewModel : ObservableObject
{
    private readonly INavigationPolicyService _policyService;

    [ObservableProperty]
    private NavigationPlacementMode _placementMode;

    public IReadOnlyList<PrimaryNavItem> PrimaryItems => PrimaryNavigationMetadata.Items;

    public IReadOnlyDictionary<PrimaryNavDestination, string> RouteByDestination { get; } =
        PrimaryNavigationMetadata.Items.ToDictionary(item => item.Destination, item => item.Route);

    public bool IsBottomNavigationVisible => PlacementMode == NavigationPlacementMode.Bottom;

    public bool IsLeftRailNavigationVisible => PlacementMode == NavigationPlacementMode.LeftRail;

    public AdaptiveShellStateViewModel(INavigationPolicyService policyService)
    {
        _policyService = policyService;
        PlacementMode = _policyService.CurrentPlacement;
        _policyService.PlacementChanged += OnPlacementChanged;
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
