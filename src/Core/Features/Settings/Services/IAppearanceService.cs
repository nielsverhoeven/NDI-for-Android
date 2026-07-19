using NdiForAndroid.Features.Settings.Models;

namespace NdiForAndroid.Features.Settings.Services;

public interface IAppearanceService
{
    void Apply(ThemeMode theme, AccentColorOption accentColor);

    /// <summary>
    /// Re-applies platform chrome colors (status-bar strip, toolbar background) using the
    /// last applied theme. Called after Shell navigation because MAUI re-applies per-page
    /// toolbar appearance, resetting the AppBarLayout background to template defaults (#296).
    /// No-op before the first <see cref="Apply"/> and on platforms without such chrome.
    /// </summary>
    void ReapplyChrome() { }
}
