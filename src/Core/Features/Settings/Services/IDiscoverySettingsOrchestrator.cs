using NdiForAndroid.Features.Settings.Models;

namespace NdiForAndroid.Features.Settings.Services;

public interface IDiscoverySettingsOrchestrator
{
    Task ApplyAsync(NdiSettingsSnapshot settings, CancellationToken cancellationToken = default);
}