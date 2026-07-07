using NdiForAndroid.Features.Navigation.Models;

namespace NdiForAndroid.Features.Navigation.Services;

public sealed class NavigationPolicyService : INavigationPolicyService
{
    private readonly IWindowSizeClassService _windowSizeClassService;

    private DeviceOrientation _lastOrientation = DeviceOrientation.Portrait;

    public NavigationPolicyService(IWindowSizeClassService windowSizeClassService)
    {
        _windowSizeClassService = windowSizeClassService;
        _windowSizeClassService.Changed += OnWindowSizeClassChanged;
    }

    public event EventHandler<NavigationPlacementMode>? PlacementChanged;

    public NavigationPlacementMode CurrentPlacement { get; private set; } = NavigationPlacementMode.Bottom;

    /// <summary>
    /// Rail when landscape OR the window is Expanded (&gt; 840dp wide); bottom tabs otherwise.
    /// Compact/Medium portrait phones keep tabs; 10" tablets get the rail in both orientations.
    /// </summary>
    public NavigationPlacementMode ResolvePlacement(DeviceOrientation orientation, WindowSizeClass sizeClass)
        => orientation == DeviceOrientation.Landscape || sizeClass == WindowSizeClass.Expanded
            ? NavigationPlacementMode.LeftRail
            : NavigationPlacementMode.Bottom;

    public void UpdateOrientation(DeviceOrientation orientation)
    {
        _lastOrientation = orientation;
        ReevaluatePlacement();
    }

    private void OnWindowSizeClassChanged(object? sender, WindowSizeClass sizeClass)
        => ReevaluatePlacement();

    private void ReevaluatePlacement()
    {
        var nextPlacement = ResolvePlacement(_lastOrientation, _windowSizeClassService.Current);
        if (nextPlacement == CurrentPlacement)
            return;

        CurrentPlacement = nextPlacement;
        PlacementChanged?.Invoke(this, CurrentPlacement);
    }
}
