using NdiForAndroid.Features.Settings.Models;

namespace NdiForAndroid.Features.Settings.Repositories;

public interface ISettingsRepository
{
    Task<NdiSettingsSnapshot> GetSettingsAsync();
    Task SaveSettingsAsync(NdiSettingsSnapshot settings);
}
