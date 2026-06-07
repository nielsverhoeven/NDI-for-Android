using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Services;

namespace NdiForAndroid.Features.Settings.Services;

public sealed class MauiAppearanceService : IAppearanceService
{
    public void Apply(ThemeMode theme, AccentColorOption accentColor)
    {
        var appTheme = theme switch
        {
            ThemeMode.Light => AppTheme.Light,
            ThemeMode.Dark  => AppTheme.Dark,
            _               => AppTheme.Unspecified,
        };

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Application.Current is null)
                return;

            Application.Current.UserAppTheme = appTheme;
            ApplyAccentColor(accentColor);
        });
    }

    private static void ApplyAccentColor(AccentColorOption accent)
    {
        if (Application.Current?.Resources is null)
            return;

        var color = accent switch
        {
            AccentColorOption.Teal   => Color.FromArgb("#009688"),
            AccentColorOption.Green  => Color.FromArgb("#4CAF50"),
            AccentColorOption.Orange => Color.FromArgb("#FF9800"),
            AccentColorOption.Red    => Color.FromArgb("#F44336"),
            AccentColorOption.Pink   => Color.FromArgb("#E91E63"),
            _                        => Color.FromArgb("#2196F3"), // Blue (default)
        };

        Application.Current.Resources["AppAccentColor"] = color;
    }
}
