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

        Application.Current.UserAppTheme = theme switch
        {
            ThemeMode.Light => AppTheme.Light,
            ThemeMode.Dark  => AppTheme.Dark,
            _               => AppTheme.Unspecified,
        };

        ApplyAccentColor(accentColor);
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
            _                        => Color.FromArgb("#2B7CB8"), // Blue (matches Colors.xaml default)
        };

        // Inserting into the top-level dictionary overrides the merged Colors.xaml entry.
        // Only effective for styles using DynamicResource Primary (not StaticResource).
        Application.Current.Resources["Primary"] = color;
    }
}
