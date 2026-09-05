using NdiForAndroid.Features.Settings.Models;

namespace NdiForAndroid.Features.Settings.Services;

public interface IAppearanceService
{
    void Apply(ThemeMode theme, AccentColorOption accentColor);

    /// <summary>
    /// Raised on the UI thread after <see cref="Apply"/> has written the new palette to the
    /// application resources. Chrome that is built in code — and therefore cannot listen to
    /// <c>DynamicResource</c> — subscribes to this to re-read its colors (#294).
    /// </summary>
    event EventHandler? AppearanceChanged;

    /// <summary>
    /// Re-applies platform chrome colors (status-bar strip, toolbar background) using the
    /// last applied theme. Called after Shell navigation because MAUI re-applies per-page
    /// toolbar appearance, resetting the AppBarLayout background to template defaults (#296).
    /// No-op before the first <see cref="Apply"/> and on platforms without such chrome.
    /// </summary>
    void ReapplyChrome() { }
}
