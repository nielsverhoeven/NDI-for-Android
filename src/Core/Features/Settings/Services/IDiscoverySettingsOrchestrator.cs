using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.NdiBridge;

namespace NdiForAndroid.Features.Settings.Services;

public interface IDiscoverySettingsOrchestrator
{
    /// <summary>The discovery mode that is currently active after the last <see cref="ApplyAsync"/> call.</summary>
    DiscoveryMode ActiveMode { get; }

    Task ApplyAsync(NdiSettingsSnapshot settings, CancellationToken cancellationToken = default);
}