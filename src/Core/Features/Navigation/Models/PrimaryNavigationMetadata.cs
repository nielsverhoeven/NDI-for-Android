namespace NdiForAndroid.Features.Navigation.Models;

public enum PrimaryNavDestination
{
    Home,
    Stream,
    View,
    Settings,
}

public enum NavigationPlacementMode
{
    Bottom,
    LeftRail,
}

public enum DeviceOrientation
{
    Portrait,
    Landscape,
}

public sealed record PrimaryNavItem(
    PrimaryNavDestination Destination,
    string Label,
    string Route,
    string IconKey);

public static class PrimaryNavigationMetadata
{
    public static readonly IReadOnlyList<PrimaryNavItem> Items =
    [
        new(PrimaryNavDestination.Home, "Home", "//home", "nav_home.svg"),
        new(PrimaryNavDestination.Stream, "Stream", "//stream", "nav_stream.svg"),
        new(PrimaryNavDestination.View, "View", "//view", "nav_view.svg"),
        new(PrimaryNavDestination.Settings, "Settings", "//settings", "nav_settings.svg"),
    ];
}
