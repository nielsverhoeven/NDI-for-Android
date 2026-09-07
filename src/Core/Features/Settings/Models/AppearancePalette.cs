namespace NdiForAndroid.Features.Settings.Models;

/// <summary>
/// Hex (#RRGGBB or #AARRGGBB) values of the theme-dependent colour tokens. The MAUI layer turns
/// these into <c>Color</c>s and writes them to <c>Application.Current.Resources</c>; the same
/// values are asserted for WCAG 2.2 AA contrast by <c>AppearancePaletteContrastTests</c> (#344).
/// </summary>
public sealed record ThemePalette(
    string PageBackground,
    string CardBackground,
    string InputBackground,
    string ScrimBackground,
    string ShellBackground,
    string ShellForeground,
    string ShellTitleColor,
    string ShellTabSelected,
    string ShellTabUnselected,
    string TextPrimary,
    string TextSecondary,
    string TextPlaceholder,
    string ErrorText,
    string SuccessText,
    string WarningText,
    string BorderColor,
    string DividerColor);

/// <summary>
/// Single source of truth for every runtime colour value the app applies. Lives in Core (MAUI-free,
/// hex strings only) so the WCAG 2.2 AA contrast of every token pair can be unit-tested; MauiAppearanceService
/// only converts these to <c>Color</c> and writes them into the application resource dictionary. Colors.xaml
/// carries the Dark palette + Blue accent as the pre-Apply baseline and must match these values (#344, #372, #373).
/// </summary>
public static class AppearancePalette
{
    /// <summary>Text on any accent/fill colour. Every accent below keeps white text at >= 4.5:1.</summary>
    public const string OnPrimary = "#FFFFFF";

    /// <summary>Button fills that carry white text (Stop, Resume Output). Theme-independent.</summary>
    public const string ErrorFill   = "#D32F2F"; // white 4.98:1; 3.29:1 vs dark page, 4.46:1 vs light page
    public const string SuccessFill = "#2E7D32"; // white 5.13:1; 3.20:1 vs dark page, 4.60:1 vs light page

    public static readonly ThemePalette Dark = new(
        PageBackground:     "#1E1E2E",
        CardBackground:     "#2A2A3E",
        InputBackground:    "#33334A",
        ScrimBackground:    "#99000000",
        ShellBackground:    "#1C1C1E",
        ShellForeground:    "#FFFFFF",
        ShellTitleColor:    "#FFFFFF",
        ShellTabSelected:   "#FFFFFF",
        ShellTabUnselected: "#8E8E93", // 5.22:1 on ShellBackground
        TextPrimary:        "#FFFFFF",
        TextSecondary:      "#AAAACC", // 7.29:1 page, 6.22:1 card
        TextPlaceholder:    "#9E9EBC", // was #666680 (2.21:1); 4.71:1 on InputBackground (#376)
        ErrorText:          "#FF7B72", // 6.51:1 page, 5.56:1 card (was ErrorRed #F44336: 4.45 / 3.80)
        SuccessText:        "#66BB6A", // 6.94:1 page, 5.92:1 card
        WarningText:        "#FFA726", // 8.44:1 page, 7.21:1 card (#329 — diagnostic log Warning level)
        BorderColor:        "#3A3A5C",
        DividerColor:       "#2E2E4A");

    public static readonly ThemePalette Light = new(
        PageBackground:     "#F2F2F7",
        CardBackground:     "#FFFFFF",
        InputBackground:    "#E8E8ED",
        ScrimBackground:    "#66000000",
        ShellBackground:    "#E5E5EA",
        ShellForeground:    "#1C1C1E",
        ShellTitleColor:    "#1C1C1E",
        ShellTabSelected:   "#1C1C1E",
        ShellTabUnselected: "#636366", // was #6E6E73 (4.04:1); 4.77:1 on ShellBackground — rail/tab captions are 10-12sp text, so SC 1.4.3's 4.5:1 applies, not 1.4.11's 3:1 (#372)
        TextPrimary:        "#1C1C1E",
        TextSecondary:      "#3C3C43", // 9.80:1 page, 10.94:1 card
        TextPlaceholder:    "#666669", // was #8E8E93 (2.67:1); 4.69:1 on InputBackground (#376)
        ErrorText:          "#C62828", // 5.04:1 page, 5.62:1 card (was ErrorRed: 3.30 / 3.68)
        SuccessText:        "#2E7D32", // 4.60:1 page, 5.13:1 card (was SuccessGreen: 2.49 / 2.78)
        WarningText:        "#A64B00", // 5.19:1 page, 5.79:1 card (#329 — diagnostic log Warning level)
        BorderColor:        "#C6C6C8",
        DividerColor:       "#D1D1D6");

    /// <summary>
    /// Accent (Primary) per user option. Each value sits in the luminance band 0.142..0.183 so that
    /// white button text reaches >= 4.5:1 (SC 1.4.3) AND the accent still clears 3:1 against the
    /// dark PageBackground as a switch track / radio ring (SC 1.4.11). Previous values failed the
    /// text bar: Blue 4.49, Teal 3.67, Green 2.78, Orange 2.16, Red 3.68, Pink 4.35 (#344/#373).
    /// </summary>
    public static string Accent(AccentColorOption accent) => accent switch
    {
        AccentColorOption.Teal   => "#00806F", // white 4.86:1, dark page 3.37:1
        AccentColorOption.Green  => "#2E7D32", // white 5.13:1, dark page 3.20:1
        AccentColorOption.Orange => "#BF5700", // white 4.59:1, dark page 3.58:1
        AccentColorOption.Red    => "#D32F2F", // white 4.98:1, dark page 3.29:1
        AccentColorOption.Pink   => "#D81B60", // white 4.95:1, dark page 3.32:1
        _                        => "#2272AC", // Blue (default): white 5.16:1, dark page 3.18:1
    };

    /// <summary>
    /// Accent for non-text graphics only (radio ring/dot, Switch track, ActivityIndicator,
    /// RefreshView, viewer sheet tab indicator) — WCAG 1.4.11 wants >= 3:1 against the surface, not
    /// the 4.5:1 that <see cref="Accent"/>'s white-button-text requirement forces. On dark surfaces
    /// the darkened <see cref="Accent"/> fills drop as low as 2.72:1 against CardBackground (Blue),
    /// under the 3:1 bar — so the dark theme keeps the original, brighter Material 500 set here,
    /// each of which already clears 3:1 against both PageBackground and CardBackground. On light
    /// surfaces <see cref="Accent"/> itself already clears 3:1 (it is >= 4.5:1 under white, so it is
    /// even brighter under white's own high luminance), so light theme reuses it (#344/#373).
    /// </summary>
    public static string AccentGraphic(bool isLight, AccentColorOption accent) => isLight ? Accent(accent) : accent switch
    {
        AccentColorOption.Teal   => "#009688", // page 4.47:1, card 3.81:1
        AccentColorOption.Green  => "#4CAF50", // page 5.90:1, card 5.04:1
        AccentColorOption.Orange => "#FF9800", // page 7.61:1, card 6.50:1
        AccentColorOption.Red    => "#F44336", // page 4.45:1, card 3.80:1
        AccentColorOption.Pink   => "#E91E63", // page 3.77:1, card 3.22:1
        _                        => "#2B7CB8", // Blue (default): page 3.65:1, card 3.12:1
    };
}
