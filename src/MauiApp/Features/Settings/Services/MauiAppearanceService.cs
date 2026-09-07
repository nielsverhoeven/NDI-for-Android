using NdiForAndroid.Features.Settings.Models;

#if ANDROID
using AndroidX.Core.View;
#endif

namespace NdiForAndroid.Features.Settings.Services;

/// <summary>
/// Single source of truth for all runtime color values.
/// Updates the application resource dictionary, Shell chrome, and the
/// Android status bar immediately when a theme/accent setting changes — no navigation required.
/// All XAML must reference the semantic keys via DynamicResource.
/// </summary>
public sealed class MauiAppearanceService : IAppearanceService
{
    public event EventHandler? AppearanceChanged;

    // Last applied chrome state, so ReapplyChrome can restore it after Shell navigation
    // re-applies per-page toolbar appearance (resets the AppBarLayout background, #296).
    // #355 hygiene: these were static on a DI singleton whose AppearanceChanged is an instance
    // event; a second instance (e.g. under test) would otherwise share this state.
    private Palette? _lastPalette;
    private bool _lastIsLight;

    public void Apply(ThemeMode theme, AccentColorOption accentColor)
    {
        if (MainThread.IsMainThread)
            ApplyCore(theme, accentColor);
        else
            MainThread.BeginInvokeOnMainThread(() => ApplyCore(theme, accentColor));
    }

