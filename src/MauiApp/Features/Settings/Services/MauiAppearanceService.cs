using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Services;

namespace NdiForAndroid.Features.Settings.Services;

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

        Application.Current.UserAppTheme = theme switch
        {
            ThemeMode.Light => AppTheme.Light,
            ThemeMode.Dark  => AppTheme.Dark,
            _               => AppTheme.Unspecified,
        };

        ApplySemanticColors(isLight);
        ApplyAccentColor(accentColor);
        ApplyShellColors(isLight);
    }

    private static void ApplySemanticColors(bool isLight)
    {
        var res = Application.Current!.Resources;

        // Overwriting these keys in the top-level dictionary takes precedence over
        // merged Colors.xaml. All elements using DynamicResource on these keys
        // will repaint immediately without navigation.
        res["PageBackground"]  = isLight ? Color.FromArgb("#F2F2F7") : Color.FromArgb("#1E1E2E");
        res["CardBackground"]  = isLight ? Color.FromArgb("#FFFFFF")  : Color.FromArgb("#2A2A3E");
        res["ShellBackground"] = isLight ? Color.FromArgb("#E5E5EA")  : Color.FromArgb("#1C1C1E");
        res["ShellForeground"] = isLight ? Color.FromArgb("#000000")  : Color.FromArgb("#FFFFFF");
        res["ShellUnselected"] = isLight ? Color.FromArgb("#8E8E93")  : Color.FromArgb("#8E8E93");
        res["TextPrimary"]     = isLight ? Color.FromArgb("#000000")  : Color.FromArgb("#FFFFFF");
        res["TextSecondary"]   = isLight ? Color.FromArgb("#3C3C43")  : Color.FromArgb("#AAAACC");
    }

    private static void ApplyAccentColor(AccentColorOption accent)
    {
        var color = accent switch
        {
            AccentColorOption.Teal   => Color.FromArgb("#009688"),
            AccentColorOption.Green  => Color.FromArgb("#4CAF50"),
            AccentColorOption.Orange => Color.FromArgb("#FF9800"),
            AccentColorOption.Red    => Color.FromArgb("#F44336"),
            AccentColorOption.Pink   => Color.FromArgb("#E91E63"),
            _                        => Color.FromArgb("#2B7CB8"),
        };

        Application.Current!.Resources["Primary"] = color;
    }

    private static void ApplyShellColors(bool isLight)
    {
        if (Application.Current?.MainPage is not Shell shell)
            return;

        var bg         = isLight ? Color.FromArgb("#E5E5EA") : Color.FromArgb("#1C1C1E");
        var fg         = isLight ? Color.FromArgb("#000000") : Color.FromArgb("#FFFFFF");
        var unselected = Color.FromArgb("#8E8E93");

        shell.SetValue(Shell.FlyoutBackgroundColorProperty,  bg);
        shell.SetValue(Shell.TabBarBackgroundColorProperty,  bg);
        shell.SetValue(Shell.TabBarForegroundColorProperty,  fg);
        shell.SetValue(Shell.TabBarTitleColorProperty,       fg);
        shell.SetValue(Shell.TabBarUnselectedColorProperty,  unselected);

        // Also update the flyout content grid background (custom rail)
        if (shell.FlyoutContent is Grid flyoutGrid)
            flyoutGrid.BackgroundColor = bg;
    }
}
