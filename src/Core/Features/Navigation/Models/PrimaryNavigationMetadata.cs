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
        new(PrimaryNavDestination.Home, "Home", "//home", "tab_sources.png"),
        new(PrimaryNavDestination.Stream, "Stream", "//stream", "tab_sources.png"),
        new(PrimaryNavDestination.View, "View", "//view", "tab_sources.png"),
        new(PrimaryNavDestination.Settings, "Settings", "//settings", "tab_settings.png"),
    ];
}
