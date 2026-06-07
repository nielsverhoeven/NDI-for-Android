using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Services;

namespace NdiForAndroid.Platforms.Android.Services;

public sealed class AndroidSettingsPlatformService : ISettingsPlatformService
{
    public SettingsAppInfo GetAppInfo() => new(
        AppInfo.Current.Name,
    AppInfo.Current.VersionString,
    AppInfo.Current.BuildString);
}