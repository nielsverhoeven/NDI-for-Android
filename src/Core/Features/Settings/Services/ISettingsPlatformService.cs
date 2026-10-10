using NdiForAndroid.Features.Settings.Models;

namespace NdiForAndroid.Features.Settings.Services;

public interface ISettingsPlatformService
{
    SettingsAppInfo GetAppInfo();

    /// <summary>
    /// Opens <paramref name="url"/> outside the app (Android: a Custom Tab via
    /// <c>Browser.Default.OpenAsync</c>). Used by the About panel's privacy-policy link.
    /// </summary>
    Task OpenExternalUrlAsync(Uri url);
}
