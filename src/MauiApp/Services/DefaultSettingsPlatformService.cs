using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Services;

namespace NdiForAndroid.Services;

public sealed class DefaultSettingsPlatformService : ISettingsPlatformService
{
    public SettingsAppInfo GetAppInfo() => new(
        AppInfo.Current.Name,
    AppInfo.Current.VersionString,
    AppInfo.Current.BuildString);
}