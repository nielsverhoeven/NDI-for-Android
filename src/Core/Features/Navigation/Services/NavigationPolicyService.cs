using NdiForAndroid.Features.Navigation.Models;

namespace NdiForAndroid.Features.Navigation.Services;

public sealed class NavigationPolicyService : INavigationPolicyService
{
    public event EventHandler<NavigationPlacementMode>? PlacementChanged;

    public NavigationPlacementMode CurrentPlacement { get; private set; } = NavigationPlacementMode.Bottom;

    public NavigationPlacementMode ResolvePlacement(DeviceOrientation orientation)
        => orientation == DeviceOrientation.Landscape
            ? NavigationPlacementMode.LeftRail
            : NavigationPlacementMode.Bottom;

    public void UpdateOrientation(DeviceOrientation orientation)
    {
        var nextPlacement = ResolvePlacement(orientation);
        if (nextPlacement == CurrentPlacement)
            return;

        CurrentPlacement = nextPlacement;
        PlacementChanged?.Invoke(this, CurrentPlacement);
    }
}
