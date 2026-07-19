using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Services;

#if ANDROID
using Android.Views;
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
    // Last applied chrome state, so ReapplyChrome can restore it after Shell navigation
    // re-applies per-page toolbar appearance (resets the AppBarLayout background, #296).
    private static Palette? _lastPalette;
    private static bool _lastIsLight;

    public void Apply(ThemeMode theme, AccentColorOption accentColor)
    {
        if (MainThread.IsMainThread)
            ApplyCore(theme, accentColor);
        else
            MainThread.BeginInvokeOnMainThread(() => ApplyCore(theme, accentColor));
    }

    public void ReapplyChrome()
    {
        if (_lastPalette is null)
            return;

        var palette = _lastPalette;
        var isLight = _lastIsLight;

        // Always queue (never run inline): the toolbar appearance tracker that resets the
        // AppBarLayout background runs synchronously during navigation, and freshly created
        // pages apply theirs once more after the Navigated event — a second, delayed pass
        // wins that race without visible flicker.
        MainThread.BeginInvokeOnMainThread(() => UpdateAndroidStatusBar(palette, isLight));
        _ = Task.Run(async () =>
        {
            await Task.Delay(250).ConfigureAwait(false);
            MainThread.BeginInvokeOnMainThread(() => UpdateAndroidStatusBar(palette, isLight));
        });
    }

    private static void ApplyCore(ThemeMode theme, AccentColorOption accentColor)
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

        var palette = isLight ? LightPalette : DarkPalette;
        var accent  = ResolveAccent(accentColor);

        UpdateResources(palette, accent);
        UpdateShell(palette, isLight);
        UpdateAndroidStatusBar(palette, isLight);

        _lastPalette = palette;
        _lastIsLight = isLight;
    }

    // ── Color palettes ──────────────────────────────────────────────

    private record Palette(
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
        Color BorderColor,
        Color DividerColor);

    private static readonly Palette DarkPalette = new(
        PageBackground:     Color.FromArgb("#1E1E2E"),
        CardBackground:     Color.FromArgb("#2A2A3E"),
        InputBackground:    Color.FromArgb("#33334A"),
        ScrimBackground:    Color.FromArgb("#99000000"),
        ShellBackground:    Color.FromArgb("#1C1C1E"),
        ShellForeground:    Color.FromArgb("#FFFFFF"),
        ShellTitleColor:    Color.FromArgb("#FFFFFF"),
        ShellTabSelected:   Color.FromArgb("#FFFFFF"),
        ShellTabUnselected: Color.FromArgb("#8E8E93"),
        TextPrimary:        Color.FromArgb("#FFFFFF"),
        TextSecondary:      Color.FromArgb("#AAAACC"),
        TextPlaceholder:    Color.FromArgb("#666680"),
        BorderColor:        Color.FromArgb("#3A3A5C"),
        DividerColor:       Color.FromArgb("#2E2E4A"));

    private static readonly Palette LightPalette = new(
        PageBackground:     Color.FromArgb("#F2F2F7"),
        CardBackground:     Color.FromArgb("#FFFFFF"),
        InputBackground:    Color.FromArgb("#E8E8ED"),
        ScrimBackground:    Color.FromArgb("#66000000"),
        ShellBackground:    Color.FromArgb("#E5E5EA"),
        ShellForeground:    Color.FromArgb("#1C1C1E"),
        ShellTitleColor:    Color.FromArgb("#1C1C1E"),
        ShellTabSelected:   Color.FromArgb("#1C1C1E"),
        ShellTabUnselected: Color.FromArgb("#8E8E93"),
        TextPrimary:        Color.FromArgb("#1C1C1E"),
        TextSecondary:      Color.FromArgb("#3C3C43"),
        TextPlaceholder:    Color.FromArgb("#8E8E93"),
        BorderColor:        Color.FromArgb("#C6C6C8"),
        DividerColor:       Color.FromArgb("#D1D1D6"));

    private static Color ResolveAccent(AccentColorOption accent) => accent switch
    {
        AccentColorOption.Teal   => Color.FromArgb("#009688"),
        AccentColorOption.Green  => Color.FromArgb("#4CAF50"),
        AccentColorOption.Orange => Color.FromArgb("#FF9800"),
        AccentColorOption.Red    => Color.FromArgb("#F44336"),
        AccentColorOption.Pink   => Color.FromArgb("#E91E63"),
        _                        => Color.FromArgb("#2B7CB8"),
    };

    // ── Resource dictionary ─────────────────────────────────────────

    private static void UpdateResources(Palette p, Color accent)
    {
        var res = Application.Current!.Resources;

        res["Primary"]            = accent;
        res["OnPrimary"]          = Color.FromArgb("#FFFFFF");
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
        res["TextOnAccent"]       = Color.FromArgb("#FFFFFF");
        res["BorderColor"]        = p.BorderColor;
        res["DividerColor"]       = p.DividerColor;
    }

    // ── Shell chrome ────────────────────────────────────────────────

    private static void UpdateShell(Palette p, bool isLight)
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

        // Rail icons/labels don't react to resource changes — retint them explicitly (#294).
        if (shell is AppShell appShell)
            appShell.ApplyThemePalette(isLight);
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
        // own background extends to y=0). Recolor both to the theme chrome, and push the
        // rail below the inset so it no longer interleaves with the system clock (#296).
        var decor = activity.Window.DecorView;
        var chrome = new Android.Graphics.Color(
            (byte)(p.ShellBackground.Red   * 255),
            (byte)(p.ShellBackground.Green * 255),
            (byte)(p.ShellBackground.Blue  * 255),
            (byte)(p.ShellBackground.Alpha * 255));

        FindView<AndroidX.DrawerLayout.Widget.DrawerLayout>(decor)?.SetStatusBarBackgroundColor(chrome);
        FindView<Google.Android.Material.AppBar.AppBarLayout>(decor)?.SetBackgroundColor(chrome);

        var topPx = ViewCompat.GetRootWindowInsets(decor)?
            .GetInsets(WindowInsetsCompat.Type.StatusBars()).Top ?? GetStatusBarHeightPx(activity);
        var density = activity.Resources?.DisplayMetrics?.Density ?? 1f;

        if (Application.Current?.Windows.FirstOrDefault()?.Page is AppShell appShell)
            appShell.SetRailTopInset(topPx / density);
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

    private static int GetStatusBarHeightPx(Android.App.Activity activity)
    {
        var resources = activity.Resources;
        var id = resources?.GetIdentifier("status_bar_height", "dimen", "android") ?? 0;
        return id > 0 ? resources!.GetDimensionPixelSize(id) : 0;
    }
#endif
}
