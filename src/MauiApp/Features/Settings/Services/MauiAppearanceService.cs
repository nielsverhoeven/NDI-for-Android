using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Services;

namespace NdiForAndroid.Features.Settings.Services;

/// <summary>
/// Single source of truth for all runtime color values.
/// Updates the application resource dictionary and Shell chrome
/// immediately when Apply is tapped — no navigation required.
/// All XAML must reference the semantic keys via DynamicResource.
/// </summary>
public sealed class MauiAppearanceService : IAppearanceService
{
    public void Apply(ThemeMode theme, AccentColorOption accentColor)
    {
        if (MainThread.IsMainThread)
            ApplyCore(theme, accentColor);
        else
            MainThread.BeginInvokeOnMainThread(() => ApplyCore(theme, accentColor));
    }

    private static void ApplyCore(ThemeMode theme, AccentColorOption accentColor)
    {
        if (Application.Current is null)
            return;

        var isLight = theme == ThemeMode.Light
            || (theme == ThemeMode.System
                && Application.Current.RequestedTheme == AppTheme.Light);

        // Set MAUI's built-in theme so AppThemeBinding elements also react.
        Application.Current.UserAppTheme = theme switch
        {
            ThemeMode.Light => AppTheme.Light,
            ThemeMode.Dark  => AppTheme.Dark,
            _               => AppTheme.Unspecified,
        };

        var palette = isLight ? LightPalette : DarkPalette;
        var accent  = ResolveAccent(accentColor);

        UpdateResources(palette, accent);
        UpdateShell(palette);
    }

    // ── Color palettes ──────────────────────────────────────────────

    private record Palette(
        Color PageBackground,
        Color CardBackground,
        Color InputBackground,
        Color ShellBackground,
        Color ShellForeground,
        Color ShellTitleColor,
        Color ShellTabSelected,
        Color ShellTabUnselected,
        Color TextPrimary,
        Color TextSecondary,
        Color TextPlaceholder,
        Color BorderColor,
        Color DividerColor);

    private static readonly Palette DarkPalette = new(
        PageBackground:    Color.FromArgb("#1E1E2E"),
        CardBackground:    Color.FromArgb("#2A2A3E"),
        InputBackground:   Color.FromArgb("#33334A"),
        ShellBackground:   Color.FromArgb("#1C1C1E"),
        ShellForeground:   Color.FromArgb("#FFFFFF"),
        ShellTitleColor:   Color.FromArgb("#FFFFFF"),
        ShellTabSelected:  Color.FromArgb("#FFFFFF"),
        ShellTabUnselected:Color.FromArgb("#8E8E93"),
        TextPrimary:       Color.FromArgb("#FFFFFF"),
        TextSecondary:     Color.FromArgb("#AAAACC"),
        TextPlaceholder:   Color.FromArgb("#666680"),
        BorderColor:       Color.FromArgb("#3A3A5C"),
        DividerColor:      Color.FromArgb("#2E2E4A"));

    private static readonly Palette LightPalette = new(
        PageBackground:    Color.FromArgb("#F2F2F7"),
        CardBackground:    Color.FromArgb("#FFFFFF"),
        InputBackground:   Color.FromArgb("#E8E8ED"),
        ShellBackground:   Color.FromArgb("#E5E5EA"),
        ShellForeground:   Color.FromArgb("#1C1C1E"),
        ShellTitleColor:   Color.FromArgb("#1C1C1E"),
        ShellTabSelected:  Color.FromArgb("#1C1C1E"),
        ShellTabUnselected:Color.FromArgb("#8E8E93"),
        TextPrimary:       Color.FromArgb("#1C1C1E"),
        TextSecondary:     Color.FromArgb("#3C3C43"),
        TextPlaceholder:   Color.FromArgb("#8E8E93"),
        BorderColor:       Color.FromArgb("#C6C6C8"),
        DividerColor:      Color.FromArgb("#D1D1D6"));

    private static Color ResolveAccent(AccentColorOption accent) => accent switch
    {
        AccentColorOption.Teal   => Color.FromArgb("#009688"),
        AccentColorOption.Green  => Color.FromArgb("#4CAF50"),
        AccentColorOption.Orange => Color.FromArgb("#FF9800"),
        AccentColorOption.Red    => Color.FromArgb("#F44336"),
        AccentColorOption.Pink   => Color.FromArgb("#E91E63"),
        _                        => Color.FromArgb("#2B7CB8"), // Blue
    };

    // ── Resource dictionary update ───────────────────────────────────

    private static void UpdateResources(Palette p, Color accent)
    {
        var res = Application.Current!.Resources;

        // Accent
        res["Primary"]            = accent;
        res["OnPrimary"]          = Color.FromArgb("#FFFFFF");

        // Surfaces
        res["PageBackground"]     = p.PageBackground;
        res["CardBackground"]     = p.CardBackground;
        res["InputBackground"]    = p.InputBackground;
        res["ControlBackground"]  = Colors.Transparent;

        // Shell (also exposed as resources for any custom XAML that references them)
        res["ShellBackground"]    = p.ShellBackground;
        res["ShellForeground"]    = p.ShellForeground;
        res["ShellTitleColor"]    = p.ShellTitleColor;
        res["ShellTabSelected"]   = p.ShellTabSelected;
        res["ShellTabUnselected"] = p.ShellTabUnselected;

        // Text
        res["TextPrimary"]        = p.TextPrimary;
        res["TextSecondary"]      = p.TextSecondary;
        res["TextPlaceholder"]    = p.TextPlaceholder;
        res["TextOnAccent"]       = Color.FromArgb("#FFFFFF");

        // Structure
        res["BorderColor"]        = p.BorderColor;
        res["DividerColor"]       = p.DividerColor;
    }

    // ── Shell chrome ─────────────────────────────────────────────────

    private static void UpdateShell(Palette p)
    {
        if (Application.Current?.MainPage is not Shell shell)
            return;

        shell.SetValue(Shell.BackgroundColorProperty,       p.ShellBackground);
        shell.SetValue(Shell.ForegroundColorProperty,       p.ShellForeground);
        shell.SetValue(Shell.TitleColorProperty,            p.ShellTitleColor);
        shell.SetValue(Shell.FlyoutBackgroundColorProperty, p.ShellBackground);
        shell.SetValue(Shell.TabBarBackgroundColorProperty, p.ShellBackground);
        shell.SetValue(Shell.TabBarForegroundColorProperty, p.ShellTabSelected);
        shell.SetValue(Shell.TabBarTitleColorProperty,      p.ShellTabSelected);
        shell.SetValue(Shell.TabBarUnselectedColorProperty, p.ShellTabUnselected);

        // Update the custom flyout rail grid (hardcoded in AppShell.xaml FlyoutContent)
        if (shell.FlyoutContent is Grid flyoutGrid)
            flyoutGrid.BackgroundColor = p.ShellBackground;
    }
}
