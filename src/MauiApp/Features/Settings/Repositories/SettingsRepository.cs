using NdiForAndroid.Data;
using NdiForAndroid.Features.Settings.Models;

namespace NdiForAndroid.Features.Settings.Repositories;

public sealed class SettingsRepository : ISettingsRepository
{
    private readonly NdiDatabase _db;

    public SettingsRepository(NdiDatabase db)
    {
        _db = db;
    }

    public Task<NdiSettingsSnapshot> GetSettingsAsync() => _db.GetSettingsAsync();

    public Task SaveSettingsAsync(NdiSettingsSnapshot settings) => _db.SaveSettingsAsync(settings);
}
