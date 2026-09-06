using NdiForAndroid.Data;
using NdiForAndroid.Features.DiagOverlay.Services;
using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Services;

namespace NdiForAndroid.Features.Settings.Repositories;

public sealed class SettingsRepository : ISettingsRepository
{
    private readonly NdiDatabase _db;
    private readonly ISettingsValidationService _validationService;
    private readonly IDiscoverySettingsOrchestrator _orchestrator;
    private readonly IAppearanceService _appearanceService;
    private readonly IDiagnosticOverlayService _diagnostics;

    public SettingsRepository(
        NdiDatabase db,
        ISettingsValidationService validationService,
        IDiscoverySettingsOrchestrator orchestrator,
        IAppearanceService appearanceService,
        IDiagnosticOverlayService diagnostics)
    {
        _db = db;
        _validationService = validationService;
        _orchestrator = orchestrator;
        _appearanceService = appearanceService;
        _diagnostics = diagnostics;
    }

    public async Task<NdiSettingsSnapshot> GetSettingsAsync()
    {
        try
        {
            var loaded = await _db.GetSettingsAsync();
            var sanitized = _validationService.Sanitize(loaded);
            await _orchestrator.ApplyAsync(sanitized);
            _appearanceService.Apply(sanitized.ThemeMode, sanitized.AccentColor);
            // Developer mode gates the in-app diagnostic overlay and logcat diagnostics;
            // applied here (like discovery + appearance) so cold start and saves both honour it.
            _diagnostics.IsDeveloperMode = sanitized.DeveloperModeEnabled;
            return sanitized;
        }
        catch
        {
            var fallback = NdiSettingsSnapshot.CreateDefault();
            await _orchestrator.ApplyAsync(fallback);
            _appearanceService.Apply(fallback.ThemeMode, fallback.AccentColor);
            _diagnostics.IsDeveloperMode = fallback.DeveloperModeEnabled;
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
        _appearanceService.Apply(sanitized.ThemeMode, sanitized.AccentColor);
        _diagnostics.IsDeveloperMode = sanitized.DeveloperModeEnabled;
    }
}
