using NdiForAndroid.Data;
using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Services;

namespace NdiForAndroid.Features.Settings.Repositories;

public sealed class SettingsRepository : ISettingsRepository
{
    private readonly NdiDatabase _db;
    private readonly ISettingsValidationService _validationService;
    private readonly IDiscoverySettingsOrchestrator _orchestrator;

    public SettingsRepository(
        NdiDatabase db,
        ISettingsValidationService validationService,
        IDiscoverySettingsOrchestrator orchestrator)
    {
        _db = db;
        _validationService = validationService;
        _orchestrator = orchestrator;
    }

    public async Task<NdiSettingsSnapshot> GetSettingsAsync()
    {
        try
        {
            var loaded = await _db.GetSettingsAsync();
            var sanitized = _validationService.Sanitize(loaded);
            await _orchestrator.ApplyAsync(sanitized);
            return sanitized;
        }
        catch
        {
            var fallback = NdiSettingsSnapshot.CreateDefault();
            await _orchestrator.ApplyAsync(fallback);
            return fallback;
        }
    }

    public async Task SaveSettingsAsync(NdiSettingsSnapshot settings)
    {
        var sanitized = _validationService.Sanitize(settings);
        if (!_validationService.TryValidateForSave(sanitized, out var errorMessage))
            throw new ArgumentException(errorMessage ?? "Settings payload is invalid.", nameof(settings));

        await _db.SaveSettingsAsync(sanitized);
        await _orchestrator.ApplyAsync(sanitized);
    }
}
