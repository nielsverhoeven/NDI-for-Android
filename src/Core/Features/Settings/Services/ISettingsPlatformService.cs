using NdiForAndroid.Features.Settings.Models;

namespace NdiForAndroid.Features.Settings.Services;

public interface ISettingsPlatformService
{
    SettingsAppInfo GetAppInfo();
}