using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Home.ViewModels;

/// <summary>
/// Holds live dashboard state derived from discovery and app state.
/// Updated by HomeViewModel as events fire or snapshots arrive.
/// </summary>
public sealed class HomeDashboardService
{
    public string? LastDiscoveryStatus { get; private set; } = "Waiting for discovery...";
    public int SourceCount { get; private set; }
    public DateTime? LastRefreshTime { get; private set; }

    public string? ViewerStatus { get; private set; } = "Idle (no source viewed yet)";
    public string? OutputStatus { get; private set; } = "Idle (no active output)";

    /// <summary>Updates discovery status from a snapshot.</summary>
    public void UpdateDiscovery(Features.Sources.Models.DiscoverySnapshot snapshot)
    {
        LastRefreshTime = DateTimeOffset.FromUnixTimeMilliseconds(snapshot.CompletedAtEpochMillis).LocalDateTime;
        
        switch (snapshot.Status)
        {
            case Features.Sources.Models.DiscoveryStatus.InProgress:
                LastDiscoveryStatus = "Discovering...";
                break;
            case Features.Sources.Models.DiscoveryStatus.Success:
                LastDiscoveryStatus = "Connected to NDI network";
                SourceCount = snapshot.Sources.Count;
                break;
            case Features.Sources.Models.DiscoveryStatus.Empty:
                LastDiscoveryStatus = "No sources found";
                SourceCount = 0;
                break;
            case Features.Sources.Models.DiscoveryStatus.Failure:
                LastDiscoveryStatus = snapshot.ErrorMessage ?? "Discovery failed";
                break;
        }
    }

    /// <summary>Updates viewer status from app state.</summary>
    public void UpdateViewerStatus(string? lastSourceId)
    {
        ViewerStatus = string.IsNullOrWhiteSpace(lastSourceId)
            ? "Idle (no source viewed yet)"
            : $"Last viewed: {lastSourceId}";
    }

    /// <summary>Updates output status from app state.</summary>
    public void UpdateOutputStatus(bool isActive, string? streamName)
    {
        OutputStatus = isActive
            ? $@"Active output to ""{streamName ?? "unknown"}"""
            : "Idle (no active output)";
    }
}