    public void ReapplyChrome()
    {
        if (_lastPalette is not { } palette)
            return;

        // Always queue (never run inline): the toolbar appearance tracker that resets the
        // AppBarLayout background runs synchronously during navigation, and freshly created
        // pages apply theirs once more after the Navigated event — a second, delayed pass
        // wins that race without visible flicker.
        MainThread.BeginInvokeOnMainThread(() => UpdateAndroidStatusBar(palette, _lastIsLight));
        _ = Task.Run(async () =>
        {
            await Task.Delay(250).ConfigureAwait(false);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_lastPalette is { } latestPalette)
                    UpdateAndroidStatusBar(latestPalette, _lastIsLight);
            });
        });
    }

    private void ApplyCore(ThemeMode theme, AccentColorOption accentColor)
    {
        if (Application.Current is null)
            return;

        // Set UserAppTheme first so that when theme == System, MAUI resolves
        // RequestedTheme from the OS immediately — reading it before this call
        // would return the previously-forced value (the bug: Light→System stays Light).
        Application.Current.UserAppTheme = theme switch
        {
            ThemeMode.Light => AppTheme.Light,
            ThemeMode.Dark  => AppTheme.Dark,
            _               => AppTheme.Unspecified,
        };

        var isLight = Application.Current.RequestedTheme == AppTheme.Light;

        var palette       = isLight ? LightPalette : DarkPalette;
        var accent        = ResolveAccent(accentColor);
        var accentGraphic = Color.FromArgb(AppearancePalette.AccentGraphic(isLight, accentColor));

        UpdateResources(palette, accent, accentGraphic);
        UpdateShell(palette);
        UpdateAndroidStatusBar(palette, isLight);

        _lastPalette = palette;
        _lastIsLight = isLight;

        // Fires last: subscribers re-read the resource dictionary this call just rewrote.
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Color palettes ──────────────────────────────────────────────
    // Hex values live in Core (AppearancePalette) so the unit tests assert the same numbers the app
    // applies. Colors.xaml carries the Dark values as the pre-Apply baseline (#344, #372, #373).

    private sealed record Palette(
        Color PageBackground,
        Color CardBackground,
        Color InputBackground,
        Color ScrimBackground,
        Color ShellBackground,
        Color ShellForeground,
        Color ShellTitleColor,
        Color ShellTabSelected,
        Color ShellTabUnselected,
        Color TextPrimary,
        Color TextSecondary,
        Color TextPlaceholder,
        Color ErrorText,
        Color SuccessText,
        Color WarningText,
        Color BorderColor,
        Color DividerColor)
    {
        public static Palette FromHex(ThemePalette p) => new(
            Color.FromArgb(p.PageBackground),
            Color.FromArgb(p.CardBackground),
            Color.FromArgb(p.InputBackground),
            Color.FromArgb(p.ScrimBackground),
            Color.FromArgb(p.ShellBackground),
            Color.FromArgb(p.ShellForeground),
            Color.FromArgb(p.ShellTitleColor),
            Color.FromArgb(p.ShellTabSelected),
            Color.FromArgb(p.ShellTabUnselected),
            Color.FromArgb(p.TextPrimary),
            Color.FromArgb(p.TextSecondary),
            Color.FromArgb(p.TextPlaceholder),
            Color.FromArgb(p.ErrorText),
            Color.FromArgb(p.SuccessText),
            Color.FromArgb(p.WarningText),
            Color.FromArgb(p.BorderColor),
            Color.FromArgb(p.DividerColor));
    }

    private static readonly Palette DarkPalette  = Palette.FromHex(AppearancePalette.Dark);
    private static readonly Palette LightPalette = Palette.FromHex(AppearancePalette.Light);

    private static Color ResolveAccent(AccentColorOption accent) =>
        Color.FromArgb(AppearancePalette.Accent(accent));

    // ── Resource dictionary ─────────────────────────────────────────

    private static void UpdateResources(Palette p, Color accent, Color accentGraphic)
    {
        var res = Application.Current!.Resources;

        res["Primary"]            = accent;
        res["AccentGraphic"]      = accentGraphic;
        res["OnPrimary"]          = Color.FromArgb(AppearancePalette.OnPrimary);
        res["PageBackground"]     = p.PageBackground;
        res["CardBackground"]     = p.CardBackground;
        res["InputBackground"]    = p.InputBackground;
        res["ControlBackground"]  = Colors.Transparent;
        res["ScrimBackground"]    = p.ScrimBackground;
        res["ShellBackground"]    = p.ShellBackground;
        res["ShellForeground"]    = p.ShellForeground;
        res["ShellTitleColor"]    = p.ShellTitleColor;
        res["ShellTabSelected"]   = p.ShellTabSelected;
        res["ShellTabUnselected"] = p.ShellTabUnselected;
        res["TextPrimary"]        = p.TextPrimary;
        res["TextSecondary"]      = p.TextSecondary;
        res["TextPlaceholder"]    = p.TextPlaceholder;
        res["TextOnAccent"]       = Color.FromArgb(AppearancePalette.OnPrimary);
        res["ErrorText"]          = p.ErrorText;
        res["SuccessText"]        = p.SuccessText;
        res["WarningText"]        = p.WarningText;
        res["ErrorFill"]          = Color.FromArgb(AppearancePalette.ErrorFill);
        res["SuccessFill"]        = Color.FromArgb(AppearancePalette.SuccessFill);
        res["BorderColor"]        = p.BorderColor;
        res["DividerColor"]       = p.DividerColor;
    }

    // ── Shell chrome ────────────────────────────────────────────────

    private static void UpdateShell(Palette p)
    {
        // Single-window app: the Shell is the first (only) window's page.
        if (Application.Current?.Windows.FirstOrDefault()?.Page is not Shell shell)
            return;

        shell.SetValue(Shell.BackgroundColorProperty,       p.ShellBackground);
        shell.SetValue(Shell.ForegroundColorProperty,       p.ShellForeground);
        shell.SetValue(Shell.TitleColorProperty,            p.ShellTitleColor);
        shell.SetValue(Shell.FlyoutBackgroundColorProperty, p.ShellBackground);
        shell.SetValue(Shell.TabBarBackgroundColorProperty, p.ShellBackground);
        shell.SetValue(Shell.TabBarForegroundColorProperty, p.ShellTabSelected);
        shell.SetValue(Shell.TabBarTitleColorProperty,      p.ShellTabSelected);
        shell.SetValue(Shell.TabBarUnselectedColorProperty, p.ShellTabUnselected);

        if (shell.FlyoutContent is Grid flyoutGrid)
            flyoutGrid.BackgroundColor = p.ShellBackground;
    }

    // ── Android status bar ──────────────────────────────────────────

    private static void UpdateAndroidStatusBar(Palette p, bool isLight)
    {
#if ANDROID
        var activity = Platform.CurrentActivity;
        if (activity?.Window is null)
            return;

        // Paint the status bar the same color as the Shell chrome so it
        // appears seamless across the full screen width. SetStatusBarColor is
        // obsolete (a no-op) from API 35: edge-to-edge is enforced there and the
        // seamless look already comes from SetDecorFitsSystemWindows(false).
        if (!OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            var c = p.ShellBackground;
            var androidColor = new Android.Graphics.Color(
                (byte)(c.Red   * 255),
                (byte)(c.Green * 255),
                (byte)(c.Blue  * 255),
                (byte)(c.Alpha * 255));
            activity.Window.SetStatusBarColor(androidColor);
        }

        // Switch status bar icon/text tint so they remain readable.
        // Light background → dark icons; dark background → light icons.
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var controller = WindowCompat.GetInsetsController(
                activity.Window, activity.Window.DecorView);
            if (controller is not null)
                controller.AppearanceLightStatusBars = isLight;
        }

        // From API 35 nothing paints a themed status bar anymore; the strip shows whatever
        // the app draws underneath it. Two views own that region and both default to MAUI
        // template colors (#2C3E50): the Shell DrawerLayout's statusBarBackground and the
        // AppBarLayout background (its Toolbar child is inset below the status bar, but its
        // own background extends to y=0). Recolor both to the theme chrome (#296).
        var decor = activity.Window.DecorView;
        var chrome = new Android.Graphics.Color(
            (byte)(p.ShellBackground.Red   * 255),
            (byte)(p.ShellBackground.Green * 255),
            (byte)(p.ShellBackground.Blue  * 255),
            (byte)(p.ShellBackground.Alpha * 255));

        FindView<AndroidX.DrawerLayout.Widget.DrawerLayout>(decor)?.SetStatusBarBackgroundColor(chrome);
        FindView<Google.Android.Material.AppBar.AppBarLayout>(decor)?.SetBackgroundColor(chrome);
#endif
    }

#if ANDROID
    private static T? FindView<T>(Android.Views.View? view) where T : Android.Views.View
    {
        if (view is T match)
            return match;

        if (view is not Android.Views.ViewGroup group)
            return null;

        for (var i = 0; i < group.ChildCount; i++)
        {
            if (FindView<T>(group.GetChildAt(i)) is { } found)
                return found;
        }

        return null;
    }
#endif
}
