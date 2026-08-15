using NdiForAndroid.Testing;

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

/// <param name="IconKey">
/// Bundled SVG used by the Shell tab bar, which tints icons natively from
/// <c>Shell.TabBarForegroundColor</c>.
/// </param>
/// <param name="IconGeometry">
/// The same glyph as SVG path mini-language, drawn on a 24x24 canvas. The left rail renders
/// this as a vector shape it can fill with the current theme color — the bundled SVGs carry a
/// baked-in white fill and cannot be re-tinted at runtime (#294).
/// </param>
/// <param name="TestId">
/// Automation id shared by both placements of this destination — the bottom tab and the left
/// rail item are one destination wearing two coats, and only one is in the view tree at a time.
/// </param>
public sealed record PrimaryNavItem(
    PrimaryNavDestination Destination,
    string Label,
    string Route,
    string IconKey,
    string IconGeometry,
    string TestId);

public static class PrimaryNavigationMetadata
{
    private const string HomeGeometry =
        "M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z";

    private const string StreamGeometry =
        "M17 10.5V7c0-.55-.45-1-1-1H4c-.55 0-1 .45-1 1v10c0 .55.45 1 1 1h12c.55 0 1-.45 1-1v-3.5l4 4v-11l-4 4z";

    private const string ViewGeometry =
        "M21 3H3c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h5v2h8v-2h5c1.1 0 1.99-.9 1.99-2L23 5c0-1.1-.9-2-2-2zm0 14H3V5h18v12z";

    private const string SettingsGeometry =
        "M19.14,12.94c0.04-0.3,0.06-0.61,0.06-0.94c0-0.32-0.02-0.64-0.07-0.94l2.03-1.58c0.18-0.14,0.23-0.41,0.12-0.61 " +
        "l-1.92-3.32c-0.12-0.22-0.37-0.29-0.59-0.22l-2.39,0.96c-0.5-0.38-1.03-0.7-1.62-0.94L14.4,2.81c-0.04-0.24-0.24-0.41-0.48-0.41 " +
        "h-3.84c-0.24,0-0.43,0.17-0.47,0.41L9.25,5.35C8.66,5.59,8.12,5.92,7.63,6.29L5.24,5.33c-0.22-0.08-0.47,0-0.59,0.22L2.74,8.87 " +
        "C2.62,9.08,2.66,9.34,2.86,9.48l2.03,1.58C4.84,11.36,4.8,11.69,4.8,12s0.02,0.64,0.07,0.94l-2.03,1.58 " +
        "c-0.18,0.14-0.23,0.41-0.12,0.61l1.92,3.32c0.12,0.22,0.37,0.29,0.59,0.22l2.39-0.96c0.5,0.38,1.03,0.7,1.62,0.94l0.36,2.54 " +
        "c0.05,0.24,0.24,0.41,0.48,0.41h3.84c0.24,0,0.44-0.17,0.47-0.41l0.36-2.54c0.59-0.24,1.13-0.56,1.62-0.94l2.39,0.96 " +
        "c0.22,0.08,0.47,0,0.59-0.22l1.92-3.32c0.12-0.22,0.07-0.47-0.12-0.61L19.14,12.94z M12,15.6c-1.98,0-3.6-1.62-3.6-3.6 " +
        "s1.62-3.6,3.6-3.6s3.6,1.62,3.6,3.6S13.98,15.6,12,15.6z";

    public static readonly IReadOnlyList<PrimaryNavItem> Items =
    [
        new(PrimaryNavDestination.Home, "Home", "//home", "nav_home.svg", HomeGeometry, TestIds.NavHome),
        new(PrimaryNavDestination.Stream, "Stream", "//stream", "nav_stream.svg", StreamGeometry, TestIds.NavStream),
        new(PrimaryNavDestination.View, "View", "//view", "nav_view.svg", ViewGeometry, TestIds.NavView),
        new(PrimaryNavDestination.Settings, "Settings", "//settings", "nav_settings.svg", SettingsGeometry, TestIds.NavSettings),
    ];
}
