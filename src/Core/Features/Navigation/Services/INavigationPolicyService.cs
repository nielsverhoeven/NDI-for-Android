using NdiForAndroid.Features.Navigation.Models;

namespace NdiForAndroid.Features.Navigation.Services;

public interface INavigationPolicyService
{
    NavigationPlacementMode CurrentPlacement { get; }
    event EventHandler<NavigationPlacementMode>? PlacementChanged;
    NavigationPlacementMode ResolvePlacement(DeviceOrientation orientation, WindowSizeClass sizeClass);
    void UpdateOrientation(DeviceOrientation orientation);
}
